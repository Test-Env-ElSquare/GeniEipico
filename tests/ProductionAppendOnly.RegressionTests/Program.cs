using System.Reflection;
using API.Controllers.IoT;
using API.Realtime.Scada;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Services;
using Domain.Common;
using Domain.Entities.IoT;
using Domain.Entities.Organization;
using Domain.Entities.Scada;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

// No database connection, hosted application, migrations, or external writes.
var assertions = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAILED: {name}");
    assertions++;
    Console.WriteLine($"PASS: {name}");
}

var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
DateTime UtcLocal(int day, int hour, int minute = 0) => TimeZoneInfo.ConvertTimeToUtc(
    new DateTime(2026, 10, day, hour, minute, 0, DateTimeKind.Unspecified), zone);
foreach (var (day, hour, minute, expectedDay, expectedHour) in new[]
{
    (29, 7, 59, 28, 20), (29, 8, 0, 29, 8), (29, 19, 59, 29, 8),
    (29, 20, 0, 29, 20), (30, 0, 0, 29, 20), (30, 7, 59, 29, 20),
    (30, 8, 0, 30, 8)
})
{
    Check(Application.Common.Time.EipicoProductionShift.ResolveUtc(UtcLocal(day, hour, minute), "Africa/Cairo")
        == UtcLocal(expectedDay, expectedHour), $"Eipico shift boundary {day} {hour}:{minute:00}");
}
var shiftReading = new MachineState { Timestamp = UtcLocal(29, 9), ShiftStartTime = UtcLocal(29, 8) };
Check(ProductionRecordFactory.Create(shiftReading, null, false).ShiftStartTime == shiftReading.ShiftStartTime,
    "Production factory preserves supplied shift start");
var at = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);
var first = new MachineState { Id = 41, MachineId = "Mixer_1_1", CustomerId = 7, FactoryId = 1, LineId = 1,
    Timestamp = at, Count = 100, CountDiff = 17, State = 1, Speed = 450, Temp = 30,
    QReject = 3, StateReason = -1, Sku = 1, Fault = 0 };
var input = new MachineState { MachineId = first.MachineId, CustomerId = 7, FactoryId = 1, LineId = 1,
    Timestamp = at.AddMinutes(1), Count = 140, State = 0, Speed = 0, QReject = 8, Sku = 1 };
var second = ProductionRecordFactory.Create(input, first, false);
Check(second.Id == 0 && !ReferenceEquals(first, second), "New reading is a new entity, not an update");
Check(first.Id == 41 && first.Count == 100 && first.CountDiff == 17 && first.Timestamp == at,
    "Previous persisted reading is unchanged");
Check(second.Count == 140 && second.CountDiff == 40, "CountDiff uses previous count");
Check(second.QReject == 8 && second.QRejectDiff == 5, "QRejectDiff uses previous reject counter");
Check(second.CustomerId == 7, "New Production record preserves its customer scope");
var firstReading = ProductionRecordFactory.Create(input, null, false);
Check(firstReading.CountDiff == 0 && firstReading.QRejectDiff == 0, "First reading has no invented deltas");
input.Count = 5;
input.QReject = 1;
var reset = ProductionRecordFactory.Create(input, first, false);
Check(reset.CountDiff == 0 && reset.QRejectDiff == 0, "Counter resets clamp deltas to zero");
input.Count = first.Count;
input.QReject = first.QReject;
var unchanged = ProductionRecordFactory.Create(input, first, false);
Check(unchanged.CountDiff == 0 && unchanged.QRejectDiff == 0, "Unchanged counters still produce a reading");
var manual = ProductionRecordFactory.Create(input, first, true);
Check(manual.Id == 0 && manual.State == 0 && manual.Count == 100 && manual.CountDiff == 0 &&
      manual.Speed == 450 && manual.Temp == 30 && manual.QReject == 3 && manual.QRejectDiff == 0,
    "Manual state change copies measurements into a NEW row without recounting output or rejects");

