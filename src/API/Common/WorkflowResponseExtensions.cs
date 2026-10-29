using Application.Common.Workflow;
using Application.Dtos.Frontend;
using Domain.Entities.Alarms;
using Domain.Entities.Configuration;
using Domain.Entities.Maintenance;
using Domain.Entities.Planning;
using Domain.Entities.Production;
using Domain.Entities.Scada;
using Microsoft.AspNetCore.Mvc;

namespace API.Common;

public static class WorkflowResponseExtensions
{
    public static IActionResult ToActionResult<T>(this ControllerBase controller, WorkflowResult<T> result)
    {
        if (result.Succeeded)
        {
            var dto = new UiActionResultDto(
                Type: "Type B/C Workflow Action",
                UseCase: ResolveUseCase(result.Data),
                Message: result.Message ?? "Workflow completed successfully.",
                Data: ToFrontendActionData(result.Data),
                FutureNote: ResolveFutureNote(result.Data));

            return controller.Ok(ApiResponse<UiActionResultDto>.Ok(dto));
        }

        var response = ApiResponse<UiActionResultDto>.Fail(result.ErrorCode ?? "workflow_error", result.Message ?? "Workflow operation failed.");
        var code = result.ErrorCode?.ToLowerInvariant();
        return code switch
        {
            "not_found" or "machine_not_found" or "scale_not_found" or "batch_not_found" or "material_not_found" or "work_order_not_found" => controller.NotFound(response),
            _ => controller.BadRequest(response)
        };
    }

    private static string ResolveUseCase<T>(T? data)
    {
        return data switch
        {
            Batch => "UC-B05/UC-B06/UC-B11/UC-B12 depending on called endpoint",
            IReadOnlyList<Batch> => "UC-B11 Split Batch",
            BatchMaterial => "UC-B07/UC-B08/UC-B10 Dispense and deviation check",
            WorkOrder => "Maintenance workflow / UC-V04 support",
            PmSchedule => "UC-MD11 PM Schedule workflow",
            SparePart => "UC-MD10 Spare Part issue workflow",
            AlarmEvent => "UC-T06/UC-B10 Alarm workflow",
            AlarmRule => "UC-MD09 Alarm Rule workflow",
            MachineConfig => "UC-S04 Machine Parameters",
            IReadOnlyList<MetricThreshold> => "UC-S03 Rating Thresholds",
            IReadOnlyList<PlatformSetting> => "UC-S01 Platform Settings",
            Silo => "UC-MF01 Receive Raw Material into Silo",
            MachineState => "UC-T05 SCADA State manual fallback",
            Scale => "UC-MF06 Scale manual fallback",
            _ => "Mapped workflow use case"
        };
    }

    private static string ResolveFutureNote<T>(T? data)
    {
        return data switch
        {
            BatchMaterial => "Current path uses manual dispensed quantity. Future path replaces it with latest stable scale telemetry reading.",
            MachineState => "Current path is manual state update. Future UC-T05 updates state from telemetry tags.",
            Scale => "Current path is manual assignment/tare. Future UC-MF06 reads stable scale telemetry.",
            AlarmEvent => "Current path updates alarm rows. Future UC-T06 creates/clears alarms from AlarmEvaluator.",
            _ => "DTO action response. Refresh the related /v1/ui/* page endpoint after successful action."
        };
    }

    private static object? ToFrontendActionData<T>(T? data)
    {
        return data switch
        {
            null => null,
            Batch b => new { b.Id, b.Uid, b.Status, b.Stage, b.PlannedQty, b.ProducedQty, b.RejectedQty, b.ActualStart, b.ActualEnd, b.Priority, refreshEndpoint = $"/v1/ui/batches/{b.Id}/detail" },
            IReadOnlyList<Batch> list => list.Select(b => new { b.Id, b.Uid, b.Status, b.Stage, b.PlannedQty, b.ParentBatchId }).ToArray(),
            BatchMaterial bm => new { bm.Id, bm.BatchId, bm.MaterialId, bm.ScaleId, bm.PlannedQty, bm.DispensedQty, DeviationQty = bm.DispensedQty > 0 ? Math.Abs(bm.DispensedQty - bm.PlannedQty) : (decimal?)null, bm.DeviationPct, bm.Status, refreshEndpoint = $"/v1/ui/batches/{bm.BatchId}/detail" },
            WorkOrder wo => new { wo.Id, wo.Number, wo.Status, wo.WorkOrderType, wo.Priority, wo.ActualStart, wo.ActualEnd, wo.FailureMode, wo.ResolutionNotes, refreshEndpoint = "/v1/ui/maintenance" },
            PmSchedule pm => new { pm.Id, pm.TaskName, pm.LastDoneAt, pm.NextDueAt, refreshEndpoint = "/v1/ui/maintenance" },
            SparePart sp => new { sp.Id, sp.Code, sp.Name, sp.QtyOnHand, sp.MinStock, stockStatus = sp.StockStatus, refreshEndpoint = "/v1/ui/maintenance" },
            AlarmEvent alarm => new { alarm.Id, alarm.Title, alarm.Severity, alarm.TriggeredAt, alarm.AcknowledgedAt, alarm.ResolvedAt, alarm.Notes, refreshEndpoint = "/v1/ui/alarms" },
            AlarmRule rule => new { rule.Id, rule.Name, rule.IsActive, rule.Severity, refreshEndpoint = "/v1/ui/alarms" },
            MachineConfig cfg => new { cfg.MachineId, cfg.RatedSpeedRpm, cfg.MinSpeedRpm, cfg.MaxTempCelsius, cfg.MaxVibrationMms, cfg.RatedPowerKw, refreshEndpoint = "/v1/ui/configuration" },
            IReadOnlyList<MetricThreshold> thresholds => thresholds.Select(t => new { t.Id, t.Metric, t.Grade, t.MinValue, t.MaxValue }).ToArray(),
            IReadOnlyList<PlatformSetting> settings => settings.Select(s => new { s.Id, s.Key, s.Value }).ToArray(),
            Silo silo => new { silo.Id, silo.Code, silo.Name, silo.CurrentQtyKg, silo.CapacityKg, silo.CurrentMaterialId, refreshEndpoint = $"/v1/scada/warehouse-zone?factoryId={silo.FactoryId}" },
            MachineState state => new { state.Id, state.MachineId, state.CustomerId, state.FactoryId, state.LineId, state.State, state.Speed, state.Count, state.CountDiff, state.QReject, state.QRejectDiff, state.Timestamp, refreshEndpoint = $"/v1/scada/production-zone?factoryId={state.FactoryId}" },
            Scale scale => new { scale.Id, scale.Code, scale.Name, scale.ScaleState, scale.WeightKg, scale.TareKg, scale.ActiveBatchId, scale.ActiveMaterialId, refreshEndpoint = $"/v1/scada/dispensing-zone?factoryId={scale.FactoryId}" },
            _ => data
        };
    }
}
