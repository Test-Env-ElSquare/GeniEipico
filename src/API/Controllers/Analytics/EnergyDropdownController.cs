using API.Common;
using Application.Abstractions.Repositories;
using Application.Dtos.EnergyDashboard;
using Domain.Entities.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers.Analytics;

[ApiController]
[Authorize]
[Route("api/Dropdown")]
public sealed class EnergyDropdownController(IUnitOfWork uow) : ControllerBase
{
    [HttpGet("transformers")]
    public async Task<IActionResult> GetAllTransformers(
        [FromQuery] int? factoryId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveFactoryScope(factoryId, out var factoryIds, out var error))
            return error;

        var result = await uow.Transformers.Query()
            .Where(x =>
                factoryIds.Contains(x.FactoryId) &&
                !x.IsDeleted &&
                x.EmsEnergySourceId.HasValue)
            .OrderBy(x => x.TransformerName)
            .Select(x => new EnergyDropdownItemDto(
                x.EmsEnergySourceId!.Value,
                x.TransformerName))
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    [HttpGet("zones")]
    public async Task<IActionResult> GetAllZones(
        [FromQuery] int? factoryId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveFactoryScope(factoryId, out var factoryIds, out var error))
            return error;

        var result = await uow.Repository<EnergyZone>().Query()
            .Where(x => factoryIds.Contains(x.FactoryId) && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new EnergyDropdownItemDto(x.EmsSourceId, x.Name))
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    [HttpGet("transformers/{transformerId:int}/zones")]
    public async Task<IActionResult> GetZonesByTransformer(
        int transformerId,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveAccess(out var access, out var error))
            return error;
        if (transformerId <= 0)
            return BadRequest(new { error = "Invalid transformer id." });

        var transformerQuery = uow.Transformers.Query()
            .Where(x => access.FactoryIds.Contains(x.FactoryId) && !x.IsDeleted);
        var transformer = transformerQuery
            .FirstOrDefault(x => x.EmsEnergySourceId == transformerId) ??
            transformerQuery.FirstOrDefault(x => x.Id == transformerId);
        if (transformer is null)
            return NotFound(new { error = "Transformer was not found or is outside the authenticated user's access scope." });

        var result = await uow.Repository<EnergyZone>().Query()
            .Where(x =>
                x.FactoryId == transformer.FactoryId &&
                x.TransformerId == transformer.Id &&
                !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new EnergyDropdownItemDto(x.EmsSourceId, x.Name))
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    private bool TryResolveFactoryScope(
        int? externalFactoryId,
        out IReadOnlySet<int> factoryIds,
        out IActionResult error)
    {
        factoryIds = new HashSet<int>();
        error = default!;

        if (!TryResolveAccess(out var access, out error))
            return false;
        if (externalFactoryId is < 0)
        {
            error = BadRequest(new { error = "Invalid factory id." });
            return false;
        }

        if (externalFactoryId is null or 0)
        {
            factoryIds = access.FactoryIds;
            return true;
        }

        var factoryQuery = uow.Factories.Query()
            .Where(x => x.IsActive && !x.IsDeleted);
        var factory = factoryQuery
            .FirstOrDefault(x => x.EmsEnergySourceId == externalFactoryId.Value) ??
            factoryQuery.FirstOrDefault(x => x.Id == externalFactoryId.Value);
        if (factory is null)
        {
            error = BadRequest(new { error = "Invalid factory id." });
            return false;
        }
        if (!access.CanAccessFactory(factory.Id))
        {
            error = Forbid();
            return false;
        }

        factoryIds = new HashSet<int> { factory.Id };
        return true;
    }

    private bool TryResolveAccess(
        out AuthenticatedAccessScope access,
        out IActionResult error)
    {
        if (AuthenticatedAccessScope.TryResolve(
                User, uow, out access, out var code, out var message))
        {
            error = default!;
            return true;
        }

        error = Unauthorized(new { error = message, code });
        return false;
    }
}
