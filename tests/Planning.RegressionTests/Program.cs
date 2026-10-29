using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using API.Controllers.Planning;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Domain.Entities.Planning;
using Domain.Common;
using Domain.Entities.Organization;
using Application.Common.Time;
using Infrastructure.Persistence;
using Infrastructure.Repository;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Offline regression checks: no application startup, database connection or seeding.
var assertions = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); assertions++; Console.WriteLine("PASS " + name); }
var start = new DateTime(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);
var candidateShifts = new[] {
    new Shift { Id = 1, FactoryId = 1, Name = "F1 Morning" },
    new Shift { Id = 3, FactoryId = 1, Name = "F1 Night" },
    new Shift { Id = 4, FactoryId = 2, Name = "F2 Morning" },
    new Shift { Id = 6, FactoryId = 2, Name = "F2 Night" }
};
var duplicateLinks = new[] {
    new ShiftFactory { FactoryId = 2, ShiftId = 1 },
    new ShiftFactory { FactoryId = 2, ShiftId = 3 },
    new ShiftFactory { FactoryId = 3, ShiftId = 1 }
};
Check(PlanningRules.SelectFactoryShifts(candidateShifts, duplicateLinks, 2).Select(x => x.Id).SequenceEqual(new[] { 4, 6 }),
    "Direct factory shifts take precedence over duplicate cross-factory links");
Check(PlanningRules.SelectFactoryShifts(candidateShifts, duplicateLinks, 3).Select(x => x.Id).SequenceEqual(new[] { 1 }),
    "ShiftFactory links remain a fallback for factories without direct shifts");
Check(PlanningRules.Overlaps(start, start.AddHours(2), start.AddHours(1), start.AddHours(3)), "Partial overlap");
Check(PlanningRules.Overlaps(start, start.AddHours(3), start.AddHours(1), start.AddHours(2)), "Contained overlap");
Check(PlanningRules.Overlaps(start, start.AddHours(1), start, start.AddHours(1)), "Identical overlap");
Check(!PlanningRules.Overlaps(start, start.AddHours(1), start.AddHours(1), start.AddHours(2)), "Adjacent slots allowed");
Check(!PlanningRules.IncludedInTotals("cancelled") && !PlanningRules.IncludedInTotals("split"), "No cancelled/split double counting");
Check(PlanningRules.IncludedInTotals("finished"), "Finished quantities included");
Check(PlanningRules.Attainment(37, 100) == 37 && PlanningRules.Attainment(0, 0) == null, "Attainment and zero denominator");
Check(PlanningRules.Attainment(120, 100) == 120, "No concealed overproduction");
Check(PlanningRules.DisplayStatus("in_progress", "dispense") == "dispensing", "Dispensing is derived from stage");
Check(PlanningRules.DisplayStatus("in_progress", "warehouse") == "in_progress", "Warehouse is not dispensing");
Check(typeof(PlanningController).IsDefined(typeof(AuthorizeAttribute)), "Controller requires existing JWT authorization");
var actions = typeof(PlanningController).GetMethods().Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any()).ToArray();
Check(actions.Length == 11, "All 11 planning endpoints exist");
Check(actions.All(x => !x.IsDefined(typeof(AllowAnonymousAttribute))), "No anonymous planning actions");
bool Valid(object input) => Validator.TryValidateObject(input, new ValidationContext(input), new List<ValidationResult>(), true);
Check(!Valid(new PlanningFilterQuery { Duration = 6 }), "Invalid duration rejected");
Check(!Valid(new PlanningFilterQuery { PageSize = 101 }), "Page size bounded");
Check(!Valid(new PlanningFilterQuery { FactoryId = 0 }), "Invalid factory rejected");
Check(!Valid(new ScheduleBatchCommand()), "Batch selection required");
Check(!Valid(new PlanningRouteCommand { SkuId = 1, CycleHours = 1 }), "Route steps required");
Check(!Valid(new PlanningStoppageCommand { LineId = 1, Title = new string('a', 201) }), "Stoppage title length bounded");
var options = new DbContextOptionsBuilder<MesDbContext>().UseSqlServer("Server=invalid.example;Database=DoNotConnect;Integrated Security=True").Options;
using var db = new MesDbContext(options);
var entity = db.Model.FindEntityType(typeof(ProductionSchedule))!;
Check(entity.FindProperty("BatchId")!.IsNullable, "Standalone stoppages supported by model");
Check(entity.FindProperty("Title")!.GetMaxLength() == 200, "Title max length matches API");
Check(entity.GetForeignKeys().Single(x => x.Properties.Any(p => p.Name == "BatchId")).IsRequired == false, "Optional Batch FK");
Check(entity.GetIndexes().Any(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "LineId", "ScheduledStart", "ScheduledEnd" })), "Overlap lookup index retained");
var sql = db.ProductionSchedules.Where(x => x.CustomerId == 7 && x.LineId == 1 &&
    (x.IsStoppage || x.Batch != null && x.Batch.Status != "cancelled") && x.ScheduledStart < start.AddHours(1) && x.ScheduledEnd > start).ToQueryString();
