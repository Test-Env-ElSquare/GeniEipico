using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Domain.Entities.IoT;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json;

namespace API.Controllers.IoT;

[Route("v1/ingest")]
public class IngestController : StaticControllerBase
{
    private const string BatterjeeSignalPath = "/Batterjee/signal";

    private readonly ITelemetryRawService _telemetryRawService;
    private readonly IDeviceService _deviceService;
    private readonly ITelemetryTagService _telemetryTagService;
    private readonly IAlarmEvaluationService _alarmEvaluationService;

    public IngestController(
        ITelemetryRawService telemetryRawService,
        IDeviceService deviceService,
        ITelemetryTagService telemetryTagService,
        IAlarmEvaluationService alarmEvaluationService)
    {
        _telemetryRawService = telemetryRawService;
        _deviceService = deviceService;
        _telemetryTagService = telemetryTagService;
        _alarmEvaluationService = alarmEvaluationService;
    }

    /// <summary>
    /// Compatibility endpoint for the existing Batterjee embedded devices.
    /// It deliberately uses the normal raw/tag ingestion path so production is
    /// projected by ScadaProductionProjectionService instead of being written
    /// by a second, integration-specific implementation.
    /// </summary>
    [HttpGet(BatterjeeSignalPath)]
    public async Task<IActionResult> SubmitBatterjeeSignal(
        [FromQuery(Name = "ID")] string? machineId,
        [FromQuery(Name = "State")] string? state,
        [FromQuery(Name = "Count")] string? count,
        [FromQuery(Name = "Qreject")] string? qreject,
        [FromQuery(Name = "Speed")] string? speed,
        [FromQuery(Name = "StateType")] string? stateType,
        [FromQuery(Name = "StateReason")] string? stateReason,
        [FromQuery(Name = "SKU")] string? sku,
        [FromQuery(Name = "Fault")] string? fault)
    {
        // Keep the original query string as the source payload. The parameters
        // remain explicit on the action to preserve the legacy Swagger/API contract.
        _ = (machineId, state, count, qreject, speed, stateType, stateReason, sku, fault);
        var rawPayload = Request.QueryString.Value ?? string.Empty;
        var validationError = Validate(new TelemetryIngestRequest(
            BatterjeeSignalPath,
            rawPayload));
        if (validationError is not null)
            return BadRequest(ApiResponse<object>.Fail("invalid_telemetry", validationError));

        await StoreAndExtractAsync(new TelemetryIngestRequest(
            BatterjeeSignalPath,
            rawPayload));

        // Preserve the response expected by the deployed Batterjee devices.
        return Ok("Batterjee Signal Saved");
    }

    // Legacy URL adapter only. Business projection is handled by the generic energy worker.
    [HttpGet("/Batterjee/energy")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> SubmitBatterjeeEnergy()
    {
        var request = new TelemetryIngestRequest("/Batterjee/energy", Request.QueryString.Value ?? string.Empty,
            DateTime.SpecifyKind(DateTime.UtcNow.AddHours(3), DateTimeKind.Unspecified));
        var error = Validate(request);
        if (error is not null)
            return BadRequest(ApiResponse<object>.Fail("invalid_telemetry", error));
        await StoreAndExtractAsync(request);
        return Ok("Energy Signal Saved");
    }

    [HttpPost("telemetry")]
    public async Task<IActionResult> SubmitTelemetry([FromBody] TelemetryIngestRequest request)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            return BadRequest(ApiResponse<object>.Fail("invalid_telemetry", validationError));

        var result = await StoreAndExtractAsync(request);
        return Created(
            $"/v1/ingest/telemetry/{result.RawId}",
            ApiResponse<object>.Ok(new
            {
                rawId = result.RawId,
                sourcePath = request.SourcePath.Trim(),
                receivedAt = result.RecordedAt,
                sourceId = result.SourceId,
                tagsCreated = result.TagsCreated,
                alarmParametersEvaluated = result.AlarmParametersEvaluated
            }, "Raw payload stored and split into telemetry tags."));
    }

