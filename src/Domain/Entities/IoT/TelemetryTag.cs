using Domain.Common;

namespace Domain.Entities.IoT;

public class TelemetryTag : Entity
{
    public string DeviceId { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public decimal? ValueNum { get; set; }
    public string? ValueStr { get; set; }
    public DateTime RecordedAt { get; set; }
}
