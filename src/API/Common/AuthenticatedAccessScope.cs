using Application.Abstractions.Repositories;
using Domain.Entities.Alarms;
using Domain.Entities.Maintenance;
using System.Security.Claims;

namespace API.Common;

public sealed record AuthenticatedAccessScope(
    int CustomerId,
    int UserId,
    IReadOnlySet<int> FactoryIds,
    IReadOnlyDictionary<int, int> LineFactoryIds)
{
    public IReadOnlySet<int> LineIds => LineFactoryIds.Keys.ToHashSet();

    public static bool TryResolve(
        ClaimsPrincipal user,
        IUnitOfWork unitOfWork,
        out AuthenticatedAccessScope scope,
        out string errorCode,
        out string errorMessage)
    {
        scope = default!;
        errorCode = string.Empty;
        errorMessage = string.Empty;

        if (!int.TryParse(user.FindFirstValue("customerId"), out var customerId) || customerId <= 0 ||
            !int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
        {
            errorCode = "not_authenticated";
            errorMessage = "The access token does not contain a valid user and customer scope.";
            return false;
        }

        var customerFactoryIds = unitOfWork.Factories.Query()
            .Where(x => x.CustomerId == customerId && x.IsActive && !x.IsDeleted)
            .Select(x => x.Id)
            .ToHashSet();
        var factoryClaims = PositiveClaims(user, "factoryId");
        var factoryIds = factoryClaims.Count == 0 && user.IsInRole("Admin")
            ? customerFactoryIds
            : customerFactoryIds.Where(factoryClaims.Contains).ToHashSet();

        var customerLines = unitOfWork.ProductionLines.Query()
            .Where(x => factoryIds.Contains(x.FactoryId) && x.IsActive && !x.IsDeleted)
            .Select(x => new { x.Id, x.FactoryId })
            .ToList();
        var lineClaims = PositiveClaims(user, "lineId");
        if (lineClaims.Count > 0)
            customerLines = customerLines.Where(x => lineClaims.Contains(x.Id)).ToList();

        scope = new AuthenticatedAccessScope(
            customerId,
            userId,
            factoryIds,
            customerLines.ToDictionary(x => x.Id, x => x.FactoryId));
        return true;
    }

    public bool CanAccessFactory(int factoryId) => FactoryIds.Contains(factoryId);

    public bool CanAccessAlarm(AlarmEvent alarm, IUnitOfWork unitOfWork)
    {
        if (alarm.CustomerId != CustomerId)
            return false;

        if (alarm.MachineId.HasValue)
        {
            var lineId = unitOfWork.Machines.Query()
                .Where(x => x.Id == alarm.MachineId.Value)
                .Select(x => x.LineId)
                .FirstOrDefault();
            return lineId > 0 && LineFactoryIds.ContainsKey(lineId);
        }

        if (alarm.SiloId.HasValue)
        {
            var factoryId = unitOfWork.Silos.Query()
                .Where(x => x.Id == alarm.SiloId.Value)
                .Select(x => x.FactoryId)
                .FirstOrDefault();
            return FactoryIds.Contains(factoryId);
        }

        if (alarm.DeviceId.HasValue)
        {
            var device = unitOfWork.Devices.Query()
                .Where(x => x.Id == alarm.DeviceId.Value)
                .Select(x => new { x.FactoryId, x.LineId })
                .FirstOrDefault();
            return device is not null &&
                   FactoryIds.Contains(device.FactoryId) &&
                   (!device.LineId.HasValue || LineFactoryIds.ContainsKey(device.LineId.Value));
        }

        return true;
    }

    public bool CanAccessWorkOrder(WorkOrder workOrder)
        => workOrder.CustomerId == CustomerId && FactoryIds.Contains(workOrder.FactoryId);

    private static HashSet<int> PositiveClaims(ClaimsPrincipal user, string claimType)
        => user.FindAll(claimType)
            .Select(x => int.TryParse(x.Value, out var id) ? id : 0)
            .Where(x => x > 0)
            .ToHashSet();
}
