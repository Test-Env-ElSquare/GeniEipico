using API.Common;
using API.Realtime.Signage;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace API.Controllers.Scada;

[ApiController]
[Authorize]
public sealed class SignageCompatibilityController(IUnitOfWork uow, ISignageLayoutService signage) : ControllerBase
{
    [HttpGet("api/Factory/GetFactoryLines")]
    public IActionResult FactoryLines([FromQuery] int factoryId)
    {
        if (!TryScope(factoryId, out var scope, out var error)) return error;
        return Ok(signage.GetFactoryLines(scope));
    }

    [HttpGet("v1/signage/signals")]
    public async Task<IActionResult> Signals([FromQuery] int factoryId)
    {
        if (!TryScope(factoryId, out var scope, out var error)) return error;
        return Ok(await signage.GetFactorySignalsAsync(scope));
    }

    [HttpGet("v1/signage/scales")]
    public IActionResult Scales([FromQuery] int factoryId)
    {
        if (!TryScope(factoryId, out var scope, out var error)) return error;
        return Ok(signage.GetScales(scope));
    }

    [HttpGet("api/Machine/getAllMachines")]
    [HttpGet("api/Machine/GetLineMachines")]
    public IActionResult Machines([FromQuery] int? factoryId = null, [FromQuery] int lineid = 0)
    {
        if (!AuthenticatedAccessScope.TryResolve(User, uow, out var access, out var code, out var message))
            return Unauthorized(ApiResponse<object>.Fail(code, message));
        if (factoryId.HasValue && !access.CanAccessFactory(factoryId.Value)) return Forbid();
        if (lineid != 0 && (!access.LineFactoryIds.TryGetValue(lineid, out var owner) ||
            (factoryId.HasValue && owner != factoryId))) return Forbid();
        var factories = access.FactoryIds.Where(x => !factoryId.HasValue || x == factoryId).OrderBy(x => x);
        return Ok(factories.SelectMany(id => signage.GetMachines(SignageAccess.Resolve(User, uow, id)))
            .Where(x => lineid == 0 || x.LineId == lineid).ToArray());
    }

    [HttpGet("api/Dashboards/GetAllLinesActivatedSkus")]
    public IActionResult CurrentBatches([FromQuery] int factoryId)
    {
        if (!TryScope(factoryId, out var scope, out var error)) return error;
        return Ok(signage.GetCurrentBatches(scope));
    }
    private bool TryScope(int factoryId, out ScadaLiveViewScope scope, out IActionResult error)
    {
        scope = default!;
        error = default!;
        if (!AuthenticatedAccessScope.TryResolve(User, uow, out _, out var code, out var message))
        {
            error = Unauthorized(ApiResponse<object>.Fail(code, message));
            return false;
        }
        try { scope = SignageAccess.Resolve(User, uow, factoryId); return true; }
        catch (HubException) { error = Forbid(); return false; }
    }
}