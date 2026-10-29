using API.Common;
using API.Contracts;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Domain.Entities.Alarms;
using Domain.Enums;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;

namespace API.Controllers.Alarms;

[ApiController]
[Authorize]
[Route("v1/alarm-rules")]
public class AlarmRulesController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IAlarmWorkflowService _workflow;
    private readonly UserManager<ApplicationUser> _userManager;

    public AlarmRulesController(
        IUnitOfWork uow,
        IAlarmWorkflowService workflow,
        UserManager<ApplicationUser> userManager)
    {
        _uow = uow;
        _workflow = workflow;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult UserRules()
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        return Ok(ApiResponse<object>.Ok(BuildRuleList(scope, AlarmRuleType.User)));
    }

    [HttpGet("system")]
    public IActionResult SystemRules()
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        return Ok(ApiResponse<object>.Ok(BuildRuleList(scope, AlarmRuleType.System)));
    }

    [HttpGet("{id:int}/detail")]
    public IActionResult Detail(int id)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var rule = _uow.AlarmRules.Query().FirstOrDefault(x => x.Id == id && x.CustomerId == scope.CustomerId);
        return rule is null
            ? NotFound(ApiResponse<object>.Fail("not_found", $"Alarm rule {id} was not found."))
            : Ok(ApiResponse<object>.Ok(BuildRuleDto(rule)));
    }

    
    [HttpGet("form-options")]
