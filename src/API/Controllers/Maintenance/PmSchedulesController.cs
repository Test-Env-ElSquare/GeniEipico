using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Domain.Entities.Maintenance;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Maintenance;

[Route("v1/pm-schedules")]
public class PmSchedulesController : CrudController<PmSchedule, IPmScheduleService>
{
    private readonly IFrontendDataService _frontend;
    private readonly IMaintenanceWorkflowService _workflow;
    private readonly ITimeUtilities _timeUtilities;

    public PmSchedulesController(IPmScheduleService service, IFrontendDataService frontend, IMaintenanceWorkflowService workflow, ITimeUtilities timeUtilities) : base(service)
    {
        _frontend = frontend;
        _workflow = workflow;
        _timeUtilities = timeUtilities;
    }

    [HttpGet]
    public override async Task<IActionResult> GetAll()
    {
        var filter = await ResolveFilterAsync(new FrontendFilterQuery());
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetMaintenancePmSchedulesAsync(filter), filter));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] PmCompleteRequest request)
    {
        var result = await _workflow.CompletePmScheduleAsync(id, request.CompletedAt, request.Notes);
        return this.ToActionResult(result);
    }

    private async Task<FrontendFilter> ResolveFilterAsync(FrontendFilterQuery query)
        => FrontendFilter.FromResolved(await _timeUtilities.ResolveAsync(query.ToInput()));
}
