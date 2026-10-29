using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Common.Workflow;
using Application.Dtos.Maintenance;
using Domain.Entities.Alarms;
using Domain.Entities.Configuration;
using Domain.Entities.Maintenance;
using Domain.Entities.Planning;
using Domain.Entities.Production;
using Domain.Entities.Scada;
using Domain.Enums;

namespace Application.Services;

public sealed class BatchWorkflowService : IBatchWorkflowService
{
    private const decimal DefaultDispenseTolerancePct = 2m;
    private readonly IUnitOfWork _uow;

    public BatchWorkflowService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<WorkflowResult<Batch>> CreateManualBatchAsync(
        int customerId, int skuId, int factoryId, int lineId, int plannedQty, DateTime plannedStart,
        DateTime? plannedEnd, int? shiftId, BatchPriority? priority, string? notes)
    {
        if (plannedQty <= 0)
            return WorkflowResult<Batch>.Fail("invalid_quantity", "Planned quantity must be greater than zero.");
        if (plannedEnd.HasValue && plannedEnd.Value <= plannedStart)
            return WorkflowResult<Batch>.Fail("invalid_date_range", "Planned end must be later than planned start.");
        var resolvedPriority = priority ?? BatchPriority.Normal;
        if (!Enum.IsDefined(resolvedPriority))
            return WorkflowResult<Batch>.Fail("invalid_priority", "Priority must be low, normal, or high.");

        var customer = await _uow.Customers.GetByIdAsync(customerId, asNoTracking: true);
        if (customer is null || !customer.IsActive || customer.IsDeleted)
            return WorkflowResult<Batch>.Fail("customer_not_found", "The authenticated customer was not found or is inactive.");
        var sku = await _uow.Skus.GetByIdAsync(skuId, asNoTracking: true);
        if (sku is null || sku.CustomerId != customerId || !sku.IsActive)
            return WorkflowResult<Batch>.Fail("invalid_sku", "SKU was not found, is inactive, or does not belong to the customer.");
        var factory = await _uow.Factories.GetByIdAsync(factoryId, asNoTracking: true);
        if (factory is null || factory.CustomerId != customerId || !factory.IsActive || factory.IsDeleted)
            return WorkflowResult<Batch>.Fail("invalid_factory", "Factory was not found, is inactive, or does not belong to the authenticated customer.");
        var line = await _uow.ProductionLines.GetByIdAsync(lineId, asNoTracking: true);
        if (line is null || line.FactoryId != factoryId || !line.IsActive || line.IsDeleted)
            return WorkflowResult<Batch>.Fail("invalid_line", "Production line was not found, is inactive, or does not belong to the selected factory.");
        if (shiftId.HasValue)
        {
            var shiftBelongsToFactory = _uow.Shifts.Query()
                .Any(x => x.Id == shiftId.Value &&
                          x.IsActive &&
                          !x.IsDeleted &&
                          (x.FactoryId == factoryId || x.FactoryLinks.Any(link => link.FactoryId == factoryId)));
            if (!shiftBelongsToFactory)
                return WorkflowResult<Batch>.Fail("invalid_shift", "Shift was not found, is inactive, or does not belong to the selected factory.");
        }

        var bom = _uow.BillOfMaterials.Query().Where(x => x.SkuId == skuId).OrderBy(x => x.SequenceNo).ToList();
        if (bom.Count == 0)
            return WorkflowResult<Batch>.Fail("bom_incomplete", "The selected SKU has no BOM lines.");

        var route = _uow.ProductionRoutes.Query()
            .Where(x => x.SkuId == skuId && x.Status == "active")
            .OrderByDescending(x => x.Version).FirstOrDefault();
        if (route is null)
            return WorkflowResult<Batch>.Fail("route_required", "The selected SKU has no active production route.");

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            var batch = new Batch
            {
                Uid = await GenerateBatchUidAsync(customerId),
                CustomerId = customerId,
                SkuId = skuId,
                LineId = lineId,
                RouteId = route.Id,
                Status = "planned",
                Stage = "warehouse",
                PlannedQty = plannedQty,
                PlannedStart = plannedStart,
                PlannedEnd = plannedEnd,
                ActualStart = null,
                ActualEnd = null,
                ShiftId = shiftId,
                Priority = resolvedPriority,
                Notes = notes
            };
            await _uow.Batches.AddAsync(batch);
            await _uow.SaveChangesAsync();

            var factor = sku.StandardBatchSize > 0 ? (decimal)plannedQty / sku.StandardBatchSize : 1m;
            foreach (var lineItem in bom)
            {
                await _uow.BatchMaterials.AddAsync(new BatchMaterial
                {
                    BatchId = batch.Id,
                    MaterialId = lineItem.MaterialId,
                    PlannedQty = Math.Round(lineItem.QtyPerBatch * factor, 4),
                    Status = "pending"
                });
            }
            await AddStageLogAsync(batch.Id, null, "warehouse", "Manual batch created.");
            await _uow.SaveChangesAsync();
            return WorkflowResult<Batch>.Ok(batch, "Manual batch created with UID and BOM lines.");
        });
    }

    public async Task<WorkflowResult<Batch>> StartBatchAsync(int id, string? notes)
    {
        var batch = await _uow.Batches.GetByIdAsync(id, asNoTracking: false);
        if (batch is null) return WorkflowResult<Batch>.Fail("not_found", $"Batch {id} was not found.");

        if (!IsStatus(batch.Status, "planned"))
            return WorkflowResult<Batch>.Fail("invalid_status", "Only planned batches can be started.");

        if (!batch.RouteId.HasValue)
            return WorkflowResult<Batch>.Fail("route_required", "An active route must be assigned before the batch can start.");
        var materials = _uow.BatchMaterials.Query().Where(x => x.BatchId == id).ToList();
        if (materials.Count == 0 || materials.Any(x => x.PlannedQty <= 0))
            return WorkflowResult<Batch>.Fail("bom_incomplete", "A complete BOM with positive planned quantities is required before start.");

        var anotherActiveBatchOnLine = _uow.Batches.Query()
            .Any(x => x.Id != id && x.LineId == batch.LineId && (x.Status == "in_progress" || x.Status == "production"));
        if (anotherActiveBatchOnLine)
            return WorkflowResult<Batch>.Fail("line_busy", "Another batch is already in progress on the same line.");

        // Type B workflow: this transition is business logic and is NOT blocked by real-time telemetry.
        var fromStage = batch.Stage;
        batch.Status = "in_progress";
        batch.Stage = string.IsNullOrWhiteSpace(batch.Stage) ? "warehouse" : batch.Stage;
        batch.ActualStart ??= DateTime.UtcNow;
        batch.Notes = AppendNote(batch.Notes, notes);

        await AddStageLogAsync(batch.Id, fromStage, batch.Stage, notes);
        _uow.Batches.Update(batch);
        await _uow.SaveChangesAsync();

        return WorkflowResult<Batch>.Ok(batch, "Batch started successfully.");
    }

    public async Task<WorkflowResult<Batch>> CompleteBatchAsync(int id, int producedQty, int rejectedQty, string? notes)
    {
        var batch = await _uow.Batches.GetByIdAsync(id, asNoTracking: false);
        if (batch is null) return WorkflowResult<Batch>.Fail("not_found", $"Batch {id} was not found.");
        if (IsStatus(batch.Status, "finished") || IsStatus(batch.Status, "cancelled") || IsStatus(batch.Status, "split"))
            return WorkflowResult<Batch>.Fail("invalid_status", "This batch cannot be completed from its current status.");
        if (!IsStatus(batch.Status, "production"))
            return WorkflowResult<Batch>.Fail("invalid_status", "Only production batches can be completed.");
        if (producedQty < 0 || rejectedQty < 0)
            return WorkflowResult<Batch>.Fail("invalid_quantity", "Produced and rejected quantities cannot be negative.");

        // Type B workflow with future Type C input: produced/rejected can later be read from machine telemetry.
        var fromStage = batch.Stage;
        batch.Status = "finished";
        batch.Stage = "finished";
        batch.ProducedQty = producedQty;
        batch.RejectedQty = rejectedQty;
        batch.ActualEnd = DateTime.UtcNow;
        batch.Notes = AppendNote(batch.Notes, notes);

        await AddStageLogAsync(batch.Id, fromStage, "finished", notes);
        _uow.Batches.Update(batch);
        await _uow.SaveChangesAsync();

        return WorkflowResult<Batch>.Ok(batch, "Batch completed successfully.");
    }

    public async Task<WorkflowResult<Batch>> AdvanceStageAsync(int id, string? notes)
    {
        var batch = await _uow.Batches.GetByIdAsync(id, asNoTracking: false);
        if (batch is null) return WorkflowResult<Batch>.Fail("not_found", $"Batch {id} was not found.");
        if (!IsStatus(batch.Status, "in_progress") && !IsStatus(batch.Status, "production"))
            return WorkflowResult<Batch>.Fail("invalid_status", "Only in-progress or production batches can advance stage.");

        var fromStage = string.IsNullOrWhiteSpace(batch.Stage) ? "warehouse" : batch.Stage;
        var nextStage = NextStage(fromStage);
        if (nextStage is null)
            return WorkflowResult<Batch>.Fail("invalid_stage", "Batch is already at final stage. Use complete endpoint to finish it.");
        if (IsStage(nextStage, "finished"))
            return WorkflowResult<Batch>.Fail("invalid_status", "Use complete endpoint to finish the batch.");

        if (IsStage(fromStage, "dispense"))
        {
            var incomplete = _uow.BatchMaterials.Query()
                .Any(x => x.BatchId == id &&
                          x.Status != "complete" &&
                          x.Status != "completed");
            if (incomplete)
                return WorkflowResult<Batch>.Fail("dispense_incomplete", "All BOM lines must be dispensed within tolerance before production.");
        }

        // Type B workflow: stage transitions are controlled by application logic, not SQL view or real-time feed.
        batch.Stage = nextStage;
        if (IsStage(nextStage, "production"))
            batch.Status = "production";
        batch.Notes = AppendNote(batch.Notes, notes);

        await AddStageLogAsync(batch.Id, fromStage, nextStage, notes);
        _uow.Batches.Update(batch);
        await _uow.SaveChangesAsync();

        return WorkflowResult<Batch>.Ok(batch, $"Batch advanced from {fromStage} to {nextStage}.");
    }

    public async Task<WorkflowResult<IReadOnlyList<Batch>>> SplitBatchAsync(int id, int splitQty, string reason)
    {
        var batch = await _uow.Batches.GetByIdAsync(id, asNoTracking: false);
        if (batch is null) return WorkflowResult<IReadOnlyList<Batch>>.Fail("not_found", $"Batch {id} was not found.");
        if (string.IsNullOrWhiteSpace(reason)) return WorkflowResult<IReadOnlyList<Batch>>.Fail("reason_required", "Split reason is required.");
        if (!batch.PlannedQty.HasValue)
            return WorkflowResult<IReadOnlyList<Batch>>.Fail("planning_required", "A planned quantity is required before splitting a batch.");
        var parentPlannedQty = batch.PlannedQty.Value;
        if (splitQty <= 0 || splitQty >= parentPlannedQty)
            return WorkflowResult<IReadOnlyList<Batch>>.Fail("invalid_quantity", "Split quantity must be greater than zero and less than the planned quantity.");
        if (IsStatus(batch.Status, "finished") || IsStatus(batch.Status, "cancelled") || IsStatus(batch.Status, "split"))
            return WorkflowResult<IReadOnlyList<Batch>>.Fail("invalid_status", "Finished, cancelled, or already split batches cannot be split.");
        if (IsStage(batch.Stage, "production") || IsStage(batch.Stage, "finished"))
            return WorkflowResult<IReadOnlyList<Batch>>.Fail("invalid_stage", "Split is only allowed before production stage.");

        return await _uow.ExecuteInTransactionAsync(async () =>
        {
            var childA = CloneChildBatch(batch, splitQty, reason);
            var childB = CloneChildBatch(batch, parentPlannedQty - splitQty, reason);
            childA.Uid = await GenerateBatchUidAsync(batch.CustomerId);
            childB.Uid = await GenerateBatchUidAsync(batch.CustomerId);

            batch.Status = "split";
            batch.SplitReason = reason;
            batch.Notes = AppendNote(batch.Notes, reason);

            _uow.Batches.Update(batch);
            await _uow.Batches.AddAsync(childA);
            await _uow.Batches.AddAsync(childB);
            await _uow.SaveChangesAsync();

            var parentMaterials = _uow.BatchMaterials.Query().Where(x => x.BatchId == batch.Id).ToList();
            foreach (var material in parentMaterials)
            {
                await _uow.BatchMaterials.AddAsync(CloneChildMaterial(material, childA.Id, splitQty, parentPlannedQty));
                await _uow.BatchMaterials.AddAsync(CloneChildMaterial(material, childB.Id, parentPlannedQty - splitQty, parentPlannedQty));
            }
            await _uow.SaveChangesAsync();

            return WorkflowResult<IReadOnlyList<Batch>>.Ok(new[] { childA, childB }, "Batch split successfully.");
        });
    }

    public async Task<WorkflowResult<BatchMaterial>> RecordManualDispenseAsync(int batchId, int materialId, int scaleId, decimal dispensedQty, DateTime? timestamp)
    {
        var batch = await _uow.Batches.GetByIdAsync(batchId, asNoTracking: true);
        if (batch is null) return WorkflowResult<BatchMaterial>.Fail("not_found", $"Batch {batchId} was not found.");
        if (IsStatus(batch.Status, "finished") || IsStatus(batch.Status, "cancelled") || IsStatus(batch.Status, "split"))
            return WorkflowResult<BatchMaterial>.Fail("invalid_status", "Cannot dispense materials for a closed batch.");
        if (!IsStatus(batch.Status, "in_progress") || !IsStage(batch.Stage, "dispense"))
            return WorkflowResult<BatchMaterial>.Fail("invalid_stage", "Material can only be dispensed while the batch is in the dispense stage.");

        // Endpoint route uses materialId. If the frontend sends the BatchMaterial.Id by mistake, the second condition still handles it.
        var batchMaterial = _uow.BatchMaterials.Query(asNoTracking: false)
            .FirstOrDefault(x => x.BatchId == batchId && (x.MaterialId == materialId || x.Id == materialId));
        if (batchMaterial is null)
            return WorkflowResult<BatchMaterial>.Fail("not_found", "Batch material line was not found.");
        if (batchMaterial.PlannedQty <= 0)
            return WorkflowResult<BatchMaterial>.Fail("invalid_planned_qty", "Planned quantity must be greater than zero before dispensing.");
        if (dispensedQty <= 0)
            return WorkflowResult<BatchMaterial>.Fail("invalid_dispense_qty", "Dispensed quantity must be greater than zero.");

        var scale = await _uow.Scales.GetByIdAsync(scaleId, asNoTracking: false);
        if (scale is null) return WorkflowResult<BatchMaterial>.Fail("scale_not_found", $"Scale {scaleId} was not found.");
        if (scale.CalibratedAt is null || string.IsNullOrWhiteSpace(scale.CalibrationCert))
            return WorkflowResult<BatchMaterial>.Fail("scale_not_calibrated", "Scale calibration date and certificate are required.");

        // Type C hybrid manual fallback: today we accept DispensedQty from request.
        // Future real-time path: replace dispensedQty with the latest stable scale telemetry reading.
        var deviation = Math.Abs(dispensedQty - batchMaterial.PlannedQty) / batchMaterial.PlannedQty * 100m;
        var tolerance = _uow.BillOfMaterials.Query()
            .Where(x => x.SkuId == batch.SkuId && x.MaterialId == batchMaterial.MaterialId)
            .Select(x => (decimal?)x.TolerancePct).FirstOrDefault() ?? DefaultDispenseTolerancePct;
        var isWithinTolerance = deviation <= tolerance;
        var completedAt = timestamp ?? DateTime.UtcNow;

        batchMaterial.ScaleId = scaleId;
        batchMaterial.DispensedQty = dispensedQty;
        batchMaterial.DeviationPct = Math.Round(deviation, 4);
        batchMaterial.DispenseStartedAt ??= completedAt;
        batchMaterial.DispenseCompletedAt = completedAt;
        batchMaterial.Status = isWithinTolerance ? "complete" : "pending";

        scale.ActiveBatchId = batchId;
        scale.ActiveMaterialId = batchMaterial.MaterialId;
        scale.WeightKg = dispensedQty;
        scale.ScaleState = "assigned";
        scale.TelemetryAt = completedAt;

        _uow.BatchMaterials.Update(batchMaterial);
        _uow.Scales.Update(scale);

        if (!isWithinTolerance)
        {
            await _uow.AlarmEvents.AddAsync(new AlarmEvent
            {
                CustomerId = batch.CustomerId,
                Title = "Dispense deviation exceeded tolerance",
                Severity = AlarmSeverity.High,
                TriggerValue = batchMaterial.DeviationPct,
                TriggeredAt = completedAt,
                Notes = $"Batch {batch.Uid}: planned={batchMaterial.PlannedQty}, actual={dispensedQty}, tolerance={tolerance}%"
            });
        }

        await _uow.SaveChangesAsync();
        return WorkflowResult<BatchMaterial>.Ok(batchMaterial, isWithinTolerance ? "Dispense recorded within tolerance." : "Dispense recorded with deviation alarm.");
    }

    private async Task AddStageLogAsync(int batchId, string? fromStage, string toStage, string? notes)
    {
        await _uow.BatchStageLogs.AddAsync(new BatchStageLog
        {
            BatchId = batchId,
            FromStage = fromStage,
            ToStage = toStage,
            TransitionedAt = DateTime.UtcNow,
            Notes = notes
        });
    }

    public async Task<string> GenerateBatchUidAsync(int customerId)
    {
        var yearMonth = DateTime.UtcNow.ToString("yyMM");
        var sequence = _uow.BatchUidSequences.Query(asNoTracking: false)
            .FirstOrDefault(x => x.CustomerId == customerId && x.Prefix == "BT" && x.YearMonth == yearMonth);

        if (sequence is null)
        {
            sequence = new BatchUidSequence { CustomerId = customerId, Prefix = "BT", YearMonth = yearMonth, LastSeq = 0 };
            await _uow.BatchUidSequences.AddAsync(sequence);
        }

        sequence.LastSeq += 1;
        await _uow.SaveChangesAsync();

        var customer = await _uow.Customers.GetByIdAsync(customerId, asNoTracking: true);
        var country = customer?.CountryCode ?? "EGY";
        return $"BT-{yearMonth}-{sequence.LastSeq:00000}-{country}";
    }

    public async Task<string> GetPreviewBatchUidAsync(int customerId)
    {
        var yearMonth = DateTime.UtcNow.ToString("yyMM");
        var sequence = _uow.BatchUidSequences.Query(asNoTracking: true)
            .FirstOrDefault(x => x.CustomerId == customerId && x.Prefix == "BT" && x.YearMonth == yearMonth);

        var nextSeq = (sequence?.LastSeq ?? 0) + 1;
        var customer = await _uow.Customers.GetByIdAsync(customerId, asNoTracking: true);
        var country = customer?.CountryCode ?? "EGY";
        return $"BT-{yearMonth}-{nextSeq:00000}-{country}";
    }

    private static Batch CloneChildBatch(Batch parent, int plannedQty, string reason)
        => new()
        {
            CustomerId = parent.CustomerId,
            SkuId = parent.SkuId,
            LineId = parent.LineId,
            RouteId = parent.RouteId,
            Status = parent.Status,
            Stage = parent.Stage,
            PlannedQty = plannedQty,
            ProducedQty = 0,
            RejectedQty = 0,
            PlannedStart = parent.PlannedStart,
            PlannedEnd = parent.PlannedEnd,
            ShiftId = parent.ShiftId,
            Priority = parent.Priority,
            ParentBatchId = parent.Id,
            SplitReason = reason,
            CreatedById = parent.CreatedById,
            Notes = $"Child batch created from {parent.Uid}. Reason: {reason}"
        };

    private static BatchMaterial CloneChildMaterial(BatchMaterial source, int childBatchId, int childQty, int parentQty)
        => new()
        {
            BatchId = childBatchId,
            MaterialId = source.MaterialId,
            MaterialUid = source.MaterialUid,
            PlannedQty = parentQty <= 0 ? 0 : Math.Round(source.PlannedQty * childQty / parentQty, 4),
            Status = "pending"
        };

    private static string? NextStage(string current)
        => current.Trim().ToLowerInvariant() switch
        {
            "warehouse" => "dispense",
            "dispense" => "production",
            "dispensing" => "production",
            "production" => "finished",
            _ => null
        };

    private static bool IsStatus(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    private static bool IsStage(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    private static bool IsWorkOrderStatus(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static string? AppendNote(string? oldNotes, string? newNote)
        => string.IsNullOrWhiteSpace(newNote) ? oldNotes : string.IsNullOrWhiteSpace(oldNotes) ? newNote : oldNotes + Environment.NewLine + newNote;
}

public sealed class RouteWorkflowService : IRouteWorkflowService
{
    private readonly IUnitOfWork _uow;
    public RouteWorkflowService(IUnitOfWork uow) => _uow = uow;

    public async Task<WorkflowResult<IReadOnlyList<RouteStep>>> ReplaceStepsAsync(int routeId, IReadOnlyList<RouteStep> steps)
    {
        var route = await _uow.ProductionRoutes.GetByIdAsync(routeId, asNoTracking: false);
        if (route is null) return WorkflowResult<IReadOnlyList<RouteStep>>.Fail("not_found", $"Route {routeId} was not found.");
        if (!string.Equals(route.Status, "draft", StringComparison.OrdinalIgnoreCase))
            return WorkflowResult<IReadOnlyList<RouteStep>>.Fail("invalid_status", "Route steps can only be replaced while route is draft.");
        if (steps.Count == 0) return WorkflowResult<IReadOnlyList<RouteStep>>.Fail("steps_required", "At least one route step is required.");
        if (steps.GroupBy(x => x.StepNo).Any(g => g.Count() > 1))
            return WorkflowResult<IReadOnlyList<RouteStep>>.Fail("duplicate_sequence", "Route step numbers must be unique.");

        var oldSteps = _uow.RouteSteps.Query(asNoTracking: false).Where(x => x.RouteId == routeId).ToList();
        _uow.RouteSteps.RemoveRange(oldSteps);

        foreach (var step in steps.OrderBy(x => x.StepNo))
        {
            step.Id = 0;
            step.RouteId = routeId;
            await _uow.RouteSteps.AddAsync(step);
        }

        await _uow.SaveChangesAsync();
        return WorkflowResult<IReadOnlyList<RouteStep>>.Ok(steps.OrderBy(x => x.StepNo).ToList(), "Route steps replaced successfully.");
    }

    public async Task<WorkflowResult<ProductionRoute>> UpdateStatusAsync(int routeId, string status)
    {
        var route = await _uow.ProductionRoutes.GetByIdAsync(routeId, asNoTracking: false);
        if (route is null) return WorkflowResult<ProductionRoute>.Fail("not_found", $"Route {routeId} was not found.");

        var normalized = status.Trim().ToLowerInvariant();
        if (normalized is not ("draft" or "active" or "archived"))
            return WorkflowResult<ProductionRoute>.Fail("invalid_status", "Allowed route statuses are draft, active, archived.");

        if (normalized == "active")
        {
            var hasSteps = _uow.RouteSteps.Query().Any(x => x.RouteId == routeId);
            if (!hasSteps) return WorkflowResult<ProductionRoute>.Fail("route_incomplete", "Route cannot be activated without steps.");

            var anotherActive = _uow.ProductionRoutes.Query(asNoTracking: false)
                .Where(x => x.Id != routeId && x.SkuId == route.SkuId && x.Status == "active")
                .ToList();
            foreach (var activeRoute in anotherActive)
            {
                activeRoute.Status = "archived";
                _uow.ProductionRoutes.Update(activeRoute);
            }
        }

        if (normalized == "archived")
        {
            var usedByActiveBatch = _uow.Batches.Query()
                .Any(x => x.RouteId == routeId && x.Status == "in_progress");
            if (usedByActiveBatch)
                return WorkflowResult<ProductionRoute>.Fail("route_in_use", "Cannot archive a route used by an in-progress batch.");
        }

        route.Status = normalized;
        _uow.ProductionRoutes.Update(route);
        await _uow.SaveChangesAsync();
        return WorkflowResult<ProductionRoute>.Ok(route, "Route status updated successfully.");
    }
}

public sealed class ConfigWorkflowService : IConfigWorkflowService
{
    private readonly IUnitOfWork _uow;
    public ConfigWorkflowService(IUnitOfWork uow) => _uow = uow;

    public async Task<WorkflowResult<MachineConfig>> UpsertMachineConfigAsync(int machineId, MachineConfig request)
    {
        if (request.MinSpeedRpm.HasValue && request.RatedSpeedRpm.HasValue && request.MinSpeedRpm > request.RatedSpeedRpm)
            return WorkflowResult<MachineConfig>.Fail("invalid_speed", "Min speed cannot be greater than rated speed.");
        if (request.MaxTempCelsius is < 0 || request.MaxVibrationMms is < 0 || request.RatedPowerKw is < 0)
            return WorkflowResult<MachineConfig>.Fail("invalid_limits", "Machine parameter limits cannot be negative.");

        var machineExists = await _uow.Machines.AnyAsync(x => x.Id == machineId);
        if (!machineExists) return WorkflowResult<MachineConfig>.Fail("machine_not_found", $"Machine {machineId} was not found.");

        var config = _uow.MachineConfigs.Query(asNoTracking: false).FirstOrDefault(x => x.MachineId == machineId);
        if (config is null)
        {
            config = new MachineConfig { MachineId = machineId };
            await _uow.MachineConfigs.AddAsync(config);
        }

        config.RatedSpeedRpm = request.RatedSpeedRpm;
        config.MinSpeedRpm = request.MinSpeedRpm;
        config.MaxTempCelsius = request.MaxTempCelsius;
        config.MaxVibrationMms = request.MaxVibrationMms;
        config.RatedPowerKw = request.RatedPowerKw;
        _uow.MachineConfigs.Update(config);
        await _uow.SaveChangesAsync();
        return WorkflowResult<MachineConfig>.Ok(config, "Machine configuration saved.");
    }

    public async Task<WorkflowResult<IReadOnlyList<MetricThreshold>>> UpsertRatingThresholdsAsync(IReadOnlyList<(string Metric, string Grade, decimal? MinValue, decimal? MaxValue)> items)
    {
        if (items.Count == 0) return WorkflowResult<IReadOnlyList<MetricThreshold>>.Fail("items_required", "At least one threshold item is required.");
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Metric) || string.IsNullOrWhiteSpace(item.Grade))
                return WorkflowResult<IReadOnlyList<MetricThreshold>>.Fail("invalid_item", "Metric and grade are required.");
            if (item.MinValue.HasValue && item.MaxValue.HasValue && item.MinValue > item.MaxValue)
                return WorkflowResult<IReadOnlyList<MetricThreshold>>.Fail("invalid_range", "Threshold minimum cannot be greater than maximum.");
            if ((item.MinValue is < 0 or > 100) || (item.MaxValue is < 0 or > 100))
                return WorkflowResult<IReadOnlyList<MetricThreshold>>.Fail("invalid_range", "Threshold values must be within 0-100.");
        }

        var customerId = ResolveDefaultCustomerId();
        var saved = new List<MetricThreshold>();
        foreach (var item in items)
        {
            var threshold = _uow.MetricThresholds.Query(asNoTracking: false)
                .FirstOrDefault(x => x.CustomerId == customerId && x.Metric == item.Metric && x.Grade == item.Grade);
            if (threshold is null)
            {
                threshold = new MetricThreshold { CustomerId = customerId, Metric = item.Metric, Grade = item.Grade };
                await _uow.MetricThresholds.AddAsync(threshold);
            }

            threshold.MinValue = item.MinValue;
            threshold.MaxValue = item.MaxValue;
            _uow.MetricThresholds.Update(threshold);
            saved.Add(threshold);
        }

        await _uow.SaveChangesAsync();
        return WorkflowResult<IReadOnlyList<MetricThreshold>>.Ok(saved, "Rating thresholds saved.");
    }

    public async Task<WorkflowResult<IReadOnlyList<PlatformSetting>>> UpsertPlatformSettingsAsync(IReadOnlyList<(string Key, string? Value)> items)
    {
        if (items.Count == 0) return WorkflowResult<IReadOnlyList<PlatformSetting>>.Fail("items_required", "At least one setting item is required.");
        if (items.Any(x => string.IsNullOrWhiteSpace(x.Key)))
            return WorkflowResult<IReadOnlyList<PlatformSetting>>.Fail("invalid_key", "Setting key is required.");

        var customerId = ResolveDefaultCustomerId();
        var saved = new List<PlatformSetting>();
        foreach (var item in items)
        {
            var setting = _uow.PlatformSettings.Query(asNoTracking: false)
                .FirstOrDefault(x => x.CustomerId == customerId && x.Key == item.Key);
            if (setting is null)
            {
                setting = new PlatformSetting { CustomerId = customerId, Key = item.Key };
                await _uow.PlatformSettings.AddAsync(setting);
            }

            setting.Value = item.Value;
            _uow.PlatformSettings.Update(setting);
            saved.Add(setting);
        }

        await _uow.SaveChangesAsync();
        return WorkflowResult<IReadOnlyList<PlatformSetting>>.Ok(saved, "Platform settings saved.");
    }

    private int ResolveDefaultCustomerId()
    {
        var id = _uow.Customers.Query().OrderBy(x => x.Id).Select(x => x.Id).FirstOrDefault();
        return id > 0 ? id : 1;
    }
}

