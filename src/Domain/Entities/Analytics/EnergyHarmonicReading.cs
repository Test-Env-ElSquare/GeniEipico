using Domain.Common;
using Domain.Entities.MasterData;

namespace Domain.Entities.Analytics;

public class EnergyHarmonicReading : Entity
{
    public int TransformerId { get; set; }
    public long? SourceRecordId { get; set; }
    public DateTime Timestamp { get; set; }

    public decimal HarmonicI31 { get; set; }
    public decimal HarmonicI32 { get; set; }
    public decimal HarmonicI33 { get; set; }
    public decimal HarmonicV31 { get; set; }
    public decimal HarmonicV32 { get; set; }
    public decimal HarmonicV33 { get; set; }
    public decimal HarmonicI51 { get; set; }
    public decimal HarmonicI52 { get; set; }
    public decimal HarmonicI53 { get; set; }
    public decimal HarmonicV51 { get; set; }
    public decimal HarmonicV52 { get; set; }
    public decimal HarmonicV53 { get; set; }
    public decimal HarmonicI71 { get; set; }
    public decimal HarmonicI72 { get; set; }
    public decimal HarmonicI73 { get; set; }
    public decimal HarmonicV71 { get; set; }
    public decimal HarmonicV72 { get; set; }
    public decimal HarmonicV73 { get; set; }
    public decimal HarmonicI91 { get; set; }
    public decimal HarmonicI92 { get; set; }
    public decimal HarmonicI93 { get; set; }
    public decimal HarmonicV91 { get; set; }
    public decimal HarmonicV92 { get; set; }
    public decimal HarmonicV93 { get; set; }
    public decimal HarmonicI111 { get; set; }
    public decimal HarmonicI112 { get; set; }
    public decimal HarmonicI113 { get; set; }
    public decimal HarmonicV111 { get; set; }
    public decimal HarmonicV112 { get; set; }
    public decimal HarmonicV113 { get; set; }
    public decimal HarmonicI131 { get; set; }
    public decimal HarmonicI132 { get; set; }
    public decimal HarmonicI133 { get; set; }
    public decimal HarmonicV131 { get; set; }
    public decimal HarmonicV132 { get; set; }
    public decimal HarmonicV133 { get; set; }

    public Transformer Transformer { get; set; } = null!;
}
