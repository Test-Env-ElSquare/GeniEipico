using Domain.Common;
using Domain.Entities.Alarms;
using Domain.Entities.Organization;
using Domain.Entities.Scada;
using Domain.Enums;

namespace Domain.Entities.Maintenance;

public class WorkOrder : SoftDeleteEntity
{
    public string Number { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int FactoryId { get; set; }
    public WorkOrderType WorkOrderType { get; set; }
    public WorkOrderPriority Priority { get; set; }
    public int? MachineId { get; set; }
    public int? ScaleId { get; set; }
    public int? PmScheduleId { get; set; }
    public int? AlarmEventId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? AssignedToId { get; set; }
    public string? Team { get; set; }
    public DateTime? PlannedStart { get; set; }
    public decimal? PlannedDurationHours { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public WorkOrderStatus Status { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string? FailureMode { get; set; }
    public string? ResolutionNotes { get; set; }
    public int? CreatedById { get; set; }

    public Customer Customer { get; set; } = null!;
    public Factory Factory { get; set; } = null!;
    public Machine? Machine { get; set; }
    public Scale? Scale { get; set; }
    public PmSchedule? PmSchedule { get; set; }
    public AlarmEvent? AlarmEvent { get; set; }
}
