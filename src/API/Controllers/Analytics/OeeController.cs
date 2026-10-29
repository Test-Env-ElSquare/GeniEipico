using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Domain.Entities.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Analytics;

[Route("v1/oee/snapshots")]
public class OeeController : CrudController<OeeSnapshot, IOeeSnapshotService>
{
    private readonly IFrontendDataService _frontend;
    private readonly ITimeUtilities _timeUtilities;

    public OeeController(IOeeSnapshotService service, IFrontendDataService frontend, ITimeUtilities timeUtilities) : base(service)
    {
        _frontend = frontend;
        _timeUtilities = timeUtilities;
    }

    [HttpGet("/v1/oee/trend")]
    public async Task<IActionResult> Trend([FromQuery] FrontendFilterQuery query)
    {
        var filter = await ResolveFilterAsync(query);
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetOverviewOeeTrendAsync(filter), filter));
    }

    [HttpGet("/v1/oee/factory-summary")]
    public async Task<IActionResult> FactorySummary([FromQuery] FrontendFilterQuery query)
    {
        var filter = await ResolveFilterAsync(query);
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetOverviewKpisAsync(filter), filter));
    }

    [HttpGet("/v1/oee/losses")]
    public async Task<IActionResult> Losses([FromQuery] FrontendFilterQuery query)
    {
        var filter = await ResolveFilterAsync(query);
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetAnalyticsOeeBreakdownAsync(filter), filter));
    }

    private async Task<FrontendFilter> ResolveFilterAsync(FrontendFilterQuery query)
        => FrontendFilter.FromResolved(await _timeUtilities.ResolveAsync(query.ToInput()));
}
