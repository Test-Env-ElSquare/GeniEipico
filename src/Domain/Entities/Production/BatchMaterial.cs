using Domain.Common;
using Domain.Entities.MasterData;
using Domain.Entities.Scada;

namespace Domain.Entities.Production;

public class BatchMaterial : SoftDeleteEntity
{
    public int BatchId { get; set; }
    public int MaterialId { get; set; }
    public long? SourceRecordId { get; set; }
    public string? MaterialUid { get; set; }
    public string? ItemCode { get; set; }
    public string? OperationCode { get; set; }
    public string? Uom { get; set; }
    public int? SiloId { get; set; }
    public int? ScaleId { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal DispensedQty { get; set; }
    public decimal? DeviationPct { get; set; }
    public DateTime? DispenseStartedAt { get; set; }
    public DateTime? DispenseCompletedAt { get; set; }
    public int? DispensedById { get; set; }
    public string Status { get; set; } = "pending";

    public Batch Batch { get; set; } = null!;
    public Material Material { get; set; } = null!;
    public Silo? Silo { get; set; }
    public Scale? Scale { get; set; }
    public ICollection<WeighingEvent> WeighingEvents { get; set; } = new List<WeighingEvent>();
    public ICollection<MaterialSplit> MaterialSplits { get; set; } = new List<MaterialSplit>();
    public ICollection<MachineMaterialLoad> MachineMaterialLoads { get; set; } = new List<MachineMaterialLoad>();
}
