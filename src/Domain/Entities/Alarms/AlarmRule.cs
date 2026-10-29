using Domain.Common;
using Domain.Entities.Organization;
using Domain.Enums;

namespace Domain.Entities.Alarms;

public class AlarmRule : SoftDeleteEntity
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AlarmRuleType RuleType { get; set; }
    public AlarmScopeType ScopeType { get; set; }
    public int? ScopeEntityId { get; set; }
    public string? TagName { get; set; }
    public decimal? MinimumValue { get; set; }
    public decimal? MaximumValue { get; set; }
    public AlarmSeverity Severity { get; set; }
    public bool CreateWorkOrder { get; set; }
    public string? WorkOrderTeam { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CreatedById { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<AlarmRuleEmailTarget> EmailTargets { get; set; } = new List<AlarmRuleEmailTarget>();
    public ICollection<AlarmRuleSmsTarget> SmsTargets { get; set; } = new List<AlarmRuleSmsTarget>();
    public ICollection<AlarmRuleScope> Scopes { get; set; } = new List<AlarmRuleScope>();
    public ICollection<AlarmRuleAction> Actions { get; set; } = new List<AlarmRuleAction>();
}
