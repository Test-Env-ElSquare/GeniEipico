namespace API.Contracts;

public sealed record TelemetryMappingUpsertRequest(
    string SourcePath,
    string BusinessTable,
    IReadOnlyList<TelemetryFieldMappingRequest>? FieldMappings = null,
    bool? IsActive = null,
    string? Notes = null);

public sealed record TelemetryFieldMappingRequest(
    string TargetField,
    string SourceTag,
    bool? Required = null);
