using Domain.Common;

namespace Domain.Entities.Maintenance;

public class WoPartUsed : AuditableEntity
{
    public int WorkOrderId { get; set; }
    public int SparePartId { get; set; }
    public decimal QtyUsed { get; set; }
    public decimal? UnitCostAtUse { get; set; }

    public WorkOrder WorkOrder { get; set; } = null!;
    public SparePart SparePart { get; set; } = null!;
}
