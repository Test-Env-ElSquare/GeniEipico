using Domain.Common;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;

namespace Domain.Entities.Planning;

public class ProductionRoute : SoftDeleteEntity
{
    public int CustomerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SkuId { get; set; }
    public decimal? CycleTimeHours { get; set; }
    public string Status { get; set; } = "draft";
    public short Version { get; set; } = 1;

    public Customer Customer { get; set; } = null!;
    public Sku Sku { get; set; } = null!;
    public ICollection<RouteStep> Steps { get; set; } = new List<RouteStep>();
}
