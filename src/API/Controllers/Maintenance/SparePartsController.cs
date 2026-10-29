using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Domain.Entities.Maintenance;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Maintenance;

[Route("v1/spare-parts")]
public class SparePartsController : CrudController<SparePart, ISparePartService>
{
    private readonly IFrontendDataService _frontend;
    private readonly IMaintenanceWorkflowService _workflow;
    private readonly ITimeUtilities _timeUtilities;

    public SparePartsController(ISparePartService service, IFrontendDataService frontend, IMaintenanceWorkflowService workflow, ITimeUtilities timeUtilities) : base(service)
    {
        _frontend = frontend;
        _workflow = workflow;
        _timeUtilities = timeUtilities;
    }

    [HttpGet]
    public override async Task<IActionResult> GetAll()
    {
        var filter = await ResolveFilterAsync(new FrontendFilterQuery());
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetMaintenanceSparePartsAsync(filter), filter));
    }

    [HttpPost("{id:int}/issue")]
    public async Task<IActionResult> Issue(int id, [FromBody] SparePartIssueRequest request)
    {
        var result = await _workflow.IssueSparePartAsync(id, request.WorkOrderId, request.QtyUsed);
        return this.ToActionResult(result);
    }

    private async Task<FrontendFilter> ResolveFilterAsync(FrontendFilterQuery query)
        => FrontendFilter.FromResolved(await _timeUtilities.ResolveAsync(query.ToInput()));
}
