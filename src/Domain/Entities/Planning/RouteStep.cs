using Domain.Common;

namespace Domain.Entities.Planning;

public class RouteStep : SoftDeleteEntity
{
    public int RouteId { get; set; }
    public short StepNo { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string? MachineType { get; set; }
    public int? EstDurationMin { get; set; }

    public ProductionRoute Route { get; set; } = null!;
    public ICollection<RouteStepParameter> Parameters { get; set; } = new List<RouteStepParameter>();
}
