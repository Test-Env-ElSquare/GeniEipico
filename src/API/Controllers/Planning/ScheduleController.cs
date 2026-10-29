using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Domain.Entities.Planning;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Planning;

[Route("v1/schedule")]
public class ScheduleController : CrudController<ProductionSchedule, IProductionScheduleService>
{
    private readonly IFrontendDataService _frontend;
    private readonly ITimeUtilities _timeUtilities;

    public ScheduleController(IProductionScheduleService service, IFrontendDataService frontend, ITimeUtilities timeUtilities) : base(service)
    {
        _frontend = frontend;
        _timeUtilities = timeUtilities;
    }

    [HttpGet]
    public override async Task<IActionResult> GetAll()
    {
        var filter = await ResolveFilterAsync(new FrontendFilterQuery());
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetPlanningGanttAsync(filter), filter));
    }

    [HttpPost("stoppage")]
    public IActionResult AddStoppage([FromBody] ScheduleStoppageRequest request)
    {
        return StaticOk("/v1/schedule/stoppage", request, "Type B: future ScheduleWorkflowService should create a stoppage row and validate overlaps.");
    }

    [HttpGet("stoppages")]
    public async Task<IActionResult> Stoppages([FromQuery] FrontendFilterQuery query)
    {
        var filter = await ResolveFilterAsync(query);
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetPlanningStoppagesAsync(filter), filter));
    }

    private async Task<FrontendFilter> ResolveFilterAsync(FrontendFilterQuery query)
        => FrontendFilter.FromResolved(await _timeUtilities.ResolveAsync(query.ToInput()));
}
