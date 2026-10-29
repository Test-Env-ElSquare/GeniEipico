using Domain.Common;

namespace Domain.Entities.Maintenance;

public class SparePartMachineType : SoftDeleteEntity
{
    public int SparePartId { get; set; }
    public string MachineType { get; set; } = string.Empty;

    public SparePart SparePart { get; set; } = null!;
}