public IActionResult FormOptions()
    {
        if (!TryGetScope(out var scope, out var error))
            return error;

        var factories = _uow.Factories.Query()
            .Where(x =>
                scope.FactoryIds.Contains(x.Id) &&
                x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                value = x.Id,
                label = x.Name,
                x.Code
            })
            .ToArray();

        var lines = _uow.ProductionLines.Query()
            .Where(x =>
                scope.LineIds.Contains(x.Id) &&
                x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                value = x.Id,
                label = x.Name,
                x.Code,
                x.FactoryId
            })
            .ToArray();

        var machines = _uow.Machines.Query()
            .Where(x =>
                scope.LineIds.Contains(x.LineId) &&
                x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                value = x.Id,
                label = x.Name,
                x.Code,
                x.LineId
            })
            .ToArray();

        var devices = _uow.Devices.Query()
            .Where(x =>
                x.CustomerId == scope.CustomerId &&
                scope.FactoryIds.Contains(x.FactoryId) &&
                x.IsActive)
            .OrderBy(x => x.SerialNumber)
            .Select(x => new
            {
                value = x.Id,
                label = x.SerialNumber,
                x.MqttClientId,
                x.FactoryId,
                x.LineId,
                x.DeviceTypeId
            })
            .ToArray();

        var allowedSourceIds = devices
            .SelectMany(x => new string?[]
            {
            x.label,
            x.MqttClientId
            })
            .Concat(machines.SelectMany(x => new string?[]
            {
            x.Code,
            x.label
            }))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var observedTagNames = _uow.TelemetryTags.Query()
            .Where(x => allowedSourceIds.Contains(x.DeviceId))
            .Select(x => x.TagName)
            .Distinct()
            .ToArray();

        var allowedDeviceTypeIds = devices
            .Select(x => x.DeviceTypeId)
            .Distinct()
            .ToArray();

        var definedTags = _uow.DeviceTypeTagDefinitions.Query()
            .Where(x => allowedDeviceTypeIds.Contains(x.DeviceTypeId))
            .Select(x => new
            {
                x.TagName,
                x.Unit
            })
            .ToArray();

        var unitsByTag = definedTags
            .Where(x => !string.IsNullOrWhiteSpace(x.Unit))
            .GroupBy(
                x => x.TagName,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.First().Unit,
                StringComparer.OrdinalIgnoreCase);

        var telemetryTags = observedTagNames
            .Concat(definedTags.Select(x => x.TagName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Select(tagName => new
            {
                TagName = tagName,
                Unit = unitsByTag.GetValueOrDefault(tagName)
            })
            .ToArray();

        var technicians = _userManager.Users
            .Where(x =>
                x.CustomerId == scope.CustomerId &&
                x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                value = x.Id,
                label = x.FullName,
                x.Email
            })
            .ToArray();

        return Ok(ApiResponse<object>.Ok(new
        {
            severities = NumericEnumOptions<AlarmSeverity>(),
            statuses = NumericEnumOptions<AlarmRuleStatus>(),
            workOrderPriorities = NumericEnumOptions<WorkOrderPriority>(),
            telemetryTags,
            scopeEntities = new
            {
                factories,
                lines,
                machines
            },
            technicians,
            defaults = new
            {
                ruleType = AlarmRuleType.User,
                severity = (int)AlarmSeverity.Medium,
                status = (int)AlarmRuleStatus.Active,
                createWorkOrder = false
            }
        }, new
        {
            source = "database_and_enum_catalog"
        }));
    }

    [HttpGet("enums")]
    public IActionResult Enums()
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            severities = NumericEnumOptions<AlarmSeverity>(),
            statuses = NumericEnumOptions<AlarmRuleStatus>(),
            workOrderPriorities = NumericEnumOptions<WorkOrderPriority>()
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AlarmRuleUpsertRequest request)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var enumError = ValidateEnums(request);
        if (enumError is not null) return enumError;
        var normalized = Normalize(request);
        var validation = ValidateRequest(scope, normalized);
        if (validation is not null) return validation;

        var duplicate = _uow.AlarmRules.Query().Any(x =>
            x.CustomerId == scope.CustomerId &&
            x.Name == normalized.Name &&
            x.RuleType == AlarmRuleType.User);
        if (duplicate)
            return Conflict(ApiResponse<object>.Fail("duplicate_rule_name", "A user-defined alarm rule with this name already exists."));

        var rule = await _uow.ExecuteInTransactionAsync(async () =>
        {
            var entity = ToEntity(normalized, scope);
            await _uow.AlarmRules.AddAsync(entity);
            await _uow.SaveChangesAsync();
            await ReplaceChildrenAsync(entity.Id, normalized);
            return entity;
        });

        return Created($"/v1/alarm-rules/{rule.Id}/detail", ApiResponse<object>.Ok(BuildRuleDto(rule)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AlarmRuleUpsertRequest request)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var enumError = ValidateEnums(request);
        if (enumError is not null) return enumError;
        var rule = await _uow.AlarmRules.GetByIdAsync(id, asNoTracking: false);
        if (rule is null || rule.CustomerId != scope.CustomerId)
            return NotFound(ApiResponse<object>.Fail("not_found", $"Alarm rule {id} was not found."));
        if (rule.RuleType == AlarmRuleType.System)
            return Forbidden("system_rule_read_only", "System alarm rules can only be enabled or disabled.");

        var normalized = Normalize(request);
        var validation = ValidateRequest(scope, normalized);
        if (validation is not null) return validation;
        var nameChanged = !string.Equals(rule.Name.Trim(), normalized.Name, StringComparison.OrdinalIgnoreCase);
        var duplicate = nameChanged && _uow.AlarmRules.Query().Any(x =>
            x.Id != id &&
            x.CustomerId == scope.CustomerId &&
            x.Name == normalized.Name &&
            x.RuleType == AlarmRuleType.User);
        if (duplicate)
            return Conflict(ApiResponse<object>.Fail("duplicate_rule_name", "A user-defined alarm rule with this name already exists."));

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            Apply(rule, normalized);
            _uow.AlarmRules.Update(rule);
            await ReplaceChildrenAsync(rule.Id, normalized);
            return rule;
        });

        return Ok(ApiResponse<object>.Ok(BuildRuleDto(rule)));
    }

    [HttpPut("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, [FromBody] AlarmRuleToggleRequest request)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var rule = _uow.AlarmRules.Query().FirstOrDefault(x => x.Id == id && x.CustomerId == scope.CustomerId);
        if (rule is null)
            return NotFound(ApiResponse<object>.Fail("not_found", $"Alarm rule {id} was not found."));

        return this.ToActionResult(await _workflow.ToggleRuleAsync(id, request.IsActive));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var rule = await _uow.AlarmRules.GetByIdAsync(id, asNoTracking: false);
        if (rule is null || rule.CustomerId != scope.CustomerId)
            return NotFound(ApiResponse<object>.Fail("not_found", $"Alarm rule {id} was not found."));
        if (rule.RuleType == AlarmRuleType.System)
            return Forbidden("system_rule_delete_denied", "System alarm rules cannot be deleted.");

        var hasEvents = _uow.AlarmEvents.Query().Any(x => x.RuleId == id);
        rule.IsActive = false;
        _uow.AlarmRules.Remove(rule);
        await _uow.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            id,
            deleted = true,
            deletionMode = "soft_delete",
            retainedEvents = hasEvents
        }));
    }

    private object[] BuildRuleList(AuthenticatedAccessScope scope, AlarmRuleType type)
        => _uow.AlarmRules.Query()
            .Where(x => x.CustomerId == scope.CustomerId && x.RuleType == type)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .ToList()
            .Select(BuildRuleDto)
            .ToArray();

    private object BuildRuleDto(AlarmRule rule)
    {
        var scopes = _uow.AlarmRuleScopes.Query()
            .Where(x => x.AlarmRuleId == rule.Id)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.ScopeType, x.EntityId })
            .ToArray();
        var actions = _uow.AlarmRuleActions.Query()
            .Where(x => x.AlarmRuleId == rule.Id)
            .OrderBy(x => x.SequenceNo)
            .ToList()
            .Select(x => new
            {
                x.Id,
                x.SequenceNo,
                x.ActionType,
                x.IsEnabled,
                workOrderPriority = x.WorkOrderPriority.HasValue
                    ? (int?)x.WorkOrderPriority.Value
                    : null,
                x.AssignedToId
            })
            .ToList<object>();
        var emails = _uow.AlarmRuleEmailTargets.Query()
            .Where(x => x.AlarmRuleId == rule.Id)
            .OrderBy(x => x.Email)
            .Select(x => x.Email)
            .ToArray();

        if (actions.Count == 0)
        {
            var sequence = (short)1;
            if (rule.CreateWorkOrder)
            {
                actions.Add(new
                {
                    Id = 0,
                    SequenceNo = sequence++,
                    ActionType = AlarmRuleActionType.CreateWorkOrder,
                    IsEnabled = true,
                    WorkOrderPriority = (int?)SeverityPriority(rule.Severity),
                    AssignedToId = (int?)null
                });
            }
            if (emails.Length > 0)
            {
                actions.Add(new
                {
                    Id = 0,
                    SequenceNo = sequence++,
                    ActionType = AlarmRuleActionType.EmailNotification,
                    IsEnabled = true,
                    WorkOrderPriority = (int?)null,
                    AssignedToId = (int?)null
                });
            }
        }

        var scopeItems = scopes.Length > 0
            ? scopes.Cast<object>().ToArray()
            : rule.ScopeEntityId.HasValue
                ? new object[] { new { Id = 0, rule.ScopeType, EntityId = rule.ScopeEntityId.Value } }
                : Array.Empty<object>();

        return new
        {
            rule.Id,
            rule.Name,
            rule.RuleType,
            rule.ScopeType,
            rule.ScopeEntityId,
            scopes = scopeItems,
            rule.TagName,
            rule.MinimumValue,
            rule.MaximumValue,
            severity = (int)rule.Severity,
            rule.IsActive,
            status = rule.IsActive
                ? (int)AlarmRuleStatus.Active
                : (int)AlarmRuleStatus.Inactive,
            createWorkOrder = rule.CreateWorkOrder,
            actions,
            emailRecipients = emails,
            canEdit = rule.RuleType == AlarmRuleType.User,
            canDelete = rule.RuleType == AlarmRuleType.User,
            canToggle = true
        };
    }

    private IActionResult? ValidateRequest(AuthenticatedAccessScope scope, NormalizedAlarmRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse<object>.Fail("name_required", "Alarm rule name is required."));
        if (request.Name.Length > 150)
            return BadRequest(ApiResponse<object>.Fail("name_too_long", "Alarm rule name cannot exceed 150 characters."));
        if (string.IsNullOrWhiteSpace(request.TagName))
            return BadRequest(ApiResponse<object>.Fail("parameter_required", "Select a machine parameter."));
        if (request.ScopeType == AlarmScopeType.Global && request.ScopeEntityIds.Count > 0)
            return BadRequest(ApiResponse<object>.Fail("invalid_global_scope", "Global rules cannot contain individual scope entities."));
        if (request.ScopeType != AlarmScopeType.Global && request.ScopeEntityIds.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("scope_required", "Select at least one operating asset for this scope."));
        if (request.ScopeEntityIds.Count != request.ScopeEntityIds.Distinct().Count())
            return BadRequest(ApiResponse<object>.Fail("duplicate_scope", "The same operating asset cannot be selected more than once."));
        if (!ValidateScopeIds(scope, request.ScopeType, request.ScopeEntityIds))
            return Forbidden("scope_access_denied", "One or more selected scope entities are invalid or outside the authenticated user's access.");
        if (!request.MinimumValue.HasValue && !request.MaximumValue.HasValue)
            return BadRequest(ApiResponse<object>.Fail("range_required", "MinimumValue or MaximumValue is required."));
        if (request.MinimumValue.HasValue &&
            request.MaximumValue.HasValue &&
            request.MinimumValue.Value >= request.MaximumValue.Value)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "invalid_range",
                "MinimumValue must be less than MaximumValue."));
        }
        if (!MachinesSupportParameter(request.ScopeEntityIds, request.TagName!))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "parameter_not_supported",
                "The selected parameter is not available for every selected machine."));
        }

        if (request.Actions.Select(x => x.SequenceNo).Distinct().Count() != request.Actions.Count ||
            request.Actions.Select(x => x.ActionType).Distinct().Count() != request.Actions.Count)
            return BadRequest(ApiResponse<object>.Fail("duplicate_action", "Action type and sequence number must be unique inside an alarm rule."));
        if (request.Actions.Any(x => x.SequenceNo <= 0))
            return BadRequest(ApiResponse<object>.Fail("invalid_action_sequence", "Action sequence numbers must be greater than zero."));
        var workOrderAction = request.Actions.FirstOrDefault(x =>
            x.ActionType == AlarmRuleActionType.CreateWorkOrder &&
            x.IsEnabled);
        if (workOrderAction is not null && !workOrderAction.AssignedToId.HasValue)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "assignee_required",
                "AssignedToId is required when automatic work-order creation is enabled."));
        }
        var assigneeIds = request.Actions.Where(x => x.AssignedToId.HasValue).Select(x => x.AssignedToId!.Value).Distinct().ToArray();
        if (assigneeIds.Length > 0)
        {
            var validAssignees = _userManager.Users.Count(x =>
                assigneeIds.Contains(x.Id) &&
                x.CustomerId == scope.CustomerId &&
                x.IsActive);
            if (validAssignees != assigneeIds.Length)
                return BadRequest(ApiResponse<object>.Fail("invalid_assignee", "One or more action assignees are not active users for this customer."));
        }

        foreach (var email in request.EmailRecipients)
        {
            try
            {
                _ = new MailAddress(email);
            }
            catch
            {
                return BadRequest(ApiResponse<object>.Fail("invalid_email", $"'{email}' is not a valid email address."));
            }
        }
        var emailActionEnabled = request.Actions.Any(x =>
            x.ActionType == AlarmRuleActionType.EmailNotification &&
            x.IsEnabled);
        if (emailActionEnabled && request.EmailRecipients.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("email_recipient_required", "Enable email notification only after adding at least one recipient."));

        return null;
    }

    private IActionResult? ValidateEnums(AlarmRuleUpsertRequest request)
    {
        if (!Enum.IsDefined(typeof(AlarmSeverity), request.Severity))
            return BadRequest(ApiResponse<object>.Fail("invalid_severity", "The selected severity is invalid."));
        if (!Enum.IsDefined(typeof(AlarmRuleStatus), request.Status))
            return BadRequest(ApiResponse<object>.Fail("invalid_status", "The selected alarm-rule status is invalid."));
        if (request.WorkOrderPriority.HasValue &&
            !Enum.IsDefined(typeof(WorkOrderPriority), request.WorkOrderPriority.Value))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "invalid_work_order_priority",
                "The selected work-order priority is invalid."));
        }

        return null;
    }

    private bool MachinesSupportParameter(
     IReadOnlyCollection<int> machineIds,
     string parameter)
    {
        var machines = _uow.Machines.Query()
            .Where(x => machineIds.Contains(x.Id) && x.DeviceId.HasValue)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                DeviceId = x.DeviceId!.Value
            })
            .ToArray();

        if (machines.Length != machineIds.Count)
            return false;

        var deviceIds = machines
            .Select(x => x.DeviceId)
            .ToArray();

        var deviceTypes = _uow.Devices.Query()
            .Where(x => deviceIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DeviceTypeId })
            .ToDictionary(x => x.Id, x => x.DeviceTypeId);

        var definitionTypeIds = _uow.DeviceTypeTagDefinitions.Query()
            .Where(x => x.TagName == parameter)
            .Select(x => x.DeviceTypeId)
            .ToHashSet();

        var deviceSources = _uow.Devices.Query()
            .Where(x => deviceIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.SerialNumber,
                x.MqttClientId
            })
            .ToArray()
            .ToDictionary(
                x => x.Id,
                x => new[] { x.SerialNumber, x.MqttClientId }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Cast<string>()
                    .ToArray());

        var observedSourceIds = _uow.TelemetryTags.Query()
            .Where(x => x.TagName == parameter)
            .Select(x => x.DeviceId)
            .ToArray()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return machines.All(machine =>
            observedSourceIds.Contains(machine.Code) ||
            observedSourceIds.Contains(machine.Name) ||
            (deviceSources.TryGetValue(machine.DeviceId, out var sources) &&
             sources.Any(observedSourceIds.Contains)) ||
            (deviceTypes.TryGetValue(machine.DeviceId, out var deviceTypeId) &&
             definitionTypeIds.Contains(deviceTypeId)));
    }

    private bool ValidateScopeIds(AuthenticatedAccessScope scope, AlarmScopeType scopeType, IReadOnlyCollection<int> ids)
    {
        if (scopeType == AlarmScopeType.Global) return ids.Count == 0;
        var requested = ids.ToHashSet();
        HashSet<int> valid = scopeType switch
        {
            AlarmScopeType.Factory => scope.FactoryIds.ToHashSet(),
            AlarmScopeType.Line => scope.LineIds.ToHashSet(),
            AlarmScopeType.Machine => _uow.Machines.Query()
                .Where(x => scope.LineIds.Contains(x.LineId) && x.IsActive)
                .Select(x => x.Id)
                .ToHashSet(),
            AlarmScopeType.Device => _uow.Devices.Query()
                .Where(x => x.CustomerId == scope.CustomerId && scope.FactoryIds.Contains(x.FactoryId) && x.IsActive)
                .Select(x => x.Id)
                .ToHashSet(),
            AlarmScopeType.Silo => _uow.Silos.Query()
                .Where(x => scope.FactoryIds.Contains(x.FactoryId))
                .Select(x => x.Id)
                .ToHashSet(),
            AlarmScopeType.Scale => _uow.Scales.Query()
                .Where(x => scope.FactoryIds.Contains(x.FactoryId))
                .Select(x => x.Id)
                .ToHashSet(),
            AlarmScopeType.Asset => _uow.Assets.Query()
                .Where(x => x.CustomerId == scope.CustomerId && scope.FactoryIds.Contains(x.FactoryId) && x.IsActive)
                .Select(x => x.Id)
                .ToHashSet(),
            _ => new HashSet<int>()
        };
        return requested.IsSubsetOf(valid);
    }

    private async Task ReplaceChildrenAsync(int ruleId, NormalizedAlarmRuleRequest request)
    {
        var oldScopes = _uow.AlarmRuleScopes.Query(false).Where(x => x.AlarmRuleId == ruleId).ToList();
        var oldActions = _uow.AlarmRuleActions.Query(false).Where(x => x.AlarmRuleId == ruleId).ToList();
        var oldEmails = _uow.AlarmRuleEmailTargets.Query(false).Where(x => x.AlarmRuleId == ruleId).ToList();
        _uow.AlarmRuleScopes.RemoveRange(oldScopes);
        _uow.AlarmRuleActions.RemoveRange(oldActions);
        _uow.AlarmRuleEmailTargets.RemoveRange(oldEmails);

        // Persist the soft deletes before inserting replacements. The child tables
        // use filtered unique indexes, so inserting an unchanged scope/action in
        // the same SaveChanges batch can otherwise conflict with its still-active
        // predecessor.
        if (oldScopes.Count > 0 || oldActions.Count > 0 || oldEmails.Count > 0)
            await _uow.SaveChangesAsync();

        await _uow.AlarmRuleScopes.AddRangeAsync(request.ScopeEntityIds.Select(entityId => new AlarmRuleScope
        {
            AlarmRuleId = ruleId,
            ScopeType = request.ScopeType,
            EntityId = entityId
        }));
        await _uow.AlarmRuleActions.AddRangeAsync(request.Actions.Select(action => new AlarmRuleAction
        {
            AlarmRuleId = ruleId,
            SequenceNo = action.SequenceNo,
            ActionType = action.ActionType,
            IsEnabled = action.IsEnabled,
            WorkOrderPriority = action.ActionType == AlarmRuleActionType.CreateWorkOrder
                ? action.WorkOrderPriority ?? WorkOrderPriority.Medium
                : null,
            AssignedToId = action.ActionType == AlarmRuleActionType.CreateWorkOrder
                ? action.AssignedToId
                : null
        }));
        await _uow.AlarmRuleEmailTargets.AddRangeAsync(request.EmailRecipients.Select(email => new AlarmRuleEmailTarget
        {
            AlarmRuleId = ruleId,
            Email = email
        }));
    }

    private static AlarmRule ToEntity(NormalizedAlarmRuleRequest request, AuthenticatedAccessScope scope)
    {
        var rule = new AlarmRule
        {
            CustomerId = scope.CustomerId,
            RuleType = AlarmRuleType.User,
            CreatedById = scope.UserId
        };
        Apply(rule, request);
        return rule;
    }

    private static void Apply(AlarmRule rule, NormalizedAlarmRuleRequest request)
    {
        rule.Name = request.Name;
        rule.ScopeType = request.ScopeType;
        rule.ScopeEntityId = request.ScopeEntityIds.FirstOrDefault() is var first && first > 0 ? first : null;
        rule.TagName = request.TagName;
        rule.MinimumValue = request.MinimumValue;
        rule.MaximumValue = request.MaximumValue;
        rule.Severity = request.Severity;
        rule.IsActive = request.IsActive;
        rule.CreateWorkOrder = request.Actions.Any(x =>
            x.ActionType == AlarmRuleActionType.CreateWorkOrder &&
            x.IsEnabled);
    }

    private static NormalizedAlarmRuleRequest Normalize(AlarmRuleUpsertRequest request)
    {
        var severity = (AlarmSeverity)request.Severity;
        var actions = request.CreateWorkOrder
            ? new[]
            {
                new NormalizedAlarmRuleAction(
                    1,
                    AlarmRuleActionType.CreateWorkOrder,
                    true,
                    request.WorkOrderPriority.HasValue
                        ? (WorkOrderPriority)request.WorkOrderPriority.Value
                        : SeverityPriority(severity),
                    request.AssignedToId)
            }
            : Array.Empty<NormalizedAlarmRuleAction>();

        return new NormalizedAlarmRuleRequest(
            request.Name?.Trim() ?? string.Empty,
            AlarmScopeType.Machine,
            request.MachineIds?.Where(x => x > 0).Distinct().ToArray() ?? Array.Empty<int>(),
            Clean(request.Parameter),
            request.MinimumValue,
            request.MaximumValue,
            severity,
            (AlarmRuleStatus)request.Status == AlarmRuleStatus.Active,
            actions,
            Array.Empty<string>());
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

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object[] EnumOptions<TEnum>() where TEnum : struct, Enum
        => Enum.GetValues<TEnum>()
            .Select(value => new { value, label = SplitEnumName(value.ToString()) })
            .Cast<object>()
            .ToArray();

    private static object[] NumericEnumOptions<TEnum>() where TEnum : struct, Enum
        => Enum.GetValues<TEnum>()
            .Select(value => new
            {
                value = Convert.ToInt32(value),
                label = SplitEnumName(value.ToString())
            })
            .Cast<object>()
            .ToArray();

    private static string SplitEnumName(string value)
        => string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));

    private static WorkOrderPriority SeverityPriority(AlarmSeverity severity) => severity switch
    {
        AlarmSeverity.Critical => WorkOrderPriority.Critical,
        AlarmSeverity.High => WorkOrderPriority.High,
        AlarmSeverity.Medium => WorkOrderPriority.Medium,
        _ => WorkOrderPriority.Low
    };

    private sealed record NormalizedAlarmRuleRequest(
        string Name,
        AlarmScopeType ScopeType,
        IReadOnlyList<int> ScopeEntityIds,
        string? TagName,
        decimal? MinimumValue,
        decimal? MaximumValue,
        AlarmSeverity Severity,
        bool IsActive,
        IReadOnlyList<NormalizedAlarmRuleAction> Actions,
        IReadOnlyList<string> EmailRecipients);

    private sealed record NormalizedAlarmRuleAction(
        short SequenceNo,
        AlarmRuleActionType ActionType,
        bool IsEnabled,
        WorkOrderPriority? WorkOrderPriority,
        int? AssignedToId);
}
