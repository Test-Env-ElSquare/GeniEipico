using Domain.Common;
using Domain.Entities.Organization;

namespace Domain.Entities.MasterData;

public class Material : SoftDeleteEntity
{
    public int CustomerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty; // RM | SF | FG | PKG
    public string Uom { get; set; } = string.Empty;
    public string? ErpCode { get; set; }
    public string? SupplierCode { get; set; }
    public decimal? ReorderPointKg { get; set; }
    public decimal? MinStockKg { get; set; }
    public bool IsActive { get; set; } = true;

    public Customer Customer { get; set; } = null!;
    public ICollection<BillOfMaterial> BillOfMaterials { get; set; } = new List<BillOfMaterial>();
}
