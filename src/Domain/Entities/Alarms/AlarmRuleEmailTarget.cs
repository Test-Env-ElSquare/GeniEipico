using Domain.Common;

namespace Domain.Entities.Alarms;

public class AlarmRuleEmailTarget : SoftDeleteEntity
{
    public int AlarmRuleId { get; set; }
    public string Email { get; set; } = string.Empty;

    public AlarmRule AlarmRule { get; set; } = null!;
}
