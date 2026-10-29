namespace Domain.Enums;

public enum AlarmSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum AlarmRuleStatus
{
    Inactive,
    Active
}

public enum AlarmScopeType
{
    Global,
    Factory,
    Line,
    Machine,
    Device,
    Silo,
    Scale,
    Asset
}

public enum AlarmRuleType
{
    User,
    System
}

public enum AlarmRuleActionType
{
    CreateWorkOrder,
    EmailNotification,
    LogOnly
}
