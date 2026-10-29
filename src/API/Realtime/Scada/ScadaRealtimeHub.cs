using API.Common;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace API.Realtime.Scada;

public static class ScadaRealtimeEvents
{
    public const string WarehouseZoneUpdated = "WarehouseZoneUpdated";
    public const string DispensingZoneUpdated = "DispensingZoneUpdated";
    public const string ProductionZoneUpdated = "ProductionZoneUpdated";
    public const string FinishedGoodsZoneUpdated = "FinishedGoodsZoneUpdated";
    public const string FactorySnapshotUpdated = "FactorySnapshotUpdated";
    public const string FactoryLayoutUpdated = "FactoryLayoutUpdated";
}

public sealed record ScadaRealtimeEnvelope(
    bool Success,
    object? Data,
    object? Error,
    DateTime Ts)
{
    public static ScadaRealtimeEnvelope Ok(object? data)
        => new(true, data, null, DateTime.UtcNow);
}

public sealed record ScadaRealtimeSubscription(
    string Zone,
    string EventName,
    string GroupName,
    ScadaLiveViewScope Scope);

public sealed class ScadaRealtimeSubscriptionRegistry
{
    private readonly ConcurrentDictionary<string, ScadaRealtimeSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, int> _subscriberCounts = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _connectionGroups = new();

    public ScadaRealtimeSubscription Create(
        string zone,
        string eventName,
        ScadaLiveViewScope scope)
    {
        var lineScope = scope.AllowedLineIds.Count == 0
            ? "none"
            : string.Join("-", scope.AllowedLineIds.OrderBy(x => x));
        var selectedLine = scope.LineId?.ToString() ?? "all";
        var identity = $"{zone}|{scope.CustomerId}|{scope.FactoryId}|{selectedLine}|{lineScope}";
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];

        return new ScadaRealtimeSubscription(
            zone,
            eventName,
            $"scada:{zone}:{hash}",
            scope);
    }

    public bool Register(
        string connectionId,
        ScadaRealtimeSubscription subscription)
    {
        var groups = _connectionGroups.GetOrAdd(
            connectionId,
            _ => new ConcurrentDictionary<string, byte>());

        if (!groups.TryAdd(subscription.GroupName, 0))
            return false;

        _subscriptions[subscription.GroupName] = subscription;
        _subscriberCounts.AddOrUpdate(
            subscription.GroupName,
            1,
            (_, count) => count + 1);
        return true;
    }

    public IReadOnlyList<ScadaRealtimeSubscription> UnregisterZone(
        string connectionId,
        string zone)
    {
        if (!_connectionGroups.TryGetValue(connectionId, out var groups))
            return Array.Empty<ScadaRealtimeSubscription>();

        var removed = new List<ScadaRealtimeSubscription>();
        foreach (var groupName in groups.Keys)
        {
            if (!_subscriptions.TryGetValue(groupName, out var subscription) ||
                !string.Equals(subscription.Zone, zone, StringComparison.Ordinal))
            {
                continue;
            }

            if (groups.TryRemove(groupName, out _))
            {
                removed.Add(subscription);
                Decrement(groupName);
            }
        }

        if (groups.IsEmpty)
            _connectionGroups.TryRemove(connectionId, out _);

        return removed;
    }

    public void UnregisterConnection(string connectionId)
    {
        if (!_connectionGroups.TryRemove(connectionId, out var groups))
            return;

        foreach (var groupName in groups.Keys)
            Decrement(groupName);
    }

    public IReadOnlyList<ScadaRealtimeSubscription> GetActiveSubscriptions()
        => _subscriptions.Values.ToArray();

    private void Decrement(string groupName)
    {
        while (_subscriberCounts.TryGetValue(groupName, out var count))
        {
            if (count <= 1)
            {
                if (_subscriberCounts.TryRemove(
                        new KeyValuePair<string, int>(groupName, count)))
                {
                    _subscriptions.TryRemove(groupName, out _);
                    return;
                }

                continue;
            }

            if (_subscriberCounts.TryUpdate(groupName, count - 1, count))
                return;
        }
    }
}

