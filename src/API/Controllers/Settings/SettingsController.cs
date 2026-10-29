using API.Common;
using API.Contracts;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Domain.Entities.Analytics;
using Domain.Entities.Configuration;
using Domain.Entities.IoT;
using Domain.Entities.Scada;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json;

namespace API.Controllers.Settings;

[ApiController]
[Authorize]
[Route("v1/settings/telemetry-mapping")]
public class SettingsController : ControllerBase
{
    private const string MappingSettingKey = "telemetry.mapping.rules";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _uow;
    private readonly IProductionRecordWriter _productionWriter;

    public SettingsController(IUnitOfWork uow, IProductionRecordWriter productionWriter)
    {
        _uow = uow;
        _productionWriter = productionWriter;
    }

    [HttpGet("sources")]
    public IActionResult Sources()
    {
        if (!TryGetScope(out var scope, out var error)) return error;

        var mappings = LoadMappings(scope.CustomerId);
        var sources = LatestSources()
            .Select(x =>
            {
                var sourceMappings = mappings
                    .Where(m => Same(m.SourcePath, x.SourcePath))
                    .ToArray();

                return new
                {
                    x.SourcePath,
                    x.LastSignal,
                    x.ReceivedAt,
                    HasMapping = sourceMappings.Length > 0,
                    Mappings = sourceMappings.Select(m => new
                    {
                        m.Id,
                        m.BusinessTable,
                        m.IsActive
                    }).ToArray()
                };
            })
            .OrderByDescending(x => x.ReceivedAt)
            .ToArray();

        return Ok(ApiResponse<object>.Ok(new
        {
            items = sources
        }));
    }

    [HttpGet("source-paths")]
    public IActionResult SourcePaths()
    {
        if (!TryGetScope(out _, out var error)) return error;

        var items = LatestSources()
            .Select(x => new
            {
                value = x.SourcePath,
                label = x.SourcePath,
                lastReceivedAt = x.ReceivedAt
            })
            .OrderBy(x => x.label)
            .ToArray();

        return Ok(ApiResponse<object>.Ok(new { items }));
    }

    [HttpGet("source-preview")]
    public IActionResult SourcePreview([FromQuery] string sourcePath)
    {
        if (!TryGetScope(out _, out var error)) return error;
        if (string.IsNullOrWhiteSpace(sourcePath))
            return BadRequest(ApiResponse<object>.Fail("source_path_required", "SourcePath is required."));

        var raw = LatestRaw(sourcePath);
        if (raw is null)
            return NotFound(ApiResponse<object>.Fail("source_not_found", "No signal was found for this SourcePath."));

        var parsed = ParsePayload(raw.RawPayload);
        return Ok(ApiResponse<object>.Ok(new
        {
            raw.Id,
            raw.SourcePath,
            LastSignal = SummarizePayload(raw.RawPayload),
            raw.ReceivedAt,
            Tags = parsed.Parameters
                .Select(x => new
                {
                    name = x.Name,
                    valueNum = x.NumericValue,
                    valueStr = x.StringValue
                })
                .OrderBy(x => x.name)
                .ToArray()
        }));
    }

    [HttpGet("business-tables")]
    public IActionResult BusinessTables()
    {
        if (!TryGetScope(out _, out var error)) return error;

        return Ok(ApiResponse<object>.Ok(new
        {
            items = BusinessCatalog()
                .Select(x => new
                {
                    value = x.Key,
                    label = x.Label,
                    x.Description,
                    x.TargetEntityType
                })
                .ToArray()
        }));
    }

    [HttpGet("business-tables/{businessTable}/fields")]
    public IActionResult BusinessTableFields(
        string businessTable,
        [FromQuery] string? sourcePath = null)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var target = ResolveBusinessTable(businessTable);
        if (target is null)
            return BadRequest(ApiResponse<object>.Fail("invalid_business_table", "BusinessTable must be either 'production' or 'energy_readings'."));

