using System.Text.Json;
using API.Controllers.Frontend;
using API.Realtime.Scada;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Overview;
using Application.Services;
using Domain.Entities.Alarms;
using Domain.Entities.Analytics;
using Domain.Entities.Planning;
using Infrastructure.Persistence;
using Infrastructure.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

var assertions = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); assertions++; Console.WriteLine("PASS " + label); }
JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value);
var scope = new Access(new(3, new HashSet<int> { 5, 6 }, new Dictionary<int, int> { [13] = 5, [14] = 5, [15] = 6 }));
var repo = new RepositorySpy();
var live = new LiveSpy();
var svc = new OverviewService(repo, scope, new[] { live }, new TestTime());
foreach (var section in Enum.GetValues<OverviewSection>())
{
    var result = await svc.ReadAsync(section, new());
    Check(result.StatusCode == 200 && Json(result.Meta).GetProperty("reason").GetString() == "select_factory", "All plants live is empty: " + section);
}
Check(repo.Calls == 0 && live.Calls == 0, "All plants live reads no calculation sources");
var forbidden = await svc.ReadAsync(OverviewSection.Kpis, new(0, 999));
Check(forbidden.StatusCode == 403 && repo.Calls == 0 && live.Calls == 0, "Forbidden factory rejected before source access");
Check((await svc.ReadAsync(OverviewSection.Kpis, new(0, 5, 15))).StatusCode == 403, "Mismatched line rejected");
Check((await svc.ReadAsync(OverviewSection.Kpis, new(0, 5, 999))).StatusCode == 403, "Forbidden line rejected");
Check((await svc.ReadAsync(OverviewSection.Kpis, new(7, 5))).StatusCode == 400, "Invalid duration rejected");
Check((await svc.ReadAsync(OverviewSection.Kpis, new(5, 5))).StatusCode == 400, "Custom dates required");
Check((await svc.ReadAsync(OverviewSection.Kpis, new(0, 5, null, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow))).StatusCode == 400, "Custom dates cannot bypass live routing");
var missing = await svc.ReadAsync(OverviewSection.Kpis, new(0, 6));
Check(Json(missing.Data)[0].GetProperty("oee").ValueKind == JsonValueKind.Null && repo.Calls == 0, "Unconfigured factory has no fabricated KPI or history fallback");
var kpis = await svc.ReadAsync(OverviewSection.Kpis, new(0, 5, 13));
Check(Json(kpis.Data)[0].GetProperty("oee").GetDecimal() == 0.7065m, "Live KPI returns original fractional ratio");
Check(Json(kpis.Data)[0].GetProperty("activeAlarms").ValueKind == JsonValueKind.Null, "No live alarm view means null count");
Check(live.LineIds.SequenceEqual(new[] { 13 }) && repo.Calls == 0, "Provider receives authorized selected lines; live never reads snapshots");
var snapshotStart = new DateTime(2026, 10, 1, 8, 0, 0);
var snapshotRow = new OverviewOee(37, "M1", 13, "L1", 5, "F1", 13, snapshotStart,
    snapshotStart.AddHours(12), null, null, null, null, 100m, null, Rejected: 2m, State: 1);
var factorySnapshot = FactorySnapshotService.Build([
    snapshotRow,
    snapshotRow with { MachineId = 38, Output = 50m, Rejected = 1m },
    snapshotRow with { MachineId = 39, LineId = 14, LineName = "L2", Output = 50m, Rejected = 1m, State = 0 }
]);
Check(factorySnapshot.RunningLines == 1, "Factory snapshot counts each running line once");
Check(factorySnapshot.TotalProduction == 200m && factorySnapshot.RejectRate == .02m,
    "Factory snapshot sums Count and calculates Qreject divided by Count");
Check(factorySnapshot.ActiveBatches == 0, "Factory snapshot keeps active batches at zero until its source is available");
Check(typeof(ScadaRealtimeHub).GetMethod("SubscribeFactorySnapshot") != null &&
    ScadaRealtimeEvents.FactorySnapshotUpdated == "FactorySnapshotUpdated",
    "Factory snapshot SignalR subscription and event are exposed");