public sealed class MaintenanceWorkflowService : IMaintenanceWorkflowService
{
    private readonly IUnitOfWork _uow;
    public MaintenanceWorkflowService(IUnitOfWork uow) => _uow = uow;

    public async Task<WorkflowResult<WorkOrder>> CreateWorkOrderAsync(CreateWorkOrderCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Description))
            return WorkflowResult<WorkOrder>.Fail("description_required", "Work order description is required.");

        if (command.AlarmEventId.HasValue)
        {
            var alarm = await _uow.AlarmEvents.GetByIdAsync(command.AlarmEventId.Value);
            if (alarm is null || alarm.CustomerId != command.CustomerId)
                return WorkflowResult<WorkOrder>.Fail("alarm_not_found", "The selected alarm was not found.");

            var duplicate = await _uow.WorkOrders.AnyAsync(x =>
                x.CustomerId == command.CustomerId &&
                x.AlarmEventId == command.AlarmEventId.Value &&
                x.Status != WorkOrderStatus.Cancelled);
            if (duplicate)
                return WorkflowResult<WorkOrder>.Fail("alarm_work_order_exists", "A work order already exists for this alarm.");
        }

        var location = ResolveWorkOrderLocation(
            command.CustomerId,
            command.MachineId,
            command.AlarmEventId);
        if (!location.Succeeded)
            return WorkflowResult<WorkOrder>.Fail(location.ErrorCode!, location.Message!);

        var workOrder = new WorkOrder
        {
            Number = CreateWorkOrderNumber(),
            CustomerId = command.CustomerId,
            FactoryId = location.FactoryId,
            WorkOrderType = command.WorkOrderType,
            Priority = command.Priority,
            MachineId = location.MachineId,
            AlarmEventId = command.AlarmEventId,
            Description = command.Description.Trim(),
            AssignedToId = command.AssignedToId,
            PlannedStart = command.PlannedStart,
            Status = WorkOrderStatus.Open,
            CreatedById = command.CreatedById
        };

        await _uow.WorkOrders.AddAsync(workOrder);
        await _uow.SaveChangesAsync();
        return WorkflowResult<WorkOrder>.Ok(workOrder, "Work order created.");
    }

    public async Task<WorkflowResult<WorkOrder>> UpdateWorkOrderAsync(
        int id,
        int customerId,
        UpdateWorkOrderCommand command)
    {
        var workOrder = await _uow.WorkOrders.GetByIdAsync(id, asNoTracking: false);
        if (workOrder is null || workOrder.CustomerId != customerId)
            return WorkflowResult<WorkOrder>.Fail("not_found", $"Work order {id} was not found.");
        if (IsClosed(workOrder.Status))
            return WorkflowResult<WorkOrder>.Fail("invalid_status", "Closed work orders cannot be edited.");
        if (string.IsNullOrWhiteSpace(command.Description))
            return WorkflowResult<WorkOrder>.Fail("description_required", "Work order description is required.");

        var location = ResolveWorkOrderLocation(
            customerId,
            command.MachineId,
            workOrder.AlarmEventId);
        if (!location.Succeeded)
            return WorkflowResult<WorkOrder>.Fail(location.ErrorCode!, location.Message!);

        workOrder.FactoryId = location.FactoryId;
        workOrder.MachineId = location.MachineId;
        workOrder.WorkOrderType = command.WorkOrderType;
        workOrder.Priority = command.Priority;
        workOrder.AssignedToId = command.AssignedToId;
        workOrder.PlannedStart = command.PlannedStart;
        workOrder.Description = command.Description.Trim();

        _uow.WorkOrders.Update(workOrder);
        await _uow.SaveChangesAsync();
        return WorkflowResult<WorkOrder>.Ok(workOrder, "Work order updated.");
    }

    public async Task<WorkflowResult<WorkOrder>> CloseWorkOrderAsync(int id, int customerId, string? notes)
    {
        var workOrder = await _uow.WorkOrders.GetByIdAsync(id, asNoTracking: false);
        if (workOrder is null || workOrder.CustomerId != customerId)
            return WorkflowResult<WorkOrder>.Fail("not_found", $"Work order {id} was not found.");
        if (IsClosed(workOrder.Status))
            return WorkflowResult<WorkOrder>.Fail("invalid_status", "Work order is already closed.");

        workOrder.Status = WorkOrderStatus.Closed;
        workOrder.ActualEnd ??= DateTime.UtcNow;
        workOrder.ResolutionNotes = AppendNote(workOrder.ResolutionNotes, notes);
        _uow.WorkOrders.Update(workOrder);
        await _uow.SaveChangesAsync();
        return WorkflowResult<WorkOrder>.Ok(workOrder, "Work order closed.");
    }

    public async Task<WorkflowResult<WorkOrder>> StartWorkOrderAsync(int id, string? notes)
    {
        var wo = await _uow.WorkOrders.GetByIdAsync(id, asNoTracking: false);
        if (wo is null) return WorkflowResult<WorkOrder>.Fail("not_found", $"Work order {id} was not found.");
        if (wo.Status is not (WorkOrderStatus.Open or WorkOrderStatus.Planned)) return WorkflowResult<WorkOrder>.Fail("invalid_status", "Only open/planned work orders can be started.");
        wo.Status = WorkOrderStatus.InProgress;
        wo.ActualStart ??= DateTime.UtcNow;
        wo.ResolutionNotes = AppendNote(wo.ResolutionNotes, notes);
        _uow.WorkOrders.Update(wo);
        await _uow.SaveChangesAsync();
        return WorkflowResult<WorkOrder>.Ok(wo, "Work order started.");
    }

    public async Task<WorkflowResult<WorkOrder>> CompleteWorkOrderAsync(int id, string? resolutionNotes, string? failureMode, decimal? actualCost, IReadOnlyList<(int SparePartId, decimal Quantity)>? partsUsed)
    {
        var wo = await _uow.WorkOrders.GetByIdAsync(id, asNoTracking: false);
        if (wo is null) return WorkflowResult<WorkOrder>.Fail("not_found", $"Work order {id} was not found.");
        if (IsClosed(wo.Status)) return WorkflowResult<WorkOrder>.Fail("invalid_status", "Closed work orders cannot be completed again.");

        if (partsUsed is not null)
        {
            foreach (var part in partsUsed)
            {
                var issueResult = await IssueSparePartAsync(part.SparePartId, id, part.Quantity);
                if (!issueResult.Succeeded)
                    return WorkflowResult<WorkOrder>.Fail(issueResult.ErrorCode ?? "spare_part_issue_failed", issueResult.Message ?? "Could not issue spare part.");
            }
        }

        wo.Status = WorkOrderStatus.Resolved;
        wo.ActualEnd = DateTime.UtcNow;
        wo.ResolutionNotes = AppendNote(wo.ResolutionNotes, resolutionNotes);
        wo.FailureMode = failureMode;
        wo.ActualCost = actualCost;
        _uow.WorkOrders.Update(wo);
        await _uow.SaveChangesAsync();
        return WorkflowResult<WorkOrder>.Ok(wo, "Work order completed.");
    }

    public async Task<WorkflowResult<WorkOrder>> CancelWorkOrderAsync(int id, string reason)
    {
        var wo = await _uow.WorkOrders.GetByIdAsync(id, asNoTracking: false);
        if (wo is null) return WorkflowResult<WorkOrder>.Fail("not_found", $"Work order {id} was not found.");
        if (IsClosed(wo.Status)) return WorkflowResult<WorkOrder>.Fail("invalid_status", "Closed work orders cannot be cancelled.");
        if (string.IsNullOrWhiteSpace(reason)) return WorkflowResult<WorkOrder>.Fail("reason_required", "Cancel reason is required.");
        wo.Status = WorkOrderStatus.Cancelled;
        wo.ResolutionNotes = AppendNote(wo.ResolutionNotes, reason);
        wo.ActualEnd = DateTime.UtcNow;
        _uow.WorkOrders.Update(wo);
        await _uow.SaveChangesAsync();
        return WorkflowResult<WorkOrder>.Ok(wo, "Work order cancelled.");
    }

    public async Task<WorkflowResult<PmSchedule>> CompletePmScheduleAsync(int id, DateTime? completedAt, string? notes)
    {
        var pm = await _uow.PmSchedules.GetByIdAsync(id, asNoTracking: false);
        if (pm is null) return WorkflowResult<PmSchedule>.Fail("not_found", $"PM schedule {id} was not found.");
        var doneAt = completedAt ?? DateTime.UtcNow;
        pm.LastDoneAt = doneAt;
        pm.NextDueAt = doneAt.AddDays(pm.IntervalDays <= 0 ? 30 : pm.IntervalDays);
        pm.Instructions = AppendNote(pm.Instructions, notes);
        _uow.PmSchedules.Update(pm);
        await _uow.SaveChangesAsync();
        return WorkflowResult<PmSchedule>.Ok(pm, "PM schedule completed and next due date recalculated.");
    }

    public async Task<WorkflowResult<SparePart>> IssueSparePartAsync(int sparePartId, int workOrderId, decimal qtyUsed)
    {
        if (qtyUsed <= 0) return WorkflowResult<SparePart>.Fail("invalid_quantity", "Quantity used must be greater than zero.");
        var part = await _uow.SpareParts.GetByIdAsync(sparePartId, asNoTracking: false);
        if (part is null) return WorkflowResult<SparePart>.Fail("not_found", $"Spare part {sparePartId} was not found.");
        var woExists = await _uow.WorkOrders.AnyAsync(x => x.Id == workOrderId);
        if (!woExists) return WorkflowResult<SparePart>.Fail("work_order_not_found", $"Work order {workOrderId} was not found.");
        if (part.QtyOnHand < qtyUsed) return WorkflowResult<SparePart>.Fail("insufficient_stock", "Not enough spare part stock on hand.");

        part.QtyOnHand -= qtyUsed;
        if (part.MinStock.HasValue && part.QtyOnHand <= part.MinStock.Value) part.StockStatus = "low";
        if (part.QtyOnHand <= 0) part.StockStatus = "critical";

        _uow.SpareParts.Update(part);
        await _uow.WoPartsUsed.AddAsync(new WoPartUsed
        {
            WorkOrderId = workOrderId,
            SparePartId = sparePartId,
            QtyUsed = qtyUsed,
            UnitCostAtUse = part.UnitCost
        });
        await _uow.SaveChangesAsync();
        return WorkflowResult<SparePart>.Ok(part, "Spare part issued.");
    }

    private static bool IsClosed(WorkOrderStatus status)
        => status is WorkOrderStatus.Resolved
            or WorkOrderStatus.Completed
            or WorkOrderStatus.Closed
            or WorkOrderStatus.Cancelled;

    private WorkOrderLocationResult ResolveWorkOrderLocation(
        int customerId,
        int? machineId,
        int? alarmEventId)
    {
        var resolvedMachineId = machineId;
        var factoryId = 0;

        if (resolvedMachineId.HasValue)
        {
            var machineLocation = (
                from machine in _uow.Machines.Query()
                join line in _uow.ProductionLines.Query() on machine.LineId equals line.Id
                join factory in _uow.Factories.Query() on line.FactoryId equals factory.Id
                where machine.Id == resolvedMachineId.Value &&
                      factory.CustomerId == customerId &&
                      machine.IsActive &&
                      !machine.IsDeleted
                select new { FactoryId = factory.Id }).FirstOrDefault();
            if (machineLocation is null)
                return WorkOrderLocationResult.Fail("machine_not_found", "The selected machine was not found.");

            factoryId = machineLocation.FactoryId;
        }

        if (alarmEventId.HasValue)
        {
            var alarm = _uow.AlarmEvents.Query().FirstOrDefault(x =>
                x.Id == alarmEventId.Value &&
                x.CustomerId == customerId);
            if (alarm is null)
                return WorkOrderLocationResult.Fail("alarm_not_found", "The selected alarm was not found.");

            resolvedMachineId ??= alarm.MachineId;
            var alarmFactoryId = ResolveAlarmFactoryId(alarm);
            if (alarmFactoryId <= 0)
                return WorkOrderLocationResult.Fail("factory_not_found", "Could not resolve a factory from the selected alarm.");
            if (factoryId > 0 && factoryId != alarmFactoryId)
                return WorkOrderLocationResult.Fail("alarm_machine_mismatch", "The selected machine does not belong to the alarm factory.");

            factoryId = alarmFactoryId;
        }

        if (factoryId <= 0)
            return WorkOrderLocationResult.Fail("machine_required", "Select a machine or create the work order from an alarm.");

        return WorkOrderLocationResult.Ok(factoryId, resolvedMachineId);
    }

    private int ResolveAlarmFactoryId(AlarmEvent alarm)
    {
        if (alarm.MachineId.HasValue)
        {
            return (
                from machine in _uow.Machines.Query()
                join line in _uow.ProductionLines.Query() on machine.LineId equals line.Id
                where machine.Id == alarm.MachineId.Value
                select line.FactoryId).FirstOrDefault();
        }

        if (alarm.SiloId.HasValue)
            return _uow.Silos.Query().Where(x => x.Id == alarm.SiloId.Value).Select(x => x.FactoryId).FirstOrDefault();

        if (alarm.DeviceId.HasValue)
            return _uow.Devices.Query().Where(x => x.Id == alarm.DeviceId.Value).Select(x => x.FactoryId).FirstOrDefault();

        return 0;
    }

    private static string CreateWorkOrderNumber()
        => $"WO-{DateTime.UtcNow:yyMMddHHmmssfff}";

    private sealed record WorkOrderLocationResult(
        bool Succeeded,
        int FactoryId,
        int? MachineId,
        string? ErrorCode,
        string? Message)
    {
        public static WorkOrderLocationResult Ok(int factoryId, int? machineId)
            => new(true, factoryId, machineId, null, null);

        public static WorkOrderLocationResult Fail(string code, string message)
            => new(false, 0, null, code, message);
    }

    private static string? AppendNote(string? oldNotes, string? newNote)
        => string.IsNullOrWhiteSpace(newNote) ? oldNotes : string.IsNullOrWhiteSpace(oldNotes) ? newNote : oldNotes + Environment.NewLine + newNote;
}

