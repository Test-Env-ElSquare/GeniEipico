namespace Application.Dtos.Alarms;

public sealed record AlarmEvaluationOutcome(
    int DeviceId,
    int? MachineId,
    string Parameter,
    decimal Value,
    int EvaluatedRuleCount,
    IReadOnlyList<int> CreatedAlarmIds,
    IReadOnlyList<int> ResolvedAlarmIds,
    IReadOnlyList<int> CreatedWorkOrderIds);
