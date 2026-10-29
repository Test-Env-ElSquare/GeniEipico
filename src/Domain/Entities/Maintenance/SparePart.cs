using Domain.Common;
using Domain.Entities.Organization;

namespace Domain.Entities.Maintenance;

public class SparePart : SoftDeleteEntity
{
    public int CustomerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Uom { get; set; } = string.Empty;
    public decimal QtyOnHand { get; set; }
    public decimal? MinStock { get; set; }
    public decimal? UnitCost { get; set; }
    public string? SupplierName { get; set; }
    public short? LeadTimeDays { get; set; }
    public string? StockStatus { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<SparePartMachineType> ApplicableMachineTypes { get; set; } = new List<SparePartMachineType>();
}
