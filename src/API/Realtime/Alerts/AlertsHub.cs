using API.Common;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Dtos.Alerts;
using Domain.Entities.Alarms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace API.Realtime.Alerts;

[Authorize]
public sealed class AlertsHub(IUnitOfWork uow) : Hub
{
    public const string Route = "/hubs/alerts";
    public const string ReceiveEvent = "ReceiveAlert";

    public override async Task OnConnectedAsync()
    {
        if (!AuthenticatedAccessScope.TryResolve(
                Context.User!, uow, out var access, out _, out var errorMessage))
        {
            throw new HubException(errorMessage);
        }

        var groups = new HashSet<string>(StringComparer.Ordinal)
        {
            UserGroup(access.UserId),
            CustomerGroup(access.CustomerId)
        };

        foreach (var factoryId in access.FactoryIds)
            groups.Add(FactoryGroup(access.CustomerId, factoryId));

        foreach (var group in groups)
            await Groups.AddToGroupAsync(Context.ConnectionId, group);

        await base.OnConnectedAsync();
    }

    // Preserves the original EMS group naming contract.
    public static string UserGroup(int userId) => $"alerts:user:{userId}";

    internal static string CustomerGroup(int customerId)
        => $"alerts:customer:{customerId}";

    internal static string FactoryGroup(int customerId, int factoryId)
        => $"alerts:customer:{customerId}:factory:{factoryId}";
}

public sealed class SignalRAlertPublisher(
    IHubContext<AlertsHub> hub,
    IUnitOfWork uow,
    ILogger<SignalRAlertPublisher> logger) : IRealtimeAlertPublisher
{
    private const int AlarmTriggeredType = 1;

    public async Task PublishTriggeredAsync(
        AlarmEvent alarm,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var factoryId = ResolveFactoryId(alarm);
            var group = factoryId.HasValue
                ? AlertsHub.FactoryGroup(alarm.CustomerId, factoryId.Value)
                : AlertsHub.CustomerGroup(alarm.CustomerId);

            var ticketId = uow.WorkOrders.Query()
                .Where(x => x.AlarmEventId == alarm.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();

            var payload = new RealtimeAlertDto(
                alarm.Id,
                alarm.Title,
                string.IsNullOrWhiteSpace(alarm.Notes) ? alarm.Title : alarm.Notes,
                AlarmTriggeredType,
                (int)alarm.Severity + 1,
                alarm.AcknowledgedAt.HasValue,
                AsUtc(alarm.TriggeredAt),
                alarm.AcknowledgedAt.HasValue ? AsUtc(alarm.AcknowledgedAt.Value) : null,
                alarm.Id,
                ticketId);

            await hub.Clients.Group(group).SendAsync(
                AlertsHub.ReceiveEvent,
                payload,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Realtime delivery must never roll back or fail the persisted alarm workflow.
            logger.LogError(
                exception,
                "Alarm {AlarmId} was saved but its SignalR notification could not be published.",
                alarm.Id);
        }
    }

    private int? ResolveFactoryId(AlarmEvent alarm)
    {
        if (alarm.MachineId.HasValue)
        {
            var machineLineId = uow.Machines.Query()
                .Where(x => x.Id == alarm.MachineId.Value)
                .Select(x => (int?)x.LineId)
                .FirstOrDefault();
            if (machineLineId.HasValue)
            {
                var machineFactoryId = uow.ProductionLines.Query()
                    .Where(x => x.Id == machineLineId.Value)
                    .Select(x => (int?)x.FactoryId)
                    .FirstOrDefault();
                if (machineFactoryId.HasValue)
                    return machineFactoryId;
            }
        }

        if (alarm.SiloId.HasValue)
        {
            var siloFactoryId = uow.Silos.Query()
                .Where(x => x.Id == alarm.SiloId.Value)
                .Select(x => (int?)x.FactoryId)
                .FirstOrDefault();
            if (siloFactoryId.HasValue)
                return siloFactoryId;
        }

        if (alarm.DeviceId.HasValue)
        {
            return uow.Devices.Query()
                .Where(x => x.Id == alarm.DeviceId.Value)
                .Select(x => (int?)x.FactoryId)
                .FirstOrDefault();
        }

        return null;
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