Check(typeof(ScadaRealtimeHub).GetMethod("SubscribeFactoryLayout") != null &&
    ScadaRealtimeEvents.FactoryLayoutUpdated == "FactoryLayoutUpdated",
    "Factory layout SignalR subscription and event are exposed");
var count = live.Calls;
foreach (var section in new[] { OverviewSection.EnergyByMachine, OverviewSection.EnergyBySku, OverviewSection.EnergyEfficiency, OverviewSection.ActiveAlarms })
    Check(Json((await svc.ReadAsync(section, new(0, 5))).Data).GetArrayLength() == 0, "Missing live section is empty: " + section);
Check(live.Calls == count && repo.Calls == 0, "Missing live sources never query history");
live.Available = false;
Check(Json((await svc.ReadAsync(OverviewSection.Kpis, new(0, 5))).Meta).GetProperty("reason").GetString() == "live_view_unavailable", "Physically absent view handled explicitly");
Check(repo.Calls == 0, "Absent SQL view has no history fallback");
live.Available = true;
var history = await svc.ReadAsync(OverviewSection.Kpis, new(2, 5, 13));
Check(repo.Partition?.CustomerId == 3 && repo.Partition.FactoryId == 5 && repo.Partition.LineIds.SequenceEqual(new[] { 13 }), "History repository receives customer/factory/line/time scope");
Check(Json(history.Data)[0].GetProperty("oee").GetDecimal() == 0.8m, "Historical ratio is returned without scaling");
var hourly = await svc.ReadAsync(OverviewSection.ProductionOutput, new(0, 5, 13));
Check(Json(hourly.Data).GetProperty("labels")[0].GetString() == "2026-09-30 08:00", "Hourly chart preserves local 08:00, not UTC 05:00");
Check(Json(hourly.Data).GetProperty("series")[0].GetProperty("values")[0].GetDecimal() == 100m &&
    Json(hourly.Data).GetProperty("series")[0].GetProperty("values")[1].GetDecimal() == 250m, "Hourly chart sums same-hour machines, not shift totals");
Check(Json(hourly.Meta).GetProperty("sources")[0].GetString() == "Analytics.VW_BatterjeeOee_PerHour", "Production output uses hourly view only");
Check(Json(hourly.Meta).GetProperty("periods")[0].GetProperty("From").GetString() == "2026-09-30T08:00:00", "Live period remains local without Z");
var nightStart = new DateTime(2026, 9, 30, 20, 0, 0);
var nightRow = new OverviewOee(37, "Machine", 13, "Line", 5, "Factory", 14, nightStart, nightStart.AddHours(12),
    .5m, .5m, 1m, 1m, 12m, null, nightStart.AddHours(4), "Night");
var overnight = Json(OverviewService.Build(OverviewSection.ProductionOutput, new(0, 5), [], [], true,
    [nightRow, nightRow with { HourStartLocal = nightStart.AddHours(3), Output = 10m }]));
Check(overnight.GetProperty("labels")[0].GetString() == "2026-09-30 23:00" &&
    overnight.GetProperty("labels")[1].GetString() == "2026-10-01 00:00", "Overnight hourly labels cross midnight locally in order");
var shiftCalls = repo.SnapshotCalls;
var hourlyHistory = await svc.ReadAsync(OverviewSection.ProductionOutput, new(2, 5, 13));
Check(repo.SnapshotCalls == shiftCalls && repo.HourlyCalls == 1, "Historical production reads hourly table, not shift snapshots");
Check(Json(hourlyHistory.Data).GetProperty("series")[0].GetProperty("values")[0].GetDecimal() == 60, "Duplicate hour uses latest snapshot without double counting");
Check(Json(hourlyHistory.Meta).GetProperty("granularity").GetString() == "hour", "Hourly metadata is explicit");
live.HourlyAvailable = false;
var noHourlyView = await svc.ReadAsync(OverviewSection.ProductionOutput, new(0, 5));
Check(Json(noHourlyView.Data).GetProperty("labels").GetArrayLength() == 0 && repo.HourlyCalls == 1,
    "Missing hourly view stays empty without historical fallback");
