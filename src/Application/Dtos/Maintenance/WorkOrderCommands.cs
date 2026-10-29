using Domain.Enums;

namespace Application.Dtos.Maintenance;

public sealed record CreateWorkOrderCommand(
    int CustomerId,
    int? CreatedById,
    WorkOrderType WorkOrderType,
    WorkOrderPriority Priority,
    int? MachineId,
    int? AlarmEventId,
    int? AssignedToId,
    DateTime? PlannedStart,
    string Description);

public sealed record UpdateWorkOrderCommand(
    WorkOrderType WorkOrderType,
    WorkOrderPriority Priority,
    int? MachineId,
    int? AssignedToId,
    DateTime? PlannedStart,
    string Description);
