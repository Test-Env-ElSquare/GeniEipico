using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Domain.Entities.Planning;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Planning;

[Route("v1/mrp/requirements")]
public class MrpController : CrudController<MrpRequirement, IMrpRequirementService>
{
    private readonly IFrontendDataService _frontend;
    private readonly ITimeUtilities _timeUtilities;

    public MrpController(IMrpRequirementService service, IFrontendDataService frontend, ITimeUtilities timeUtilities) : base(service)
    {
        _frontend = frontend;
        _timeUtilities = timeUtilities;
    }

    [HttpGet]
    public override async Task<IActionResult> GetAll()
    {
        var filter = await ResolveFilterAsync(new FrontendFilterQuery());
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetPlanningMrpAsync(filter), filter));
    }

    [HttpPost("/v1/mrp/recalculate")]
    public IActionResult Recalculate([FromBody] MrpRecalculateRequest request)
    {
        return StaticOk("/v1/mrp/recalculate", request, "Type B: future MrpWorkflowService should calculate material gaps and purchase signals.");
    }

    [HttpGet("/v1/mrp/summary")]
    public async Task<IActionResult> Summary([FromQuery] FrontendFilterQuery query)
    {
        var filter = await ResolveFilterAsync(query);
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetPlanningMrpAsync(filter), filter));
    }

    private async Task<FrontendFilter> ResolveFilterAsync(FrontendFilterQuery query)
        => FrontendFilter.FromResolved(await _timeUtilities.ResolveAsync(query.ToInput()));
}
