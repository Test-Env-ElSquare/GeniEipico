using Domain.Common;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;
using Domain.Entities.Planning;
using Domain.Enums;

namespace Domain.Entities.Production;

public class Batch : SoftDeleteEntity
{
    public string Uid { get; set; } = string.Empty;
    public string SourceType { get; set; } = "manual";
    public long? SourceRecordId { get; set; }
    public string? JobOrderId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? StorageLocation { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public int CustomerId { get; set; }
    public int SkuId { get; set; }
    public int LineId { get; set; }
    public int? RouteId { get; set; }
    public string Status { get; set; } = "planned";
    public string Stage { get; set; } = "warehouse";
    public int? PlannedQty { get; set; }
    public int ProducedQty { get; set; }
    public int RejectedQty { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public int? ShiftId { get; set; }
    public BatchPriority Priority { get; set; } = BatchPriority.Normal;
    public int? ParentBatchId { get; set; }
    public string? SplitReason { get; set; }
    public int? CreatedById { get; set; }
    public string? Notes { get; set; }

    public Customer Customer { get; set; } = null!;
    public Sku Sku { get; set; } = null!;
    public ProductionLine Line { get; set; } = null!;
    public ProductionRoute? Route { get; set; }
    public Shift? Shift { get; set; }
    public Batch? ParentBatch { get; set; }
    public ICollection<BatchMaterial> Materials { get; set; } = new List<BatchMaterial>();
    public ICollection<WeighingEvent> WeighingEvents { get; set; } = new List<WeighingEvent>();
    public ICollection<MachineMaterialLoad> MachineMaterialLoads { get; set; } = new List<MachineMaterialLoad>();
}
