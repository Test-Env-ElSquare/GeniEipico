namespace Domain.Entities.Organization;

/// <summary>
/// Many-to-many relationship between a production line and its electrical
/// transformers. Eipico's Definitions.LineTransformers has the same shape.
/// </summary>
public class ProductionLineTransformer
{
    public int ProductionLineId { get; set; }
    public int TransformerId { get; set; }

    public ProductionLine ProductionLine { get; set; } = null!;
    public Domain.Entities.MasterData.Transformer Transformer { get; set; } = null!;
}