var options = new DbContextOptionsBuilder<MesDbContext>()
    .UseSqlServer("Server=invalid.example;Database=DoNotConnect;Integrated Security=True;TrustServerCertificate=True")
    .Options;
using (var db = new MesDbContext(options))
{
    var model = db.Model.FindEntityType(typeof(MachineState))!;
    Check(!model.GetIndexes().Any(x => x.IsUnique), "Production has no unique machine index");
    Check(model.GetIndexes().Any(x => x.Properties.Select(p => p.Name)
        .SequenceEqual(new[] { "FactoryId", "MachineId", "Timestamp" })), "Time-ordered lookup index exists");
    Check(model.GetForeignKeys().Any(x => x.Properties.Select(p => p.Name)
        .SequenceEqual(new[] { "CustomerId" })), "Production CustomerId has a foreign key");
    var rejectDiff = model.FindProperty(nameof(MachineState.QRejectDiff))!;
    Check(rejectDiff.GetPrecision() == 18 && rejectDiff.GetScale() == 4,
        "Production QRejectDiff uses decimal(18,4)");

    var lineCode = db.Model.FindEntityType(typeof(ProductionLine))!
        .FindProperty(nameof(ProductionLine.Code))!;
    Check(lineCode.GetMaxLength() == 20,
        "Eipico realtime line codes must respect the ProductionLine 20-character limit");
}

var realtimeSyncType = typeof(MesDbContext).Assembly
    .GetType("Infrastructure.Integration.EipicoRealtimeSyncService", throwOnError: true)!;
var describeException = realtimeSyncType.GetMethod(
    "DescribeException",
    BindingFlags.Static | BindingFlags.NonPublic)!;
var describedFailure = (string)describeException.Invoke(
    null,
    new object[]
    {
        new InvalidOperationException("outer failure", new Exception("SQL root cause"))
    })!;
Check(describedFailure.Contains("InvalidOperationException: outer failure") &&
      describedFailure.Contains("Exception: SQL root cause"),
    "Eipico sync errors preserve the database root-cause message");

// Both convenience and bool SaveChanges overloads must fail BEFORE any SQL is sent.
for (var overload = 0; overload < 4; overload++)
{
    using var db = new MesDbContext(options);
    db.Attach(new MachineState { Id = 5, MachineId = "Mixer_1_1" }).Entity.Count = 50;
    var blocked = false;
    try
    {
        switch (overload)
        {
            case 0: db.SaveChanges(); break;
            case 1: db.SaveChanges(false); break;
            case 2: await db.SaveChangesAsync(); break;
            case 3: await db.SaveChangesAsync(false); break;
        }
    }
    catch (InvalidOperationException e) when (e.Message.Contains("append-only")) { blocked = true; }
    Check(blocked, $"SaveChanges overload {overload} blocks overwriting Production before SQL");
}

var machine = new Machine { Id = 1, Name = "Mixer_1_1", Code = "MIX01", LineId = 1 };
var latestState = typeof(FrontendDataService).GetMethod("FindMachineState", BindingFlags.Static | BindingFlags.NonPublic)!;
var chosen = (MachineState?)latestState.Invoke(null, new object[] { new[] { first, second }, machine });
Check(ReferenceEquals(chosen, second), "Current-state UI selects latest reading, not first saved row");

var tags = new List<TelemetryTag>();
void Signal(DateTime timestamp, int count)
{
    foreach (var (tag, value) in new (string, decimal)[]
    {
        ("State", 1), ("Speed", 450), ("Count", count), ("Qreject", 0),
        ("StateReason", -1), ("SKU", 1), ("Fault", 0)
    })
        tags.Add(new TelemetryTag { Id = tags.Count + 1, DeviceId = machine.Name,
            TagName = tag, ValueNum = value, RecordedAt = timestamp });
}