live.HourlyAvailable = true;
var aggregate = await svc.ReadAsync(OverviewSection.Page, new(0, 5));
Check(Json(aggregate.Data).GetProperty("Sections").GetProperty("productionOutput").GetProperty("labels").GetArrayLength() == 2,
    "Aggregate overview also uses hourly production");
var localDate = new DateTime(2026, 9, 30, 8, 0, 0);
Check(OverviewPeriodResolver.ToUtc(localDate, 3).Hour == 5, "Local custom filters converted internally exactly once");
var batterjeeShifts = new[]
{
    new Shift { Id = 13, FactoryId = 5, Name = "Morning", StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(20) },
    new Shift { Id = 14, FactoryId = 5, Name = "Night", StartTime = TimeSpan.FromHours(20), EndTime = TimeSpan.FromHours(8) }
};
var currentMorning = OverviewPeriodResolver.CurrentShift(
    batterjeeShifts, new DateTime(2026, 10, 1, 7, 0, 0, DateTimeKind.Utc), 3, 5, null);
Check(currentMorning.ShiftId == 13 && currentMorning.From.Hour == 5 && currentMorning.To.Hour == 17,
    "Batterjee current morning shift uses local UTC+3 boundaries expressed in UTC");
var currentNight = OverviewPeriodResolver.CurrentShift(
    batterjeeShifts, new DateTime(2026, 10, 1, 23, 0, 0, DateTimeKind.Utc), 3, 5, null);
Check(currentNight.ShiftId == 14 && currentNight.From == new DateTime(2026, 10, 1, 17, 0, 0, DateTimeKind.Utc) &&
    currentNight.To == new DateTime(2026, 10, 2, 5, 0, 0, DateTimeKind.Utc),
    "Batterjee overnight current shift crosses midnight correctly");
using (var modelDb = new MesDbContext(new DbContextOptionsBuilder<MesDbContext>()
    .UseSqlServer("Server=invalid.example;Database=NoConnection;Integrated Security=True").Options))
{
    var hourlyEntity = modelDb.Model.FindEntityType(typeof(OeeSnapshotsPerHour))!;
    var originalEntity = modelDb.Model.FindEntityType(typeof(OeeSnapshot))!;
    Check(hourlyEntity.GetTableName() == "OeeSnapshotsPerHour" && hourlyEntity.GetSchema() == "Analytics", "Hourly table mapping");
    Check(hourlyEntity.GetProperties().Select(x => (x.Name, x.ClrType, x.IsNullable, x.GetPrecision(), x.GetScale()))
        .SequenceEqual(originalEntity.GetProperties().Select(x => (x.Name, x.ClrType, x.IsNullable, x.GetPrecision(), x.GetScale()))),
        "Hourly snapshot has exactly the existing snapshot columns and types");
    Check(hourlyEntity.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name)
        .SequenceEqual(new[] { "MachineId", "ShiftId", "ShiftDate" })), "Hourly snapshot unique machine/shift/hour key");
}
var allowedScope = new OverviewAccess(3, new HashSet<int> { 5 }, new Dictionary<int, int> { [13] = 5 });
scope.Value = allowedScope;
var callsBeforeTrend = repo.SnapshotCalls;
var trend = await svc.ReadAsync(OverviewSection.OeeTrend, new(0, 5, 13));
Check(Json(trend.Data).GetProperty("labels")[0].GetString() == "2026-09-30 08:00" &&
    Json(trend.Data).GetProperty("labels").GetArrayLength() == 2, "OEE breakdown uses local hourly labels");
