using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Analytics;

[Route("v1/insights")]
public class InsightsController : CrudController<AiInsight, IAiInsightService>
{
    public InsightsController(IAiInsightService service) : base(service)
    {
    }


    // Endpoint Type B: Workflow / Business Logic. Dismiss insight placeholder.
    [HttpPost("{id:int}/dismiss")]
    public IActionResult Dismiss(int id, [FromBody] API.Contracts.DismissInsightRequest request)
    {
        return StaticOk($"/v1/insights/{id}/dismiss", new { id, request });
    }

    // Endpoint Type C: Hybrid / AI/Telemetry Dependent. Insight generation placeholder.
    [HttpPost("generate")]
    public IActionResult Generate([FromBody] API.Contracts.GenerateInsightRequest request)
    {
        return StaticOk("/v1/insights/generate", request);
    }

}
