using Domain.Common;

namespace Domain.Entities.Scada;

public class MachineStateHistory : AuditableEntity
{
    public int MachineId { get; set; }
    public string? FromState { get; set; }
    public string ToState { get; set; } = string.Empty;
    public DateTime TransitionedAt { get; set; }
    public string? TriggerSource { get; set; }
    public int? TriggeredByUserId { get; set; }
    public string? Notes { get; set; }

    public Machine Machine { get; set; } = null!;
}
