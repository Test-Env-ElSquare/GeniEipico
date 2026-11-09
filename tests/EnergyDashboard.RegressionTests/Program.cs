using API.Controllers.Analytics;
using API.Realtime.Alerts;
using Application.Abstractions.Services;
using Application.Dtos.Alerts;
using Infrastructure.Persistence;
using Infrastructure.Repository;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MCPAConn");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings__MCPAConn is required for this read-only integration test.");

var options = new DbContextOptionsBuilder<MesDbContext>()
    .UseSqlServer(connectionString)
    .Options;
await using var db = new MesDbContext(options);
var factory = await db.Factories
    .SingleAsync(x => x.EmsEnergySourceId == 3 && !x.IsDeleted);
var zeroTransformerSourceId = await db.Transformers
    .Where(x => x.FactoryId == factory.Id && x.TransformerName == "EP2_TR1")
    .Select(x => x.EmsEnergySourceId)
    .SingleAsync() ?? throw new InvalidOperationException("EP2_TR1 has no EMS source id.");
var scope = new EnergyDashboardScope([factory.Id], factory.Id, 3);
var filter = new EnergyDashboardFilter(scope);
using var cache = new MemoryCache(new MemoryCacheOptions());
var service = new EnergyDashboardService(db, TimeProvider.System, cache);
var assertions = 0;

void Check(bool value, string label)
{
    if (!value) throw new Exception(label);
    assertions++;
    Console.WriteLine("PASS " + label);
}

var summary = await service.GetSummaryAsync(filter);
Check(summary.Transformers.TotalCount == 4, "GetSummary returns four Eipico2 transformers");
Check(summary.Zones.TotalCount == 6, "GetSummary returns six Eipico2 zones");
Check(summary.Lines.TotalCount == 12, "GetSummary returns twelve Eipico2 lines");
Check(summary.TotalEnergy > 0, "GetSummary returns live energy");

var online = await service.GetOnlineStatusAsync(filter);
Check(online.Zones.OnlineCount + online.Zones.OfflineCount == 6,
    "GetOnlineStatus covers all Eipico2 zones");
Check(online.Lines.OnlineCount == 8 && online.Lines.OfflineCount == 4,
    "GetOnlineStatus matches the Eipico2 line screen");

var hourly = await service.GetHourlyEnergyAsync(filter);
Check(hourly.Select(x => x.TransformerId).Distinct().Count() == 4,
    "GetHourlyEnergy keeps the zero-reading transformer series");
Check(hourly.Any(x => x.TransformerId == zeroTransformerSourceId && x.Energy == 0),
    "GetHourlyEnergy returns EP2_TR1 as zero");

var total = await service.GetTotalEnergyAsync(filter);
Check(total.Count == 4 && total.Any(x => x.TransformerId == zeroTransformerSourceId && x.Energy == 0),
    "GetTotalEnergy returns all four transformers including EP2_TR1");

var heatmap = await service.GetEnergyHeatmapAsync(filter with { TransformerId = 0, ZoneId = 0 });
Check(heatmap.Select(x => x.LineId).Distinct().Count() == 12,
    "GetEnergyHeatmap returns all twelve Eipico2 lines");
Check(heatmap.All(x => x.Level == "Line" && x.Status is >= 0 and <= 3),
    "GetEnergyHeatmap returns EMS-compatible numeric status values");

Check((await service.GetVoltageGraphAsync(filter)).Count > 0,
    "GetVoltageGraph returns current points");
Check((await service.GetCurrentGraphAsync(filter)).Count > 0,
    "GetCurrentGraph returns current points");
Check((await service.GetHarmonicsGraphAsync(filter)).Count > 0,
    "GetHarmonicsGraph returns current points");

var top = await service.GetTopEnergyConsumersAsync(filter, 5);
Check(top.Count == 4 && top[0].Energy >= top[^1].Energy,
    "GetTopEnergyConsumers ranks all Eipico2 transformers");

var expectedRoutes = new[]
{
    "GetSummary", "GetOnlineStatus", "GetEnergyHeatmap", "GetHourlyEnergy",
    "GetTotalEnergy", "GetHarmonicsGraph", "GetVoltageGraph", "GetCurrentGraph",
    "GetTopEnergyConsumers"
};
var controllerRoutes = typeof(EnergyDashboardController)
    .GetMethods()
    .SelectMany(x => x.GetCustomAttributes(typeof(HttpGetAttribute), true)
        .Cast<HttpGetAttribute>())
    .Select(x => x.Template)
    .Where(x => x is not null)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
Check(expectedRoutes.All(controllerRoutes.Contains), "All nine EMS-compatible routes are exposed");
Check(typeof(EnergyDashboardController).GetCustomAttributes(typeof(RouteAttribute), true)
        .Cast<RouteAttribute>().Single().Template == "api/energy-dashboard",
    "Controller preserves the EMS route prefix");

var dropdownRoutes = typeof(EnergyDropdownController)
    .GetMethods()
    .SelectMany(x => x.GetCustomAttributes(typeof(HttpGetAttribute), true)
        .Cast<HttpGetAttribute>())
    .Select(x => x.Template)
    .Where(x => x is not null)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
Check(new[] { "transformers", "zones", "transformers/{transformerId:int}/zones" }
        .All(dropdownRoutes.Contains),
    "EMS-compatible transformer and zone dropdown routes are exposed");

Check(AlertsHub.Route == "/hubs/alerts" &&
      AlertsHub.ReceiveEvent == "ReceiveAlert",
    "Energy Dashboard alerts preserve the EMS SignalR route and event");
Check(typeof(AlertsHub).IsDefined(typeof(AuthorizeAttribute), inherit: true),
    "Energy Dashboard alerts hub requires authentication");
var alertContract = typeof(RealtimeAlertDto).GetProperties()
    .Select(x => x.Name)
    .ToHashSet(StringComparer.Ordinal);
Check(new[]
    {
        "Id", "Title", "Message", "Type", "Severity", "IsRead",
        "CreatedAtUtc", "ReadAtUtc", "AlarmLogId", "TicketId"
    }.All(alertContract.Contains),
    "ReceiveAlert payload preserves the EMS alert contract");

await using var unitOfWork = new UnitOfWork(db);
var controller = new EnergyDashboardController(service, unitOfWork)
{
    ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim("customerId", factory.CustomerId.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            ], "regression-test"))
        }
    }
};
var controllerResult = await controller.GetSummary(3, 0, null, null);
Check(controllerResult is OkObjectResult,
    "Controller resolves EMS factoryId=3 to the authorized GenieEipico factory");

Console.WriteLine($"PASS: {assertions} energy dashboard checks; no database writes.");
