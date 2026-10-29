using Domain.Common;
using Domain.Entities.Planning;

namespace Domain.Entities.Organization;

public class Factory : SoftDeleteEntity
{
    public int CustomerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public string TimeZone { get; set; } = "Africa/Cairo";
    public bool IsActive { get; set; } = true;

    public Customer Customer { get; set; } = null!;
    public ICollection<ProductionLine> ProductionLines { get; set; } = new List<ProductionLine>();
    public ICollection<ShiftFactory> ShiftLinks { get; set; } = new List<ShiftFactory>();
}
