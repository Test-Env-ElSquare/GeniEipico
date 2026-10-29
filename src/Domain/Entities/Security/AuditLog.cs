using Domain.Common;
using Domain.Entities.Organization;

namespace Domain.Entities.Security;

public class AuditLog : AuditableEntity
{
    public int CustomerId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? OldValues { get; set; } // stored as nvarchar(max), not JsonDocument
    public string? NewValues { get; set; } // stored as nvarchar(max), not JsonDocument
    public string? IpAddress { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
}
