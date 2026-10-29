using Domain.Common;
using Domain.Enums;

namespace Domain.Entities.Alarms;

public class AlarmRuleScope : SoftDeleteEntity
{
    public int AlarmRuleId { get; set; }
    public AlarmScopeType ScopeType { get; set; }
    public int EntityId { get; set; }

    public AlarmRule AlarmRule { get; set; } = null!;
}
