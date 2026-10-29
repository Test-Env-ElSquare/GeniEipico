using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Domain.Entities.IoT;
using Domain.Entities.Scada;

namespace API.Realtime.Scada;

/// <summary>
/// Appends one RealTime.Production reading per machine telemetry signal.
/// Only sources that match an active machine name are considered; transformer
/// and other telemetry sources are intentionally ignored.
/// </summary>
public sealed class ScadaProductionProjectionService : BackgroundService
{
    private const int ReadBatchSize = 5_000;
    private const int SignalBoundaryLookAhead = 256;
    private static readonly string[] RequiredProductionTags =
    {
        "State",
        "Speed",
        "Count",
        "Qreject",
        "StateReason",
        "SKU",
        "Fault"
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScadaProductionProjectionService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly Dictionary<SignalKey, Dictionary<string, TelemetryTag>>
        _pendingSignals = new();
    private int _lastProcessedTagId;

    public ScadaProductionProjectionService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ScadaProductionProjectionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var configuredInterval =
            configuration.GetValue<int?>("ScadaRealtime:ProjectionPollIntervalMilliseconds") ??
            configuration.GetValue<int?>("ScadaRealtime:PollIntervalMilliseconds") ??
            1_000;
        _pollInterval = TimeSpan.FromMilliseconds(
            Math.Clamp(configuredInterval, 250, 60_000));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProjectNextBatchAsync(stoppingToken);

        using var timer = new PeriodicTimer(_pollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ProjectNextBatchAsync(stoppingToken);
    }

    private async Task ProjectNextBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var writer = scope.ServiceProvider.GetRequiredService<IProductionRecordWriter>();

            var tags = unitOfWork.TelemetryTags.Query()
                .Where(x => x.Id > _lastProcessedTagId)
                .OrderBy(x => x.Id)
                .Take(ReadBatchSize)
                .ToList();
            if (tags.Count == 0 && _pendingSignals.Count == 0)
                return;

            // Ingest writes all tags of one signal consecutively. If a read
            // page ends in the middle of that signal, include its remaining
            // consecutive tags before projecting it.
            if (tags.Count == ReadBatchSize)
            {
                var boundary = tags[^1];
                var lookAhead = unitOfWork.TelemetryTags.Query()
                    .Where(x => x.Id > boundary.Id)
                    .OrderBy(x => x.Id)
                    .Take(SignalBoundaryLookAhead)
                    .ToList();
                foreach (var next in lookAhead)
                {
                    if (!string.Equals(
                            next.DeviceId,
                            boundary.DeviceId,
                            StringComparison.OrdinalIgnoreCase) ||
                        next.RecordedAt != boundary.RecordedAt)
                    {
                        break;
                    }

                    tags.Add(next);
                }
            }

            var batchWatermark = tags.Count == 0 ? _lastProcessedTagId : tags[^1].Id;
            var machines = unitOfWork.Machines.Query()
                .Where(x => x.IsActive && !x.IsDeleted)
                .ToList();
            var lineIds = machines.Select(x => x.LineId).Distinct().ToArray();
            var factoryByLineId = unitOfWork.ProductionLines.Query()
                .Where(x => lineIds.Contains(x.Id) && x.IsActive && !x.IsDeleted)
                .ToDictionary(x => x.Id, x => x.FactoryId);

            // Machine names are the external identifiers used by the current
            // telemetry contract. Ignore duplicate names because they cannot
            // be mapped to a single machine safely.
            var machineByExternalId = machines
                .Where(x => !string.IsNullOrWhiteSpace(x.Name) &&
                            factoryByLineId.ContainsKey(x.LineId))
                .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() == 1)
                .ToDictionary(
                    x => x.Key,
                    x => x.Single(),
                    StringComparer.OrdinalIgnoreCase);

            CollectPendingSignals(tags, machineByExternalId);
            var readySignals = _pendingSignals
                .Where(x => IsProductionSignal(x.Value))
                .OrderBy(x => x.Key.RecordedAt)
                .ThenBy(x => x.Value.Values.Min(tag => tag.Id))
                .ToArray();
            if (readySignals.Length == 0)
            {
                _lastProcessedTagId = batchWatermark;
                return;
            }

