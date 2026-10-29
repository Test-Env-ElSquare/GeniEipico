using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Domain.Entities.IoT;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.IoT;

[Route("v1/devices")]
public class DevicesController : CrudController<Device, IDeviceService>
{
    private readonly ITelemetryTagService _telemetryTagService;

    public DevicesController(IDeviceService service, ITelemetryTagService telemetryTagService) : base(service)
    {
        _telemetryTagService = telemetryTagService;
    }

    [HttpPost("{id:int}/rotate-key")]
    public IActionResult RotateKey(int id)
    {
        return StaticOk($"/v1/devices/{id}/rotate-key", new { id });
    }

    [HttpGet("{id:int}/telemetry")]
    public IActionResult GetTelemetry(int id)
    {
        var device = Service.Query().FirstOrDefault(x => x.Id == id && x.IsActive && !x.IsDeleted);
        if (device is null)
            return NotFound(ApiResponse<object>.Fail("not_found", $"Device {id} was not found."));

        var sourceIds = new[] { device.SerialNumber, device.MqttClientId }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToArray();
        var items = _telemetryTagService.Query()
            .Where(x => sourceIds.Contains(x.DeviceId))
            .OrderByDescending(x => x.RecordedAt)
            .Take(1000)
            .ToList();
        return Ok(ApiResponse<object>.Ok(new
        {
            deviceId = id,
            sourceIds,
            items
        }));
    }

    [HttpGet("{id:int}/health")]
    public IActionResult GetHealth(int id)
    {
        return StaticOk($"/v1/devices/{id}/health", new { id });
    }

    [HttpPost("{id:int}/ota")]
    public IActionResult DispatchOta(int id, [FromBody] OtaCommandRequest request)
    {
        return StaticOk($"/v1/devices/{id}/ota", new { id, request });
    }
}
