using API.Common;
using API.Contracts;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Common.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Scada;

[Authorize]
[Route("v1/scada")]
public class ScadaController : StaticControllerBase
{
    private readonly IScadaLiveViewService _scada;
    private readonly IUnitOfWork _uow;
    private readonly ITimeUtilities _timeUtilities;

    public ScadaController(
        IScadaLiveViewService scada,
        IUnitOfWork uow,
        ITimeUtilities timeUtilities)
    {
        _scada = scada;
        _uow = uow;
        _timeUtilities = timeUtilities;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] int factoryId, [FromQuery] int? lineId = null)
    {
        if (!TryResolveScope(factoryId, lineId, out var scope, out var error)) return error;
        var result = await _scada.GetDashboardAsync(scope);
        return ScadaOk(result.Data);
    }

    [HttpGet("status-counts")]
    public async Task<IActionResult> StatusCounts([FromQuery] int factoryId, [FromQuery] int? lineId = null)
    {
        if (!TryResolveScope(factoryId, lineId, out var scope, out var error)) return error;
        var result = await _scada.GetStatusCountsAsync(scope);
        return ScadaOk(result.Data);
    }

    [HttpGet("warehouse-zone")]
    public async Task<IActionResult> WarehouseZone([FromQuery] int factoryId, [FromQuery] int? lineId = null)
    {
        if (!TryResolveScope(factoryId, lineId, out var scope, out var error)) return error;
        var result = await _scada.GetWarehouseZoneAsync(scope);
        return ScadaOk(result.Data);
    }

    [HttpGet("dispensing-zone")]
    public async Task<IActionResult> DispensingZone([FromQuery] int factoryId, [FromQuery] int? lineId = null)
    {
        if (!TryResolveScope(factoryId, lineId, out var scope, out var error)) return error;
        var result = await _scada.GetDispensingZoneAsync(scope);
        return ScadaOk(result.Data);
    }

    [HttpGet("production-zone")]
    public async Task<IActionResult> ProductionZone([FromQuery] int factoryId, [FromQuery] int? lineId = null)
    {
        if (!TryResolveScope(factoryId, lineId, out var scope, out var error)) return error;
        var result = await _scada.GetProductionZoneAsync(scope);
        return ScadaOk(result.Data);
    }

    [HttpGet("machines/{machineId:int}/detail")]
    public async Task<IActionResult> MachineDetail(
        int machineId,
        [FromQuery] ScadaMachineDetailQuery query)
    {
        if (!TryResolveMachineScope(machineId, out var scope, out var error))
            return error;

        var duration = query.Duration ?? (int)DurationFilter.CurrentShift;
        if (duration < (int)DurationFilter.CurrentShift ||
            duration > (int)DurationFilter.Custom)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "invalid_duration",
                "Duration must be between 0 and 5."));
        }

        if (duration == (int)DurationFilter.Custom &&
            (!query.From.HasValue ||
             !query.To.HasValue ||
             query.From.Value >= query.To.Value))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "invalid_custom_range",
                "Duration 5 requires valid From and To values where From is earlier than To."));
        }

        var period = await _timeUtilities.ResolveAsync(new TimeFilterInput(
            duration,
            scope.FactoryId,
            scope.LineId,
            duration == (int)DurationFilter.Custom ? query.From : null,
            duration == (int)DurationFilter.Custom ? query.To : null));
        var detail = await _scada.GetMachineDetailAsync(scope, machineId, period);

        return detail is null
            ? NotFound(ApiResponse<object>.Fail(
                "not_found",
                $"Machine {machineId} was not found."))
            : ScadaOk(detail);
    }

    [HttpGet("finished-goods-zone")]
    public async Task<IActionResult> FinishedGoodsZone([FromQuery] int factoryId, [FromQuery] int? lineId = null)
    {
        if (!TryResolveScope(factoryId, lineId, out var scope, out var error)) return error;
        var result = await _scada.GetFinishedGoodsZoneAsync(scope);
        return ScadaOk(result.Data);
    }

    [HttpGet("energy")]
    public async Task<IActionResult> Energy([FromQuery] int factoryId, [FromQuery] int? lineId = null)
    {
        if (!TryResolveScope(factoryId, lineId, out var scope, out var error)) return error;
        var result = await _scada.GetEnergyAsync(scope);
        return ScadaOk(result.Data);
    }

    private IActionResult ScadaOk(object? data)
        => Ok(new
        {
            success = true,
            data,
            error = (object?)null,
            ts = DateTime.UtcNow
        });

    private bool TryResolveScope(
        int factoryId,
        int? lineId,
        out ScadaLiveViewScope scope,
        out IActionResult error)
    {
        scope = default!;
        error = default!;

        if (!AuthenticatedAccessScope.TryResolve(
                User,
                _uow,
                out var access,
                out var errorCode,
                out var errorMessage))
        {
            error = Unauthorized(ApiResponse<object>.Fail(errorCode, errorMessage));
            return false;
        }

        if (factoryId <= 0)
        {
            error = BadRequest(ApiResponse<object>.Fail(
                "factory_required",
                "FactoryId must be greater than zero."));
            return false;
        }

        if (!access.CanAccessFactory(factoryId))
        {
            error = StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail(
                    "factory_forbidden",
                    $"Factory {factoryId} is outside the authenticated user's access scope."));
            return false;
        }

        if (lineId.HasValue && lineId.Value <= 0)
        {
            error = BadRequest(ApiResponse<object>.Fail(
                "invalid_line",
                "LineId must be greater than zero when supplied."));
            return false;
        }

        if (lineId.HasValue &&
            (!access.LineFactoryIds.TryGetValue(lineId.Value, out var lineFactoryId) ||
             lineFactoryId != factoryId))
        {
            error = StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail(
                    "line_forbidden",
                    $"Line {lineId.Value} is outside Factory {factoryId} or the authenticated user's access scope."));
            return false;
        }

        var allowedLineIds = lineId.HasValue
            ? new HashSet<int> { lineId.Value }
            : access.LineFactoryIds
                .Where(x => x.Value == factoryId)
                .Select(x => x.Key)
                .ToHashSet();

        scope = new ScadaLiveViewScope(
            access.CustomerId,
            factoryId,
            lineId,
            allowedLineIds);
        return true;
    }

    private bool TryResolveMachineScope(
        int machineId,
        out ScadaLiveViewScope scope,
        out IActionResult error)
    {
        scope = default!;
        error = default!;

        if (!AuthenticatedAccessScope.TryResolve(
                User,
                _uow,
                out var access,
                out var errorCode,
                out var errorMessage))
        {
            error = Unauthorized(ApiResponse<object>.Fail(errorCode, errorMessage));
            return false;
        }

        var machine = _uow.Machines.Query()
            .Where(x => x.Id == machineId &&
                        x.IsActive &&
                        !x.IsDeleted)
            .Select(x => new { x.Id, x.LineId })
            .FirstOrDefault();
        if (machine is null)
        {
            error = NotFound(ApiResponse<object>.Fail(
                "not_found",
                $"Machine {machineId} was not found."));
            return false;
        }

        if (!access.LineFactoryIds.TryGetValue(
                machine.LineId,
                out var factoryId))
        {
            error = StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail(
                    "machine_forbidden",
                    $"Machine {machineId} is outside the authenticated user's access scope."));
            return false;
        }

        scope = new ScadaLiveViewScope(
            access.CustomerId,
            factoryId,
            machine.LineId,
            new HashSet<int> { machine.LineId });
        return true;
    }

}
