using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Analytics;

[Route("v1/analytics")]
public class AnalyticsController : StaticControllerBase
{
    private readonly IFrontendDataService _frontend;

    public AnalyticsController(IFrontendDataService frontend)
    {
        _frontend = frontend;
    }

    [HttpGet("insights")]
    public async Task<IActionResult> Insights()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetAnalyticsInsightsAsync()));

    [HttpGet("production")]
    public async Task<IActionResult> Production([FromQuery] int? lineId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetAnalyticsHeatmapAsync()));

    [HttpGet("energy")]
    public async Task<IActionResult> Energy([FromQuery] int? factoryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetOverviewEnergyByMachineAsync()));

    [HttpGet("quality")]
    public async Task<IActionResult> Quality([FromQuery] int? lineId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetAnalyticsOeeBreakdownAsync()));

    [HttpGet("failure-risk")]
    public async Task<IActionResult> FailureRisk()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetAnalyticsFailureRiskAsync()));

    [HttpPost("export")]
    public IActionResult Export([FromBody] ExportRequest request)
    {
        return StaticOk("/v1/analytics/export", request, "Endpoint Type D: export command placeholder. Later generate signed export file.");
    }
}
