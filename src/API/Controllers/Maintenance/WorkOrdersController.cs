using API.Common;
using API.Contracts;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Dtos.Maintenance;
using Domain.Entities.Maintenance;
using Domain.Enums;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Maintenance;

[ApiController]
[Authorize]
[Route("v1/work-orders")]
public class WorkOrdersController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IMaintenanceWorkflowService _workflow;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkOrdersController(
        IUnitOfWork uow,
        IMaintenanceWorkflowService workflow,
        UserManager<ApplicationUser> userManager)
    {
        _uow = uow;
        _workflow = workflow;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? priority = null,
        [FromQuery] int? factoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        if (factoryId.HasValue && !scope.CanAccessFactory(factoryId.Value))
            return Forbidden("factory_access_denied", "The selected factory is not available to the authenticated user.");

        if (!TryParseOptional(status, out WorkOrderStatus? parsedStatus))
            return BadRequest(ApiResponse<object>.Fail("invalid_status", $"Unknown work order status '{status}'."));
        if (!TryParseOptional(type, out WorkOrderType? parsedType))
            return BadRequest(ApiResponse<object>.Fail("invalid_work_order_type", $"Unknown work order type '{type}'."));
        if (!TryParseOptional(priority, out WorkOrderPriority? parsedPriority))
            return BadRequest(ApiResponse<object>.Fail("invalid_priority", $"Unknown work order priority '{priority}'."));

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _uow.WorkOrders.Query()
            .Where(x =>
                x.CustomerId == scope.CustomerId &&
                scope.FactoryIds.Contains(x.FactoryId));
        if (factoryId.HasValue) query = query.Where(x => x.FactoryId == factoryId.Value);
        if (parsedStatus.HasValue) query = query.Where(x => x.Status == parsedStatus.Value);
        if (parsedType.HasValue) query = query.Where(x => x.WorkOrderType == parsedType.Value);
        if (parsedPriority.HasValue) query = query.Where(x => x.Priority == parsedPriority.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Number.Contains(term) || x.Description.Contains(term));
        }

        var total = query.Count();
        var rows = query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        var users = _userManager.Users
            .Where(x => x.CustomerId == scope.CustomerId)
            .ToDictionary(x => x.Id, x => x.FullName);
        var machineIds = rows
            .Where(x => x.MachineId.HasValue)
            .Select(x => x.MachineId!.Value)
            .Distinct()
            .ToArray();
        var machines = _uow.Machines.Query()
            .Where(x => machineIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.Name);

        var items = rows.Select(x => new
        {
            x.Id,
            x.Number,
            title = ShortTitle(x.Description),
            x.MachineId,
            machineName = x.MachineId.HasValue && machines.TryGetValue(x.MachineId.Value, out var machineName)
                ? machineName
                : null,
            type = x.WorkOrderType,
            x.Priority,
            x.Status,
            x.AssignedToId,
            assignee = x.AssignedToId.HasValue && users.TryGetValue(x.AssignedToId.Value, out var userName) ? userName : null,
            created = x.CreatedAt,
            x.PlannedStart,
            canEdit = !IsClosed(x.Status),
            canClose = !IsClosed(x.Status)
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
        }));
    }

    [HttpGet("kpis")]
    public IActionResult Kpis()
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var rows = _uow.WorkOrders.Query()
            .Where(x => x.CustomerId == scope.CustomerId && scope.FactoryIds.Contains(x.FactoryId))
            .ToList();
        var now = DateTime.UtcNow;

        return Ok(ApiResponse<object>.Ok(new
        {
            open = rows.Count(x => x.Status is WorkOrderStatus.Open or WorkOrderStatus.Planned),
            inProgress = rows.Count(x => x.Status == WorkOrderStatus.InProgress),
            resolved = rows.Count(x => x.Status is WorkOrderStatus.Resolved or WorkOrderStatus.Completed),
            overdue = rows.Count(x => !IsClosed(x.Status) && x.PlannedStart.HasValue && x.PlannedStart.Value < now)
        }));
    }

    [HttpGet("form-options")]
    public IActionResult FormOptions()
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        return Ok(ApiResponse<object>.Ok(
            WorkOrderFormOptions.Build(_uow, _userManager, scope),
            new { source = "database_and_enum_catalog" }));
    }

    [HttpGet("{id:int}/detail")]
    public IActionResult Detail(int id)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var workOrder = _uow.WorkOrders.Query().FirstOrDefault(x => x.Id == id);
        if (workOrder is null || !scope.CanAccessWorkOrder(workOrder))
            return NotFound(ApiResponse<object>.Fail("not_found", $"Work order {id} was not found."));

        var machine = workOrder.MachineId.HasValue
            ? _uow.Machines.Query().Where(x => x.Id == workOrder.MachineId.Value).Select(x => new { x.Id, x.Code, x.Name }).FirstOrDefault()
            : null;
        var assignee = workOrder.AssignedToId.HasValue
            ? _userManager.Users.Where(x => x.Id == workOrder.AssignedToId.Value).Select(x => new { x.Id, x.FullName, x.Email }).FirstOrDefault()
            : null;

        return Ok(ApiResponse<object>.Ok(new
        {
            workOrder.Id,
            workOrder.Number,
            workOrder.CustomerId,
            workOrder.WorkOrderType,
            workOrder.Priority,
            workOrder.Status,
            workOrder.Description,
            workOrder.MachineId,
            machine,
            workOrder.AlarmEventId,
            workOrder.AssignedToId,
            assignee,
            workOrder.PlannedStart,
            workOrder.ActualStart,
            workOrder.ActualEnd,
            workOrder.CreatedAt,
            workOrder.ResolutionNotes,
            canEdit = !IsClosed(workOrder.Status),
            canClose = !IsClosed(workOrder.Status)
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkOrderRequest request)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var validation = ValidateSelection(scope, request.MachineId, request.AlarmEventId, request.AssignedToId);
        if (validation is not null) return validation;

        var command = new CreateWorkOrderCommand(
            scope.CustomerId,
            scope.UserId,
            request.WorkOrderType ?? WorkOrderType.Corrective,
            request.Priority ?? WorkOrderPriority.Medium,
            request.MachineId,
            request.AlarmEventId,
            request.AssignedToId,
            request.PlannedStart,
            request.Description);
        return this.ToActionResult(await _workflow.CreateWorkOrderAsync(command));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateWorkOrderRequest request)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var existing = _uow.WorkOrders.Query().FirstOrDefault(x => x.Id == id);
        if (existing is null || !scope.CanAccessWorkOrder(existing))
            return NotFound(ApiResponse<object>.Fail("not_found", $"Work order {id} was not found."));
        var machineId = request.MachineId ?? existing.MachineId;
        var validation = ValidateSelection(scope, machineId, existing.AlarmEventId, request.AssignedToId);
        if (validation is not null) return validation;

        var command = new UpdateWorkOrderCommand(
            request.WorkOrderType ?? existing.WorkOrderType,
            request.Priority ?? existing.Priority,
            machineId,
            request.AssignedToId,
            request.PlannedStart,
            request.Description);
        return this.ToActionResult(await _workflow.UpdateWorkOrderAsync(id, scope.CustomerId, command));
    }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id, [FromBody] CloseWorkOrderRequest request)
    {
        if (!TryGetAccessibleWorkOrder(id, out var scope, out var error)) return error;
        return this.ToActionResult(await _workflow.CloseWorkOrderAsync(id, scope.CustomerId, request.Notes));
    }

    [HttpPost("{id:int}/start")]
    public async Task<IActionResult> Start(int id, [FromBody] StartWorkOrderRequest request)
    {
        if (!TryGetAccessibleWorkOrder(id, out _, out var error)) return error;
        return this.ToActionResult(await _workflow.StartWorkOrderAsync(id, request.Notes));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteWorkOrderRequest request)
    {
        if (!TryGetAccessibleWorkOrder(id, out _, out var error)) return error;
        var parts = request.PartsUsed?.Select(x => (x.SparePartId, x.Quantity)).ToList();
        return this.ToActionResult(await _workflow.CompleteWorkOrderAsync(
            id,
            request.ResolutionNotes,
            request.FailureMode,
            request.ActualCost,
            parts));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest request)
    {
        if (!TryGetAccessibleWorkOrder(id, out _, out var error)) return error;
        return this.ToActionResult(await _workflow.CancelWorkOrderAsync(id, request.Reason));
    }

    private IActionResult? ValidateSelection(
        AuthenticatedAccessScope scope,
        int? machineId,
        int? alarmEventId,
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

        if (alarmEventId.HasValue)
        {
            var alarm = _uow.AlarmEvents.Query().FirstOrDefault(x => x.Id == alarmEventId.Value);
            if (alarm is null || !scope.CanAccessAlarm(alarm, _uow))
                return Forbidden("alarm_access_denied", "The selected alarm is not available to the authenticated user.");
        }

        if (assignedToId.HasValue)
        {
            var validUser = _userManager.Users.Any(x =>
                x.Id == assignedToId.Value &&
                x.CustomerId == scope.CustomerId &&
                x.IsActive);
            if (!validUser)
                return BadRequest(ApiResponse<object>.Fail("invalid_assignee", "The selected technician is not an active user for this customer."));
        }

        return null;
    }

    private bool TryGetAccessibleWorkOrder(
        int id,
        out AuthenticatedAccessScope scope,
        out IActionResult error)
    {
        if (!TryGetScope(out scope, out error)) return false;
        var workOrder = _uow.WorkOrders.Query().FirstOrDefault(x => x.Id == id);
        if (workOrder is not null && scope.CanAccessWorkOrder(workOrder)) return true;

        error = NotFound(ApiResponse<object>.Fail("not_found", $"Work order {id} was not found."));
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

    private static bool IsClosed(WorkOrderStatus status)
        => status is WorkOrderStatus.Resolved
            or WorkOrderStatus.Completed
            or WorkOrderStatus.Closed
            or WorkOrderStatus.Cancelled;

    private static string ShortTitle(string description)
    {
        var firstLine = description
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .FirstOrDefault() ?? description;
        return firstLine.Length <= 100 ? firstLine : firstLine[..100];
    }

    private static object[] EnumOptions<TEnum>() where TEnum : struct, Enum
        => Enum.GetValues<TEnum>()
            .Select(value => new { value, label = SplitEnumName(value.ToString()) })
            .Cast<object>()
            .ToArray();

    private static bool TryParseOptional<TEnum>(string? value, out TEnum? parsed)
        where TEnum : struct, Enum
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
            return true;

        var normalized = value.Replace("_", string.Empty).Replace("-", string.Empty);
        foreach (var enumValue in Enum.GetValues<TEnum>())
        {
            if (!string.Equals(enumValue.ToString(), normalized, StringComparison.OrdinalIgnoreCase)) continue;
            parsed = enumValue;
            return true;
        }

        return false;
    }

    private static string SplitEnumName(string value)
        => string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));
}