            foreach (var signal in readySignals)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!machineByExternalId.TryGetValue(signal.Key.ExternalId, out var machine))
                {
                    _pendingSignals.Remove(signal.Key);
                    continue;
                }
                var factoryId = factoryByLineId[machine.LineId];
                var record = new MachineState
                {
                    MachineId = machine.Name,
                    FactoryId = factoryId,
                    LineId = machine.LineId
                };
                Apply(signal.Value, record, signal.Key.RecordedAt);
                await writer.AppendAsync(machine.Id, record, "telemetry", cancellationToken: cancellationToken);
                // Remove only after this signal and its history have committed.
                _pendingSignals.Remove(signal.Key);
            }

            _lastProcessedTagId = batchWatermark;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            // Keep the watermark unchanged so the same batch is retried after
            // a transient database or mapping failure.
            _logger.LogError(
                exception,
                "Failed to project telemetry tags into RealTime.Production after tag {TagId}.",
                _lastProcessedTagId);
        }
    }

    private void CollectPendingSignals(
        IEnumerable<TelemetryTag> tags,
        IReadOnlyDictionary<string, Machine> machineByExternalId)
    {
        foreach (var tag in tags)
        {
            if (!machineByExternalId.ContainsKey(tag.DeviceId))
                continue;

            var key = new SignalKey(
                tag.DeviceId.Trim().ToUpperInvariant(),
                tag.RecordedAt);
            if (!_pendingSignals.TryGetValue(key, out var values))
            {
                values = new Dictionary<string, TelemetryTag>(
                    StringComparer.OrdinalIgnoreCase);
                _pendingSignals[key] = values;
            }

            if (!values.TryGetValue(tag.TagName, out var previous) ||
                tag.Id > previous.Id)
            {
                values[tag.TagName] = tag;
            }
        }

        const int maximumPendingSignals = 10_000;
        if (_pendingSignals.Count <= maximumPendingSignals)
            return;

        foreach (var staleKey in _pendingSignals.Keys
                     .OrderBy(x => x.RecordedAt)
                     .Take(_pendingSignals.Count - maximumPendingSignals)
                     .ToArray())
        {
            _pendingSignals.Remove(staleKey);
        }
    }

    private static bool IsProductionSignal(
        IReadOnlyDictionary<string, TelemetryTag> values)
        => RequiredProductionTags.All(values.ContainsKey);

    private static void Apply(
        IReadOnlyDictionary<string, TelemetryTag> values,
        MachineState state,
        DateTime recordedAt)
    {
        state.Count = IntValue(values, "Count", 0);
        state.State = IntValue(values, "State", state.State);
        state.Speed = IntValue(values, "Speed", state.Speed);
        state.QReject = IntValue(values, "Qreject", state.QReject);
        state.StateReason = IntValue(values, "StateReason", state.StateReason);
        state.Sku = IntValue(values, "SKU", state.Sku);
        state.Fault = IntValue(values, "Fault", state.Fault);
        state.Temp = DecimalValue(values, "Temp") ??
                     DecimalValue(values, "Temperature") ??
                     DecimalValue(values, "TempCelsius") ??
                     state.Temp;
        state.Timestamp = recordedAt;
    }

    private static int IntValue(
        IReadOnlyDictionary<string, TelemetryTag> values,
        string name,
        int fallback)
    {
        if (!values.TryGetValue(name, out var tag) || !tag.ValueNum.HasValue)
            return fallback;

        var value = decimal.Truncate(tag.ValueNum.Value);
        if (value > int.MaxValue) return int.MaxValue;
        if (value < int.MinValue) return int.MinValue;
        return decimal.ToInt32(value);
    }

    private static decimal? DecimalValue(
        IReadOnlyDictionary<string, TelemetryTag> values,
        string name)
        => values.TryGetValue(name, out var tag) ? tag.ValueNum : null;

    private sealed record SignalKey(string ExternalId, DateTime RecordedAt);
}
