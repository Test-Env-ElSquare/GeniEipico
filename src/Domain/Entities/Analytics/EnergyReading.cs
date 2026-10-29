using Domain.Common;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;

namespace Domain.Entities.Analytics;

/// <summary>
/// One transformer power signal projected from RealTime.TelemetryTags.
/// The row is identified by the internal transformer and the signal timestamp.
/// </summary>
public class EnergyReading : Entity
{
    public long? SourceRecordId { get; set; }
    public int CustomerId { get; set; }
    public int FactoryId { get; set; }
    public int? LineId { get; set; }
    public int TransformerId { get; set; }
    public short? Quality { get; set; }

    public decimal? VoltageL1 { get; set; }
    public decimal? VoltageL2 { get; set; }
    public decimal? VoltageL3 { get; set; }
    public decimal? CurrentL1 { get; set; }
    public decimal? CurrentL2 { get; set; }
    public decimal? CurrentL3 { get; set; }
    public decimal? PowerFactorL1 { get; set; }
    public decimal? PowerFactorL2 { get; set; }
    public decimal? PowerFactorL3 { get; set; }
    public decimal? ActivePowerL1 { get; set; }
    public decimal? ActivePowerL2 { get; set; }
    public decimal? ActivePowerL3 { get; set; }
    public decimal? ReactivePowerL1 { get; set; }
    public decimal? ReactivePowerL2 { get; set; }
    public decimal? ReactivePowerL3 { get; set; }
    public decimal? ApparentPowerL1 { get; set; }
    public decimal? ApparentPowerL2 { get; set; }
    public decimal? ApparentPowerL3 { get; set; }
    public decimal? EnergyL1 { get; set; }
    public decimal? EnergyL2 { get; set; }
    public decimal? EnergyL3 { get; set; }
    public decimal? VoltageThdL1 { get; set; }
    public decimal? VoltageThdL2 { get; set; }
    public decimal? VoltageThdL3 { get; set; }
    public decimal? CurrentThdL1 { get; set; }
    public decimal? CurrentThdL2 { get; set; }
    public decimal? CurrentThdL3 { get; set; }
    public DateTime Timestamp { get; set; }

    public Customer Customer { get; set; } = null!;
    public Factory Factory { get; set; } = null!;
    public ProductionLine? Line { get; set; }
    public Transformer Transformer { get; set; } = null!;
}
