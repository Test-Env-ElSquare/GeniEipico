using Domain.Common;
using Domain.Entities.IoT;
using Domain.Entities.Organization;

namespace Domain.Entities.Scada;

public class Machine : SoftDeleteEntity
{
    public int LineId { get; set; }
    public int? DeviceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MachineType { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? ModelNumber { get; set; }
    public int? RatedSpeedRpm { get; set; }
    public decimal? RatedPowerKw { get; set; }
    public DateTime? InstallDate { get; set; }
    public bool IsActive { get; set; } = true;

    public ProductionLine Line { get; set; } = null!;
    public Device? Device { get; set; }
}