[Authorize]
public sealed class ScadaRealtimeHub : Hub
{
    private readonly IUnitOfWork _uow;
    private readonly IScadaLiveViewService _scada;
    private readonly IFactorySnapshotService _factorySnapshot;
    private readonly ScadaRealtimeSubscriptionRegistry _registry;

    public ScadaRealtimeHub(
        IUnitOfWork uow,
        IScadaLiveViewService scada,
        IFactorySnapshotService factorySnapshot,
        ScadaRealtimeSubscriptionRegistry registry)
    {
        _uow = uow;
        _scada = scada;
        _factorySnapshot = factorySnapshot;
        _registry = registry;
    }

    public async Task<ScadaRealtimeEnvelope> SubscribeWarehouse(
        int factoryId,
        int? lineId)
    {
        var scope = ResolveScope(factoryId, lineId);
        var result = await _scada.GetWarehouseZoneAsync(scope);
        var subscription = _registry.Create(
            "warehouse",
            ScadaRealtimeEvents.WarehouseZoneUpdated,
            scope);
        await ReplaceZoneSubscriptionAsync(subscription);
        return ScadaRealtimeEnvelope.Ok(result.Data);
    }

    public async Task<ScadaRealtimeEnvelope> SubscribeDispensing(
        int factoryId,
        int? lineId)
    {
        var scope = ResolveScope(factoryId, lineId);
        var result = await _scada.GetDispensingZoneAsync(scope);
        var subscription = _registry.Create(
            "dispensing",
            ScadaRealtimeEvents.DispensingZoneUpdated,
            scope);
        await ReplaceZoneSubscriptionAsync(subscription);
        return ScadaRealtimeEnvelope.Ok(result.Data);
    }

    public async Task<ScadaRealtimeEnvelope> SubscribeProduction(
        int factoryId,
        int? lineId)
    {
        var scope = ResolveScope(factoryId, lineId);
        var result = await _scada.GetProductionZoneAsync(scope);
        var subscription = _registry.Create(
            "production",
            ScadaRealtimeEvents.ProductionZoneUpdated,
            scope);
        await ReplaceZoneSubscriptionAsync(subscription);
        return ScadaRealtimeEnvelope.Ok(result.Data);
    }
    public async Task<ScadaRealtimeEnvelope> SubscribeFinishedGoods(
    int factoryId,
    int? lineId)
    {
        var scope = ResolveScope(factoryId, lineId);

        var result = await _scada.GetFinishedGoodsZoneAsync(scope);

        var subscription = _registry.Create(
            "finished-goods",
            ScadaRealtimeEvents.FinishedGoodsZoneUpdated,
            scope);

        await ReplaceZoneSubscriptionAsync(subscription);

        return ScadaRealtimeEnvelope.Ok(result.Data);
    }

    public async Task<ScadaRealtimeEnvelope> SubscribeFactorySnapshot(
        int factoryId,
        int? lineId)
    {
        var scope = ResolveScope(factoryId, lineId);
        var result = await _factorySnapshot.ReadCurrentAsync(scope, Context.ConnectionAborted);
        if (!result.Available)
            throw new HubException("The live factory snapshot view is not configured or unavailable for this factory.");

        var subscription = _registry.Create(
            "factory-snapshot",
            ScadaRealtimeEvents.FactorySnapshotUpdated,
            scope);
        await ReplaceZoneSubscriptionAsync(subscription);
        return ScadaRealtimeEnvelope.Ok(result.Data);
    }

    public async Task<ScadaRealtimeEnvelope> SubscribeFactoryLayout(
        int factoryId,
        int? lineId)
    {
        var scope = ResolveScope(factoryId, lineId);
        var result = await _scada.GetFactoryLayoutAsync(scope);
        var subscription = _registry.Create(
            "factory-layout",
            ScadaRealtimeEvents.FactoryLayoutUpdated,
            scope);
        await ReplaceZoneSubscriptionAsync(subscription);
        return ScadaRealtimeEnvelope.Ok(result.Data);
    }

