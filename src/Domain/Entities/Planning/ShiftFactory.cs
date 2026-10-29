using Domain.Common;
using Domain.Entities.Organization;

namespace Domain.Entities.Planning;

public class ShiftFactory : AuditableEntity
{
    public int ShiftId { get; set; }
    public int FactoryId { get; set; }

    public Shift Shift { get; set; } = null!;
    public Factory Factory { get; set; } = null!;
}
