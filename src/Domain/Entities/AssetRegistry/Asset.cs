using Domain.Common;
using Domain.Entities.IoT;
using Domain.Entities.Organization;
using Domain.Entities.Scada;

namespace Domain.Entities.AssetRegistry;

public class Asset : SoftDeleteEntity
{
    public int CustomerId { get; set; }
    public int FactoryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AssetType { get; set; }
    public int? MachineId { get; set; }
    public int? DeviceId { get; set; }
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public DateTime? InstallDate { get; set; }
    public DateTime? WarrantyExpires { get; set; }
    public short? HealthScore { get; set; }
    public string? Status { get; set; }
    public decimal? UtilizationPct { get; set; }
    public bool IsActive { get; set; } = true;

    public Customer Customer { get; set; } = null!;
    public Factory Factory { get; set; } = null!;
    public Machine? Machine { get; set; }
    public Device? Device { get; set; }
}
