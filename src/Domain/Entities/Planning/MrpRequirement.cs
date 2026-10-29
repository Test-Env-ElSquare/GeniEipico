using Domain.Common;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;

namespace Domain.Entities.Planning;

public class MrpRequirement : AuditableEntity
{
    public int CustomerId { get; set; }
    public int MaterialId { get; set; }
    public DateTime PlanDate { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal? AvailableQty { get; set; }
    public decimal? GapQty { get; set; }
    public string MrpStatus { get; set; } = "ok";
    public string? PoReference { get; set; }
    public DateTime? PoExpectedDate { get; set; }

    public Customer Customer { get; set; } = null!;
    public Material Material { get; set; } = null!;
}
