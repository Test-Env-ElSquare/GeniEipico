namespace Domain.Enums;

public enum WorkOrderPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum WorkOrderType
{
    Corrective,
    Preventive,
    Predictive
}

public enum WorkOrderStatus
{
    Planned,
    Open,
    InProgress,
    Resolved,
    Completed,
    Closed,
    Cancelled
}
