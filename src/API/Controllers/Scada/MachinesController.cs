using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Domain.Entities.Scada;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Scada;

[Authorize]
[Route("v1/machines")]
public class MachinesController : CrudController<Machine, IMachineService>
{
    private readonly IMachineStateService _stateService;
    private readonly IScadaManualWorkflowService _workflow;
    private readonly IMachineStateHistoryService _historyService;
    private readonly IOeeSnapshotService _oeeService;
    private readonly IDeviceService _deviceService;
    private readonly IDeviceTypeTagDefinitionService _tagDefinitionService;
    private readonly ITelemetryTagService _telemetryTagService;

    public MachinesController(
        IMachineService service,
        IMachineStateService stateService,
        IScadaManualWorkflowService workflow,
        IMachineStateHistoryService historyService,
        IOeeSnapshotService oeeService,
        IDeviceService deviceService,
        IDeviceTypeTagDefinitionService tagDefinitionService,
        ITelemetryTagService telemetryTagService) : base(service)
    {
        _stateService = stateService;
        _workflow = workflow;
        _historyService = historyService;
        _oeeService = oeeService;
        _deviceService = deviceService;
        _tagDefinitionService = tagDefinitionService;
        _telemetryTagService = telemetryTagService;
    }

    [HttpGet("{id:int}/parameters")]
    public IActionResult GetParameters(int id)
    {
        var machine = Service.Query().FirstOrDefault(x => x.Id == id && x.IsActive);
        if (machine is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                "machine_not_found",
                $"Machine {id} was not found."));
        }
        var device = machine.DeviceId.HasValue
            ? _deviceService.Query().FirstOrDefault(x =>
                x.Id == machine.DeviceId.Value &&
                x.IsActive &&
                !x.IsDeleted)
            : null;
        var definitions = device is null
            ? new List<MachineParameterOption>()
            : _tagDefinitionService.Query()
                .Where(x => x.DeviceTypeId == device.DeviceTypeId)
                .Select(x => new MachineParameterOption(
                    x.TagName,
                    x.DataType,
                    x.Unit,
                    x.MinValue,
                    x.MaxValue))
                .ToList();
        var sourceIds = new[]
            {
                machine.Code,
                machine.Name,
                device?.SerialNumber,
                device?.MqttClientId
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var observed = _telemetryTagService.Query()
            .Where(x => sourceIds.Contains(x.DeviceId))
            .Select(x => x.TagName)
            .Distinct()
            .Select(x => new MachineParameterOption(
                x,
                "number",
                null,
                null,
                null))
            .ToList();
        var parameters = definitions
            .Concat(observed)
            .GroupBy(x => x.TagName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.TagName)
            .Select(x => new
            {
                value = x.TagName,
                label = HumanizeParameter(x.TagName),
                x.DataType,
                x.Unit,
                suggestedMinimumValue = x.MinValue,
                suggestedMaximumValue = x.MaxValue
            })
            .ToArray();

        return Ok(ApiResponse<object>.Ok(new
        {
            machineId = machine.Id,
            machineName = machine.Name,
            deviceId = device?.Id,
            sourceIds,
            items = parameters
        }));
    }

    [HttpGet("by-line/{lineId:int}")]
    public IActionResult GetByLine(int lineId)
    {
        if (lineId <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "invalid_line_id",
                "LineId must be greater than zero."));
        }

        var machines = Service.Query()
            .Where(x => x.LineId == lineId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.LineId,
                x.DeviceId,
                x.Code,
                x.Name,
                x.MachineType,
                x.Manufacturer,
                x.ModelNumber,
                x.RatedSpeedRpm,
                x.RatedPowerKw,
                x.InstallDate,
                x.IsActive
            })
            .ToArray();

        return Ok(ApiResponse<object>.Ok(new
        {
            lineId,
            items = machines
        }));
    }

    [HttpGet("{id:int}/state")]
    public IActionResult GetState(int id)
    {
        var machine = Service.Query().FirstOrDefault(x => x.Id == id && x.IsActive && !x.IsDeleted);
        if (machine is null)
            return NotFound(ApiResponse<object>.Fail("machine_not_found", $"Machine {id} was not found."));
        var state = _stateService.Query()
            .Where(x => x.LineId == machine.LineId &&
                        (x.MachineId == machine.Code || x.MachineId == machine.Name))
            .OrderByDescending(x => x.Timestamp)
            .FirstOrDefault();
        if (state is null)
        {
            return Ok(ApiResponse<object>.Ok(new { machineId = id, state = "unknown", note = "No matching RealTime.Production snapshot has been projected yet." }));
        }
        return Ok(ApiResponse<object>.Ok(new
        {
            state.Id,
            state.MachineId,
            stateCode = state.State,
            state = MachineStateLabel(state.State, state.Fault),
            state.Speed,
            speedRpm = state.Speed,
            state.Count,
            state.CountDiff,
            state.FactoryId,
            state.LineId,
            state.Temp,
            tempCelsius = state.Temp,
            state.QReject,
            state.StateReason,
            state.Sku,
            state.Fault,
            state.Timestamp,
            telemetryAt = state.Timestamp,
            type = "RealTime.Production snapshot"
        }));
    }

    [HttpPost("{id:int}/state")]
    public async Task<IActionResult> UpdateState(int id, [FromBody] ManualMachineStateRequest request)
    {
        var result = await _workflow.UpdateMachineStateManualAsync(id, request.State, request.Notes);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}/state-history")]
    public IActionResult GetStateHistory(int id)
    {
        var history = _historyService.Query()
            .Where(x => x.MachineId == id)
            .OrderByDescending(x => x.TransitionedAt)
            .Select(x => new { x.Id, x.MachineId, x.FromState, x.ToState, x.TransitionedAt, x.TriggerSource, x.Notes })
            .ToList<object>();
        return Ok(ApiResponse<IReadOnlyList<object>>.Ok(history));
    }

    [HttpGet("{id:int}/oee")]
    public IActionResult GetOee(int id)
    {
        var snapshots = _oeeService.Query()
            .Where(x => x.MachineId == id)
            .OrderByDescending(x => x.ComputedAt)
            .Select(x => new { x.Id, x.MachineId, x.MachineName, x.CustomerId, x.FactoryId, x.LineId, x.ShiftId, x.ShiftDate, x.AvailabilityPct, x.PerformancePct, x.QualityPct, x.OeePct, x.AverageSpeedRpm, x.LastState, x.TotalOutput, x.RejectedUnits, x.DowntimeMin, x.EnergyKwh, x.ComputedAt })
            .ToList<object>();
        return Ok(ApiResponse<IReadOnlyList<object>>.Ok(snapshots));
    }

    [HttpGet("{id:int}/telemetry-tags")]
    public IActionResult GetTelemetryTags(int id)
    {
        var machine = Service.Query()
            .FirstOrDefault(x => x.Id == id && x.IsActive && !x.IsDeleted);
        if (machine is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                "machine_not_found",
                $"Machine {id} was not found."));
        }

        var recordedAt = _telemetryTagService.Query()
            .Where(x => x.DeviceId == machine.Name)
            .Select(x => (DateTime?)x.RecordedAt)
            .Max();
        var items = recordedAt.HasValue
            ? _telemetryTagService.Query()
                .Where(x => x.DeviceId == machine.Name &&
                            x.RecordedAt == recordedAt.Value)
                .OrderBy(x => x.TagName)
                .Select(x => new
                {
                    x.TagName,
                    x.ValueNum,
                    x.ValueStr,
                    x.RecordedAt
                })
                .ToArray()
            : Array.Empty<object>();

        return Ok(ApiResponse<object>.Ok(new
        {
            machineId = machine.Id,
            sourceId = machine.Name,
            recordedAt,
            items
        }));
    }

    private static string MachineStateLabel(int state, int fault)
    {
        if (fault != 0) return "fault";
        return state switch
        {
            1 => "running",
            0 => "idle",
            2 => "offline",
            3 => "maintenance",
            _ => "unknown"
        };
    }

    private static string HumanizeParameter(string value)
        => string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));

    private sealed record MachineParameterOption(
        string TagName,
        string DataType,
        string? Unit,
        decimal? MinValue,
        decimal? MaxValue);
}
