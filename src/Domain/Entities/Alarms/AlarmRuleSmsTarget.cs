using Domain.Common;

namespace Domain.Entities.Alarms;

public class AlarmRuleSmsTarget : SoftDeleteEntity
{
    public int AlarmRuleId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;

    public AlarmRule AlarmRule { get; set; } = null!;
}
