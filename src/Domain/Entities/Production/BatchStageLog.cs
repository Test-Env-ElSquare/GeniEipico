using Domain.Common;
using Domain.Entities.Scada;

namespace Domain.Entities.Production;

public class BatchStageLog : AuditableEntity
{
    public int BatchId { get; set; }
    public string? FromStage { get; set; }
    public string ToStage { get; set; } = string.Empty;
    public DateTime TransitionedAt { get; set; } = DateTime.UtcNow;
    public int? TransitionedById { get; set; }
    public int? MachineId { get; set; }
    public string? Notes { get; set; }

    public Batch Batch { get; set; } = null!;
    public Machine? Machine { get; set; }
}
