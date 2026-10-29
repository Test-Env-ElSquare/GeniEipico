using Domain.Common;
using Domain.Enums;

namespace Domain.Entities.Alarms;

public class AlarmRuleAction : SoftDeleteEntity
{
    public int AlarmRuleId { get; set; }
    public short SequenceNo { get; set; }
    public AlarmRuleActionType ActionType { get; set; }
    public bool IsEnabled { get; set; }
    public WorkOrderPriority? WorkOrderPriority { get; set; }
    public int? AssignedToId { get; set; }

    public AlarmRule AlarmRule { get; set; } = null!;
}
