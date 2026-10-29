using Domain.Common;
using Domain.Entities.Alarms;
using Domain.Entities.Scada;

namespace Domain.Entities.Maintenance;

public class BreakdownLog : AuditableEntity
{
    public string? Reference { get; set; }
    public int MachineId { get; set; }
    public int? AlarmEventId { get; set; }
    public int? WorkOrderId { get; set; }
    public DateTime BreakdownAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public decimal? MttrHours { get; set; }
    public string? RootCause { get; set; }
    public string? FailureMode { get; set; }
    public string Status { get; set; } = "open";

    public Machine Machine { get; set; } = null!;
    public AlarmEvent? AlarmEvent { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}
