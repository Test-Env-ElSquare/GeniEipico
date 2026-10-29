using API.Realtime.Scada;
using Application.Abstractions.Services;
using Microsoft.AspNetCore.SignalR;

namespace API.Realtime.Signage;

public sealed class SignageSignalsMonitor(IServiceScopeFactory scopes,
    ScadaRealtimeSubscriptionRegistry registry, IHubContext<SignageSignalsHub> hub,
    ILogger<SignageSignalsMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Same two-second full-snapshot broadcast as Eipico; only subscribed scopes are queried.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var subscription in registry.GetActiveSubscriptions()
                    .Where(x => x.Zone == SignageSignalsHub.Zone))
                {
                    try
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        var service = scope.ServiceProvider.GetRequiredService<ISignageLayoutService>();
                        var data = await service.GetFactorySignalsAsync(subscription.Scope);
                        await hub.Clients.Group(subscription.GroupName)
                            .SendAsync(SignageSignalsHub.Event, data, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Could not publish signage signals for factory {FactoryId}.",
                            subscription.Scope.FactoryId);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}