IRepository<T> Repo<T>(IEnumerable<T> data) where T : Entity
    => StubProxy.For<IRepository<T>>((method, _) => method.Name switch
    {
        "Query" => data.AsQueryable(),
        _ => throw new Exception($"Unexpected repository write/call: {method.Name}")
    });

// Signage compatibility: keep the old wire shape and let the view own aggregates.
var signageMachines = new[]
{
    new Machine { Id = 10, LineId = 1, Name = "E1_Test_Fill", MachineType = "Fill", IsDominant = true },
    new Machine { Id = 11, LineId = 1, Name = "E1_Test_Cart", MachineType = "Cart" },
    new Machine { Id = 12, LineId = 99, Name = "OtherTenant" }
};
var signageLines = new[]
{
    new ProductionLine { Id = 1, FactoryId = 7, Name = "E1_Test", ViewName = "Display line", AliasName = "Tablet - Line", Number = 1 },
    new ProductionLine { Id = 2, FactoryId = 7, Name = "E1_Scheduled", ViewName = "Scheduled line" },
    new ProductionLine { Id = 99, FactoryId = 8, Name = "Other" }
};
var signageLoads = new[]
{
    new Domain.Entities.Production.MachineMaterialLoad { Id = 1, BatchId = 1, MachineId = 10, LineId = 2, ActivationDate = at, IsPending = false },
    new Domain.Entities.Production.MachineMaterialLoad { Id = 2, BatchId = 2, MachineId = 10, LineId = 2, ActivationDate = at, IsPending = false },
    new Domain.Entities.Production.MachineMaterialLoad { Id = 3, BatchId = 2, MachineId = 10, LineId = 2, ActivationDate = at, IsPending = false, FinishingDate = at },
    new Domain.Entities.Production.MachineMaterialLoad { Id = 4, BatchId = 3, MachineId = 10, LineId = 2, ActivationDate = at, IsPending = true }
};
var signageBatches = Enumerable.Range(1, 3).Select(id => new Domain.Entities.Production.Batch
    { Id = id, CustomerId = 3, SkuId = 1, LineId = 1, Uid = $"Batch{id}" }).ToArray();
var signageUow = StubProxy.For<IUnitOfWork>((method, _) => method.Name switch
{
    "get_Machines" => Repo(signageMachines),
    "get_ProductionLines" => Repo(signageLines),
    "get_MachineMaterialLoads" => Repo(signageLoads),
    "get_Batches" => Repo(signageBatches),
    "get_Skus" => Repo(new[] { new Domain.Entities.MasterData.Sku { Id = 1, CustomerId = 3, Name = "Product" } }),
    "get_Scales" => Repo(new[] { new Scale { Id = 5, FactoryId = 7, Name = "Scale1", Zone = "4", TelemetryAt = at } }),
    _ => throw new Exception($"Unexpected signage repository call: {method.Name}")
});
int[] queriedSignageLines = [];
var signageReader = StubProxy.For<IFactoryLayoutSignalReader>((method, values) =>
{
    queriedSignageLines = (int[])values![2]!;
    return Task.FromResult<IReadOnlyList<FactoryLayoutSignal>>([
        new() { CustomerId = 3, FactoryId = 7, LineId = 1, MachineName = "E1_Test_Fill", SignalId = 8,
            ShiftStartTime = at, LatestTimeStamp = at.AddHours(1), TotalCountDiff = 1234m, Count = 9876, Speed = 100 },
        new() { CustomerId = 9, FactoryId = 8, LineId = 99, MachineName = "OtherTenant", TotalCountDiff = 999 }
    ]);
});
var signageService = new SignageLayoutService(signageUow, signageReader);
var signageScope = new ScadaLiveViewScope(3, 7, null, new HashSet<int> { 1 });
var signalPayload = await signageService.GetFactorySignalsAsync(signageScope);
Check(queriedSignageLines.SequenceEqual(new[] { 1 }) && signalPayload.Count == 1 && signalPayload[0].LineId == 1,
    "Signage view reads and responses stay within authorized lines and tenant");
