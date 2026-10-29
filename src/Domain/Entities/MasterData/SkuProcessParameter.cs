using Domain.Common;

namespace Domain.Entities.MasterData;

public class SkuProcessParameter : SoftDeleteEntity
{
    public int SkuId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string DataType { get; set; } = "string";

    public Sku Sku { get; set; } = null!;
}
