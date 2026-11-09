using API.Common;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Domain.Entities.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Analytics;

[ApiController]
[Authorize]
[Route("api/energy-dashboard")]
public sealed class EnergyDashboardController(
    IEnergyDashboardService dashboard,
    IUnitOfWork uow) : ControllerBase
{
    [HttpGet("GetSummary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int? factoryId,
        [FromQuery] int duration = 0,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildFilter(factoryId, duration, null, null, from, to,
                factoryRequired: false, allowZeroHierarchy: false,
                out var filter, out var error))
            return error;

        return await ExecuteAsync(
            () => dashboard.GetSummaryAsync(filter, cancellationToken));
    }

    [HttpGet("GetOnlineStatus")]
    public async Task<IActionResult> GetOnlineStatus(
        [FromQuery] int? factoryId,
        [FromQuery] int duration = 0,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildFilter(factoryId, duration, null, null, from, to,
                factoryRequired: false, allowZeroHierarchy: false,
                out var filter, out var error))
            return error;

        return await ExecuteAsync(
            () => dashboard.GetOnlineStatusAsync(filter, cancellationToken));
    }

    [HttpGet("GetEnergyHeatmap")]
    public async Task<IActionResult> GetEnergyHeatmap(
        [FromQuery] int factoryId,
        [FromQuery] int? transformerId = null,
        [FromQuery] int? zoneId = null,
        [FromQuery] int? lineId = null,
        [FromQuery] int duration = 0,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        _ = lineId; // Retained for compatibility with the existing EMS frontend query string.
        if (!TryBuildFilter(factoryId, duration, transformerId, zoneId, from, to,
                factoryRequired: true, allowZeroHierarchy: true,
                out var filter, out var error))
            return error;

        return await ExecuteAsync(
            () => dashboard.GetEnergyHeatmapAsync(filter, cancellationToken));
    }

    [HttpGet("GetHourlyEnergy")]
    public async Task<IActionResult> GetHourlyEnergy(
        [FromQuery] int factoryId,
        [FromQuery] int? transformerId = null,
        [FromQuery] int? zoneId = null,
        [FromQuery] int duration = 0,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildFilter(factoryId, duration, transformerId, zoneId, from, to,
                factoryRequired: true, allowZeroHierarchy: false,
                out var filter, out var error))
            return error;

        return await ExecuteAsync(
            () => dashboard.GetHourlyEnergyAsync(filter, cancellationToken));
    }

    [HttpGet("GetTotalEnergy")]
    public async Task<IActionResult> GetTotalEnergy(
        [FromQuery] int factoryId,
        [FromQuery] int? transformerId = null,
        [FromQuery] int? zoneId = null,
        [FromQuery] int duration = 0,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildFilter(factoryId, duration, transformerId, zoneId, from, to,
                factoryRequired: true, allowZeroHierarchy: false,
                out var filter, out var error))
            return error;

        return await ExecuteAsync(
            () => dashboard.GetTotalEnergyAsync(filter, cancellationToken));
    }

    [HttpGet("GetHarmonicsGraph")]
    public async Task<IActionResult> GetHarmonicsGraph(
        [FromQuery] int factoryId,
        [FromQuery] int duration = 0,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildFilter(factoryId, duration, null, null, from, to,
                factoryRequired: false, allowZeroHierarchy: false,
                out var filter, out var error))
            return error;

        return await ExecuteAsync(
            () => dashboard.GetHarmonicsGraphAsync(filter, cancellationToken));
    }

    [HttpGet("GetVoltageGraph")]
    public async Task<IActionResult> GetVoltageGraph(
        [FromQuery] int factoryId,
        [FromQuery] int duration = 0,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildFilter(factoryId, duration, null, null, from, to,
                factoryRequired: false, allowZeroHierarchy: false,
                out var filter, out var error))
            return error;

        return await ExecuteAsync(
            () => dashboard.GetVoltageGraphAsync(filter, cancellationToken));
    }

    [HttpGet("GetCurrentGraph")]
    public async Task<IActionResult> GetCurrentGraph(
        [FromQuery] int factoryId,
        [FromQuery] int duration = 0,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildFilter(factoryId, duration, null, null, from, to,
                factoryRequired: false, allowZeroHierarchy: false,
                out var filter, out var error))
            return error;

        return await ExecuteAsync(
            () => dashboard.GetCurrentGraphAsync(filter, cancellationToken));
    }

    [HttpGet("GetTopEnergyConsumers")]
    public async Task<IActionResult> GetTopEnergyConsumers(
        [FromQuery] int factoryId,
        [FromQuery] int? transformerId = null,
        [FromQuery] int? zoneId = null,
        [FromQuery] int duration = 0,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int top = 5,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildFilter(factoryId, duration, transformerId, zoneId, from, to,
                factoryRequired: true, allowZeroHierarchy: false,
                out var filter, out var error))
            return error;

        return await ExecuteAsync(
            () => dashboard.GetTopEnergyConsumersAsync(filter, top, cancellationToken));
    }

    private bool TryBuildFilter(
        int? externalFactoryId,
        int duration,
        int? externalTransformerId,
        int? externalZoneId,
        DateTime? from,
        DateTime? to,
        bool factoryRequired,
        bool allowZeroHierarchy,
        out EnergyDashboardFilter filter,
        out IActionResult error)
    {
        filter = default!;
        error = default!;

        if (!AuthenticatedAccessScope.TryResolve(
                User, uow, out var access, out var code, out var message))
        {
            error = Unauthorized(new { error = message, code });
            return false;
        }

        if (duration is < 0 or > 5)
        {
            error = BadRequest(new { error = "Invalid duration type." });
            return false;
        }

        var selectedFactory = externalFactoryId is > 0
            ? ResolveFactory(externalFactoryId.Value)
            : null;
        if (externalFactoryId is > 0 && selectedFactory is null)
        {
            error = BadRequest(new { error = "Invalid factory id." });
            return false;
        }
        if (factoryRequired && selectedFactory is null)
        {
            error = BadRequest(new { error = "FactoryId is required." });
            return false;
        }
        if (selectedFactory is not null && !access.CanAccessFactory(selectedFactory.Id))
        {
            error = Forbid();
            return false;
        }

        var factoryIds = selectedFactory is null
            ? access.FactoryIds.ToArray()
            : new[] { selectedFactory.Id };
        var responseFactoryId = selectedFactory?.EmsEnergySourceId ??
                                selectedFactory?.Id ?? 0;

        int? targetTransformerId = null;
        if (externalTransformerId.HasValue)
        {
            if (externalTransformerId.Value == 0 && allowZeroHierarchy)
            {
                targetTransformerId = 0;
            }
            else if (externalTransformerId.Value <= 0 || selectedFactory is null)
            {
                error = BadRequest(new { error = "Invalid transformer id." });
                return false;
            }
            else
            {
                var transformer = ResolveTransformer(
                    selectedFactory.Id,
                    externalTransformerId.Value);
                if (transformer is null)
                {
                    error = BadRequest(new { error = "Transformer does not belong to this factory." });
                    return false;
                }
                targetTransformerId = transformer.Id;
            }
        }

        int? targetZoneId = null;
        if (externalZoneId.HasValue)
        {
            if (externalZoneId.Value == 0 && allowZeroHierarchy)
            {
                targetZoneId = 0;
            }
            else if (externalZoneId.Value <= 0 || selectedFactory is null)
            {
                error = BadRequest(new { error = "Invalid zone id." });
                return false;
            }
            else
            {
                var zone = ResolveZone(
                    selectedFactory.Id,
                    targetTransformerId,
                    externalZoneId.Value);
                if (zone is null)
                {
                    error = BadRequest(new { error = "Zone does not belong to this transformer or factory." });
                    return false;
                }
                targetZoneId = zone.Id;
            }
        }

        filter = new EnergyDashboardFilter(
            new EnergyDashboardScope(
                factoryIds,
                selectedFactory?.Id,
                responseFactoryId),
            duration,
            targetTransformerId,
            targetZoneId,
            from,
            to);
        return true;
    }

    private Domain.Entities.Organization.Factory? ResolveFactory(int externalId)
    {
        var query = uow.Factories.Query()
            .Where(x => x.IsActive && !x.IsDeleted);
        return query.FirstOrDefault(x => x.EmsEnergySourceId == externalId) ??
               query.FirstOrDefault(x => x.Id == externalId);
    }

    private Domain.Entities.MasterData.Transformer? ResolveTransformer(
        int factoryId,
        int externalId)
    {
        var query = uow.Transformers.Query()
            .Where(x => x.FactoryId == factoryId && !x.IsDeleted);
        return query.FirstOrDefault(x => x.EmsEnergySourceId == externalId) ??
               query.FirstOrDefault(x => x.Id == externalId);
    }

    private EnergyZone? ResolveZone(
        int factoryId,
        int? transformerId,
        int externalId)
    {
        var query = uow.Repository<EnergyZone>().Query()
            .Where(x => x.FactoryId == factoryId && !x.IsDeleted);
        if (transformerId is > 0)
            query = query.Where(x => x.TransformerId == transformerId.Value);
        return query.FirstOrDefault(x => x.EmsSourceId == externalId) ??
               query.FirstOrDefault(x => x.Id == externalId);
    }

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
