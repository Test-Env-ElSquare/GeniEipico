using Domain.Common;

namespace Domain.Entities.Organization;

public class ProductionLine : SoftDeleteEntity
{
    public int FactoryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? Number { get; set; }
    public string? ProductType { get; set; }
    public string? Area { get; set; }
    public string? ViewName { get; set; }
    public string? AliasName { get; set; }
    public int? MaxSpeedUpm { get; set; }
    public bool IsActive { get; set; } = true;

    public Factory Factory { get; set; } = null!;
    public ICollection<ProductionLineTransformer> TransformerLinks { get; set; } = new List<ProductionLineTransformer>();
}
