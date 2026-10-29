using Domain.Common;

namespace Domain.Entities.IoT;

public class TelemetryRaw : Entity
{
    public string SourcePath { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
