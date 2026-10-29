using Domain.Enums;

namespace API.Contracts;

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record VerifyPasswordResetTokenRequest(string Email, string Code);
public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);
public sealed record MfaVerifyRequest(string Code);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record TelemetryIngestRequest(string SourcePath, string RawPayload, DateTime? ReceivedAt = null);
public sealed record TelemetryBatchIngestRequest(IReadOnlyList<TelemetryIngestRequest> Items);
public sealed record HeartbeatRequest(int DeviceId, DateTime? SentAt = null);
public sealed record OtaAckRequest(int DeviceId, string Version, DateTime? CompletedAt = null);
public sealed record OtaCommandRequest(string Version, string? Notes = null);

public sealed record ManualMachineStateRequest(int State, string? Notes = null);
public sealed record SiloLoadRequest(int MaterialId, string? LotUid, decimal QuantityKg, DateTime? Timestamp = null);
public sealed record ScaleAssignBatchRequest(int BatchId, int MaterialId);

public sealed record StartBatchRequest(string? Notes = null);
public sealed record CompleteBatchRequest(int ProducedQty, int RejectedQty, string? Notes = null);
public sealed record AdvanceBatchStageRequest(string? Notes = null);
public sealed record SplitBatchRequest(int SplitQty, string Reason);
public sealed record DispenseMaterialRequest(int ScaleId, decimal DispensedQty, DateTime? Timestamp = null);
public sealed record CreateBatchRequest(
    int SkuId,
    int FactoryId,
    int LineId,
    int PlannedQty,
    DateTime PlannedStart,
    DateTime? PlannedEnd = null,
    int? ShiftId = null,
    BatchPriority? Priority = null,
    string? Notes = null);

public sealed record RouteStatusRequest(string Status);
public sealed record ScheduleStoppageRequest(int LineId, DateTime Start, DateTime End, string? StoppageType = null, string? Notes = null);
public sealed record MrpRecalculateRequest(DateTime? PlanDate = null);

public sealed record AlarmActionRequest(string? Notes = null, string? ResolutionAction = null);
public sealed record AlarmRuleToggleRequest(bool IsActive);

public sealed record StartWorkOrderRequest(string? Notes = null);
public sealed record CompleteWorkOrderRequest(string? ResolutionNotes, string? FailureMode, decimal? ActualCost, IReadOnlyList<PartUsedRequest>? PartsUsed = null);
public sealed record PartUsedRequest(int SparePartId, decimal Quantity);
public sealed record CancelRequest(string Reason);
public sealed record PmCompleteRequest(DateTime? CompletedAt = null, string? Notes = null);
public sealed record SparePartIssueRequest(int WorkOrderId, decimal QtyUsed);

public sealed record DismissInsightRequest(string? Reason = null);
public sealed record GenerateInsightRequest(string EntityType, int EntityId);
public sealed record ExportRequest(string ReportType, string Format, DateTime? From = null, DateTime? To = null);

public sealed record ErpConfigRequest(string ErpType, string? BaseUrl = null, string? AuthType = null);
public sealed record ErpInboundRequest(string Source, string Payload);
public sealed record ErpManualSyncRequest(DateTime? From = null, DateTime? To = null);
public sealed record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    int? CustomerId = null,
    string? Role = null,
    int? FactoryId = null,
    IReadOnlyList<int>? FactoryIds = null,
    int? LineId = null,
    IReadOnlyList<int>? LineIds = null,
    IReadOnlyList<int>? ExpertBoards = null,
    string? PhoneNumber = null,
    bool IsActive = true);
public sealed record UpdateUserRequest(
    string? Email,
    string? FullName,
    string? Role,
    int? FactoryId,
    IReadOnlyList<int>? FactoryIds,
    int? LineId,
    IReadOnlyList<int>? LineIds,
    IReadOnlyList<int>? ExpertBoards,
    string? PhoneNumber,
    bool? IsActive);
public sealed record AdminResetPasswordRequest(string NewPassword);
public sealed record RoleClaimRequest(string Type, string Value);
public sealed record CreateRoleRequest(string Name, IReadOnlyList<RoleClaimRequest>? Claims = null);
public sealed record UpdateRoleRequest(string? Name = null, IReadOnlyList<RoleClaimRequest>? Claims = null);
public sealed record PlatformSettingsUpdateRequest(IReadOnlyList<PlatformSettingItemRequest> Items);
public sealed record PlatformSettingItemRequest(string Key, string? Value);
public sealed record MetricThresholdsUpdateRequest(IReadOnlyList<MetricThresholdItemRequest> Items);
public sealed record MetricThresholdItemRequest(string Metric, string Grade, decimal? MinValue, decimal? MaxValue);
public sealed record CreateBomLineRequest(
    int MaterialId,
    decimal QtyPerBatch,
    decimal TolerancePct,
    short SequenceNo,
    bool IsCritical
);

