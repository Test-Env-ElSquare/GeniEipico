using Domain.Common;
using Domain.Entities.Organization;

namespace Domain.Entities.Production;

public class BatchUidSequence : AuditableEntity
{
    public int CustomerId { get; set; }
    public string Prefix { get; set; } = string.Empty; // BT | RM | SF | FG
    public string YearMonth { get; set; } = string.Empty; // YYMM
    public int LastSeq { get; set; }

    public Customer Customer { get; set; } = null!;
}
