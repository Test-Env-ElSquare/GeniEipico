using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Domain.Entities.Scada;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Scada;

[Authorize]
[Route("v1/scales")]
public class ScalesController : CrudController<Scale, IScaleService>
{
    private readonly IScadaManualWorkflowService _workflow;

    public ScalesController(IScaleService service, IScadaManualWorkflowService workflow) : base(service)
    {
        _workflow = workflow;
    }

    [HttpPost("{id:int}/tare")]
    public async Task<IActionResult> Tare(int id)
    {
        var result = await _workflow.TareScaleAsync(id);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:int}/assign-batch")]
    public async Task<IActionResult> AssignBatch(int id, [FromBody] ScaleAssignBatchRequest request)
    {
        var result = await _workflow.AssignScaleToBatchAsync(id, request.BatchId, request.MaterialId);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:int}/release")]
    public async Task<IActionResult> Release(int id)
    {
        var result = await _workflow.ReleaseScaleAsync(id);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}/readings")]
    public IActionResult Readings(int id)
    {
        return StaticOk($"/v1/scales/{id}/readings", new { id }, "Endpoint Type C: real-time scale readings are future telemetry work. Manual dispense endpoint is implemented now.");
    }

}
