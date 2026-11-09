using Domain.Common;
using Domain.Entities.MasterData;

namespace Domain.Entities.Organization;

/// <summary>
/// Electrical hierarchy level supplied by EMS. It links existing Genie
/// factories, transformers and production lines without duplicating them.
/// </summary>
public sealed class EnergyZone : Entity
{
    public int EmsSourceId { get; set; }
    public int FactoryId { get; set; }
    public int? TransformerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? RatioFromParent { get; set; }
    public DateTime SourceCreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Factory Factory { get; set; } = null!;
    public Transformer? Transformer { get; set; }
    public ICollection<ProductionLine> ProductionLines { get; set; } = [];
}