    public Task UnsubscribeWarehouse()
        => RemoveZoneSubscriptionsAsync("warehouse");

    public Task UnsubscribeDispensing()
        => RemoveZoneSubscriptionsAsync("dispensing");

    public Task UnsubscribeProduction()
        => RemoveZoneSubscriptionsAsync("production");
    public Task UnsubscribeFinishedGoods()
    => RemoveZoneSubscriptionsAsync("finished-goods");

    public Task UnsubscribeFactorySnapshot()
        => RemoveZoneSubscriptionsAsync("factory-snapshot");

    public Task UnsubscribeFactoryLayout()
        => RemoveZoneSubscriptionsAsync("factory-layout");

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _registry.UnregisterConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private async Task RemoveZoneSubscriptionsAsync(string zone)
    {
        var subscriptions = _registry.UnregisterZone(Context.ConnectionId, zone);
        foreach (var subscription in subscriptions)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                subscription.GroupName);
        }
    }

    private async Task ReplaceZoneSubscriptionAsync(
        ScadaRealtimeSubscription subscription)
    {
        await RemoveZoneSubscriptionsAsync(subscription.Zone);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            subscription.GroupName);

        if (_registry.Register(Context.ConnectionId, subscription))
            return;

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            subscription.GroupName);
        throw new HubException(
            $"Could not register the {subscription.Zone} SCADA subscription.");
    }

    private ScadaLiveViewScope ResolveScope(int factoryId, int? lineId)
    {
        if (!AuthenticatedAccessScope.TryResolve(
                Context.User!,
                _uow,
                out var access,
                out _,
                out var accessError))
        {
            throw new HubException(accessError);
        }

        if (factoryId <= 0)
            throw new HubException("FactoryId must be greater than zero.");

        if (!access.CanAccessFactory(factoryId))
        {
            throw new HubException(
                $"Factory {factoryId} is outside the authenticated user's access scope.");
        }

        if (lineId.HasValue && lineId.Value <= 0)
            throw new HubException("LineId must be greater than zero when supplied.");

        if (lineId.HasValue &&
            (!access.LineFactoryIds.TryGetValue(lineId.Value, out var lineFactoryId) ||
             lineFactoryId != factoryId))
        {
            throw new HubException(
                $"Line {lineId.Value} is outside Factory {factoryId} or the authenticated user's access scope.");
        }

        var allowedLineIds = lineId.HasValue
            ? new HashSet<int> { lineId.Value }
            : access.LineFactoryIds
                .Where(x => x.Value == factoryId)
                .Select(x => x.Key)
                .ToHashSet();

        return new ScadaLiveViewScope(
            access.CustomerId,
            factoryId,
            lineId,
            allowedLineIds);
    }
}

public sealed class ScadaRealtimeMonitorService : BackgroundService
{
    private static readonly JsonSerializerOptions HashJsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ScadaRealtimeHub> _hubContext;
    private readonly ScadaRealtimeSubscriptionRegistry _registry;
    private readonly ILogger<ScadaRealtimeMonitorService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _factorySnapshotPollInterval;
    private readonly TimeSpan _factoryLayoutPollInterval;
    private readonly ConcurrentDictionary<string, string> _lastHashes = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastPolls = new();

