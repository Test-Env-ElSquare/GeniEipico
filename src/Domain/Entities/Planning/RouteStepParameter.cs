using Domain.Common;

namespace Domain.Entities.Planning;

public class RouteStepParameter : SoftDeleteEntity
{
    public int RouteStepId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string DataType { get; set; } = "string";

    public RouteStep RouteStep { get; set; } = null!;
}
