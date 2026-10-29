using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Common.Workflow;
using Application.Dtos.Alarms;
using Domain.Entities.Alarms;
using Domain.Entities.Maintenance;
using Domain.Enums;

namespace Application.Services;

public sealed class AlarmEvaluationService : IAlarmEvaluationService
{
    private readonly IUnitOfWork _uow;

    public AlarmEvaluationService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<WorkflowResult<AlarmEvaluationOutcome>> EvaluateAsync(
        int deviceId,
        string parameter,
        decimal value,
        DateTime recordedAt)
    {
        if (string.IsNullOrWhiteSpace(parameter))
            return WorkflowResult<AlarmEvaluationOutcome>.Fail(
                "parameter_required",
                "Telemetry parameter is required.");

        var device = _uow.Devices.Query().FirstOrDefault(x => x.Id == deviceId && x.IsActive);
        if (device is null)
            return WorkflowResult<AlarmEvaluationOutcome>.Fail(
                "device_not_found",
                $"Device {deviceId} was not found.");

        var machine = _uow.Machines.Query()
            .FirstOrDefault(x => x.DeviceId == deviceId && x.IsActive);
        if (machine is null)
        {
            return WorkflowResult<AlarmEvaluationOutcome>.Ok(
                new AlarmEvaluationOutcome(
                    deviceId,
                    null,
                    parameter,
                    value,
                    0,
                    Array.Empty<int>(),
                    Array.Empty<int>(),
                    Array.Empty<int>()),
                "The telemetry was stored, but the device is not assigned to an active machine.");
        }

        var machineRuleIds = _uow.AlarmRuleScopes.Query()
            .Where(x =>
                x.ScopeType == AlarmScopeType.Machine &&
                x.EntityId == machine.Id)
            .Select(x => x.AlarmRuleId)
            .ToHashSet();
        var rules = _uow.AlarmRules.Query()
            .Where(x =>
                x.CustomerId == device.CustomerId &&
                x.IsActive &&
                x.ScopeType == AlarmScopeType.Machine &&
                (machineRuleIds.Contains(x.Id) || x.ScopeEntityId == machine.Id))
            .ToList()
            .Where(x =>
                string.Equals(x.TagName, parameter, StringComparison.OrdinalIgnoreCase) &&
                (x.MinimumValue.HasValue || x.MaximumValue.HasValue))
            .ToArray();

        var createdAlarmIds = new List<int>();
        var resolvedAlarmIds = new List<int>();
        var createdWorkOrderIds = new List<int>();

        foreach (var rule in rules)
        {
            var outsideRange =
                rule.MinimumValue.HasValue && value < rule.MinimumValue.Value ||
                rule.MaximumValue.HasValue && value > rule.MaximumValue.Value;
            var openAlarms = _uow.AlarmEvents.Query(asNoTracking: false)
                .Where(x =>
                    x.RuleId == rule.Id &&
                    x.MachineId == machine.Id &&
                    x.ResolvedAt == null)
                .ToList();

            if (!outsideRange)
            {
                foreach (var openAlarm in openAlarms)
                {
                    openAlarm.ResolvedAt = recordedAt;
                    openAlarm.Notes = AppendNote(
                        openAlarm.Notes,
                        $"{parameter} returned inside the configured range at {value}.");
                    _uow.AlarmEvents.Update(openAlarm);
                    resolvedAlarmIds.Add(openAlarm.Id);
                }
                continue;
            }

            if (openAlarms.Count > 0)
                continue;

            var alarm = new AlarmEvent
            {
                RuleId = rule.Id,
                CustomerId = device.CustomerId,
                DeviceId = device.Id,
                MachineId = machine.Id,
                Title = $"{rule.Name}: {parameter} outside configured range",
                Severity = rule.Severity,
                TagName = parameter,
                TriggerValue = value,
                TriggeredAt = recordedAt,
                NotificationSent = false,
                Notes = RangeDescription(rule.MinimumValue, rule.MaximumValue, value)
            };
            await _uow.AlarmEvents.AddAsync(alarm);
            await _uow.SaveChangesAsync();
            createdAlarmIds.Add(alarm.Id);

            var action = _uow.AlarmRuleActions.Query()
                .Where(x =>
                    x.AlarmRuleId == rule.Id &&
                    x.ActionType == AlarmRuleActionType.CreateWorkOrder &&
                    x.IsEnabled)
                .OrderBy(x => x.SequenceNo)
                .FirstOrDefault();
            if (action is null)
                continue;

            var factoryId = _uow.ProductionLines.Query()
                .Where(x => x.Id == machine.LineId)
                .Select(x => x.FactoryId)
                .FirstOrDefault();
            if (factoryId <= 0)
                continue;

            var workOrder = new WorkOrder
            {
                Number = $"WO-{DateTime.UtcNow:yyMMddHHmmssfff}-{alarm.Id}",
                CustomerId = device.CustomerId,
                FactoryId = factoryId,
                WorkOrderType = WorkOrderType.Corrective,
                Priority = action.WorkOrderPriority ?? SeverityPriority(rule.Severity),
                MachineId = machine.Id,
                AlarmEventId = alarm.Id,
                Description = $"Automatically created from alarm: {alarm.Title}",
                AssignedToId = action.AssignedToId,
                PlannedStart = recordedAt,
                Status = WorkOrderStatus.Open,
                CreatedById = rule.CreatedById
            };
            await _uow.WorkOrders.AddAsync(workOrder);
            await _uow.SaveChangesAsync();
            createdWorkOrderIds.Add(workOrder.Id);
        }

        if (resolvedAlarmIds.Count > 0)
            await _uow.SaveChangesAsync();

        return WorkflowResult<AlarmEvaluationOutcome>.Ok(
            new AlarmEvaluationOutcome(
                deviceId,
                machine.Id,
                parameter,
                value,
                rules.Length,
                createdAlarmIds,
                resolvedAlarmIds,
                createdWorkOrderIds),
            "Telemetry value evaluated against active machine alarm rules.");
    }

    private static string RangeDescription(decimal? minimum, decimal? maximum, decimal value)
        => $"Value={value}; Minimum={minimum?.ToString() ?? "not set"}; Maximum={maximum?.ToString() ?? "not set"}.";

    private static string? AppendNote(string? current, string note)
        => string.IsNullOrWhiteSpace(current) ? note : current + Environment.NewLine + note;

    private static WorkOrderPriority SeverityPriority(AlarmSeverity severity) => severity switch
    {
        AlarmSeverity.Critical => WorkOrderPriority.Critical,
        AlarmSeverity.High => WorkOrderPriority.High,
        AlarmSeverity.Medium => WorkOrderPriority.Medium,
        _ => WorkOrderPriority.Low
    };
}