public sealed record UpdateBomLineRequest(
    int MaterialId,
    decimal QtyPerBatch,
    decimal TolerancePct,
    short SequenceNo,
    bool IsCritical
);


public abstract record MaterialRequestBase(
    int CustomerId,
    string Code,
    string Name,
    string MaterialType,
    string Uom,
    string? ErpCode,
    string? SupplierCode,
    decimal? ReorderPointKg,
    decimal? MinStockKg,
    bool IsActive
);

public sealed record CreateMaterialRequest(
    int CustomerId,
    string Code,
    string Name,
    string MaterialType,
    string Uom,
    string? ErpCode,
    string? SupplierCode,
    decimal? ReorderPointKg,
    decimal? MinStockKg,
    bool IsActive
) : MaterialRequestBase(
    CustomerId,
    Code,
    Name,
    MaterialType,
    Uom,
    ErpCode,
    SupplierCode,
    ReorderPointKg,
    MinStockKg,
    IsActive
);

public sealed record UpdateMaterialRequest(
    int CustomerId,
    string Code,
    string Name,
    string MaterialType,
    string Uom,
    string? ErpCode,
    string? SupplierCode,
    decimal? ReorderPointKg,
    decimal? MinStockKg,
    bool IsActive
) : MaterialRequestBase(
    CustomerId,
    Code,
    Name,
    MaterialType,
    Uom,
    ErpCode,
    SupplierCode,
    ReorderPointKg,
    MinStockKg,
    IsActive
);
public abstract record BillOfMaterialRequestBase(
    int SkuId,
    int MaterialId,
    decimal QtyPerBatch,
    decimal TolerancePct,
    short SequenceNo,
    bool IsCritical
);

public sealed record CreateBillOfMaterialRequest(
    int SkuId,
    int MaterialId,
    decimal QtyPerBatch,
    decimal TolerancePct,
    short SequenceNo,
    bool IsCritical
) : BillOfMaterialRequestBase(
    SkuId,
    MaterialId,
    QtyPerBatch,
    TolerancePct,
    SequenceNo,
    IsCritical
);

public sealed record UpdateBillOfMaterialRequest(
    int SkuId,
    int MaterialId,
    decimal QtyPerBatch,
    decimal TolerancePct,
    short SequenceNo,
    bool IsCritical
) : BillOfMaterialRequestBase(
    SkuId,
    MaterialId,
    QtyPerBatch,
    TolerancePct,
    SequenceNo,
    IsCritical
);

public sealed record SkuProcessParameterRequest(
    string Name,
    string Value,
    string? Unit = null,
    string DataType = "number");

public sealed record SkuRawMaterialRequest(
    int MaterialId,
    decimal QtyPerBatch,
    short SequenceNo = 1,
    bool IsCritical = false);

public abstract record SkuRequestBase(
    string Code,
    string Name,
    string? Category,
    string Uom,
    int StandardBatchSize,
    short? ShelfLifeMonths,
    int? RatedSpeedRpm,
    decimal? UnitWeightG,
    bool ErpSource,
    string? ErpMaterialCode,
    bool IsActive,
    IReadOnlyList<SkuProcessParameterRequest>? ProcessParameters,
    IReadOnlyList<SkuRawMaterialRequest>? RawMaterials);

public sealed record CreateSkuRequest(
    string Code,
    string Name,
    string? Category,
    string Uom,
    int StandardBatchSize,
    short? ShelfLifeMonths,
    int? RatedSpeedRpm,
    decimal? UnitWeightG,
    bool ErpSource,
    string? ErpMaterialCode,
    bool IsActive = true,
    IReadOnlyList<SkuProcessParameterRequest>? ProcessParameters = null,
    IReadOnlyList<SkuRawMaterialRequest>? RawMaterials = null)
    : SkuRequestBase(Code, Name, Category, Uom, StandardBatchSize,
        ShelfLifeMonths, RatedSpeedRpm, UnitWeightG, ErpSource, ErpMaterialCode,
        IsActive, ProcessParameters, RawMaterials);

public sealed record UpdateSkuRequest(
    string Code,
    string Name,
    string? Category,
    string Uom,
    int StandardBatchSize,
    short? ShelfLifeMonths,
    int? RatedSpeedRpm,
    decimal? UnitWeightG,
    bool ErpSource,
    string? ErpMaterialCode,
    bool IsActive = true,
    IReadOnlyList<SkuProcessParameterRequest>? ProcessParameters = null,
    IReadOnlyList<SkuRawMaterialRequest>? RawMaterials = null)
    : SkuRequestBase(Code, Name, Category, Uom, StandardBatchSize,
        ShelfLifeMonths, RatedSpeedRpm, UnitWeightG, ErpSource, ErpMaterialCode,
        IsActive, ProcessParameters, RawMaterials);
