using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Integration;

public sealed class EmsEnergySyncWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EmsEnergySyncOptions> configuredOptions,
    ILogger<EmsEnergySyncWorker> logger) : BackgroundService
{
    private readonly EmsEnergySyncOptions options = configuredOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initialized = false;
        var nextDefinitionsAtUtc = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!initialized)
                {
                    // Capture the live boundary before definitions are copied so rows
                    // arriving during definition synchronization are not missed.
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var realtime = scope.ServiceProvider
                        .GetRequiredService<EmsEnergyRealtimeSyncService>();
                    var checkpoint = await realtime.EnsureLiveCheckpointAsync(stoppingToken);
                    logger.LogInformation(
                        checkpoint.InitializedNow
                            ? "EMS Energy live-only checkpoint initialized at source id {SourceId}; historical rows are skipped."
                            : "EMS Energy synchronization resumed after source id {SourceId}.",
                        checkpoint.LastSourceId);
                    initialized = true;
                }

                if (DateTime.UtcNow >= nextDefinitionsAtUtc)
                {
                    await using var definitionScope = scopeFactory.CreateAsyncScope();
                    var definitions = definitionScope.ServiceProvider
                        .GetRequiredService<EmsEnergyDefinitionSyncService>();
                    var result = await definitions.SyncAndVerifyAsync(stoppingToken);
                    logger.LogInformation(
                        "EMS definitions mapped and verified: {Factories} existing factories, " +
                        "{Transformers} transformers, {Zones} energy zones and {Lines} lines.",
                        result.Factories,
                        result.Transformers,
                        result.Zones,
                        result.Lines);
                    nextDefinitionsAtUtc = DateTime.UtcNow.AddSeconds(
                        Math.Max(30, options.DefinitionsSyncIntervalSeconds));
                }

                EmsEnergyPageSyncResult page;
                do
                {
                    await using var realtimeScope = scopeFactory.CreateAsyncScope();
                    var realtime = realtimeScope.ServiceProvider
                        .GetRequiredService<EmsEnergyRealtimeSyncService>();
                    page = await realtime.SyncNextPageAsync(options.BatchSize, stoppingToken);
                    if (page.Count > 0)
                    {
                        logger.LogInformation(
                            "Mirrored and verified {Count} new EMS Energy rows through source id {SourceId}.",
                            page.Count,
                            page.LastSourceId);
                    }
                }
                while (page.Count >= Math.Clamp(options.BatchSize, 1, 5000) &&
                       !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "EMS Energy synchronization cycle failed; the same checkpoint will be retried.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, options.PollIntervalSeconds)),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