public sealed class AlarmWorkflowService : IAlarmWorkflowService
{
    private readonly IUnitOfWork _uow;
    public AlarmWorkflowService(IUnitOfWork uow) => _uow = uow;

    public async Task<WorkflowResult<AlarmRule>> ToggleRuleAsync(int id, bool isActive)
    {
        var rule = await _uow.AlarmRules.GetByIdAsync(id, asNoTracking: false);
        if (rule is null) return WorkflowResult<AlarmRule>.Fail("not_found", $"Alarm rule {id} was not found.");
        rule.IsActive = isActive;
        _uow.AlarmRules.Update(rule);
        await _uow.SaveChangesAsync();
        return WorkflowResult<AlarmRule>.Ok(rule, isActive ? "Alarm rule activated." : "Alarm rule deactivated.");
    }

    public async Task<WorkflowResult<AlarmEvent>> AcknowledgeAsync(int id, int acknowledgedById, string? notes)
    {
        var alarm = await _uow.AlarmEvents.GetByIdAsync(id, asNoTracking: false);
        if (alarm is null) return WorkflowResult<AlarmEvent>.Fail("not_found", $"Alarm {id} was not found.");
        alarm.AcknowledgedAt ??= DateTime.UtcNow;
        alarm.AcknowledgedById ??= acknowledgedById;
        alarm.Notes = AppendNote(alarm.Notes, notes);
        _uow.AlarmEvents.Update(alarm);
        await _uow.SaveChangesAsync();
        return WorkflowResult<AlarmEvent>.Ok(alarm, "Alarm acknowledged.");
    }

