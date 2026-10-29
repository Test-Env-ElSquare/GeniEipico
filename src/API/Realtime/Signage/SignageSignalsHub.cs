using API.Realtime.Scada;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace API.Realtime.Signage;

[Authorize]
public sealed class SignageSignalsHub(IUnitOfWork uow, ISignageLayoutService signage,
    ScadaRealtimeSubscriptionRegistry registry) : Hub
{
    public const string Zone = "signage-signals";
    public const string Event = "ReceiveFactorySignals";

    public async Task JoinFactoryGroup(int factoryId)
    {
        var scope = SignageAccess.Resolve(Context.User!, uow, factoryId);
        await LeaveSubscriptionsAsync();
        var subscription = registry.Create(Zone, Event, scope);
        await Groups.AddToGroupAsync(Context.ConnectionId, subscription.GroupName);
        if (!registry.Register(Context.ConnectionId, subscription))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, subscription.GroupName);
            throw new HubException("Could not register factory signals subscription.");
        }
    }

    public async Task GetFactorySignals(int factoryId)
    {
        var scope = SignageAccess.Resolve(Context.User!, uow, factoryId);
        await Clients.Caller.SendAsync(Event, await signage.GetFactorySignalsAsync(scope));
    }

    public Task LeaveFactoryGroup(int factoryId) => LeaveSubscriptionsAsync();

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registry.UnregisterConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private async Task LeaveSubscriptionsAsync()
    {
        foreach (var subscription in registry.UnregisterZone(Context.ConnectionId, Zone))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, subscription.GroupName);
    }
}

[Authorize]
public sealed class SignageScaleStatusHub(IUnitOfWork uow, ISignageLayoutService signage) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var query = Context.GetHttpContext()?.Request.Query["factoryId"].ToString();
        if (!int.TryParse(query, out var factoryId)) throw new HubException("A valid factoryId is required.");
        var scope = SignageAccess.Resolve(Context.User!, uow, factoryId);
        await Clients.Caller.SendAsync("InitialScales", signage.GetScales(scope));
        await base.OnConnectedAsync();
    }
}