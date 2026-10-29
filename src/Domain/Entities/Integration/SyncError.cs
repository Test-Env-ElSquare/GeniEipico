using Domain.Common;

namespace Domain.Entities.Integration;

public class SyncError : Entity
{
    public string SourceSystem { get; set; } = "Eipico";
    public string StreamName { get; set; } = string.Empty;
    public long? SourceRecordId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime FirstOccurredAt { get; set; }
    public DateTime LastOccurredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
