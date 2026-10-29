using Domain.Common;
using Domain.Entities.Organization;

namespace Domain.Entities.Configuration;

public class PlatformSetting : AuditableEntity
{
    public int CustomerId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool IsSensitive { get; set; }

    public Customer Customer { get; set; } = null!;
}