Check(sql.Contains("LEFT JOIN") && sql.Contains("CustomerId"), "Nullable-batch overlap SQL translates without a connection");
var service = new PlanningService(db, null!);
var scope = new PlanningScope(7, new Dictionary<int, int>(), new Dictionary<int, Application.Common.Time.ResolvedTimeFilter>());
Check(!(await service.ScheduleAsync(scope, new ScheduleBatchCommand { Start = start, End = start })).Succeeded, "Invalid schedule dates fail before SQL");
Check(!(await service.StoppageAsync(scope, new PlanningStoppageCommand { LineId = 99 })).Succeeded, "Foreign line fails before SQL");
Check(!(await service.RouteAsync(scope, new PlanningRouteCommand { SkuId = 1, Steps = ["  "], CycleHours = 1 })).Succeeded, "Blank route step rejected before SQL");
var controller = new PlanningController(null!, null!, null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
Check(await controller.Summary(new()) is UnauthorizedObjectResult, "Missing token scope returns 401");
Check(await controller.AddSchedule(new()) is UnauthorizedObjectResult, "Writes also require customer/user claims");
var uow = DispatchProxy.Create<IUnitOfWork, ScopeUow>();
controller = new PlanningController(uow, new TestTime(), null!) {
    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim("customerId", "7"), new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "Admin")
        }, "test")) } } };
Check(await controller.Summary(new() { FactoryId = 2 }) is ObjectResult { StatusCode: 403 }, "Other customer factory forbidden even to Admin");
Check(await controller.Summary(new() { LineId = 20 }) is ObjectResult { StatusCode: 403 }, "Other customer line forbidden");
Check(await controller.Summary(new() { FactoryId = 1, LineId = 11 }) is ObjectResult { StatusCode: 403 }, "Factory/line mismatch forbidden");
Check(await controller.Summary(new() { Duration = 5 }) is BadRequestObjectResult, "Custom range required");
Check(await controller.Summary(new() { From = start }) is BadRequestObjectResult, "Partial date range rejected");
Check(await controller.Summary(new() { From = start, To = start.AddDays(367) }) is BadRequestObjectResult, "Excessive range rejected");
controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
    new Claim("customerId", "7"), new Claim(ClaimTypes.NameIdentifier, "1"),
    new Claim("factoryId", "1"), new Claim("lineId", "10") }, "test"));
Check(API.Common.AuthenticatedAccessScope.TryResolve(controller.User, uow, out var limited, out _, out _) &&
    limited.FactoryIds.SetEquals(new[] { 1 }) && limited.LineIds.SetEquals(new[] { 10 }), "Factory and line claims constrain scope");

if (args.Contains("--read-live-shifts"))
{
    var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath("src/API")).AddJsonFile("appsettings.json").Build();
    using var liveDb = new MesDbContext(new DbContextOptionsBuilder<MesDbContext>()
        .UseSqlServer(config.GetConnectionString("MCPAConn")).Options);
    var shifts = await liveDb.Shifts.AsNoTracking()
        .Where(x => !x.IsDeleted && x.IsActive && (x.FactoryId == 2 || liveDb.ShiftFactories.Any(f => f.FactoryId == 2 && f.ShiftId == x.Id)))
        .OrderBy(x => x.Id)
        .Select(x => new { x.Id, x.FactoryId, x.Name, x.StartTime, x.EndTime,
            linkedToFactory2 = liveDb.ShiftFactories.Any(f => f.FactoryId == 2 && f.ShiftId == x.Id) })
        .ToArrayAsync();
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(shifts));

    var factory = await liveDb.Factories.AsNoTracking().SingleAsync(x => x.Id == 2);
    var lineFactories = await liveDb.ProductionLines.AsNoTracking()
        .Where(x => x.FactoryId == 2 && x.IsActive && !x.IsDeleted)
        .ToDictionaryAsync(x => x.Id, x => x.FactoryId);
    var liveUow = new UnitOfWork(liveDb);
    var liveTime = new TimeUtilities(liveUow);
    var currentPeriod = await liveTime.ResolveAsync(new TimeFilterInput(0, 2));
    var liveScope = new PlanningScope(factory.CustomerId, lineFactories,
        new Dictionary<int, ResolvedTimeFilter> { [2] = currentPeriod });
    var schedule = System.Text.Json.JsonSerializer.SerializeToElement(
        await new PlanningService(liveDb, null!).ReadAsync("schedule", liveScope));
    Check(schedule.GetProperty("periods")[0].GetProperty("ShiftId").GetInt32() == 4 &&
        schedule.GetProperty("periods")[0].GetProperty("Mode").GetString() == "current_shift",
        "Live Factory 2 duration zero resolves its direct current shift without day expansion");
    var returnedShifts = schedule.GetProperty("shifts").EnumerateArray().ToArray();
    Check(returnedShifts.Length == 1 && returnedShifts[0].GetProperty("shiftId").GetInt32() == 4,
        "Live Factory 2 schedule returns only its current direct shift");
}
Console.WriteLine($"Planning: {assertions} offline checks passed.");

public class ScopeUow : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
        "get_Factories" => QueryRepo<Factory>.Create(new() { Id=1, CustomerId=7, IsActive=true },
            new() { Id=2, CustomerId=8, IsActive=true }, new() { Id=3, CustomerId=7, IsActive=true }),
        "get_ProductionLines" => QueryRepo<ProductionLine>.Create(new() { Id=10, FactoryId=1, IsActive=true },
            new() { Id=11, FactoryId=3, IsActive=true }, new() { Id=20, FactoryId=2, IsActive=true }),
        _ => throw new NotSupportedException(method.Name)
    };
}
public class QueryRepo<T> : DispatchProxy where T : Entity
{
    private T[] rows = [];
    public static IRepository<T> Create(params T[] rows) {
        var result = DispatchProxy.Create<IRepository<T>, QueryRepo<T>>();
        ((QueryRepo<T>)(object)result).rows = rows;
        return result;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
        => method!.Name == "Query" ? rows.AsQueryable() : throw new NotSupportedException(method.Name);
}
public sealed class TestTime : ITimeUtilities
{
    public Task<ResolvedTimeFilter> ResolveAsync(TimeFilterInput input) => Task.FromResult(new ResolvedTimeFilter(
        input.Duration, input.FactoryId, input.LineId, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(1), null, null, "custom", "test"));
}