    public async Task<WorkflowResult<AlarmEvent>> ResolveAsync(int id, int acknowledgedById, string? notes, string? resolutionAction)
    {
        var alarm = await _uow.AlarmEvents.GetByIdAsync(id, asNoTracking: false);
        if (alarm is null) return WorkflowResult<AlarmEvent>.Fail("not_found", $"Alarm {id} was not found.");
        alarm.AcknowledgedAt ??= DateTime.UtcNow;
        alarm.AcknowledgedById ??= acknowledgedById;
        alarm.ResolvedAt ??= DateTime.UtcNow;
        alarm.Notes = AppendNote(alarm.Notes, string.Join(" | ", new[] { notes, resolutionAction }.Where(x => !string.IsNullOrWhiteSpace(x))));
        _uow.AlarmEvents.Update(alarm);
        await _uow.SaveChangesAsync();
        return WorkflowResult<AlarmEvent>.Ok(alarm, "Alarm resolved.");
    }

    public async Task<WorkflowResult<WorkOrder>> CreateWorkOrderFromAlarmAsync(int id, string? notes)
    {
        var alarm = await _uow.AlarmEvents.GetByIdAsync(id, asNoTracking: false);
        if (alarm is null) return WorkflowResult<WorkOrder>.Fail("not_found", $"Alarm {id} was not found.");

        var factoryId = ResolveFactoryId(alarm);
        if (factoryId <= 0) return WorkflowResult<WorkOrder>.Fail("factory_not_found", "Could not resolve factory for alarm work order.");

        var wo = new WorkOrder
        {
            Number = $"WO-{DateTime.UtcNow:yyMMddHHmmss}-{id}",
            CustomerId = alarm.CustomerId,
            FactoryId = factoryId,
            WorkOrderType = WorkOrderType.Corrective,
            Priority = alarm.Severity switch
            {
                AlarmSeverity.Critical => WorkOrderPriority.Critical,
                AlarmSeverity.High => WorkOrderPriority.High,
                AlarmSeverity.Medium => WorkOrderPriority.Medium,
                _ => WorkOrderPriority.Low
            },
            MachineId = alarm.MachineId,
            AlarmEventId = alarm.Id,
            Description = $"Auto-created from alarm: {alarm.Title}",
            Status = WorkOrderStatus.Open,
            CreatedById = null,
            ResolutionNotes = notes
        };
        await _uow.WorkOrders.AddAsync(wo);
        alarm.Notes = AppendNote(alarm.Notes, "Work order created from alarm.");
        _uow.AlarmEvents.Update(alarm);
        await _uow.SaveChangesAsync();
        return WorkflowResult<WorkOrder>.Ok(wo, "Work order created from alarm.");
    }