    public ScadaRealtimeMonitorService(
        IServiceScopeFactory scopeFactory,
        IHubContext<ScadaRealtimeHub> hubContext,
        ScadaRealtimeSubscriptionRegistry registry,
        IConfiguration configuration,
        ILogger<ScadaRealtimeMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _registry = registry;
        _logger = logger;

        var configuredInterval =
            configuration.GetValue<int?>("ScadaRealtime:PollIntervalMilliseconds") ?? 1000;
        _pollInterval = TimeSpan.FromMilliseconds(
            Math.Clamp(configuredInterval, 250, 60_000));
        var factorySnapshotInterval =
            configuration.GetValue<int?>("ScadaRealtime:FactorySnapshotPollIntervalMilliseconds") ?? 10_000;
        _factorySnapshotPollInterval = TimeSpan.FromMilliseconds(
            Math.Clamp(factorySnapshotInterval, 1_000, 300_000));
        var factoryLayoutInterval =
            configuration.GetValue<int?>("ScadaRealtime:FactoryLayoutPollIntervalMilliseconds") ?? 10_000;
        _factoryLayoutPollInterval = TimeSpan.FromMilliseconds(
            Math.Clamp(factoryLayoutInterval, 1_000, 300_000));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var subscriptions = _registry.GetActiveSubscriptions();
            var activeGroups = subscriptions
                .Select(x => x.GroupName)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var cachedGroup in _lastHashes.Keys)
            {
                if (!activeGroups.Contains(cachedGroup))
                    _lastHashes.TryRemove(cachedGroup, out _);
            }

            foreach (var cachedGroup in _lastPolls.Keys)
            {
                if (!activeGroups.Contains(cachedGroup))
                    _lastPolls.TryRemove(cachedGroup, out _);
            }

            foreach (var subscription in subscriptions)
            {
                if (!ShouldPoll(subscription))
                    continue;

                try
                {
                    await PublishIfChangedAsync(subscription, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Failed to publish SCADA {Zone} update to group {GroupName}.",
                        subscription.Zone,
                        subscription.GroupName);
                }
            }
        }
    }

    private bool ShouldPoll(ScadaRealtimeSubscription subscription)
    {
        var interval = subscription.Zone switch
        {
            "factory-snapshot" => _factorySnapshotPollInterval,
            "factory-layout" => _factoryLayoutPollInterval,
            _ => (TimeSpan?)null
        };
        if (!interval.HasValue)
            return true;

        var now = DateTimeOffset.UtcNow;
        if (!_lastPolls.TryGetValue(subscription.GroupName, out var lastPoll))
        {
            _lastPolls[subscription.GroupName] = now;
            return false;
        }

        if (now - lastPoll < interval.Value)
            return false;

        _lastPolls[subscription.GroupName] = now;
        return true;
    }

    private async Task PublishIfChangedAsync(
        ScadaRealtimeSubscription subscription,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var scada = scope.ServiceProvider.GetRequiredService<IScadaLiveViewService>();
        var data = subscription.Zone switch
        {
            "warehouse" => (await scada.GetWarehouseZoneAsync(subscription.Scope)).Data,
            "dispensing" => (await scada.GetDispensingZoneAsync(subscription.Scope)).Data,
            "production" => (await scada.GetProductionZoneAsync(subscription.Scope)).Data,
            "finished-goods" => (await scada.GetFinishedGoodsZoneAsync(subscription.Scope)).Data,
            "factory-layout" => (await scada.GetFactoryLayoutAsync(subscription.Scope)).Data,
            "factory-snapshot" => await ReadFactorySnapshotAsync(scope.ServiceProvider, subscription, cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unsupported SCADA realtime zone '{subscription.Zone}'.")
        };

        var serialized = JsonSerializer.Serialize(data, HashJsonOptions);
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));

        if (_lastHashes.TryGetValue(subscription.GroupName, out var previousHash) &&
            string.Equals(previousHash, hash, StringComparison.Ordinal))
        {
            return;
        }

        _lastHashes[subscription.GroupName] = hash;
        await _hubContext.Clients
            .Group(subscription.GroupName)
            .SendAsync(
                subscription.EventName,
                ScadaRealtimeEnvelope.Ok(data),
                cancellationToken);
    }

    private static async Task<object?> ReadFactorySnapshotAsync(
        IServiceProvider services,
        ScadaRealtimeSubscription subscription,
        CancellationToken cancellationToken)
    {
        var service = services.GetRequiredService<IFactorySnapshotService>();
        var result = await service.ReadCurrentAsync(subscription.Scope, cancellationToken);
        if (!result.Available)
            throw new InvalidOperationException(
                $"The live factory snapshot source is unavailable for Factory {subscription.Scope.FactoryId}.");

        return result.Data;
    }
}
