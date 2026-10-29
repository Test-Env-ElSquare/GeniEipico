using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Integration;

public sealed class EipicoSyncWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EipicoSyncOptions> configuredOptions,
    ILogger<EipicoSyncWorker> logger) : BackgroundService
{
    private readonly EipicoSyncOptions options = configuredOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Eipico synchronization started. The source connection is enforced as read-only.");

        var nextReconciliationAtUtc = DateTime.MinValue;
        var nextDefinitionsSyncAtUtc = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // SAP batches and realtime projections require the canonical
                // factory/line/scale definitions, particularly after a reset.
                if (DateTime.UtcNow >= nextDefinitionsSyncAtUtc)
                {
                    await using var definitionScope = scopeFactory.CreateAsyncScope();
                    var definitionService = definitionScope.ServiceProvider
                        .GetRequiredService<EipicoDefinitionSyncService>();
                    await definitionService.SyncAsync(stoppingToken);

                    nextDefinitionsSyncAtUtc = DateTime.UtcNow.AddSeconds(
                        Math.Max(30, options.DefinitionsSyncIntervalSeconds));
                }

                // Bound SAP work per cycle so a backlog of pending orders
                // cannot delay the first realtime checkpoint or live ingestion.
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var syncService = scope.ServiceProvider.GetRequiredService<EipicoSapSyncService>();
                    var synchronizedCount = await syncService.SyncNextPageAsync(stoppingToken);
                    if (synchronizedCount > 0)
                    {
                        logger.LogInformation(
                            "Processed {Count} Eipico SAP orders.",
                            synchronizedCount);
                    }
                }

                EipicoRealtimeSyncResult realtimeResult;
                do
                {
                    await using var realtimeScope = scopeFactory.CreateAsyncScope();
                    var realtimeService = realtimeScope.ServiceProvider
                        .GetRequiredService<EipicoRealtimeSyncService>();
                    realtimeResult = await realtimeService.SyncNextPagesAsync(stoppingToken);

                    if (realtimeResult.TotalCount > 0)
                    {
                        logger.LogInformation(
                            "Processed Eipico realtime records: {SignalCount} signals, " +
                            "{ScaleSignalCount} scale signals, {ScaleLiveMessageCount} scale heartbeats, " +
                            "{EnergyCount} energy, {HarmonicCount} harmonics, {HandheldCount} handheld, " +
                            "{SplitCount} splits, {MachineLoadCount} machine loads.",
                            realtimeResult.SignalCount,
                            realtimeResult.ScaleSignalCount,
                            realtimeResult.ScaleLiveMessageCount,
                            realtimeResult.EnergyCount,
                            realtimeResult.HarmonicCount,
                            realtimeResult.HandheldCount,
                            realtimeResult.SplitCount,
                            realtimeResult.MachineLoadCount);
                    }
                }
                while ((realtimeResult.SignalCount >= Math.Clamp(options.BatchSize, 1, 1000) ||
                        realtimeResult.ScaleSignalCount >= Math.Clamp(options.BatchSize, 1, 1000) ||
                        realtimeResult.ScaleLiveMessageCount >= Math.Clamp(options.BatchSize, 1, 1000) ||
                        realtimeResult.EnergyCount >= Math.Clamp(options.BatchSize, 1, 1000) ||
                        realtimeResult.HarmonicCount >= Math.Clamp(options.BatchSize, 1, 1000) ||
                        realtimeResult.HandheldCount >= Math.Clamp(options.BatchSize, 1, 1000) ||
                        realtimeResult.SplitCount >= Math.Clamp(options.BatchSize, 1, 1000) ||
                        realtimeResult.MachineLoadCount >= Math.Clamp(options.BatchSize, 1, 1000)) &&
                       !stoppingToken.IsCancellationRequested);

                if (options.ReconciliationBatchSize > 0 &&
                    DateTime.UtcNow >= nextReconciliationAtUtc)
                {
                    await using var reconciliationScope = scopeFactory.CreateAsyncScope();
                    var reconciliationService = reconciliationScope.ServiceProvider
                        .GetRequiredService<EipicoSapSyncService>();
                    var reconciledCount = await reconciliationService
                        .ReconcileRecentOrdersAsync(stoppingToken);

                    if (reconciledCount > 0)
                    {
                        logger.LogInformation(
                            "Reconciled {Count} recent Eipico SAP orders.",
                            reconciledCount);
                    }

                    var realtimeReconciliationService = reconciliationScope.ServiceProvider
                        .GetRequiredService<EipicoRealtimeSyncService>();
                    var reconciledRealtime = await realtimeReconciliationService
                        .ReconcileRecentAsync(stoppingToken);

                    if (reconciledRealtime.TotalCount > 0)
                    {
                        logger.LogInformation(
                            "Reconciled Eipico realtime records: {HandheldCount} handheld, " +
                            "{SplitCount} splits, {MachineLoadCount} machine loads.",
                            reconciledRealtime.HandheldCount,
                            reconciledRealtime.SplitCount,
                            reconciledRealtime.MachineLoadCount);
                    }

                    nextReconciliationAtUtc = DateTime.UtcNow.AddSeconds(
                        Math.Max(1, options.ReconciliationIntervalSeconds));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The Eipico synchronization cycle failed.");
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
