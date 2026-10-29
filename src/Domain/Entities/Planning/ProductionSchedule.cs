using Domain.Common;
using Domain.Entities.Organization;
using Domain.Entities.Production;

namespace Domain.Entities.Planning;

public class ProductionSchedule : SoftDeleteEntity
{
    public int CustomerId { get; set; }
    public int? BatchId { get; set; }
    public int LineId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public bool IsStoppage { get; set; }
    public string? Title { get; set; }
    public string? StoppageType { get; set; }
    public string? Notes { get; set; }

    public Customer Customer { get; set; } = null!;
    public Batch? Batch { get; set; }
    public ProductionLine Line { get; set; } = null!;
}