Check(Json(trend.Data).GetProperty("oee")[0].GetDecimal() == .7m, "Hourly OEE is averaged, not summed or multiplied by 100");
Check(Json(trend.Meta).GetProperty("sources")[0].GetString() == "Analytics.VW_BatterjeeOee_PerHour" &&
    Json(trend.Meta).GetProperty("granularity").GetString() == "hour", "OEE breakdown uses hourly live source");
var hourlyCallsBeforeTrend = repo.HourlyCalls;
var historicalTrend = await svc.ReadAsync(OverviewSection.OeeTrend, new(2, 5, 13));
Check(repo.HourlyCalls == hourlyCallsBeforeTrend + 1 && repo.SnapshotCalls == callsBeforeTrend,
    "Historical OEE breakdown reads hourly snapshots only");
Check(Json(historicalTrend.Data).GetProperty("oee")[0].GetDecimal() == .6m, "Historical hourly OEE uses latest saved hour");
live.HourlyAvailable = false;
var emptyTrend = await svc.ReadAsync(OverviewSection.OeeTrend, new(0, 5, 13));
Check(Json(emptyTrend.Data).GetProperty("oee").GetArrayLength() == 0 &&
    Json(emptyTrend.Meta).GetProperty("unavailableSections")[0].GetString() == "oeeTrend", "Missing hourly view never falls back to shift OEE");
live.HourlyAvailable = true;
Check(Json(aggregate.Data).GetProperty("Sections").GetProperty("oeeTrend").GetProperty("labels").GetArrayLength() == 2,
    "Aggregate OEE breakdown is hourly too");
scope.Value = null;
Check((await svc.ReadAsync(OverviewSection.Kpis, new(0, 5))).StatusCode == 401, "Missing customer claims rejected");
var methods = typeof(UiController).GetMethods().Where(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true)
    .Cast<HttpMethodAttribute>().Any(a => a.Template?.StartsWith("overview") == true)).ToArray();
Check(methods.Length == 9 && methods.All(m => m.IsDefined(typeof(AuthorizeAttribute), true)), "Every overview route requires authorization");
Check(!typeof(UiController).GetMethod("Planning")!.IsDefined(typeof(AuthorizeAttribute), true), "Unrelated UI route attributes unchanged");
Console.WriteLine($"Overview: {assertions} checks passed.");

if (args.Contains("--read-live"))
{
    // Explicit opt-in, read-only database integration. Does not start the API or its identity seed.
    var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath("src/API")).AddJsonFile("appsettings.json").Build();
    using var db = new MesDbContext(new DbContextOptionsBuilder<MesDbContext>().UseSqlServer(config.GetConnectionString("MCPAConn")).Options);
    var settings = config.GetSection("Overview:Eipico").Get<EipicoOverviewOptions>()!;
    var factory = await db.Factories.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted)
        .Select(x => new { FactoryId = x.Id, x.CustomerId }).FirstAsync();
    var lines = await db.ProductionLines.AsNoTracking().Where(x => x.FactoryId == factory.FactoryId && x.Factory.CustomerId == factory.CustomerId)
        .ToDictionaryAsync(x => x.Id, x => x.FactoryId);
    var provider = new EipicoOverviewProvider(db, Options.Create(settings), TimeProvider.System);
    var result = await provider.ReadOeeAsync(factory.CustomerId, factory.FactoryId, lines.Keys.ToArray(), CancellationToken.None);
    Check(result.Available && result.Source == "Analytics.VW_EipicoOee", "Eipico OEE view executes");
    Check(result.Rows.All(x => x.FactoryId == factory.FactoryId && lines.ContainsKey(x.LineId)), "Live rows remain in selected scope");
    var localNow = DateTime.UtcNow.AddHours(settings.UtcOffsetHours);
    Check(result.Rows.All(x => x.ShiftStartLocal <= localNow && x.ShiftEndLocal >= localNow), "Only the current factory shift is returned");
    Console.WriteLine(JsonSerializer.Serialize(new { result.Source, machines = result.Rows.Count,
        kpis = OverviewService.Build(OverviewSection.Kpis, new(0, factory.FactoryId), result.Rows, [], true) }));
}

