using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Domain.Entities.Maintenance;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Maintenance;

[Route("v1/breakdowns")]
public class BreakdownsController : CrudController<BreakdownLog, IBreakdownLogService>
{
    private readonly IFrontendDataService _frontend;
    private readonly ITimeUtilities _timeUtilities;

    public BreakdownsController(IBreakdownLogService service, IFrontendDataService frontend, ITimeUtilities timeUtilities) : base(service)
    {
        _frontend = frontend;
        _timeUtilities = timeUtilities;
    }

    [HttpGet]
    public override async Task<IActionResult> GetAll()
    {
        var filter = await ResolveFilterAsync(new FrontendFilterQuery());
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetMaintenanceBreakdownHistoryAsync(filter), filter));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats([FromQuery] FrontendFilterQuery query)
    {
        var filter = await ResolveFilterAsync(query);
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetMaintenanceBreakdownHistoryAsync(filter), filter));
    }

    private async Task<FrontendFilter> ResolveFilterAsync(FrontendFilterQuery query)
        => FrontendFilter.FromResolved(await _timeUtilities.ResolveAsync(query.ToInput()));
}
