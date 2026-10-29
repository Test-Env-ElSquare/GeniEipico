using Domain.Common;

namespace Domain.Entities.IoT;

public class DeviceType : SoftDeleteEntity
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Protocol { get; set; }
    public int? DefaultPollIntervalMs { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<DeviceTypeTagDefinition> TagDefinitions { get; set; } = new List<DeviceTypeTagDefinition>();
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}
