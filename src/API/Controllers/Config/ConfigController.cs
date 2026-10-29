using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Domain.Entities.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Config;

[Route("v1/config")]
public class ConfigController : StaticControllerBase
{
    private readonly IFrontendDataService _frontend;
    private readonly IConfigWorkflowService _workflow;

    public ConfigController(IFrontendDataService frontend, IConfigWorkflowService workflow)
    {
        _frontend = frontend;
        _workflow = workflow;
    }

    [HttpGet("machine/{machineId:int}")]
    public async Task<IActionResult> GetMachineConfig(int machineId)
    {
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetConfigurationMachineParametersAsync()));
    }

    [HttpPut("machine/{machineId:int}")]
    public async Task<IActionResult> UpdateMachineConfig(int machineId, [FromBody] MachineConfig request)
    {
        var result = await _workflow.UpsertMachineConfigAsync(machineId, request);
        return this.ToActionResult(result);
    }

    [HttpGet("rating-thresholds")]
    public async Task<IActionResult> GetRatingThresholds()
    {
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetConfigurationRatingThresholdsAsync()));
    }

    [HttpPut("rating-thresholds")]
    public async Task<IActionResult> UpdateRatingThresholds([FromBody] MetricThresholdsUpdateRequest request)
    {
        var items = request.Items.Select(x => (x.Metric, x.Grade, x.MinValue, x.MaxValue)).ToList();
        var result = await _workflow.UpsertRatingThresholdsAsync(items);
        return this.ToActionResult(result);
    }

    [HttpGet("platform-settings")]
    public async Task<IActionResult> GetPlatformSettings()
    {
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetConfigurationGeneralSettingsAsync()));
    }

    [HttpPut("platform-settings")]
    public async Task<IActionResult> UpdatePlatformSettings([FromBody] PlatformSettingsUpdateRequest request)
    {
        var items = request.Items.Select(x => (x.Key, x.Value)).ToList();
        var result = await _workflow.UpsertPlatformSettingsAsync(items);
        return this.ToActionResult(result);
    }

    [HttpGet("color-codes")]
    public async Task<IActionResult> ColorCodes()
    {
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetConfigurationColorCodesAsync()));
    }
}