    private int ResolveFactoryId(AlarmEvent alarm)
    {
        if (alarm.MachineId.HasValue)
        {
            var machine = _uow.Machines.Query().FirstOrDefault(x => x.Id == alarm.MachineId.Value);
            if (machine is not null)
            {
                var line = _uow.ProductionLines.Query().FirstOrDefault(x => x.Id == machine.LineId);
                if (line is not null) return line.FactoryId;
            }
        }
        if (alarm.SiloId.HasValue)
        {
            var silo = _uow.Silos.Query().FirstOrDefault(x => x.Id == alarm.SiloId.Value);
            if (silo is not null) return silo.FactoryId;
        }
        return _uow.Factories.Query().Where(x => x.CustomerId == alarm.CustomerId).Select(x => x.Id).FirstOrDefault();
    }

    private static string? AppendNote(string? oldNotes, string? newNote)
        => string.IsNullOrWhiteSpace(newNote) ? oldNotes : string.IsNullOrWhiteSpace(oldNotes) ? newNote : oldNotes + Environment.NewLine + newNote;
}

public sealed class ScadaManualWorkflowService : IScadaManualWorkflowService
{
    private readonly IUnitOfWork _uow;
    private readonly IProductionRecordWriter _productionWriter;
    public ScadaManualWorkflowService(IUnitOfWork uow, IProductionRecordWriter productionWriter)
    {
        _uow = uow;
        _productionWriter = productionWriter;
    }

