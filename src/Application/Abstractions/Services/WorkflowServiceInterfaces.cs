using Application.Common.Workflow;
using Domain.Entities.Alarms;
using Domain.Entities.Configuration;
using Domain.Entities.Maintenance;
using Domain.Entities.Planning;
using Domain.Entities.Production;
using Domain.Entities.Scada;
using Application.Dtos.Maintenance;
using Application.Dtos.Alarms;
using Domain.Enums;

namespace Application.Abstractions.Services;

public interface IBatchWorkflowService
{
    Task<WorkflowResult<Batch>> CreateManualBatchAsync(int customerId, int skuId, int factoryId, int lineId, int plannedQty, DateTime plannedStart, DateTime? plannedEnd, int? shiftId, BatchPriority? priority, string? notes);
    Task<WorkflowResult<Batch>> StartBatchAsync(int id, string? notes);
    Task<WorkflowResult<Batch>> CompleteBatchAsync(int id, int producedQty, int rejectedQty, string? notes);
    Task<WorkflowResult<Batch>> AdvanceStageAsync(int id, string? notes);
    Task<WorkflowResult<IReadOnlyList<Batch>>> SplitBatchAsync(int id, int splitQty, string reason);
    Task<WorkflowResult<BatchMaterial>> RecordManualDispenseAsync(int batchId, int materialId, int scaleId, decimal dispensedQty, DateTime? timestamp);
    Task<string> GenerateBatchUidAsync(int customerId);
    Task<string> GetPreviewBatchUidAsync(int customerId);
}

public interface IRouteWorkflowService
{
    Task<WorkflowResult<IReadOnlyList<RouteStep>>> ReplaceStepsAsync(int routeId, IReadOnlyList<RouteStep> steps);
    Task<WorkflowResult<ProductionRoute>> UpdateStatusAsync(int routeId, string status);
}

public interface IConfigWorkflowService
{
    Task<WorkflowResult<MachineConfig>> UpsertMachineConfigAsync(int machineId, MachineConfig request);
    Task<WorkflowResult<IReadOnlyList<MetricThreshold>>> UpsertRatingThresholdsAsync(IReadOnlyList<(string Metric, string Grade, decimal? MinValue, decimal? MaxValue)> items);
    Task<WorkflowResult<IReadOnlyList<PlatformSetting>>> UpsertPlatformSettingsAsync(IReadOnlyList<(string Key, string? Value)> items);
}

public interface IMaintenanceWorkflowService
{
    Task<WorkflowResult<WorkOrder>> CreateWorkOrderAsync(CreateWorkOrderCommand command);
    Task<WorkflowResult<WorkOrder>> UpdateWorkOrderAsync(int id, int customerId, UpdateWorkOrderCommand command);
    Task<WorkflowResult<WorkOrder>> CloseWorkOrderAsync(int id, int customerId, string? notes);
    Task<WorkflowResult<WorkOrder>> StartWorkOrderAsync(int id, string? notes);
    Task<WorkflowResult<WorkOrder>> CompleteWorkOrderAsync(int id, string? resolutionNotes, string? failureMode, decimal? actualCost, IReadOnlyList<(int SparePartId, decimal Quantity)>? partsUsed);
    Task<WorkflowResult<WorkOrder>> CancelWorkOrderAsync(int id, string reason);
    Task<WorkflowResult<PmSchedule>> CompletePmScheduleAsync(int id, DateTime? completedAt, string? notes);
    Task<WorkflowResult<SparePart>> IssueSparePartAsync(int sparePartId, int workOrderId, decimal qtyUsed);
}

public interface IAlarmWorkflowService
{
    Task<WorkflowResult<AlarmRule>> ToggleRuleAsync(int id, bool isActive);
    Task<WorkflowResult<AlarmEvent>> AcknowledgeAsync(int id, int acknowledgedById, string? notes);
    Task<WorkflowResult<AlarmEvent>> ResolveAsync(int id, int acknowledgedById, string? notes, string? resolutionAction);
    Task<WorkflowResult<WorkOrder>> CreateWorkOrderFromAlarmAsync(int id, string? notes);
}

public interface IAlarmEvaluationService
{
    Task<WorkflowResult<AlarmEvaluationOutcome>> EvaluateAsync(
        int deviceId,
        string parameter,
        decimal value,
        DateTime recordedAt);
}

public interface IScadaManualWorkflowService
{
    Task<WorkflowResult<MachineState>> UpdateMachineStateManualAsync(int machineId, int state, string? notes);
    Task<WorkflowResult<Silo>> LoadSiloAsync(int siloId, int materialId, string? lotUid, decimal quantityKg, DateTime? timestamp);
    Task<WorkflowResult<Scale>> TareScaleAsync(int scaleId);
    Task<WorkflowResult<Scale>> AssignScaleToBatchAsync(int scaleId, int batchId, int materialId);
    Task<WorkflowResult<Scale>> ReleaseScaleAsync(int scaleId);
}
