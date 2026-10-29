using Domain.Common;
using Domain.Entities.Scada;

namespace Domain.Entities.Production;

public class MachineMaterialLoad : Entity
{
    public int BatchId { get; set; }
    public int BatchMaterialId { get; set; }
    public int? MaterialSplitId { get; set; }
    public int MachineId { get; set; }
    public string MaterialUid { get; set; } = string.Empty;
    public decimal? Weight { get; set; }
    public DateTime LoadedAt { get; set; }
    public DateTime ShiftStartTime { get; set; }
    public DateTime? ActivationDate { get; set; }
    public DateTime? FinishingDate { get; set; }
    public bool? IsPending { get; set; }
    public int? Count { get; set; }
    public long? SourceRecordId { get; set; }

    public Batch Batch { get; set; } = null!;
    public BatchMaterial BatchMaterial { get; set; } = null!;
    public MaterialSplit? MaterialSplit { get; set; }
    public Machine Machine { get; set; } = null!;
}
