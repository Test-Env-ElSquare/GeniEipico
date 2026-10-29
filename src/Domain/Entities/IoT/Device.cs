using Domain.Common;
using Domain.Entities.Organization;

namespace Domain.Entities.IoT;

public class Device : SoftDeleteEntity
{
    public int CustomerId { get; set; }
    public int FactoryId { get; set; }
    public int? LineId { get; set; }
    public int DeviceTypeId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? MqttClientId { get; set; }
    public string? MqttTopicPrefix { get; set; }
    public string? ApiKeyHash { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? HardwareModel { get; set; }
    public int? PollIntervalMs { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string ConnectionStatus { get; set; } = "provisioning";
    public DateTime? ProvisionedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public Customer Customer { get; set; } = null!;
    public Factory Factory { get; set; } = null!;
    public ProductionLine? Line { get; set; }
    public DeviceType DeviceType { get; set; } = null!;
}
