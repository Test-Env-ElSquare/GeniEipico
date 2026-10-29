using API.Common;
using API.Contracts;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Application.Dtos.Maintenance;
using Domain.Entities.Alarms;
using Domain.Entities.IoT;
using Domain.Enums;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Alarms;

[ApiController]
[Authorize]
[Route("v1/alarms")]
public class AlarmsController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IAlarmWorkflowService _alarmWorkflow;
    private readonly IMaintenanceWorkflowService _maintenanceWorkflow;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITimeUtilities _timeUtilities;

    public AlarmsController(
        IUnitOfWork uow,
        IAlarmWorkflowService alarmWorkflow,
        IMaintenanceWorkflowService maintenanceWorkflow,
        UserManager<ApplicationUser> userManager,
        ITimeUtilities timeUtilities)
    {
        _uow = uow;
        _alarmWorkflow = alarmWorkflow;
        _maintenanceWorkflow = maintenanceWorkflow;
        _userManager = userManager;
        _timeUtilities = timeUtilities;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] AlarmTimeFilterQuery query)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var filterError = ValidateAlarmFilter(scope, query);
        if (filterError is not null) return filterError;
        var filter = await ResolveAlarmFilterAsync(query);

        var active = AccessibleEvents(scope)
            .Where(x => x.ResolvedAt == null)
            .Select(x => new { Alarm = x, Source = ResolveSource(x) })
            .Where(x => MatchesOperationalFilter(x.Alarm, x.Source, filter))
            .Select(x => x.Alarm)
            .ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            all = active.Count,
            critical = active.Count(x => x.Severity == AlarmSeverity.Critical),
            high = active.Count(x => x.Severity == AlarmSeverity.High),
            medium = active.Count(x => x.Severity == AlarmSeverity.Medium),
            low = active.Count(x => x.Severity == AlarmSeverity.Low)
        }, new
        {
            filter.Duration,
            filter.FactoryId,
            filter.LineId,
            filter.From,
            filter.To,
            filter.ShiftId,
            filter.ShiftName,
            filter.Mode,
            filter.Note,
            activeDefinition = "ResolvedAt is null (acknowledged and unacknowledged alarms)"
        }));
    }

    [HttpGet("active")]
    public async Task<IActionResult> Active([FromQuery] AlarmListFilterQuery query)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var filterError = ValidateAlarmFilter(scope, query);
        if (filterError is not null) return filterError;
        var filter = (await ResolveAlarmFilterAsync(query)) with
        {
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 100)
        };
        return AlarmList(scope, true, query.Severity, filter);
    }

    [HttpGet("active/by-scope")]
    public IActionResult ActiveByScope([FromQuery] int factoryId, [FromQuery] int? lineId = null, [FromQuery] int? machineId = null)
    {
        if (!TryGetScope(out var scope, out var error)) return error;

        var scopeError = ValidateActiveAlarmScope(
            scope,
            factoryId,
            lineId,
            machineId);
        if (scopeError is not null) return scopeError;

        var events = AccessibleEvents(scope)
            .Where(x => x.ResolvedAt == null)
            .Select(x => new { Alarm = x, Source = ResolveSource(x) })
            .Where(x => x.Source.FactoryId == factoryId)
            .Where(x => !lineId.HasValue || x.Source.LineId == lineId.Value)
            .Where(x => !machineId.HasValue || x.Source.MachineId == machineId.Value)
            .OrderByDescending(x => x.Alarm.TriggeredAt)
            .ThenByDescending(x => x.Alarm.Id)
            .ToList();

        var ruleIds = events
            .Where(x => x.Alarm.RuleId.HasValue)
            .Select(x => x.Alarm.RuleId!.Value)
            .Distinct()
            .ToArray();
        var rules = _uow.AlarmRules.Query()
            .Where(x => ruleIds.Contains(x.Id))
            .ToDictionary(x => x.Id);
        var forwarding = _uow.AlarmRuleEmailTargets.Query()
            .Where(x => ruleIds.Contains(x.AlarmRuleId))
            .ToList()
            .GroupBy(x => x.AlarmRuleId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Email).ToArray());
        var alarmIds = events.Select(x => x.Alarm.Id).ToArray();
        var workOrderAlarmIds = _uow.WorkOrders.Query()
            .Where(x => x.AlarmEventId.HasValue && alarmIds.Contains(x.AlarmEventId.Value))
            .Select(x => x.AlarmEventId!.Value)
            .ToHashSet();

        var items = events.Select(x =>
        {
            rules.TryGetValue(x.Alarm.RuleId ?? 0, out var rule);
            forwarding.TryGetValue(x.Alarm.RuleId ?? 0, out var emails);

            return new
            {
                x.Alarm.Id,
                x.Alarm.Severity,
                type = rule?.RuleType ?? AlarmRuleType.System,
                alarm = x.Alarm.Title,
                description = x.Alarm.TagName is null
                    ? x.Alarm.Notes
                    : $"{x.Alarm.TagName}: {x.Alarm.TriggerValue}",
                source = new
                {
                    x.Source.SourceType,
                    x.Source.SourceId,
                    x.Source.Code,
                    x.Source.Name,
                    x.Source.FactoryId,
                    x.Source.LineId,
                    x.Source.MachineId
                },
                time = x.Alarm.TriggeredAt,
                forwarding = emails ?? Array.Empty<string>(),
                status = EventStatus(x.Alarm),
                x.Alarm.AcknowledgedAt,
                canView = true,
                canAcknowledge = x.Alarm.AcknowledgedAt == null,
                canCreateWorkOrder = !workOrderAlarmIds.Contains(x.Alarm.Id)
            };
        }).ToArray();

        return Ok(ApiResponse<object>.Ok(new
        {
            factoryId,
            lineId,
            machineId,
            total = items.Length,
            items
        }));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] AlarmListFilterQuery query)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var filterError = ValidateAlarmFilter(scope, query);
        if (filterError is not null) return filterError;
        var filter = (await ResolveAlarmFilterAsync(query)) with
        {
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 100)
        };
        return AlarmList(scope, false, query.Severity, filter);
    }

    [HttpGet("{id:int}/detail")]
    public IActionResult Detail(int id, [FromQuery] int windowMinutes = 30)
    {
        if (!TryGetAccessibleAlarm(id, out _, out var alarm, out var error)) return error;
        windowMinutes = Math.Clamp(windowMinutes, 1, 1440);

        var rule = alarm.RuleId.HasValue
            ? _uow.AlarmRules.Query().FirstOrDefault(x => x.Id == alarm.RuleId.Value)
            : null;
        var source = ResolveSource(alarm);
        var emails = alarm.RuleId.HasValue
            ? _uow.AlarmRuleEmailTargets.Query()
                .Where(x => x.AlarmRuleId == alarm.RuleId.Value)
                .OrderBy(x => x.Email)
                .Select(x => x.Email)
                .ToArray()
            : Array.Empty<string>();
        var workOrder = _uow.WorkOrders.Query()
            .Where(x => x.AlarmEventId == alarm.Id)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.Id, x.Number, x.Status })
            .FirstOrDefault();
        var waveforms = BuildWaveforms(alarm, source, windowMinutes);
        var endAt = alarm.ResolvedAt ?? DateTime.UtcNow;
        return Ok(ApiResponse<object>.Ok(new
        {
            eventDetail = new
            {
                alarm.Id,
                eventId = $"EVT-{alarm.Id:D4}",
                type = alarm.Title,
                ruleType = rule?.RuleType ?? AlarmRuleType.System,
                alarm.Severity,
                equipment = new
                {
                    source.SourceType,
                    source.SourceId,
                    source.Code,
                    source.Name,
                    source.FactoryId,
                    source.LineId
                },
                alarm.TriggeredAt,
                alarm.AcknowledgedAt,
                alarm.AcknowledgedById,
                alarm.ResolvedAt,
                durationMs = Math.Max(0, Math.Round((endAt - alarm.TriggeredAt).TotalMilliseconds, 0)),
                status = EventStatus(alarm),
                alarm.TagName,
                alarm.TriggerValue,
                forwarding = emails,
                alarm.ProbableCause,
                alarm.Notes
            },
            waveforms,
            linkedWorkOrder = workOrder,
            workOrderDraft = new
            {
                workOrderType = WorkOrderType.Corrective,
                priority = SeverityPriority(alarm.Severity),
                machineId = source.MachineId,
                alarmEventId = alarm.Id,
                plannedStart = (DateTime?)null,
                description = $"Investigate alarm: {alarm.Title}"
            },
            actions = new
            {
                acknowledge = alarm.AcknowledgedAt == null,
                createWorkOrder = workOrder is null,
                updateProbableCause = true
            }
        }, new
        {
            telemetrySource = "RealTime.TelemetryTags",
            hasTelemetry = waveforms.Length > 0,
            windowMinutes
        }));
    }

    [HttpPut("{id:int}/probable-cause")]
    public async Task<IActionResult> UpdateProbableCause(int id, [FromBody] AlarmProbableCauseRequest request)
    {
        if (!TryGetAccessibleAlarm(id, out _, out _, out var error)) return error;
        var alarm = await _uow.AlarmEvents.GetByIdAsync(id, asNoTracking: false);
        if (alarm is null)
            return NotFound(ApiResponse<object>.Fail("not_found", $"Alarm {id} was not found."));

        alarm.ProbableCause = string.IsNullOrWhiteSpace(request.ProbableCause)
            ? null
            : request.ProbableCause.Trim();
        _uow.AlarmEvents.Update(alarm);
        await _uow.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { alarm.Id, alarm.ProbableCause }));
    }

    [HttpPost("{id:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int id, [FromBody] AlarmActionRequest request)
    {
        if (!TryGetAccessibleAlarm(id, out var scope, out var alarm, out var error)) return error;
        if (alarm.AcknowledgedAt.HasValue)
            return Conflict(ApiResponse<object>.Fail("already_acknowledged", "This alarm has already been acknowledged."));

        return this.ToActionResult(await _alarmWorkflow.AcknowledgeAsync(id, scope.UserId, request.Notes));
    }

    [HttpPost("{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id, [FromBody] AlarmActionRequest request)
    {
        if (!TryGetAccessibleAlarm(id, out var scope, out _, out var error)) return error;
        return this.ToActionResult(await _alarmWorkflow.ResolveAsync(
            id,
            scope.UserId,
            request.Notes,
            request.ResolutionAction));
    }

    [HttpPost("{id:int}/create-wo")]
    public async Task<IActionResult> CreateWorkOrder(int id, [FromBody] CreateWorkOrderRequest request)
    {
        if (!TryGetAccessibleAlarm(id, out var scope, out var alarm, out var error)) return error;
        var source = ResolveSource(alarm);
        var machineId = request.MachineId ?? source.MachineId;

        var validation = ValidateWorkOrderSelection(scope, machineId, request.AssignedToId);
        if (validation is not null) return validation;
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? $"Investigate alarm: {alarm.Title}"
            : request.Description.Trim();

        var command = new CreateWorkOrderCommand(
            scope.CustomerId,
            scope.UserId,
            request.WorkOrderType ?? WorkOrderType.Corrective,
            request.Priority ?? SeverityPriority(alarm.Severity),
            machineId,
            id,
            request.AssignedToId,
            request.PlannedStart,
            description);
        return this.ToActionResult(await _maintenanceWorkflow.CreateWorkOrderAsync(command));
    }

    private IActionResult AlarmList(
        AuthenticatedAccessScope scope,
        bool active,
        string? severity,
        FrontendFilter filter)
    {
        if (!TryParseSeverity(severity, out var parsedSeverity))
            return BadRequest(ApiResponse<object>.Fail("invalid_severity", $"Unknown alarm severity '{severity}'."));

        var page = filter.Page;
        var pageSize = filter.PageSize;
        var events = AccessibleEvents(scope)
            .Where(x => active
                ? x.ResolvedAt == null
                : x.AcknowledgedAt != null || x.ResolvedAt != null)
            .Where(x => !parsedSeverity.HasValue || x.Severity == parsedSeverity.Value)
            .Select(x => new { Alarm = x, Source = ResolveSource(x) })
            .Where(x => MatchesOperationalFilter(x.Alarm, x.Source, filter))
            .Where(x => string.IsNullOrWhiteSpace(filter.Search) ||
                        x.Alarm.Title.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) ||
                        x.Source.Name.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) ||
                        (x.Alarm.TagName?.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(x => x.Alarm.TriggeredAt)
            .ThenByDescending(x => x.Alarm.Id)
            .ToList();
        var total = events.Count;
        var pageRows = events.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var ruleIds = pageRows.Where(x => x.Alarm.RuleId.HasValue).Select(x => x.Alarm.RuleId!.Value).Distinct().ToArray();
        var rules = _uow.AlarmRules.Query()
            .Where(x => ruleIds.Contains(x.Id))
            .ToDictionary(x => x.Id);
        var forwarding = _uow.AlarmRuleEmailTargets.Query()
            .Where(x => ruleIds.Contains(x.AlarmRuleId))
            .ToList()
            .GroupBy(x => x.AlarmRuleId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Email).ToArray());
        var alarmIds = pageRows.Select(x => x.Alarm.Id).ToArray();
        var workOrderAlarmIds = _uow.WorkOrders.Query()
            .Where(x => x.AlarmEventId.HasValue && alarmIds.Contains(x.AlarmEventId.Value))
            .Select(x => x.AlarmEventId!.Value)
            .ToHashSet();

        var items = pageRows.Select(x =>
        {
            rules.TryGetValue(x.Alarm.RuleId ?? 0, out var rule);
            forwarding.TryGetValue(x.Alarm.RuleId ?? 0, out var emails);
            return new
            {
                x.Alarm.Id,
                x.Alarm.Severity,
                type = rule?.RuleType ?? AlarmRuleType.System,
                alarm = x.Alarm.Title,
                description = x.Alarm.TagName is null
                    ? x.Alarm.Notes
                    : $"{x.Alarm.TagName}: {x.Alarm.TriggerValue}",
                source = new
                {
                    x.Source.SourceType,
                    x.Source.SourceId,
                    x.Source.Code,
                    x.Source.Name,
                    x.Source.FactoryId,
                    x.Source.LineId
                },
                time = x.Alarm.TriggeredAt,
                forwarding = emails ?? Array.Empty<string>(),
                status = EventStatus(x.Alarm),
                x.Alarm.AcknowledgedAt,
                x.Alarm.ResolvedAt,
                canView = true,
                canAcknowledge = x.Alarm.AcknowledgedAt == null,
                canCreateWorkOrder = !workOrderAlarmIds.Contains(x.Alarm.Id)
            };
        }).ToArray();

        return Ok(ApiResponse<object>.Ok(new
        {
            items,
            pagination = new
            {
                page,
                pageSize,
                total,
                totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
                hasPrevious = page > 1,
                hasNext = page * pageSize < total
            }
        }, new
        {
            filter.Duration,
            filter.FactoryId,
            filter.LineId,
            filter.From,
            filter.To,
            filter.ShiftId,
            filter.ShiftName,
            filter.Mode,
            filter.Note,
            filter.Search,
            severity
        }));
    }

    private IActionResult? ValidateAlarmFilter(
        AuthenticatedAccessScope scope,
        AlarmTimeFilterQuery query)
    {
        if (!query.FactoryId.HasValue || query.FactoryId.Value <= 0)
            return BadRequest(ApiResponse<object>.Fail(
                "factory_required",
                "FactoryId is required and must be greater than zero."));
        if (!scope.CanAccessFactory(query.FactoryId.Value))
            return Forbidden(
                "factory_access_denied",
                "The selected factory is not available to the authenticated user.");

        if (query.LineId.HasValue)
        {
            if (query.LineId.Value <= 0 ||
                !scope.LineFactoryIds.TryGetValue(query.LineId.Value, out var lineFactoryId) ||
                lineFactoryId != query.FactoryId.Value)
            {
                return Forbidden(
                    "line_access_denied",
                    "The selected line is not available to the authenticated user in the selected factory.");
            }
        }

        var duration = query.Duration ?? 0;
        if (duration is < 0 or > 5)
            return BadRequest(ApiResponse<object>.Fail(
                "invalid_duration",
                "Duration must be between 0 and 5."));
        if (duration == (int)DurationFilter.Custom &&
            (!query.From.HasValue || !query.To.HasValue))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "custom_range_required",
                "From and To are required when Duration is 5."));
        }
        if (duration == (int)DurationFilter.Custom &&
            query.From!.Value >= query.To!.Value)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "invalid_time_range",
                "From must be earlier than To."));
        }

        return null;
    }

    private IActionResult? ValidateActiveAlarmScope(
        AuthenticatedAccessScope scope,
        int factoryId,
        int? lineId,
        int? machineId)
    {
        if (factoryId <= 0)
            return BadRequest(ApiResponse<object>.Fail(
                "factory_required",
                "FactoryId is required and must be greater than zero."));

        if (!scope.CanAccessFactory(factoryId))
            return Forbidden(
                "factory_access_denied",
                "The selected factory is not available to the authenticated user.");

        if (lineId.HasValue &&
            (lineId.Value <= 0 ||
             !scope.LineFactoryIds.TryGetValue(lineId.Value, out var lineFactoryId) ||
             lineFactoryId != factoryId))
        {
            return Forbidden(
                "line_access_denied",
                "The selected line is not available to the authenticated user in the selected factory.");
        }

        if (!machineId.HasValue) return null;
        if (machineId.Value <= 0)
            return BadRequest(ApiResponse<object>.Fail(
                "invalid_machine",
                "MachineId must be greater than zero when supplied."));

        var machineLocation = (
            from machine in _uow.Machines.Query()
            join line in _uow.ProductionLines.Query() on machine.LineId equals line.Id
            where machine.Id == machineId.Value &&
                  machine.IsActive &&
                  !machine.IsDeleted &&
                  line.IsActive &&
                  !line.IsDeleted
            select new { machine.LineId, line.FactoryId })
            .FirstOrDefault();

        if (machineLocation is null ||
            machineLocation.FactoryId != factoryId ||
            !scope.LineIds.Contains(machineLocation.LineId) ||
            (lineId.HasValue && machineLocation.LineId != lineId.Value))
        {
            return Forbidden(
                "machine_access_denied",
                "The selected machine is not available to the authenticated user in the selected factory and line scope.");
        }

        return null;
    }

    private async Task<FrontendFilter> ResolveAlarmFilterAsync(AlarmTimeFilterQuery query)
    {
        return FrontendFilter.FromResolved(
            await _timeUtilities.ResolveAsync(query.ToTimeFilterInput()));
    }

    private static bool MatchesOperationalFilter(
        AlarmEvent alarm,
        AlarmSourceInfo source,
        FrontendFilter filter)
        => source.FactoryId == filter.FactoryId &&
           (!filter.LineId.HasValue || source.LineId == filter.LineId.Value) &&
           alarm.TriggeredAt >= filter.From &&
           alarm.TriggeredAt <= filter.To;

    private IEnumerable<AlarmEvent> AccessibleEvents(AuthenticatedAccessScope scope)
        => _uow.AlarmEvents.Query()
            .Where(x => x.CustomerId == scope.CustomerId)
            .ToList()
            .Where(x => scope.CanAccessAlarm(x, _uow));
    private object[] BuildWaveforms(
        AlarmEvent alarm,
        AlarmSourceInfo source,
        int windowMinutes)
    {
        var sourceIds = ResolveTelemetrySourceIds(
            alarm,
            source.DeviceId);

        if (sourceIds.Length == 0)
            return Array.Empty<object>();

        var from = alarm.TriggeredAt.AddMinutes(-windowMinutes);
        var to = alarm.TriggeredAt.AddMinutes(windowMinutes);

        var readings = _uow.TelemetryTags.Query()
            .Where(x =>
                sourceIds.Contains(x.DeviceId) &&
                x.RecordedAt >= from &&
                x.RecordedAt <= to &&
                x.ValueNum.HasValue)
            .OrderBy(x => x.RecordedAt)
            .ToList();

        if (readings.Count == 0)
            return Array.Empty<object>();

        var groups = readings
            .GroupBy(
                x => x.TagName,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        var primary = groups.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(alarm.TagName) &&
            string.Equals(
                x.Key,
                alarm.TagName,
                StringComparison.OrdinalIgnoreCase));

        primary ??= groups.FirstOrDefault();

        if (primary is null)
            return Array.Empty<object>();

        var correlatedName = CorrelatedTag(primary.Key);

        var correlated = groups.FirstOrDefault(x =>
            string.Equals(
                x.Key,
                correlatedName,
                StringComparison.OrdinalIgnoreCase))
            ?? groups.FirstOrDefault(x =>
                !string.Equals(
                    x.Key,
                    primary.Key,
                    StringComparison.OrdinalIgnoreCase));

        var selected = new[] { primary, correlated }
            .Where(x => x is not null)
            .DistinctBy(
                x => x!.Key,
                StringComparer.OrdinalIgnoreCase)
            .Cast<IGrouping<string, TelemetryTag>>()
            .ToArray();

        return selected.Select((group, index) =>
        {
            var values = DownSample(group.ToList(), 300);

            var closestId = values
                .OrderBy(x =>
                    Math.Abs(
                        (x.RecordedAt - alarm.TriggeredAt).Ticks))
                .Select(x => x.Id)
                .FirstOrDefault();

            return (object)new
            {
                tagName = group.Key,
                label = HumanizeTag(group.Key),
                unit = ResolveTelemetryUnit(
                    source.DeviceId,
                    group.Key),
                isPrimary = index == 0,
                points = values.Select(x => new
                {
                    timestamp = x.RecordedAt,
                    value = x.ValueNum,
                    isTriggerPoint = x.Id == closestId
                }).ToArray()
            };
        }).ToArray();
    }

    private string[] ResolveTelemetrySourceIds(
     AlarmEvent alarm,
     int? deviceId)
    {
        var sourceIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        if (deviceId.HasValue)
        {
            var device = _uow.Devices.Query()
                .FirstOrDefault(x => x.Id == deviceId.Value);

            if (device is not null)
            {
                if (!string.IsNullOrWhiteSpace(device.SerialNumber))
                    sourceIds.Add(device.SerialNumber);

                if (!string.IsNullOrWhiteSpace(device.MqttClientId))
                    sourceIds.Add(device.MqttClientId);
            }
        }

        if (alarm.MachineId.HasValue)
        {
            var machine = _uow.Machines.Query()
                .FirstOrDefault(x =>
                    x.Id == alarm.MachineId.Value);

            if (machine is not null)
            {
                if (!string.IsNullOrWhiteSpace(machine.Code))
                    sourceIds.Add(machine.Code);

                if (!string.IsNullOrWhiteSpace(machine.Name))
                    sourceIds.Add(machine.Name);
            }
        }

        return sourceIds.ToArray();
    }

    private string? ResolveTelemetryUnit(
        int? deviceId,
        string tagName)
    {
        if (!deviceId.HasValue)
            return null;

        var deviceTypeId = _uow.Devices.Query()
            .Where(x => x.Id == deviceId.Value)
            .Select(x => (int?)x.DeviceTypeId)
            .FirstOrDefault();

        if (!deviceTypeId.HasValue)
            return null;

        return _uow.DeviceTypeTagDefinitions.Query()
            .Where(x =>
                x.DeviceTypeId == deviceTypeId.Value &&
                x.TagName == tagName)
            .Select(x => x.Unit)
            .FirstOrDefault();
    }

    private AlarmSourceInfo ResolveSource(AlarmEvent alarm)
    {
        if (alarm.MachineId.HasValue)
        {
            var result = (
                from machine in _uow.Machines.Query()
                join line in _uow.ProductionLines.Query() on machine.LineId equals line.Id
                where machine.Id == alarm.MachineId.Value
                select new AlarmSourceInfo(
                    "machine",
                    machine.Id,
                    machine.Code,
                    machine.Name,
                    line.FactoryId,
                    line.Id,
                    machine.DeviceId,
                    machine.Id))
                .FirstOrDefault();
            if (result is not null) return result;
        }

        if (alarm.SiloId.HasValue)
        {
            var result = _uow.Silos.Query()
                .Where(x => x.Id == alarm.SiloId.Value)
                .Select(x => new AlarmSourceInfo(
                    "silo",
                    x.Id,
                    x.Code,
                    x.Name,
                    x.FactoryId,
                    null,
                    x.DeviceId,
                    null))
                .FirstOrDefault();
            if (result is not null) return result;
        }

        if (alarm.DeviceId.HasValue)
        {
            var result = _uow.Devices.Query()
                .Where(x => x.Id == alarm.DeviceId.Value)
                .Select(x => new AlarmSourceInfo(
                    "device",
                    x.Id,
                    x.SerialNumber,
                    x.SerialNumber,
                    x.FactoryId,
                    x.LineId,
                    x.Id,
                    null))
                .FirstOrDefault();
            if (result is not null) return result;
        }

        return new AlarmSourceInfo("system", 0, "SYSTEM", "System", 0, null, null, null);
    }

    private IActionResult? ValidateWorkOrderSelection(
        AuthenticatedAccessScope scope,
        int? machineId,
        int? assignedToId)
    {
        if (machineId.HasValue)
        {
            var lineId = _uow.Machines.Query()
                .Where(x => x.Id == machineId.Value && x.IsActive)
                .Select(x => x.LineId)
                .FirstOrDefault();
            if (lineId <= 0 || !scope.LineIds.Contains(lineId))
                return Forbidden("machine_access_denied", "The selected machine is not available to the authenticated user.");
        }

        if (assignedToId.HasValue)
        {
            var valid = _userManager.Users.Any(x =>
                x.Id == assignedToId.Value &&
                x.CustomerId == scope.CustomerId &&
                x.IsActive);
            if (!valid)
                return BadRequest(ApiResponse<object>.Fail("invalid_assignee", "The selected technician is not an active user for this customer."));
        }

        return null;
    }

    private bool TryGetAccessibleAlarm(
        int id,
        out AuthenticatedAccessScope scope,
        out AlarmEvent alarm,
        out IActionResult error)
    {
        alarm = default!;
        if (!TryGetScope(out scope, out error)) return false;
        var found = _uow.AlarmEvents.Query().FirstOrDefault(x => x.Id == id);
        if (found is not null && scope.CanAccessAlarm(found, _uow))
        {
            alarm = found;
            return true;
        }

        error = NotFound(ApiResponse<object>.Fail("not_found", $"Alarm {id} was not found."));
        return false;
    }

    private bool TryGetScope(out AuthenticatedAccessScope scope, out IActionResult error)
    {
        if (AuthenticatedAccessScope.TryResolve(User, _uow, out scope, out var code, out var message))
        {
            error = default!;
            return true;
        }

        error = Unauthorized(ApiResponse<object>.Fail(code, message));
        return false;
    }

    private ObjectResult Forbidden(string code, string message)
        => StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(code, message));

    private static bool TryParseSeverity(string? value, out AlarmSeverity? severity)
    {
        severity = null;
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
            return true;
        var normalized = value.Replace("_", string.Empty).Replace("-", string.Empty);
        foreach (var candidate in Enum.GetValues<AlarmSeverity>())
        {
            if (!string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase)) continue;
            severity = candidate;
            return true;
        }
        return false;
    }

    private static string EventStatus(AlarmEvent alarm)
        => alarm.ResolvedAt.HasValue
            ? "resolved"
            : alarm.AcknowledgedAt.HasValue
                ? "acknowledged"
                : "active";

    private static WorkOrderPriority SeverityPriority(AlarmSeverity severity) => severity switch
    {
        AlarmSeverity.Critical => WorkOrderPriority.Critical,
        AlarmSeverity.High => WorkOrderPriority.High,
        AlarmSeverity.Medium => WorkOrderPriority.Medium,
        _ => WorkOrderPriority.Low
    };

    private static string CorrelatedTag(string primaryTag)
    {
        var normalized = primaryTag.ToLowerInvariant();
        if (normalized.Contains("voltage")) return "current";
        if (normalized.Contains("current")) return "voltage";
        if (normalized.Contains("vibration")) return "temperature";
        if (normalized.Contains("temperature")) return "vibration";
        if (normalized.Contains("speed")) return "power";
        if (normalized.Contains("power")) return "current";
        return string.Empty;
    }

    private static string HumanizeTag(string value)
        => string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));

    private static List<TelemetryTag> DownSample(
      List<TelemetryTag> readings,
      int maxPoints)
    {
        if (readings.Count <= maxPoints)
            return readings;

        var step = (int)Math.Ceiling(
            readings.Count / (double)maxPoints);

        return readings
            .Where((_, index) => index % step == 0)
            .Take(maxPoints)
            .ToList();
    }

    private sealed record AlarmSourceInfo(
        string SourceType,
        int SourceId,
        string Code,
        string Name,
        int FactoryId,
        int? LineId,
        int? DeviceId,
        int? MachineId);
}
