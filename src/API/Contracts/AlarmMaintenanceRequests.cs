using Application.Common.Time;
using Domain.Enums;

namespace API.Contracts;

public class AlarmTimeFilterQuery
{
    public int? Duration { get; set; }
    public int? FactoryId { get; set; }
    public int? LineId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public TimeFilterInput ToTimeFilterInput()
    {
        var duration = Duration ?? 0;
        return duration == (int)DurationFilter.Custom
            ? new TimeFilterInput(duration, FactoryId, LineId, From, To)
            : new TimeFilterInput(duration, FactoryId, LineId);
    }
}

public sealed class AlarmListFilterQuery : AlarmTimeFilterQuery
{
    public string? Search { get; set; }
    public string? Severity { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed record AlarmRuleUpsertRequest(
    string Name,
    IReadOnlyList<int> MachineIds,
    string Parameter,
    decimal? MinimumValue,
    decimal? MaximumValue,
    int Severity,
    int Status,
    bool CreateWorkOrder,
    int? AssignedToId = null,
    int? WorkOrderPriority = null);

public sealed record AlarmProbableCauseRequest(string? ProbableCause);

public sealed record CreateWorkOrderRequest(
    WorkOrderType? WorkOrderType,
    WorkOrderPriority? Priority,
    int? MachineId,
    int? AlarmEventId,
    int? AssignedToId,
    DateTime? PlannedStart,
    string Description);

public sealed record UpdateWorkOrderRequest(
    WorkOrderType? WorkOrderType,
    WorkOrderPriority? Priority,
    int? MachineId,
    int? AssignedToId,
    DateTime? PlannedStart,
    string Description);

public sealed record CloseWorkOrderRequest(string? Notes = null);
