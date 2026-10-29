using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Domain.Entities.Scada;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Scada;

[Authorize]
[Route("v1/silos")]
public class SilosController : CrudController<Silo, ISiloService>
{
    private readonly IScadaManualWorkflowService _workflow;

    public SilosController(ISiloService service, IScadaManualWorkflowService workflow) : base(service)
    {
        _workflow = workflow;
    }

    [HttpPost("{id:int}/load")]
    public async Task<IActionResult> Load(int id, [FromBody] SiloLoadRequest request)
    {
        var result = await _workflow.LoadSiloAsync(id, request.MaterialId, request.LotUid, request.QuantityKg, request.Timestamp);
        return this.ToActionResult(result);
    }
}