    public async Task<WorkflowResult<MachineState>> UpdateMachineStateManualAsync(int machineId, int state, string? notes)
    {
        var machine = _uow.Machines.Query().FirstOrDefault(x => x.Id == machineId && x.IsActive && !x.IsDeleted);
        if (machine is null) return WorkflowResult<MachineState>.Fail("machine_not_found", $"Machine {machineId} was not found.");
        var line = _uow.ProductionLines.Query().FirstOrDefault(x => x.Id == machine.LineId && x.IsActive && !x.IsDeleted);
        if (line is null) return WorkflowResult<MachineState>.Fail("line_not_found", $"Line {machine.LineId} was not found.");

        var machineState = await _productionWriter.AppendAsync(machineId, new MachineState
        {
            MachineId = machine.Name,
            FactoryId = line.FactoryId,
            LineId = line.Id,
            State = state,
            Timestamp = DateTime.UtcNow
        }, "manual-api", notes, manualStateOnly: true);
        return WorkflowResult<MachineState>.Ok(machineState, "Machine state updated manually.");
    }

    public async Task<WorkflowResult<Silo>> LoadSiloAsync(int siloId, int materialId, string? lotUid, decimal quantityKg, DateTime? timestamp)
    {
        if (quantityKg <= 0) return WorkflowResult<Silo>.Fail("invalid_quantity", "Quantity must be greater than zero.");
        var silo = await _uow.Silos.GetByIdAsync(siloId, asNoTracking: false);
        if (silo is null) return WorkflowResult<Silo>.Fail("not_found", $"Silo {siloId} was not found.");
        var materialExists = await _uow.Materials.AnyAsync(x => x.Id == materialId);
        if (!materialExists) return WorkflowResult<Silo>.Fail("material_not_found", $"Material {materialId} was not found.");
        var newQty = (silo.CurrentQtyKg ?? 0m) + quantityKg;
        if (newQty > silo.CapacityKg) return WorkflowResult<Silo>.Fail("capacity_exceeded", "Silo capacity would be exceeded.");
        if (silo.CurrentMaterialId.HasValue && silo.CurrentMaterialId.Value != materialId && (silo.CurrentQtyKg ?? 0m) > 0)
            return WorkflowResult<Silo>.Fail("material_mismatch", "Cannot load a different material into a non-empty silo.");

        silo.CurrentQtyKg = newQty;
        silo.CurrentMaterialId = materialId;
        silo.CurrentLotUid = lotUid;
        silo.TelemetryAt = timestamp ?? DateTime.UtcNow;
        _uow.Silos.Update(silo);
        await _uow.SaveChangesAsync();
        return WorkflowResult<Silo>.Ok(silo, "Silo loaded successfully.");
    }

