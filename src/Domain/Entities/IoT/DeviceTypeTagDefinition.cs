using Domain.Common;

namespace Domain.Entities.IoT;

public class DeviceTypeTagDefinition : SoftDeleteEntity
{
    public int DeviceTypeId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string DataType { get; set; } = "number"; // number | string | bool
    public string? Unit { get; set; }
    public bool IsRequired { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }

    public DeviceType DeviceType { get; set; } = null!;
}
