using Domain.Common;
using Domain.Entities.Organization;

namespace Domain.Entities.MasterData;

public class Transformer : Entity
{
    public string TransformerName { get; set; } = string.Empty;
    public int FactoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Factory Factory { get; set; } = null!;
    public ICollection<ProductionLineTransformer> LineLinks { get; set; } = new List<ProductionLineTransformer>();
}