    [HttpPost("telemetry/batch")]
    public async Task<IActionResult> SubmitTelemetryBatch([FromBody] TelemetryBatchIngestRequest request)
    {
        var created = new List<TelemetryStoreResult>();

        foreach (var item in request.Items)
        {
            var validationError = Validate(item);
            if (validationError is not null)
                return BadRequest(ApiResponse<object>.Fail("invalid_telemetry", validationError));

            created.Add(await StoreAndExtractAsync(item));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            items = created,
            count = created.Count
        }, "Raw telemetry batch stored and split into telemetry tags."));
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
    {
        var device = await _deviceService.GetByIdAsync(request.DeviceId);
        if (device is null)
            return NotFound(ApiResponse<Device>.Fail("not_found", $"Device {request.DeviceId} was not found."));

        device.LastSeenAt = request.SentAt ?? DateTime.UtcNow;
        device.ConnectionStatus = "online";
        var updated = await _deviceService.UpdateAsync(device.Id, device);
        return Ok(ApiResponse<Device?>.Ok(updated, "Endpoint Type C: heartbeat update implemented; broker/telemetry health is future work."));
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetDeviceConfig([FromQuery] int deviceId)
    {
        var device = await _deviceService.GetByIdAsync(deviceId);
        if (device is null)
            return NotFound(ApiResponse<object>.Fail("not_found", $"Device {deviceId} was not found."));

        var config = new
        {
            device.Id,
            device.SerialNumber,
            device.MqttClientId,
            device.MqttTopicPrefix,
            device.PollIntervalMs,
            device.FirmwareVersion,
            device.ConnectionStatus
        };
        return Ok(ApiResponse<object>.Ok(config, "Endpoint Type C: basic config now; full tag mapping/OTA config later."));
    }

    [HttpPost("ota-ack")]
    public async Task<IActionResult> OtaAck([FromBody] OtaAckRequest request)
    {
        var device = await _deviceService.GetByIdAsync(request.DeviceId);
        if (device is null)
            return NotFound(ApiResponse<Device>.Fail("not_found", $"Device {request.DeviceId} was not found."));

        device.FirmwareVersion = request.Version;
        device.LastSeenAt = request.CompletedAt ?? DateTime.UtcNow;
        device.ConnectionStatus = "online";
        var updated = await _deviceService.UpdateAsync(device.Id, device);
        return Ok(ApiResponse<Device?>.Ok(updated, "Endpoint Type C: OTA acknowledgement saved on device."));
    }

    private async Task<TelemetryStoreResult> StoreAndExtractAsync(TelemetryIngestRequest request)
    {
        var recordedAt = request.ReceivedAt ?? DateTime.UtcNow;
        var raw = await _telemetryRawService.CreateAsync(new TelemetryRaw
        {
            SourcePath = request.SourcePath.Trim(),
            RawPayload = request.RawPayload,
            ReceivedAt = recordedAt
        });

        var parsed = ParsePayload(request.RawPayload);
        if (string.IsNullOrWhiteSpace(parsed.SourceId))
            return new TelemetryStoreResult(raw.Id, recordedAt, null, 0, 0);

        var internalDeviceId = ResolveInternalDeviceId(parsed.SourceId);
        var tagsCreated = 0;
        var evaluated = 0;
        foreach (var parameter in parsed.Parameters)
        {
            await _telemetryTagService.CreateAsync(new TelemetryTag
            {
                DeviceId = parsed.SourceId,
                TagName = parameter.Name,
                ValueNum = parameter.NumericValue,
                ValueStr = parameter.StringValue,
                RecordedAt = recordedAt
            });
            tagsCreated++;

            if (internalDeviceId.HasValue && parameter.NumericValue.HasValue)
            {
                await _alarmEvaluationService.EvaluateAsync(
                    internalDeviceId.Value,
                    parameter.Name,
                    parameter.NumericValue.Value,
                    recordedAt);
                evaluated++;
            }
        }

        return new TelemetryStoreResult(
            raw.Id,
            recordedAt,
            parsed.SourceId,
            tagsCreated,
            evaluated);
    }

    private int? ResolveInternalDeviceId(string externalSourceId)
        => _deviceService.Query()
            .Where(x => x.IsActive && !x.IsDeleted)
            .Where(x => x.SerialNumber == externalSourceId ||
                        x.MqttClientId == externalSourceId)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();

    private static ParsedTelemetryPayload ParsePayload(string rawPayload)
    {
        var jsonResult = TryParseJson(rawPayload);
        return jsonResult ?? ParseQueryString(rawPayload);
    }

    private static ParsedTelemetryPayload? TryParseJson(string rawPayload)
    {
        try
        {
            using var payload = JsonDocument.Parse(rawPayload);
            if (payload.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            string? sourceId = null;
            var parameters = new List<ParsedTelemetryParameter>();
            foreach (var property in payload.RootElement.EnumerateObject())
            {
                var value = JsonValue(property.Value);
                if (IsSourceIdentifier(property.Name))
                {
                    sourceId ??= value;
                    continue;
                }

                if (value is null) continue;
                parameters.Add(Parameter(property.Name, value));
            }

            return new ParsedTelemetryPayload(sourceId, parameters);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ParsedTelemetryPayload ParseQueryString(string rawPayload)
    {
        var query = rawPayload.Trim();
        var questionMark = query.IndexOf('?');
        if (questionMark >= 0)
            query = query[(questionMark + 1)..];
        query = query.TrimStart('?', '&');

        string? sourceId = null;
        var parameters = new List<ParsedTelemetryParameter>();
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            var name = Decode(separator >= 0 ? part[..separator] : part);
            var value = Decode(separator >= 0 ? part[(separator + 1)..] : string.Empty);
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (IsSourceIdentifier(name))
            {
                sourceId ??= value;
                continue;
            }

            parameters.Add(Parameter(name, value));
        }

        return new ParsedTelemetryPayload(sourceId, parameters);
    }

    private static ParsedTelemetryParameter Parameter(string name, string value)
        => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric)
            ? new ParsedTelemetryParameter(name.Trim(), numeric, null)
            : new ParsedTelemetryParameter(name.Trim(), null, value);

    private static string? JsonValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };

    private static bool IsSourceIdentifier(string name)
        => name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("machineId", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("deviceId", StringComparison.OrdinalIgnoreCase);

    private static string Decode(string value)
        => Uri.UnescapeDataString(value.Replace('+', ' '));

    private static string? Validate(TelemetryIngestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourcePath))
            return "SourcePath is required.";
        if (string.IsNullOrWhiteSpace(request.RawPayload))
            return "RawPayload is required.";
        return null;
    }

    private sealed record ParsedTelemetryPayload(
        string? SourceId,
        IReadOnlyList<ParsedTelemetryParameter> Parameters);

    private sealed record ParsedTelemetryParameter(
        string Name,
        decimal? NumericValue,
        string? StringValue);

    public sealed record TelemetryStoreResult(
        int RawId,
        DateTime RecordedAt,
        string? SourceId,
        int TagsCreated,
        int AlarmParametersEvaluated);
}