if (args.Contains("--read-live-layout"))
{
    // Explicit opt-in, read-only validation of the current-shift factory layout.
    var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath("src/API")).AddJsonFile("appsettings.json").Build();
    using var db = new MesDbContext(new DbContextOptionsBuilder<MesDbContext>().UseSqlServer(config.GetConnectionString("MCPAConn")).Options);
    var settings = config.GetSection("Overview:Eipico").Get<EipicoOverviewOptions>()!;
    var factory = await db.Factories.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted)
        .Select(x => new { FactoryId = x.Id, x.CustomerId }).FirstAsync();
    var lineIds = await db.ProductionLines.AsNoTracking()
        .Where(x => x.FactoryId == factory.FactoryId && x.Factory.CustomerId == factory.CustomerId && x.IsActive && !x.IsDeleted)
        .Select(x => x.Id)
        .ToArrayAsync();
    var uow = new UnitOfWork(db);
    var timeUtilities = new TimeUtilities(uow);
    var periodResolver = new OverviewPeriodResolver(uow, timeUtilities, Options.Create(settings), TimeProvider.System);
    var scada = new ScadaLiveViewService(uow, timeUtilities, periodResolver);
    var result = await scada.GetFactoryLayoutAsync(new ScadaLiveViewScope(
        factory.CustomerId, factory.FactoryId, null, lineIds.ToHashSet()));
    var layout = Json(result.Data);
    Check(layout.GetProperty("lineCount").GetInt32() == lineIds.Length,
        "Live factory layout includes every authorized active line");
    Check(layout.GetProperty("lines").EnumerateArray().SelectMany(x => x.GetProperty("machines").EnumerateArray())
        .All(x => x.TryGetProperty("speed", out _) && x.TryGetProperty("count", out _) && x.TryGetProperty("timestamp", out _)),
        "Live factory layout exposes speed, shift CountDiff total and latest timestamp for every machine");
    Console.WriteLine(JsonSerializer.Serialize(result.Data));
}

if (args.Contains("--verify-hourly-sql"))
{
    // Retained as a compatible switch; it now verifies the Eipico hourly read model.
    var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath("src/API")).AddJsonFile("appsettings.json").Build();
    using var db = new MesDbContext(new DbContextOptionsBuilder<MesDbContext>().UseSqlServer(config.GetConnectionString("MCPAConn")).Options);
    var settings = config.GetSection("Overview:Eipico").Get<EipicoOverviewOptions>()!;
    var factory = await db.Factories.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted)
        .Select(x => new { FactoryId = x.Id, x.CustomerId }).FirstAsync();
    var lineIds = await db.ProductionLines.AsNoTracking().Where(x => x.FactoryId == factory.FactoryId)
        .Select(x => x.Id).ToArrayAsync();
    var provider = new EipicoOverviewProvider(db, Options.Create(settings), TimeProvider.System);
    var actual = await provider.ReadHourlyOeeAsync(factory.CustomerId, factory.FactoryId, lineIds, CancellationToken.None);
    Check(actual.Available && actual.Source == "Analytics.VW_EipicoOee_PerHour", "Hourly Eipico OEE view executes");
    Check(actual.Rows.All(x => x.HourStartLocal?.Minute == 0 && x.HourStartLocal?.Second == 0), "Hourly Eipico rows use real clock-hour buckets");
    Check(actual.Rows.Select(x => (x.MachineId, x.HourStartLocal)).Distinct().Count() == actual.Rows.Count, "Hourly Eipico rows are unique per machine/hour");
    Console.WriteLine(JsonSerializer.Serialize(actual.Rows));
}

