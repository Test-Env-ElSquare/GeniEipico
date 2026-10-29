using Domain.Common;

namespace Domain.Entities.Integration;

public class SyncCheckpoint : Entity
{
    public string SourceSystem { get; set; } = "Eipico";
    public string StreamName { get; set; } = string.Empty;
    public long LastSourceId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
