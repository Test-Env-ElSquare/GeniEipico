using Domain.Common;

namespace Domain.Entities.Organization;

/// <summary>
/// Unified top-level customer record.
/// Replaces the old Tenant + Customer split.
/// Use CustomerId on downstream entities to scope data per customer.
/// </summary>
public class Customer : SoftDeleteEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Commercial / subscription data that was previously on Tenant
    public string Tier { get; set; } = "basic";
    public DateTime? SubscriptionStarts { get; set; }
    public DateTime? SubscriptionEnds { get; set; }
    public string? CountryCode { get; set; }
    public string? ContactEmail { get; set; }

    // Operational / ERP data that was previously on Customer
    public string? Industry { get; set; }
    public string? ErpSystem { get; set; }
    public string? ErpEndpointUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Factory> Factories { get; set; } = new List<Factory>();
}