        var sourceTags = Array.Empty<SourceTagOptionDto>();
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            var raw = LatestRaw(sourcePath);
            if (raw is not null)
            {
                sourceTags = ParsePayload(raw.RawPayload)
                    .Parameters
                    .Select(x => new SourceTagOptionDto(
                        x.Name,
                        x.Name,
                        x.NumericValue?.ToString(CultureInfo.InvariantCulture) ?? x.StringValue))
                    .OrderBy(x => x.Label)
                    .ToArray();
            }
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            businessTable = target.Key,
            label = target.Label,
            targetEntityType = target.TargetEntityType,
            sourceTags,
            targetFields = target.TargetFields,
            systemFields = target.SystemFields,
            defaults = DefaultFieldMappings(target.Key, sourceTags.Select(x => x.Value).ToArray())
        }));
    }

    [HttpGet("mappings")]
    public IActionResult Mappings(
        [FromQuery] string? sourcePath = null,
        [FromQuery] string? businessTable = null)
    {
        if (!TryGetScope(out var scope, out var error)) return error;

        var query = LoadMappings(scope.CustomerId).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(sourcePath))
            query = query.Where(x => Same(x.SourcePath, sourcePath));
        if (!string.IsNullOrWhiteSpace(businessTable))
            query = query.Where(x => Same(x.BusinessTable, NormalizeBusinessTable(businessTable)));

        return Ok(ApiResponse<object>.Ok(new
        {
            items = query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ToArray()
        }));
    }

    [HttpGet("mappings/{id}")]
    public IActionResult Mapping(string id)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var mapping = LoadMappings(scope.CustomerId).FirstOrDefault(x => Same(x.Id, id));

        return mapping is null
            ? NotFound(ApiResponse<object>.Fail("mapping_not_found", $"Mapping '{id}' was not found."))
            : Ok(ApiResponse<object>.Ok(mapping));
    }

    [HttpPost("mappings")]
    public async Task<IActionResult> CreateMapping([FromBody] TelemetryMappingUpsertRequest request)
    {
        if (!TryGetScope(out var scope, out var error)) return error;

        var validation = ValidateMappingRequest(scope, request);
        if (validation.Errors.Count > 0)
            return BadRequest(ApiResponse<object>.Fail("invalid_mapping", "Mapping request is invalid.", validation.Errors));

        var mappings = LoadMappings(scope.CustomerId).ToList();
        var now = DateTime.UtcNow;
        var mapping = new TelemetryMappingDto(
            Guid.NewGuid().ToString("N"),
            request.SourcePath.Trim(),
            NormalizeBusinessTable(request.BusinessTable),
            NormalizeFieldMappings(request.FieldMappings),
            request.IsActive ?? false,
            now,
            null,
            request.Notes);

        mappings.Add(mapping);
        await SaveMappingsAsync(scope.CustomerId, mappings);

        return Created(
            $"/v1/settings/telemetry-mapping/mappings/{mapping.Id}",
            ApiResponse<object>.Ok(mapping, new { validation.Warnings }));
    }

    [HttpPut("mappings/{id}")]
    public async Task<IActionResult> UpdateMapping(string id, [FromBody] TelemetryMappingUpsertRequest request)
    {
        if (!TryGetScope(out var scope, out var error)) return error;

        var validation = ValidateMappingRequest(scope, request);
        if (validation.Errors.Count > 0)
            return BadRequest(ApiResponse<object>.Fail("invalid_mapping", "Mapping request is invalid.", validation.Errors));

        var mappings = LoadMappings(scope.CustomerId).ToList();
        var index = mappings.FindIndex(x => Same(x.Id, id));
        if (index < 0)
            return NotFound(ApiResponse<object>.Fail("mapping_not_found", $"Mapping '{id}' was not found."));

        var updated = mappings[index] with
        {
            SourcePath = request.SourcePath.Trim(),
            BusinessTable = NormalizeBusinessTable(request.BusinessTable),
            FieldMappings = NormalizeFieldMappings(request.FieldMappings),
            IsActive = request.IsActive ?? mappings[index].IsActive,
            UpdatedAt = DateTime.UtcNow,
            Notes = request.Notes
        };
        mappings[index] = updated;
        await SaveMappingsAsync(scope.CustomerId, mappings);

        return Ok(ApiResponse<object>.Ok(updated, new { validation.Warnings }));
    }

    [HttpPost("mappings/validate")]
    public IActionResult ValidateDraft([FromBody] TelemetryMappingUpsertRequest request)
    {
        if (!TryGetScope(out var scope, out var error)) return error;
        var result = ValidateMappingRequest(scope, request);
        return Ok(ApiResponse<object>.Ok(new
        {
            isValid = result.Errors.Count == 0,
            result.Errors,
            result.Warnings
        }));
    }

    [HttpPost("mappings/{id}/activate")]
    public async Task<IActionResult> ActivateMapping(string id)
    {
        if (!TryGetScope(out var scope, out var error)) return error;

        var mappings = LoadMappings(scope.CustomerId).ToList();
        var mapping = mappings.FirstOrDefault(x => Same(x.Id, id));
        if (mapping is null)
            return NotFound(ApiResponse<object>.Fail("mapping_not_found", $"Mapping '{id}' was not found."));

        var request = new TelemetryMappingUpsertRequest(
            mapping.SourcePath,
            mapping.BusinessTable,
            mapping.FieldMappings.Select(x => new TelemetryFieldMappingRequest(x.TargetField, x.SourceTag, x.Required)).ToArray(),
            true,
            mapping.Notes);
        var validation = ValidateMappingRequest(scope, request);
        if (validation.Errors.Count > 0)
            return BadRequest(ApiResponse<object>.Fail("invalid_mapping", "Mapping cannot be activated before fixing validation errors.", validation.Errors));

        var activated = mapping with { IsActive = true, UpdatedAt = DateTime.UtcNow };
        for (var i = 0; i < mappings.Count; i++)
        {
            if (Same(mappings[i].SourcePath, activated.SourcePath) &&
                Same(mappings[i].BusinessTable, activated.BusinessTable))
            {
                mappings[i] = mappings[i] with { IsActive = Same(mappings[i].Id, activated.Id), UpdatedAt = DateTime.UtcNow };
            }
        }

        await SaveMappingsAsync(scope.CustomerId, mappings);
        return Ok(ApiResponse<object>.Ok(activated, new { validation.Warnings }));
    }

    [HttpDelete("mappings/{id}")]
    public async Task<IActionResult> DeleteMapping(string id)
    {
        if (!TryGetScope(out var scope, out var error)) return error;

        var mappings = LoadMappings(scope.CustomerId).ToList();
        var index = mappings.FindIndex(x => Same(x.Id, id));
        if (index < 0)
            return NotFound(ApiResponse<object>.Fail("mapping_not_found", $"Mapping '{id}' was not found."));

        var removed = mappings[index];
        mappings.RemoveAt(index);
        await SaveMappingsAsync(scope.CustomerId, mappings);

        return Ok(ApiResponse<object>.Ok(new
        {
            removed.Id,
            removed.SourcePath,
            removed.BusinessTable,
            message = "Mapping deleted successfully."
        }));
    }

    [HttpPost("mappings/{id}/execute")]
    public async Task<IActionResult> ExecuteMapping(string id)
    {
        if (!TryGetScope(out var scope, out var error)) return error;

        var mappings = LoadMappings(scope.CustomerId);
        var mapping = mappings.FirstOrDefault(x => Same(x.Id, id));
        if (mapping is null)
            return NotFound(ApiResponse<object>.Fail("mapping_not_found", $"Mapping '{id}' was not found."));

        if (!mapping.IsActive)
            return BadRequest(ApiResponse<object>.Fail("mapping_not_active", "Mapping must be activated before execution."));

        var raw = LatestRaw(mapping.SourcePath);
        if (raw is null)
            return NotFound(ApiResponse<object>.Fail("no_signal", "No signal was found for this mapping's SourcePath."));

        var parsed = ParsePayload(raw.RawPayload);
        var tagValues = parsed.Parameters.ToDictionary(
            x => x.Name,
            x => x.NumericValue?.ToString(CultureInfo.InvariantCulture) ?? x.StringValue ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);

        var target = ResolveBusinessTable(mapping.BusinessTable);
        if (target is null)
            return BadRequest(ApiResponse<object>.Fail("invalid_business_table", "BusinessTable is invalid."));

        var missingRequired = mapping.FieldMappings
            .Where(f => f.Required && !tagValues.ContainsKey(f.SourceTag))
            .Select(f => f.SourceTag)
            .ToArray();
        if (missingRequired.Length > 0)
            return BadRequest(ApiResponse<object>.Fail("missing_tags",
                $"Required source tags not found in latest signal: {string.Join(", ", missingRequired)}"));

        try
        {
            if (mapping.BusinessTable == "production")
            {
                var record = await ExecuteProductionMappingAsync(scope, mapping, tagValues, raw.ReceivedAt);
                return Ok(ApiResponse<object>.Ok(new
                {
                    businessTable = "production",
                    entityType = "MachineState",
                    recordId = record.Id,
                    timestamp = record.Timestamp,
                    message = "Production signal saved successfully (replays reuse the existing record)."
                }));
            }

            if (mapping.BusinessTable == "energy_readings")
            {
                var record = await ExecuteEnergyReadingMappingAsync(scope, mapping, tagValues, raw.ReceivedAt);
                return Ok(ApiResponse<object>.Ok(new
                {
                    businessTable = "energy_readings",
                    entityType = "EnergyReading",
                    recordId = record.Id,
                    timestamp = record.Timestamp,
                    message = "Energy reading record created successfully."
                }));
            }

            return BadRequest(ApiResponse<object>.Fail("unsupported_table", $"BusinessTable '{mapping.BusinessTable}' is not supported for execution."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Fail("execution_error", $"Failed to execute mapping: {ex.Message}"));
        }
    }

    // ── Execution helpers ─────────────────────────────────────────────

    private async Task<MachineState> ExecuteProductionMappingAsync(
        AuthenticatedAccessScope scope,
        TelemetryMappingDto mapping,
        Dictionary<string, string> tagValues,
        DateTime signalTimestamp)
    {
        string GetString(string field) =>
            mapping.FieldMappings.FirstOrDefault(f => Same(f.TargetField, field)) is { } fm
            && tagValues.TryGetValue(fm.SourceTag, out var v) ? v : string.Empty;

        var mappedMachineId = GetString("MachineId");
        if (string.IsNullOrWhiteSpace(mappedMachineId))
            throw new InvalidOperationException("MachineId was not provided in the telemetry payload.");

        var machine = _uow.Machines.Query()
            .Where(x => x.Name == mappedMachineId && x.IsActive && !x.IsDeleted && scope.LineIds.Contains(x.LineId))
            .Select(x => new { x.Id, x.Name, x.LineId })
            .SingleOrDefault()
            ?? throw new InvalidOperationException($"Machine '{mappedMachineId}' is not accessible or was not found.");

        var lineFactoryId = scope.LineFactoryIds.TryGetValue(machine.LineId, out var fId) ? fId : 0;

        int GetInt(string field) =>
            mapping.FieldMappings.FirstOrDefault(f => Same(f.TargetField, field)) is { } fm
            && tagValues.TryGetValue(fm.SourceTag, out var v)
            && int.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;

        decimal? GetDecimal(string field) =>
            mapping.FieldMappings.FirstOrDefault(f => Same(f.TargetField, field)) is { } fm
            && tagValues.TryGetValue(fm.SourceTag, out var v)
            && decimal.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;

        var currentCount = GetInt("Count");

        var record = new MachineState
        {
            MachineId = machine.Name,
            State = GetInt("State"),
            Speed = GetInt("Speed"),
            Count = currentCount,
            LineId = machine.LineId,
            FactoryId = lineFactoryId,
            Temp = GetDecimal("Temp"),
            QReject = GetInt("Qreject"),
            StateReason = GetInt("StateReason"),
            Sku = GetInt("SKU"),
            Fault = GetInt("Fault"),
            Timestamp = signalTimestamp
        };

        return await _productionWriter.AppendAsync(machine.Id, record, "telemetry-mapping");
    }

    private async Task<EnergyReading> ExecuteEnergyReadingMappingAsync(
        AuthenticatedAccessScope scope,
        TelemetryMappingDto mapping,
        Dictionary<string, string> tagValues,
        DateTime signalTimestamp)
    {
        string GetString(string field) =>
            mapping.FieldMappings.FirstOrDefault(f => Same(f.TargetField, field)) is { } fm
            && tagValues.TryGetValue(fm.SourceTag, out var v) ? v : string.Empty;

        var mappedTransformerId = GetString("TransformerId");
        if (string.IsNullOrWhiteSpace(mappedTransformerId))
            throw new InvalidOperationException("TransformerId was not provided in the telemetry payload.");

        var transformer = _uow.Transformers.Query()
            .Where(x => (x.TransformerName == mappedTransformerId || x.Id.ToString() == mappedTransformerId) && !x.IsDeleted && scope.FactoryIds.Contains(x.FactoryId))
            .Select(x => new { x.Id, x.FactoryId })
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Transformer '{mappedTransformerId}' is not accessible or was not found.");

        decimal? GetDecimal(string field) =>
            mapping.FieldMappings.FirstOrDefault(f => Same(f.TargetField, field)) is { } fm
            && tagValues.TryGetValue(fm.SourceTag, out var v)
            && decimal.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;

        short? GetShort(string field) =>
            mapping.FieldMappings.FirstOrDefault(f => Same(f.TargetField, field)) is { } fm
            && tagValues.TryGetValue(fm.SourceTag, out var v)
            && short.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

        var record = new EnergyReading
        {
            CustomerId = scope.CustomerId,
            FactoryId = transformer.FactoryId,
            LineId = null,
            TransformerId = transformer.Id,
            Quality = GetShort("Quality"),
            VoltageL1 = GetDecimal("VoltageL1"),
            VoltageL2 = GetDecimal("VoltageL2"),
            VoltageL3 = GetDecimal("VoltageL3"),
            CurrentL1 = GetDecimal("CurrentL1"),
            CurrentL2 = GetDecimal("CurrentL2"),
            CurrentL3 = GetDecimal("CurrentL3"),
            PowerFactorL1 = GetDecimal("PowerFactorL1"),
            PowerFactorL2 = GetDecimal("PowerFactorL2"),
            PowerFactorL3 = GetDecimal("PowerFactorL3"),
            ActivePowerL1 = GetDecimal("ActivePowerL1"),
            ActivePowerL2 = GetDecimal("ActivePowerL2"),
            ActivePowerL3 = GetDecimal("ActivePowerL3"),
            ReactivePowerL1 = GetDecimal("ReactivePowerL1"),
            ReactivePowerL2 = GetDecimal("ReactivePowerL2"),
            ReactivePowerL3 = GetDecimal("ReactivePowerL3"),
            ApparentPowerL1 = GetDecimal("ApparentPowerL1"),
            ApparentPowerL2 = GetDecimal("ApparentPowerL2"),
            ApparentPowerL3 = GetDecimal("ApparentPowerL3"),
            EnergyL1 = GetDecimal("EnergyL1"),
            EnergyL2 = GetDecimal("EnergyL2"),
            EnergyL3 = GetDecimal("EnergyL3"),
            VoltageThdL1 = GetDecimal("VoltageThdL1"),
            VoltageThdL2 = GetDecimal("VoltageThdL2"),
            VoltageThdL3 = GetDecimal("VoltageThdL3"),
            CurrentThdL1 = GetDecimal("CurrentThdL1"),
            CurrentThdL2 = GetDecimal("CurrentThdL2"),
            CurrentThdL3 = GetDecimal("CurrentThdL3"),
            Timestamp = signalTimestamp
        };

        await _uow.EnergyReadings.AddAsync(record);
        await _uow.SaveChangesAsync();

        return record;
    }

    // ── Data access helpers ───────────────────────────────────────────

    private IReadOnlyList<TelemetrySourceDto> LatestSources()
    {
        var rawRows = _uow.TelemetryRaw.Query()
            .OrderByDescending(x => x.ReceivedAt)
            .Take(5000)
            .Select(x => new { x.Id, x.SourcePath, x.RawPayload, x.ReceivedAt })
            .ToList();

        return rawRows
            .Select(x => new TelemetrySourceDto(
                x.SourcePath,
                SummarizePayload(x.RawPayload),
                x.ReceivedAt))
            .GroupBy(x => NormalizePath(x.SourcePath), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(s => s.ReceivedAt).First())
            .ToArray();
    }

    private TelemetryRaw? LatestRaw(string sourcePath)
    {
        return _uow.TelemetryRaw.Query()
            .Where(x => x.SourcePath == sourcePath.Trim())
            .OrderByDescending(x => x.ReceivedAt)
            .FirstOrDefault();
    }

    private IReadOnlyList<TelemetryMappingDto> LoadMappings(int customerId)
    {
        var value = _uow.PlatformSettings.Query()
            .Where(x => x.CustomerId == customerId && x.Key == MappingSettingKey)
            .Select(x => x.Value)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<TelemetryMappingDto>();

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<TelemetryMappingDto>>(value, JsonOptions)
                   ?? Array.Empty<TelemetryMappingDto>();
        }
        catch (JsonException)
        {
            return Array.Empty<TelemetryMappingDto>();
        }
    }

    private async Task SaveMappingsAsync(int customerId, IReadOnlyList<TelemetryMappingDto> mappings)
    {
        var setting = _uow.PlatformSettings.Query(asNoTracking: false)
            .FirstOrDefault(x => x.CustomerId == customerId && x.Key == MappingSettingKey);

        var value = JsonSerializer.Serialize(mappings, JsonOptions);
        if (setting is null)
        {
            await _uow.PlatformSettings.AddAsync(new PlatformSetting
            {
                CustomerId = customerId,
                Key = MappingSettingKey,
                Value = value,
                IsSensitive = false
            });
        }
        else
        {
            setting.Value = value;
            _uow.PlatformSettings.Update(setting);
        }

        await _uow.SaveChangesAsync();
    }

    // ── Validation helpers ────────────────────────────────────────────

    private MappingValidationResult ValidateMappingRequest(
        AuthenticatedAccessScope scope,
        TelemetryMappingUpsertRequest request)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(request.SourcePath))
            errors.Add("SourcePath is required.");

        var target = ResolveBusinessTable(request.BusinessTable);
        if (target is null)
        {
            errors.Add("BusinessTable must be either 'production' or 'energy_readings'.");
            return new MappingValidationResult(errors, warnings);
        }

        var raw = string.IsNullOrWhiteSpace(request.SourcePath)
            ? null
            : LatestRaw(request.SourcePath);
        if (raw is null)
            warnings.Add("No latest signal was found for this SourcePath yet.");

        var sourceTagNames = raw is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : ParsePayload(raw.RawPayload).Parameters.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fieldMappings = NormalizeFieldMappings(request.FieldMappings);
        var mappedTargets = fieldMappings.Select(x => x.TargetField).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var field in target.TargetFields.Where(x => x.Required))
        {
            if (!mappedTargets.Contains(field.Field))
                errors.Add($"Target field '{field.Field}' must be mapped.");
        }

        foreach (var mapping in fieldMappings)
        {
            if (!target.TargetFields.Any(x => Same(x.Field, mapping.TargetField)))
                errors.Add($"Target field '{mapping.TargetField}' is not valid for {target.Label}.");

            if (sourceTagNames.Count > 0 && !sourceTagNames.Contains(mapping.SourceTag))
                warnings.Add($"Source tag '{mapping.SourceTag}' was not found in the latest signal sample.");
        }

        return new MappingValidationResult(errors, warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }



    // ── Business table catalog ────────────────────────────────────────

    private static BusinessTableDefinition? ResolveBusinessTable(string? value)
        => BusinessCatalog().FirstOrDefault(x => Same(x.Key, NormalizeBusinessTable(value)));

    private static BusinessTableDefinition[] BusinessCatalog()
        =>
        [
            new(
                "production",
                "Production",
                "Creates one production business row for every valid machine signal.",
                "machine",
                [
                    Field("State", "State", "int", true),
                    Field("Speed", "Speed", "int", true),
                    Field("Count", "Count", "int", true),
                    Field("Qreject", "Q Reject", "int", true),
                    Field("StateReason", "State Reason", "int", true),
                    Field("SKU", "SKU", "int", true),
                    Field("Fault", "Fault", "int", true),
                    Field("Temp", "Temperature", "decimal", false),
                    Field("MachineId", "Machine ID", "string", true)
                ],
                [
                    SystemField("FactoryId", "From selected machine line"),
                    SystemField("LineId", "From selected machine"),
                    SystemField("CountDiff", "Calculated from current count minus previous count"),
                    SystemField("TimeStamp", "From signal RecordedAt")
                ]),
            new(
                "energy_readings",
                "Energy Readings",
                "Creates one energy reading row for every valid transformer signal.",
                "transformer",
                [
                    Field("Quality", "Quality", "short", false),
                    Field("VoltageL1", "Voltage L1", "decimal", true),
                    Field("VoltageL2", "Voltage L2", "decimal", true),
                    Field("VoltageL3", "Voltage L3", "decimal", true),
                    Field("CurrentL1", "Current L1", "decimal", true),
                    Field("CurrentL2", "Current L2", "decimal", true),
                    Field("CurrentL3", "Current L3", "decimal", true),
                    Field("PowerFactorL1", "Power Factor L1", "decimal", false),
                    Field("PowerFactorL2", "Power Factor L2", "decimal", false),
                    Field("PowerFactorL3", "Power Factor L3", "decimal", false),
                    Field("ActivePowerL1", "Active Power L1", "decimal", false),
                    Field("ActivePowerL2", "Active Power L2", "decimal", false),
                    Field("ActivePowerL3", "Active Power L3", "decimal", false),
                    Field("ReactivePowerL1", "Reactive Power L1", "decimal", false),
                    Field("ReactivePowerL2", "Reactive Power L2", "decimal", false),
                    Field("ReactivePowerL3", "Reactive Power L3", "decimal", false),
                    Field("ApparentPowerL1", "Apparent Power L1", "decimal", false),
                    Field("ApparentPowerL2", "Apparent Power L2", "decimal", false),
                    Field("ApparentPowerL3", "Apparent Power L3", "decimal", false),
                    Field("EnergyL1", "Energy L1", "decimal", false),
                    Field("EnergyL2", "Energy L2", "decimal", false),
                    Field("EnergyL3", "Energy L3", "decimal", false),
                    Field("VoltageThdL1", "Voltage THD L1", "decimal", false),
                    Field("VoltageThdL2", "Voltage THD L2", "decimal", false),
                    Field("VoltageThdL3", "Voltage THD L3", "decimal", false),
                    Field("CurrentThdL1", "Current THD L1", "decimal", false),
                    Field("CurrentThdL2", "Current THD L2", "decimal", false),
                    Field("CurrentThdL3", "Current THD L3", "decimal", false),
                    Field("TransformerId", "Transformer ID", "int", true)
                ],
                [
                    SystemField("CustomerId", "From selected transformer factory"),
                    SystemField("FactoryId", "From selected transformer"),
                    SystemField("LineId", "Nullable for transformer readings"),
                    SystemField("TimeStamp", "From signal RecordedAt")
                ])
        ];

    private static object[] DefaultFieldMappings(string businessTable, IReadOnlyCollection<string> sourceTags)
    {
        var pairs = businessTable == "energy_readings"
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["V1"] = "VoltageL1",
                ["V2"] = "VoltageL2",
                ["V3"] = "VoltageL3",
                ["I1"] = "CurrentL1",
                ["I2"] = "CurrentL2",
                ["I3"] = "CurrentL3",
                ["Pf1"] = "PowerFactorL1",
                ["Pf2"] = "PowerFactorL2",
                ["Pf3"] = "PowerFactorL3",
                ["Pact1"] = "ActivePowerL1",
                ["Pact2"] = "ActivePowerL2",
                ["Pact3"] = "ActivePowerL3",
                ["Preact1"] = "ReactivePowerL1",
                ["Preact2"] = "ReactivePowerL2",
                ["Preact3"] = "ReactivePowerL3",
                ["Papp1"] = "ApparentPowerL1",
                ["Papp2"] = "ApparentPowerL2",
                ["Papp3"] = "ApparentPowerL3",
                ["Energy1"] = "EnergyL1",
                ["Energy2"] = "EnergyL2",
                ["Energy3"] = "EnergyL3",
                ["THDv1"] = "VoltageThdL1",
                ["THDv2"] = "VoltageThdL2",
                ["THDv3"] = "VoltageThdL3",
                ["THDi1"] = "CurrentThdL1",
                ["THDi2"] = "CurrentThdL2",
                ["THDi3"] = "CurrentThdL3"
            }
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["State"] = "State",
                ["Speed"] = "Speed",
                ["Count"] = "Count",
                ["Qreject"] = "Qreject",
                ["StateReason"] = "StateReason",
                ["SKU"] = "SKU",
                ["Fault"] = "Fault",
                ["Temp"] = "Temp",
                ["id"] = "MachineId",
                ["machineId"] = "MachineId",
                ["deviceId"] = "MachineId"
            };

        return sourceTags
            .Where(pairs.ContainsKey)
            .Select(x => new
            {
                sourceTag = x,
                targetField = pairs[x]
            })
            .Cast<object>()
            .ToArray();
    }

    private static IReadOnlyList<TelemetryFieldMappingDto> NormalizeFieldMappings(
        IReadOnlyList<TelemetryFieldMappingRequest>? mappings)
        => mappings?
               .Where(x => !string.IsNullOrWhiteSpace(x.TargetField) && !string.IsNullOrWhiteSpace(x.SourceTag))
               .Select(x => new TelemetryFieldMappingDto(
                   x.TargetField.Trim(),
                   x.SourceTag.Trim(),
                   x.Required ?? false))
               .DistinctBy(x => x.TargetField, StringComparer.OrdinalIgnoreCase)
               .ToArray()
           ?? Array.Empty<TelemetryFieldMappingDto>();

    // ── Payload parsing ───────────────────────────────────────────────

    private static ParsedTelemetryPayload ParsePayload(string rawPayload)
    {
        var jsonPayload = TryParseJsonPayload(rawPayload);
        if (jsonPayload is not null)
            return jsonPayload;

        var query = ExtractQuerySegment(rawPayload);
        var questionMark = query.IndexOf('?');
        if (questionMark >= 0)
            query = query[(questionMark + 1)..];

        query = query.TrimStart('?', '&');
        var firstSpace = query.IndexOf(' ');
        if (firstSpace > 0 && query[..firstSpace].Contains('='))
            query = query[..firstSpace];

        var parameters = new List<ParsedTelemetryParameter>();
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            var name = Decode(separator >= 0 ? part[..separator] : part);
            var value = Decode(separator >= 0 ? part[(separator + 1)..] : string.Empty);
            if (string.IsNullOrWhiteSpace(name)) continue;

            parameters.Add(Parameter(name, value));
        }

        return new ParsedTelemetryPayload(parameters);
    }

    private static string ExtractQuerySegment(string rawPayload)
    {
        var query = rawPayload.Trim();
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < tokens.Length - 2; i++)
        {
            if (!tokens[i].Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                !tokens[i].Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidate = tokens[i + 2];
            if (!string.IsNullOrWhiteSpace(candidate) &&
                candidate != "-" &&
                candidate.Contains('='))
            {
                return candidate;
            }
        }

        return query;
    }

    private static ParsedTelemetryPayload? TryParseJsonPayload(string rawPayload)
    {
        try
        {
            using var payload = JsonDocument.Parse(rawPayload);
            if (payload.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var parameters = new List<ParsedTelemetryParameter>();
            foreach (var property in payload.RootElement.EnumerateObject())
            {
                var value = JsonValue(property.Value);
                if (value is null) continue;
                parameters.Add(Parameter(property.Name, value));
            }

            return new ParsedTelemetryPayload(parameters);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? JsonValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };

    private static ParsedTelemetryParameter Parameter(string name, string value)
        => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric)
            ? new ParsedTelemetryParameter(name.Trim(), numeric, null)
            : new ParsedTelemetryParameter(name.Trim(), null, value);

    private static string SummarizePayload(string rawPayload)
    {
        var parsed = ParsePayload(rawPayload);
        var sample = parsed.Parameters
            .Take(4)
            .Select(x => $"{x.Name}={x.NumericValue?.ToString(CultureInfo.InvariantCulture) ?? x.StringValue}")
            .ToArray();

        var text = sample.Length == 0 ? rawPayload : string.Join(" | ", sample);
        return text.Length <= 180 ? text : text[..177] + "...";
    }

    // ── Utility helpers ───────────────────────────────────────────────

    private static string Decode(string value)
        => Uri.UnescapeDataString(value.Replace('+', ' '));

    private static string NormalizePath(string value)
        => value.Trim().TrimEnd('/').ToLowerInvariant();

    private static string NormalizeBusinessTable(string? value)
        => (value ?? string.Empty).Trim().Replace("-", "_").ToLowerInvariant();

    private static bool Same(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private bool TryGetScope(out AuthenticatedAccessScope scope, out IActionResult error)
    {
        if (AuthenticatedAccessScope.TryResolve(User, _uow, out scope, out var code, out var message))
        {
            error = null!;
            return true;
        }

        error = Unauthorized(ApiResponse<object>.Fail(code, message));
        return false;
    }

    private static TargetFieldDto Field(string field, string label, string dataType, bool required)
        => new(field, label, dataType, required);

    private static SystemFieldDto SystemField(string field, string description)
        => new(field, description);

    // ── Inner DTOs ────────────────────────────────────────────────────

    private sealed record TelemetrySourceDto(
        string SourcePath,
        string LastSignal,
        DateTime ReceivedAt);

    private sealed record SourceTagOptionDto(
        string Value,
        string Label,
        string? SampleValue);

    private sealed record ParsedTelemetryPayload(
        IReadOnlyList<ParsedTelemetryParameter> Parameters);

    private sealed record ParsedTelemetryParameter(
        string Name,
        decimal? NumericValue,
        string? StringValue);

    private sealed record BusinessTableDefinition(
        string Key,
        string Label,
        string Description,
        string TargetEntityType,
        IReadOnlyList<TargetFieldDto> TargetFields,
        IReadOnlyList<SystemFieldDto> SystemFields);

    private sealed record TargetFieldDto(
        string Field,
        string Label,
        string DataType,
        bool Required);

    private sealed record SystemFieldDto(
        string Field,
        string Description);

    private sealed record MappingValidationResult(
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> Warnings);

    private sealed record TelemetryMappingDto(
        string Id,
        string SourcePath,
        string BusinessTable,
        IReadOnlyList<TelemetryFieldMappingDto> FieldMappings,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        string? Notes);

    private sealed record TelemetryFieldMappingDto(
        string TargetField,
        string SourceTag,
        bool Required);
}
