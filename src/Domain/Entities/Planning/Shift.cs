using Domain.Common;
using Domain.Entities.Organization;

namespace Domain.Entities.Planning;

public class Shift : SoftDeleteEntity
{
    public int FactoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsActive { get; set; } = true;

    public Factory Factory { get; set; } = null!;
    public ICollection<ShiftFactory> FactoryLinks { get; set; } = new List<ShiftFactory>();
}
