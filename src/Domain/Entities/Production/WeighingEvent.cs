using Domain.Common;
using Domain.Entities.Scada;

namespace Domain.Entities.Production;

public class WeighingEvent : Entity
{
    public int BatchId { get; set; }
    public int BatchMaterialId { get; set; }
    public int? ScaleId { get; set; }
    public string? MaterialUid { get; set; }
    public decimal Weight { get; set; }
    public DateTime RecordedAt { get; set; }
    public bool Dispatched { get; set; }
    public string WeighingSource { get; set; } = "handheld";
    public long? SourceRecordId { get; set; }

    public Batch Batch { get; set; } = null!;
    public BatchMaterial BatchMaterial { get; set; } = null!;
    public Scale? Scale { get; set; }
}
