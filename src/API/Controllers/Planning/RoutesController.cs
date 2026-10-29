using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Domain.Entities.Planning;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Planning;

[Route("v1/routes")]
public class RoutesController : CrudController<ProductionRoute, IProductionRouteService>
{
    private readonly IFrontendDataService _frontend;
    private readonly IRouteStepService _stepService;
    private readonly IRouteWorkflowService _workflow;
    private readonly ITimeUtilities _timeUtilities;

    public RoutesController(IProductionRouteService service, IFrontendDataService frontend, IRouteStepService stepService, IRouteWorkflowService workflow, ITimeUtilities timeUtilities) : base(service)
    {
        _frontend = frontend;
        _stepService = stepService;
        _workflow = workflow;
        _timeUtilities = timeUtilities;
    }

    [HttpGet]
    public override async Task<IActionResult> GetAll()
    {
        var filter = await ResolveFilterAsync(new FrontendFilterQuery());
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetPlanningRoutesAsync(filter), filter));
    }

    [HttpGet("{id:int}/steps")]
    public IActionResult GetSteps(int id)
    {
        var steps = _stepService.Query()
            .Where(x => x.RouteId == id)
            .OrderBy(x => x.StepNo)
            .Select(x => new { x.Id, x.RouteId, x.StepNo, x.StepName, x.MachineType, x.EstDurationMin, type = "Type D route-step DTO. UC-R02 validation is handled by PUT /v1/routes/{id}/steps." })
            .ToList<object>();
        return Ok(ApiResponse<IReadOnlyList<object>>.Ok(steps));
    }

    [HttpPut("{id:int}/steps")]
    public async Task<IActionResult> ReplaceSteps(int id, [FromBody] IReadOnlyList<RouteStep> steps)
    {
        var result = await _workflow.ReplaceStepsAsync(id, steps);
        return this.ToActionResult(result);
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] RouteStatusRequest request)
    {
        var result = await _workflow.UpdateStatusAsync(id, request.Status);
        return this.ToActionResult(result);
    }

    private async Task<FrontendFilter> ResolveFilterAsync(FrontendFilterQuery query)
        => FrontendFilter.FromResolved(await _timeUtilities.ResolveAsync(query.ToInput()));
}
