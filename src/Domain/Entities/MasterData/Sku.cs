using Domain.Common;
using Domain.Entities.Organization;

namespace Domain.Entities.MasterData;

public class Sku : SoftDeleteEntity
{
    public int CustomerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Uom { get; set; } = string.Empty;
    public int StandardBatchSize { get; set; }
    public short? ShelfLifeMonths { get; set; }
    public int? RatedSpeedRpm { get; set; }
    public decimal? UnitWeightG { get; set; }
    public bool ErpSource { get; set; }
    public string? ErpMaterialCode { get; set; }
    public DateTime? LastErpSyncAt { get; set; }
    public bool IsActive { get; set; } = true;

    public Customer Customer { get; set; } = null!;
    public ICollection<SkuProcessParameter> ProcessParameters { get; set; } = new List<SkuProcessParameter>();
    public ICollection<BillOfMaterial> BillOfMaterials { get; set; } = new List<BillOfMaterial>();
}
