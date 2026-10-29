using Domain.Common;

namespace Domain.Entities.MasterData;

public class BillOfMaterial : SoftDeleteEntity
{
    public int SkuId { get; set; }
    public int MaterialId { get; set; }
    public decimal QtyPerBatch { get; set; }
    public decimal TolerancePct { get; set; } = 2m;
    public short? SequenceNo { get; set; }
    public bool IsCritical { get; set; }

    public Sku Sku { get; set; } = null!;
    public Material Material { get; set; } = null!;
}
