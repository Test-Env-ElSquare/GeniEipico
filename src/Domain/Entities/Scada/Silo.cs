using Domain.Common;
using Domain.Entities.IoT;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;

namespace Domain.Entities.Scada;

public class Silo : SoftDeleteEntity
{
    public int FactoryId { get; set; }
    public int? DeviceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SiloType { get; set; }
    public string? Zone { get; set; }
    public decimal CapacityKg { get; set; }
    public decimal LowLevelPct { get; set; } = 20m;
    public decimal CriticalLevelPct { get; set; } = 10m;
    public decimal? CurrentQtyKg { get; set; }
    public int? CurrentMaterialId { get; set; }
    public string? CurrentLotUid { get; set; }
    public DateTime? TelemetryAt { get; set; }

    public Factory Factory { get; set; } = null!;
    public Device? Device { get; set; }
    public Material? CurrentMaterial { get; set; }
}