public sealed class Access(OverviewAccess? value) : IOverviewAccessProvider
{
    public OverviewAccess? Value = value;
    public OverviewAccess? GetAccess() => Value;
}
public sealed class RepositorySpy : IOverviewRepository
{
    public int Calls;
    public int SnapshotCalls;
    public int HourlyCalls;
    public OverviewPartition? Partition;
    public Task<IReadOnlyList<OverviewMachine>> MachinesAsync(int customerId, int factoryId, int[] lines, CancellationToken ct)
    { Calls++; return Task.FromResult<IReadOnlyList<OverviewMachine>>([new(37, "Machine", 13, "Line", 5, "Factory")]); }
    public Task<IReadOnlyList<OeeSnapshot>> SnapshotsAsync(OverviewPartition p, CancellationToken ct)
    { Calls++; SnapshotCalls++; Partition = p; return Task.FromResult<IReadOnlyList<OeeSnapshot>>([new() { MachineId = 37, ShiftId = 13, ShiftDate = p.Period.From.AddHours(p.SnapshotUtcOffsetHours), OeePct = 0.8m, TotalOutput = 100 }]); }
    public Task<IReadOnlyList<OeeSnapshotsPerHour>> HourlySnapshotsAsync(OverviewPartition p, CancellationToken ct)
    {
        Calls++; HourlyCalls++; Partition = p;
        var hour = new DateTime(2026, 9, 30, 8, 0, 0);
        return Task.FromResult<IReadOnlyList<OeeSnapshotsPerHour>>([
            new() { Id = 1, MachineId = 37, ShiftId = 13, ShiftDate = hour, ComputedAt = hour, TotalOutput = 50 },
            new() { Id = 2, MachineId = 37, ShiftId = 13, ShiftDate = hour, ComputedAt = hour.AddMinutes(10), TotalOutput = 60, OeePct = .6m }]);
    }
    public Task<IReadOnlyList<AlarmEvent>> AlarmsAsync(OverviewPartition p, CancellationToken ct)
    { Calls++; return Task.FromResult<IReadOnlyList<AlarmEvent>>([]); }
}
public sealed class LiveSpy : IOverviewLiveProvider
{
    public bool Available = true;
    public bool HourlyAvailable = true;
    public int Calls;
    public int[] LineIds = [];
    public bool Supports(int customerId, int factoryId) => customerId == 3 && factoryId == 5;
    public Task<OverviewLiveResult> ReadHourlyOeeAsync(int customerId, int factoryId, int[] lines, CancellationToken ct)
    {
        Calls++; LineIds = lines;
        var start = new DateTime(2026, 9, 30, 8, 0, 0);
        var first = new OverviewOee(37, "Machine", 13, "Line", 5, "Factory", 13, start, start.AddHours(12),
            .7m, .7m, 1m, 1m, 75m, null, start, "Morning");
        return Task.FromResult(new OverviewLiveResult(HourlyAvailable, "Analytics.VW_BatterjeeOee_PerHour",
            HourlyAvailable ? [first, first with { MachineId = 38, Output = 25m }, first with { HourStartLocal = start.AddHours(1), Output = 250m }] : []));
    }
    public Task<OverviewLiveResult> ReadOeeAsync(int customerId, int factoryId, int[] lines, CancellationToken ct)
    {
        Calls++; LineIds = lines;
        return Task.FromResult(new OverviewLiveResult(Available, "Analytics.VW_BatterjeeOee", Available ? new[] {
            new OverviewOee(37, "Machine", 13, "Line", 5, "Factory", 13, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1),
                0.7065m, 0.7065m, 1m, 1m, 37708m, null) } : []));
    }
}
public sealed class TestTime : IOverviewPeriodResolver
{
    public int SnapshotUtcOffsetHours(int customerId, int factoryId) => 3;
    public Task<ResolvedTimeFilter> ResolveAsync(int customerId, TimeFilterInput x) => Task.FromResult(new ResolvedTimeFilter(x.Duration, x.FactoryId, x.LineId,
        DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, null, null, "last_day", "test"));
}