Check(signalPayload[0].Machines[0].LatestSignal.Count == 1234m,
    "Legacy latestSignal.count uses view total, not raw cumulative counter or code recalculation");
Check(signalPayload[0].MachineCount == 1 && signageService.GetMachines(signageScope).Count == 2,
    "No-signal machines remain in definitions without inventing telemetry");
var wire = System.Text.Json.JsonSerializer.SerializeToElement(signalPayload, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
Check(wire.ValueKind == System.Text.Json.JsonValueKind.Array && wire[0].GetProperty("machines")[0]
    .GetProperty("latestSignal").GetProperty("timeStamp").GetString()!.EndsWith("Z"),
    "Legacy signal response is a bare array with nested latestSignal and UTC timestamps");
Check(wire[0].GetProperty("machines")[0].GetProperty("latestSignal").TryGetProperty("sku", out _) &&
    wire[0].GetProperty("machines")[0].GetProperty("latestSignal").TryGetProperty("qrejectDiff", out _),
    "Legacy telemetry JSON field casing is preserved");
Check(signageService.GetFactoryLines(signageScope)[0].Name == "Display line" &&
    signageService.GetFactoryLines(signageScope)[0].Lines.Single(x => x.Id == 10).IsDominant,
    "Legacy definitions expose display names and dominant machine flags");
Check(signageService.GetScales(signageScope).Single().Room == "Room 4",
    "Scale room format matches Eipico's InitialScales");
var scheduledScope = new ScadaLiveViewScope(3, 7, null, new HashSet<int> { 2 });
var currentBatches = signageService.GetCurrentBatches(scheduledScope);
Check(currentBatches.Count == 1 && currentBatches[0].LineId == 2 && currentBatches[0].BatchId == "Batch1",
    "Current batch follows scheduler line, excludes finished groups and pending loads");
Check(signageService.GetCurrentBatches(signageScope).Count == 0,
    "Current batch never falls back to machine or batch definition line");
var signageRegistry = new API.Realtime.Scada.ScadaRealtimeSubscriptionRegistry();
var groupA = signageRegistry.Create("signage-signals", "ReceiveFactorySignals", signageScope);
var groupB = signageRegistry.Create("signage-signals", "ReceiveFactorySignals", scheduledScope);
Check(groupA.GroupName != groupB.GroupName,
    "Different line permissions never share a legacy factory broadcast group");
signageRegistry.Register("client", groupA);
signageRegistry.UnregisterConnection("client");
Check(signageRegistry.GetActiveSubscriptions().Count == 0, "Disconnect removes legacy subscription");
var machineRepo = Repo(new[] { machine });
var lineRepo = Repo(new[] { new ProductionLine { Id = 1, FactoryId = 1 } });
var tagRepo = Repo(tags);
var uow = StubProxy.For<IUnitOfWork>((method, _) => method.Name switch
{
    "get_Machines" => machineRepo,
    "get_ProductionLines" => lineRepo,
    "get_TelemetryTags" => tagRepo,
    "DisposeAsync" => ValueTask.CompletedTask,
    _ => throw new Exception($"Projector must not mutate repositories directly: {method.Name}")
});
var writer = new RecordingWriter();
var services = new ServiceCollection();
services.AddSingleton(uow);
services.AddSingleton<IProductionRecordWriter>(writer);
await using var provider = services.BuildServiceProvider();
ScadaProductionProjectionService Projector() => new(provider.GetRequiredService<IServiceScopeFactory>(),
    new ConfigurationBuilder().Build(), NullLogger<ScadaProductionProjectionService>.Instance);
var project = typeof(ScadaProductionProjectionService).GetMethod("ProjectNextBatchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
Task Poll(ScadaProductionProjectionService projector) => (Task)project.Invoke(projector, new object[] { CancellationToken.None })!;

using var projector = Projector();
Signal(at, 100);
Signal(at.AddMinutes(2), 150);
await Poll(projector);
Check(writer.Rows.Count == 2 && !ReferenceEquals(writer.Rows[0], writer.Rows[1]),
    "Projector delegates every signal to append writer, including unchanged machine state");
Check(writer.Rows[0].Count == 100 && writer.Rows[1].Count == 150, "Later telemetry does not overwrite earlier telemetry");
Signal(at.AddMinutes(1), 130);
await Poll(projector);
Check(writer.Rows.Count == 3 && writer.Rows.Any(x => x.Timestamp == at.AddMinutes(1)), "Delayed signal is not discarded");
await Poll(projector);
Check(writer.Rows.Count == 3, "Idle poll does not invent another reading");

Signal(at.AddMinutes(3), 160);
writer.FailNext = true;
await Poll(projector);
Check(writer.Rows.Count == 3, "Failed append does not pretend to have saved a signal");
await Poll(projector);
Check(writer.Rows.Count == 4, "Failed signal remains available for retry");

var workflow = new ScadaManualWorkflowService(uow, writer);
await workflow.UpdateMachineStateManualAsync(machine.Id, 0, "Regression test");
Check(writer.LastManualStateOnly && writer.Rows.Count == 5, "Manual workflow also delegates to append writer");

var rawRows = new List<TelemetryRaw>();
var tagRows = new List<TelemetryTag>();
object? RawService(MethodInfo method, object?[]? args)
{
    if (method.Name == "CreateAsync")
    {
        var row = (TelemetryRaw)args![0]!;
        row.Id = rawRows.Count + 1;
        rawRows.Add(row);
        return Task.FromResult(row);
    }

    throw new Exception($"Unexpected raw telemetry service call: {method.Name}");
}
object? TagService(MethodInfo method, object?[]? args)
{
    if (method.Name == "CreateAsync")
    {
        var row = (TelemetryTag)args![0]!;
        row.Id = tagRows.Count + 1;
        tagRows.Add(row);
        return Task.FromResult(row);
    }

    throw new Exception($"Unexpected telemetry tag service call: {method.Name}");
}
object? DeviceService(MethodInfo method, object?[]? args)
    => method.Name == "Query"
        ? Array.Empty<Device>().AsQueryable()
        : throw new Exception($"Unexpected device service call: {method.Name}");

var ingest = new IngestController(
    StubProxy.For<ITelemetryRawService>(RawService),
    StubProxy.For<IDeviceService>(DeviceService),
    StubProxy.For<ITelemetryTagService>(TagService),
    StubProxy.For<IAlarmEvaluationService>((method, _) =>
        throw new Exception($"Batterjee signal without an IoT device must not evaluate alarms: {method.Name}")));
var batterjeeQuery = "?ID=Bat_Tab_L1_Blstr&State=1&Count=100&Qreject=0&Speed=500&StateType=1&StateReason=-1&SKU=-1&Fault=0";
ingest.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
ingest.Request.QueryString = new QueryString(batterjeeQuery);
var batterjeeResponse = await ingest.SubmitBatterjeeSignal(
    "Bat_Tab_L1_Blstr", "1", "100", "0", "500", "1", "-1", "-1", "0");

var batterjeeAction = typeof(IngestController).GetMethod(nameof(IngestController.SubmitBatterjeeSignal))!;
var batterjeeRoute = batterjeeAction.GetCustomAttributes<HttpGetAttribute>().Single().Template;
Check(batterjeeRoute == "/Batterjee/signal", "Batterjee compatibility route preserves the embedded-device URL");
Check(batterjeeResponse is OkObjectResult { Value: "Batterjee Signal Saved" },
    "Batterjee compatibility route preserves the legacy 200 response");
Check(rawRows.Count == 1 && rawRows[0].SourcePath == "/Batterjee/signal" &&
      rawRows[0].RawPayload == batterjeeQuery && rawRows[0].ReceivedAt.Kind == DateTimeKind.Utc,
    "Batterjee request stores one raw row with its original query string and a UTC timestamp");
var expectedBatterjeeTags = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
{
    ["State"] = 1,
    ["Count"] = 100,
    ["Qreject"] = 0,
    ["Speed"] = 500,
    ["StateType"] = 1,
    ["StateReason"] = -1,
    ["SKU"] = -1,
    ["Fault"] = 0
};
Check(tagRows.Count == 8 && tagRows.All(x =>
        x.DeviceId == "Bat_Tab_L1_Blstr" &&
        x.RecordedAt == rawRows[0].ReceivedAt &&
        x.ValueNum == expectedBatterjeeTags[x.TagName]),
    "Batterjee query parsing stores the expected eight tags under the machine name");

var batterjeeMachine = new Machine
{
    Id = 37,
    Name = "Bat_Tab_L1_Blstr",
    Code = "BAT-L1-BLSTR",
    LineId = 13,
    IsActive = true
};
var batterjeeUow = StubProxy.For<IUnitOfWork>((method, _) => method.Name switch
{
    "get_Machines" => Repo(new[] { batterjeeMachine }),
    "get_ProductionLines" => Repo(new[] { new ProductionLine
        { Id = 13, FactoryId = 5, IsActive = true } }),
    "get_TelemetryTags" => Repo(tagRows),
    "DisposeAsync" => ValueTask.CompletedTask,
    _ => throw new Exception($"Batterjee projector must not mutate repositories directly: {method.Name}")
});
var batterjeeWriter = new RecordingWriter();
var batterjeeServices = new ServiceCollection();
batterjeeServices.AddSingleton(batterjeeUow);
batterjeeServices.AddSingleton<IProductionRecordWriter>(batterjeeWriter);
await using (var batterjeeProvider = batterjeeServices.BuildServiceProvider())
using (var batterjeeProjector = new ScadaProductionProjectionService(
           batterjeeProvider.GetRequiredService<IServiceScopeFactory>(),
           new ConfigurationBuilder().Build(),
           NullLogger<ScadaProductionProjectionService>.Instance))
{
    await Poll(batterjeeProjector);
}
Check(batterjeeWriter.Rows is
    [
        {
            MachineId: "Bat_Tab_L1_Blstr",
            FactoryId: 5,
            LineId: 13,
            State: 1,
            Count: 100,
            CountDiff: 0,
            QReject: 0,
            QRejectDiff: 0,
            Speed: 500,
            StateReason: -1,
            Sku: -1,
            Fault: 0
        }
    ], "Batterjee Raw/Tags flow through the projector and writer into the scoped Production record");

Console.WriteLine($"All {assertions} assertions passed. SQL transaction/concurrency behavior requires a separate SQL Server integration test.");

public class StubProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
    public static T For<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var value = Create<T, StubProxy>();
        ((StubProxy)(object)value).Handler = handler;
        return value;
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
}

public sealed class RecordingWriter : IProductionRecordWriter
{
    public List<MachineState> Rows { get; } = new();
    public bool FailNext { get; set; }
    public bool LastManualStateOnly { get; private set; }
    public Task<MachineState> AppendAsync(int machineId, MachineState reading, string source,
        string? notes = null, bool manualStateOnly = false, CancellationToken cancellationToken = default)
    {
        if (FailNext) { FailNext = false; throw new InvalidOperationException("Simulated write failure"); }
        LastManualStateOnly = manualStateOnly;
        var result = ProductionRecordFactory.Create(reading,
            Rows.Where(x => x.Timestamp <= reading.Timestamp).OrderByDescending(x => x.Timestamp).FirstOrDefault(), manualStateOnly);
        result.Id = Rows.Count + 1;
        Rows.Add(result);
        return Task.FromResult(result);
    }
}
