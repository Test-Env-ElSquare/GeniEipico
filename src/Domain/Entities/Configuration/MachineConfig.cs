using Domain.Common;
using Domain.Entities.Scada;

namespace Domain.Entities.Configuration;

public class MachineConfig : AuditableEntity
{
    public int MachineId { get; set; }
    public int? RatedSpeedRpm { get; set; }
    public int? MinSpeedRpm { get; set; }
    public decimal? MaxTempCelsius { get; set; }
    public decimal? MaxVibrationMms { get; set; }
    public decimal? RatedPowerKw { get; set; }

    public Machine Machine { get; set; } = null!;
}
