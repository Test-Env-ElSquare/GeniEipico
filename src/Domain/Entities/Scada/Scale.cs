using Domain.Common;
using Domain.Entities.IoT;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;
using Domain.Entities.Production;

namespace Domain.Entities.Scada;

public class Scale : SoftDeleteEntity
{
    public int FactoryId { get; set; }
    public int? DeviceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public decimal? CapacityKg { get; set; }
    public decimal? ResolutionG { get; set; }
    public string? Protocol { get; set; }
    public decimal TareKg { get; set; }
    public decimal? WeightKg { get; set; }
    public string ScaleState { get; set; } = "offline";
    public int? ActiveBatchId { get; set; }
    public int? ActiveMaterialId { get; set; }
    public DateTime? CalibratedAt { get; set; }
    public string? CalibrationCert { get; set; }
    public DateTime? TelemetryAt { get; set; }

    public Factory Factory { get; set; } = null!;
    public Device? Device { get; set; }
    public Batch? ActiveBatch { get; set; }
    public Material? ActiveMaterial { get; set; }
}
