using Domain.Common;
using Domain.Entities.IoT;
using Domain.Entities.Organization;
using Domain.Entities.Scada;
using Domain.Enums;

namespace Domain.Entities.Alarms;

public class AlarmEvent : AuditableEntity
{
    public int? RuleId { get; set; }
    public int CustomerId { get; set; }
    public int? DeviceId { get; set; }
    public int? MachineId { get; set; }
    public int? SiloId { get; set; }
    public string Title { get; set; } = string.Empty;
    public AlarmSeverity Severity { get; set; }
    public string? TagName { get; set; }
    public decimal? TriggerValue { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public int? AcknowledgedById { get; set; }
    public bool NotificationSent { get; set; }
    public string? ProbableCause { get; set; }
    public string? Notes { get; set; }

    public AlarmRule? Rule { get; set; }
    public Customer Customer { get; set; } = null!;
    public Device? Device { get; set; }
    public Machine? Machine { get; set; }
    public Silo? Silo { get; set; }
}