    public async Task<WorkflowResult<Scale>> TareScaleAsync(int scaleId)
    {
        var scale = await _uow.Scales.GetByIdAsync(scaleId, asNoTracking: false);
        if (scale is null) return WorkflowResult<Scale>.Fail("not_found", $"Scale {scaleId} was not found.");
        scale.TareKg = scale.WeightKg ?? 0m;
        scale.ScaleState = "tare";
        scale.TelemetryAt = DateTime.UtcNow;
        _uow.Scales.Update(scale);
        await _uow.SaveChangesAsync();
        return WorkflowResult<Scale>.Ok(scale, "Scale tare saved.");
    }

    public async Task<WorkflowResult<Scale>> AssignScaleToBatchAsync(int scaleId, int batchId, int materialId)
    {
        var scale = await _uow.Scales.GetByIdAsync(scaleId, asNoTracking: false);
        if (scale is null) return WorkflowResult<Scale>.Fail("not_found", $"Scale {scaleId} was not found.");
        var batchExists = await _uow.Batches.AnyAsync(x => x.Id == batchId);
        if (!batchExists) return WorkflowResult<Scale>.Fail("batch_not_found", $"Batch {batchId} was not found.");
        var materialExists = await _uow.Materials.AnyAsync(x => x.Id == materialId);
        if (!materialExists) return WorkflowResult<Scale>.Fail("material_not_found", $"Material {materialId} was not found.");
        scale.ActiveBatchId = batchId;
        scale.ActiveMaterialId = materialId;
        scale.ScaleState = "assigned";
        scale.TelemetryAt = DateTime.UtcNow;
        _uow.Scales.Update(scale);
        await _uow.SaveChangesAsync();
        return WorkflowResult<Scale>.Ok(scale, "Scale assigned to batch/material.");
    }

    public async Task<WorkflowResult<Scale>> ReleaseScaleAsync(int scaleId)
    {
        var scale = await _uow.Scales.GetByIdAsync(scaleId, asNoTracking: false);
        if (scale is null)
            return WorkflowResult<Scale>.Fail("not_found", $"Scale {scaleId} was not found.");

        scale.ActiveBatchId = null;
        scale.ActiveMaterialId = null;
        scale.ScaleState = "ready";
        scale.TelemetryAt = DateTime.UtcNow;

        _uow.Scales.Update(scale);
        await _uow.SaveChangesAsync();
        return WorkflowResult<Scale>.Ok(scale, "Scale released and is ready for a new assignment.");
    }
}
