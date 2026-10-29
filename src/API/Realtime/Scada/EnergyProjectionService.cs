using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Services;
using Domain.Entities.IoT;

namespace API.Realtime.Scada;

/// <summary>Generic transformer projection from the same telemetry tags used by Production.</summary>
public sealed class EnergyProjectionService(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<EnergyProjectionService> logger) : BackgroundService
{
    private int _lastTagId;
    private readonly Dictionary<(string Source, DateTime At), Dictionary<string, TelemetryTag>> _pending = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Clamp(
            configuration.GetValue<int?>("ScadaRealtime:ProjectionPollIntervalMilliseconds") ?? 1000, 250, 60000)));
        do { await ProjectAsync(stoppingToken); }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProjectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var writer = scope.ServiceProvider.GetRequiredService<IEnergyRecordWriter>();
            var tags = uow.TelemetryTags.Query().Where(x => x.Id > _lastTagId).OrderBy(x => x.Id).Take(5000).ToList();
            var transformers = uow.Transformers.Query().Where(x => !x.IsDeleted).ToList()
                .Where(x => !string.IsNullOrWhiteSpace(x.TransformerName))
                .GroupBy(x => x.TransformerName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() == 1).ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
            foreach (var tag in tags)
            {
                if (!transformers.ContainsKey(tag.DeviceId.Trim())) continue;
                var key = (tag.DeviceId.Trim().ToUpperInvariant(), tag.RecordedAt);
                if (!_pending.TryGetValue(key, out var fields))
                    _pending[key] = fields = new(StringComparer.OrdinalIgnoreCase);
                if (!fields.TryGetValue(tag.TagName, out var old) || old.Id < tag.Id)
                    fields[tag.TagName] = tag;
            }
            // Keep incomplete groups across pages/polls until every energy field has arrived.
            foreach (var signal in _pending.Where(x => EnergySignalMapper.IsComplete(x.Value))
                         .OrderBy(x => x.Key.At).ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!transformers.TryGetValue(signal.Key.Source, out var transformer)) continue;
                await writer.AppendAsync(transformer.Id, EnergySignalMapper.Create(signal.Value, signal.Key.At), cancellationToken);
                _pending.Remove(signal.Key);
            }
            if (tags.Count > 0) _lastTagId = tags[^1].Id;
            if (_pending.Count > 10000)
            {
                logger.LogWarning("Energy pending signal limit reached; oldest incomplete signals will be discarded from memory (tags remain stored).");
                foreach (var key in _pending.Keys.OrderBy(x => x.At).Take(_pending.Count - 10000).ToArray()) _pending.Remove(key);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Energy projection failed after tag {TagId}; batch will be retried.", _lastTagId);
        }
    }
}
