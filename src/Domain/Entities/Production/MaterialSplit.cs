using Domain.Common;

namespace Domain.Entities.Production;

public class MaterialSplit : Entity
{
    public int BatchMaterialId { get; set; }
    public string SplitUid { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public DateTime? SplitAt { get; set; }
    public long? SourceRecordId { get; set; }

    public BatchMaterial BatchMaterial { get; set; } = null!;
    public ICollection<MachineMaterialLoad> MachineMaterialLoads { get; set; } = new List<MachineMaterialLoad>();
}
