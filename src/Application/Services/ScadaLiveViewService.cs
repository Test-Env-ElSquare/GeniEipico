using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Domain.Entities.MasterData;
using Domain.Entities.Production;
using Domain.Entities.Scada;

namespace Application.Services;

public sealed class ScadaLiveViewService : IScadaLiveViewService
{
    private readonly IUnitOfWork _uow;
    private readonly ITimeUtilities _timeUtilities;
    private readonly IOverviewPeriodResolver _overviewPeriodResolver;

    public ScadaLiveViewService(
        IUnitOfWork uow,
        ITimeUtilities timeUtilities,
        IOverviewPeriodResolver overviewPeriodResolver)
    {
        _uow = uow;
        _timeUtilities = timeUtilities;
        _overviewPeriodResolver = overviewPeriodResolver;
    }

    public async Task<ScadaLiveViewResult> GetDashboardAsync(ScadaLiveViewScope scope)
    {
        var currentShift = await ResolveCurrentShiftAsync(scope);
        var statusCounts = BuildStatusCounts(scope, currentShift);
        var warehouseZone = BuildWarehouseZone(scope, currentShift);
        var dispensingZone = BuildDispensingZone(scope, currentShift);
        var productionZone = BuildProductionZone(scope, currentShift);
        var finishedGoodsZone = BuildFinishedGoodsZone(scope, currentShift);

        var page = new UiPageDto(
            "SCADA Live View",
            "/v1/scada/dashboard",
            "Current-state read model",
            "database",
            new[]
            {
                new UseCaseLinkDto("UC-T05", "C", "Apply tags to SCADA state", "Reads RealTime.Production populated from machine telemetry tags.", "Signals are appended; SCADA displays the latest reading for each machine."),
                new UseCaseLinkDto("UC-MF01", "B", "Receive raw material into silo", "Reads the current Silo snapshot.", "ERP or telemetry integration will update the snapshot later."),
                new UseCaseLinkDto("UC-MF05", "C", "Dispense material on scale", "Reads the current Scale snapshot.", "Stable scale telemetry will update the snapshot later."),
                new UseCaseLinkDto("UC-MF08", "C", "Pair material with machine", "Reads current machine and batch state.", "Machine-ready validation can be added later."),
                new UseCaseLinkDto("UC-MF11", "B", "Record finished goods", "Summarizes finished batches in the current shift.", "ERP confirmation can be added later.")
            },
            new Dictionary<string, object?>
            {
                ["statusCounts"] = statusCounts,
                ["warehouseZone"] = warehouseZone,
                ["dispensingZone"] = dispensingZone,
                ["productionZone"] = productionZone,
                ["finishedGoodsZone"] = finishedGoodsZone
            });

        return new ScadaLiveViewResult(page, currentShift);
    }

    public async Task<ScadaLiveViewResult> GetStatusCountsAsync(ScadaLiveViewScope scope)
    {
        var currentShift = await ResolveCurrentShiftAsync(scope);
        return new(BuildStatusCounts(scope, currentShift), currentShift);
    }

    public async Task<ScadaLiveViewResult> GetWarehouseZoneAsync(ScadaLiveViewScope scope)
    {
        var currentShift = await ResolveCurrentShiftAsync(scope);
        return new ScadaLiveViewResult(BuildWarehouseZone(scope, currentShift), currentShift);
    }

    public async Task<ScadaLiveViewResult> GetDispensingZoneAsync(ScadaLiveViewScope scope)
    {
        var currentShift = await ResolveCurrentShiftAsync(scope);
        return new ScadaLiveViewResult(BuildDispensingZone(scope, currentShift), currentShift);
    }

    public async Task<ScadaLiveViewResult> GetProductionZoneAsync(ScadaLiveViewScope scope)
    {
        var currentShift = await ResolveCurrentShiftAsync(scope);
        return new ScadaLiveViewResult(
            BuildProductionZone(scope, currentShift),
            currentShift);
    }

    public async Task<ScadaLiveViewResult> GetFactoryLayoutAsync(ScadaLiveViewScope scope)
    {
        var currentShift = await _overviewPeriodResolver.ResolveAsync(
            scope.CustomerId,
            new TimeFilterInput(
                Duration: (int)DurationFilter.CurrentShift,
                FactoryId: scope.FactoryId,
                LineId: scope.LineId));
        return new ScadaLiveViewResult(BuildFactoryLayout(scope, currentShift), currentShift);
    }

    public Task<object?> GetMachineDetailAsync(
        ScadaLiveViewScope scope,
        int machineId,
        ResolvedTimeFilter period)
    {
        var allowedLineIds = scope.AllowedLineIds.ToArray();
        var machine = _uow.Machines.Query()
            .FirstOrDefault(x => x.Id == machineId &&
                                 allowedLineIds.Contains(x.LineId) &&
                                 x.IsActive &&
                                 !x.IsDeleted);
        if (machine is null)
            return Task.FromResult<object?>(null);

        var line = _uow.ProductionLines.Query()
            .FirstOrDefault(x => x.Id == machine.LineId &&
                                 x.FactoryId == scope.FactoryId &&
                                 x.IsActive &&
                                 !x.IsDeleted);
        if (line is null)
            return Task.FromResult<object?>(null);

        var state = _uow.MachineStates.Query()
            .Where(x => x.FactoryId == scope.FactoryId &&
                        x.LineId == line.Id &&
                        (x.MachineId == machine.Code || x.MachineId == machine.Name))
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();
        var periodEnd = EffectivePeriodEnd(period);
        var periodState = state is not null &&
                          state.Timestamp >= period.From &&
                          state.Timestamp <= periodEnd
            ? state
            : null;
        var asset = _uow.Assets.Query()
            .FirstOrDefault(x => x.CustomerId == scope.CustomerId &&
                                 x.FactoryId == scope.FactoryId &&
                                 x.MachineId == machineId &&
                                 !x.IsDeleted);
        var timeline = BuildMachineTimeline(machineId, periodState, period);

        var snapshotQuery = _uow.OeeSnapshots.Query()
            .Where(x => x.MachineId == machineId &&
                        x.ComputedAt >= period.From &&
                        x.ComputedAt <= periodEnd);
        if (period.ShiftId.HasValue)
            snapshotQuery = snapshotQuery.Where(x => x.ShiftId == period.ShiftId.Value);
        var snapshots = snapshotQuery
            .OrderBy(x => x.ComputedAt)
            .ToList();

        var speedRpm = AverageOrNull(
            snapshots.Select(x => x.AverageSpeedRpm));
        var availabilityPct = AverageOrNull(
            snapshots.Select(x => x.AvailabilityPct));
        var qualityPct = AverageOrNull(
            snapshots.Select(x => x.QualityPct));
        var oeePct = AverageOrNull(
            snapshots.Select(x => x.OeePct));

        var keyEvents = _uow.Batches.Query()
            .Where(x => x.CustomerId == scope.CustomerId &&
                        x.LineId == line.Id &&
                        !x.IsDeleted &&
                        x.ActualStart.HasValue &&
                        x.ActualStart.Value >= period.From &&
                        x.ActualStart.Value <= periodEnd)
            .OrderBy(x => x.ActualStart)
            .Select(x => new
            {
                timestamp = x.ActualStart!.Value,
                type = "batch",
                batchId = x.Id,
                batchUid = x.Uid,
                message = "Started batch."
            })
            .ToArray();

        var now = DateTime.UtcNow;
        var normalizedState = periodState is null
            ? "unknown"
            : NormalizeMachineState(periodState.State, periodState.Fault);

        return Task.FromResult<object?>(new
        {
            machine = new
            {
                id = machine.Id,
                assetId = asset?.Id,
                assetCode = asset?.Code,
                machine.Code,
                machine.Name,
                type = machine.MachineType,
                lineId = line.Id,
                lineCode = line.Code,
                lineName = line.Name,
                state = normalizedState,
                isLive = periodState is not null &&
                         periodState.Timestamp >= now.AddMinutes(-2)
            },
            period = new
            {
                duration = period.Duration,
                period.Mode,
                period.From,
                period.To,
                period.ShiftId,
                period.ShiftName
            },
            readings = new
            {
                speedRpm,
                availabilityPct,
                qualityPct,
                oeePct
            },
            stateTimeline = timeline.Select(x => new
            {
                x.State,
                x.StartedAt,
                x.EndedAt,
                x.DurationMinutes,
                x.Percentage,
                x.IsCurrent
            }).ToArray(),
            keyEvents
        });
    }

    public async Task<ScadaLiveViewResult> GetFinishedGoodsZoneAsync(ScadaLiveViewScope scope)
    {
        var currentShift = await ResolveCurrentShiftAsync(scope);
        return new ScadaLiveViewResult(BuildFinishedGoodsZone(scope, currentShift), currentShift);
    }

    public async Task<ScadaLiveViewResult> GetEnergyAsync(ScadaLiveViewScope scope)
        => new(BuildEnergy(scope), await ResolveCurrentShiftAsync(scope));

    private Task<ResolvedTimeFilter> ResolveCurrentShiftAsync(ScadaLiveViewScope scope)
        => _timeUtilities.ResolveAsync(new TimeFilterInput(
            Duration: (int)DurationFilter.CurrentShift,
            FactoryId: scope.FactoryId,
            LineId: scope.LineId));

    private object BuildStatusCounts(
        ScadaLiveViewScope scope,
        ResolvedTimeFilter currentShift)
    {
        var machines = ActiveMachines(scope);
        var allowedLineIds = machines.Select(x => x.LineId).Distinct().ToArray();
        var periodEnd = EffectivePeriodEnd(currentShift);
        var stateByMachineId = MatchMachineStates(
            machines,
            _uow.MachineStates.Query()
                .Where(x => x.FactoryId == scope.FactoryId &&
                            allowedLineIds.Contains(x.LineId) &&
                            x.Timestamp >= currentShift.From &&
                            x.Timestamp <= periodEnd)
                .ToList());

        var states = machines.Select(machine =>
                stateByMachineId.TryGetValue(machine.Id, out var state)
                    ? NormalizeMachineState(state.State, state.Fault)
                    : "offline")
            .ToArray();

        return new
        {
            total = machines.Count,
            running = states.Count(x => x == "running"),
            idle = states.Count(x => x == "idle"),
            alarm = states.Count(x => x is "alarm" or "fault"),
            offline = states.Count(x => x == "offline"),
            maintenance = states.Count(x => x == "maintenance")
        };
    }

    private object BuildWarehouseZone(
        ScadaLiveViewScope scope,
        ResolvedTimeFilter currentShift)
    {
        var factorySilos = _uow.Silos.Query()
            .Where(x => x.FactoryId == scope.FactoryId && !x.IsDeleted)
            .OrderBy(x => x.Code)
            .ToList();
        var currentShiftSilos = factorySilos
            .Where(x => x.TelemetryAt.HasValue &&
                        x.TelemetryAt.Value >= currentShift.From &&
                        x.TelemetryAt.Value < currentShift.To)
            .ToList();
        var silos = currentShiftSilos.Count > 0
            ? currentShiftSilos
            : factorySilos.Where(x => x.TelemetryAt.HasValue).ToList();
        var materialIds = silos
            .Where(x => x.CurrentMaterialId.HasValue)
            .Select(x => x.CurrentMaterialId!.Value)
            .ToHashSet();
        var materials = _uow.Materials.Query()
            .Where(x => x.CustomerId == scope.CustomerId &&
                        x.IsActive &&
                        !x.IsDeleted &&
                        materialIds.Contains(x.Id))
            .ToDictionary(x => x.Id);

        var items = silos.Select(silo =>
        {
            materials.TryGetValue(silo.CurrentMaterialId ?? 0, out var material);
            var fillPct = Percent(silo.CurrentQtyKg ?? 0, silo.CapacityKg);

            return new
            {
                silo.Id,
                name = silo.Name,
                materialName = material?.Name,
                currentQtyKg = silo.CurrentQtyKg ?? 0,
                silo.CapacityKg,
                fillPct,
                status = SiloStatus(fillPct, silo.LowLevelPct, silo.CriticalLevelPct),
                materialLotUid = silo.CurrentLotUid,
                timestamp = silo.TelemetryAt
            };
        }).ToArray();

        return new
        {
            title = "ZONE 1 - RAW MATERIAL WAREHOUSE",
            count = items.Length,
            items
        };
    }

    private object BuildDispensingZone(
        ScadaLiveViewScope scope,
        ResolvedTimeFilter currentShift)
    {
        var allowedLineIds = scope.AllowedLineIds.ToArray();
        var factoryScales = _uow.Scales.Query()
            .Where(x => x.FactoryId == scope.FactoryId && !x.IsDeleted)
            .OrderBy(x => x.Code)
            .ToList();
        var referencedBatchIds = factoryScales
            .Where(x => x.ActiveBatchId.HasValue)
            .Select(x => x.ActiveBatchId!.Value)
            .ToHashSet();
        var referencedBatches = _uow.Batches.Query()
            .Where(x => x.CustomerId == scope.CustomerId &&
                        !x.IsDeleted &&
                        allowedLineIds.Contains(x.LineId) &&
                        referencedBatchIds.Contains(x.Id) &&
                        x.Status.ToLower() == "in_progress" &&
                        x.Stage.ToLower() == "dispense")
            .ToDictionary(x => x.Id);
        var lineScopedScales = scope.LineId.HasValue
            ? factoryScales
                .Where(x => !x.ActiveBatchId.HasValue ||
                            referencedBatches.ContainsKey(x.ActiveBatchId.Value))
                .ToList()
            : factoryScales;
        var currentShiftScales = lineScopedScales
            .Where(x => x.TelemetryAt.HasValue &&
                        x.TelemetryAt.Value >= currentShift.From &&
                        x.TelemetryAt.Value < currentShift.To)
            .ToList();
        var scales = currentShiftScales.Count > 0
            ? currentShiftScales
            : lineScopedScales.Where(x => x.TelemetryAt.HasValue).ToList();

        var activeBatches = _uow.Batches.Query()
            .Where(x => x.CustomerId == scope.CustomerId &&
                        !x.IsDeleted &&
                        allowedLineIds.Contains(x.LineId) &&
                        x.Status.ToLower() == "in_progress" &&
                        x.Stage.ToLower() == "dispense")
            .OrderBy(x => x.PlannedStart)
            .ToList();
        var activeBatchIds = activeBatches.Select(x => x.Id).ToHashSet();
        var scaleBatchIds = scales
            .Where(x => x.ActiveBatchId.HasValue &&
                        referencedBatches.ContainsKey(x.ActiveBatchId.Value))
            .Select(x => x.ActiveBatchId!.Value)
            .ToHashSet();
        var relevantBatchIds = activeBatchIds
            .Union(scaleBatchIds)
            .ToArray();
        var batches = _uow.Batches.Query()
            .Where(x => x.CustomerId == scope.CustomerId &&
                        !x.IsDeleted &&
                        allowedLineIds.Contains(x.LineId) &&
                        relevantBatchIds.Contains(x.Id) &&
                        x.Status.ToLower() == "in_progress" &&
                        x.Stage.ToLower() == "dispense")
            .ToDictionary(x => x.Id);
        var batchMaterials = _uow.BatchMaterials.Query()
            .Where(x => !x.IsDeleted && relevantBatchIds.Contains(x.BatchId))
            .OrderBy(x => x.Id)
            .ToList();
        var materialIds = batchMaterials.Select(x => x.MaterialId)
            .Union(scales
                .Where(x => x.ActiveMaterialId.HasValue)
                .Select(x => x.ActiveMaterialId!.Value))
            .ToArray();
        var materials = _uow.Materials.Query()
            .Where(x => x.CustomerId == scope.CustomerId &&
                        !x.IsDeleted &&
                        materialIds.Contains(x.Id))
            .ToDictionary(x => x.Id);
        var skuIds = batches.Values.Select(x => x.SkuId).ToArray();
        var skus = _uow.Skus.Query()
            .Where(x => x.CustomerId == scope.CustomerId &&
                        !x.IsDeleted &&
                        skuIds.Contains(x.Id))
            .ToDictionary(x => x.Id);

        var scaleItems = scales.Select(scale =>
        {
            batches.TryGetValue(scale.ActiveBatchId ?? 0, out var batch);
            var batchMaterial = batchMaterials
                .Where(x => x.BatchId == scale.ActiveBatchId &&
                            x.MaterialId == scale.ActiveMaterialId)
                .OrderByDescending(x => x.ScaleId == scale.Id)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();

            return new
            {
                scaleId = scale.Id,
                name = scale.Name,
                status = ScaleDisplayStatus(scale, batch, batchMaterial),
                weightKg = scale.WeightKg ?? 0,
                materialLotUid = batchMaterial?.MaterialUid,
                batchUid = batch?.Uid,
                timestamp = scale.TelemetryAt
            };
        }).ToArray();

        var activeRecipes = activeBatches.Select(batch =>
        {
            skus.TryGetValue(batch.SkuId, out var sku);
            var recipeMaterials = batchMaterials
                .Where(x => x.BatchId == batch.Id)
                .Select(batchMaterial =>
                {
                    materials.TryGetValue(batchMaterial.MaterialId, out var material);
                    return new
                    {
                        batchMaterial.MaterialId,
                        batchMaterial.ScaleId,
                        name = material?.Name,
                        materialLotUid = batchMaterial.MaterialUid,
                        dispensedQty = batchMaterial.DispensedQty,
                        plannedQty = batchMaterial.PlannedQty,
                        uom = material?.Uom,
                        progressPct = Percent(
                            batchMaterial.DispensedQty,
                            batchMaterial.PlannedQty),
                        status = MaterialDisplayStatus(batchMaterial.Status),
                        timestamp = batchMaterial.DispenseCompletedAt
                    };
                })
                .ToArray();

            return new
            {
                batchId = batch.Id,
                batchUid = batch.Uid,
                productName = sku?.Name,
                scaleIds = scales
                    .Where(x => x.ActiveBatchId == batch.Id)
                    .Select(x => x.Id)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToArray(),
                materials = recipeMaterials
            };
        }).ToArray();

        return new
        {
            title = "ZONE 2 - DISPENSING AREA",
            count = scaleItems.Length,
            scales = scaleItems,
            activeRecipes
        };
    }

    //private object BuildProductionZone(
    //    ScadaLiveViewScope scope,
    //    ResolvedTimeFilter currentShift)
    //{
    //    var machines = ActiveMachines(scope);
    //    var machineIds = machines.Select(x => x.Id).ToHashSet();
    //    var lineIds = machines.Select(x => x.LineId).ToHashSet();
    //    var periodEnd = EffectivePeriodEnd(currentShift);
    //    var states = _uow.MachineStates.Query()
    //        .Where(x => x.FactoryId == scope.FactoryId &&
    //                    lineIds.Contains(x.LineId))
    //        .ToList();
    //    var stateByMachineId = MatchMachineStates(machines, states);
    //    var oeeQuery = _uow.OeeSnapshots.Query()
    //        .Where(x => machineIds.Contains(x.MachineId) &&
    //                    x.ComputedAt >= currentShift.From &&
    //                    x.ComputedAt <= periodEnd);
    //    if (currentShift.ShiftId.HasValue)
    //        oeeQuery = oeeQuery.Where(
    //            x => x.ShiftId == currentShift.ShiftId.Value);
    //    var oeeByMachine = oeeQuery
    //        .ToList()
    //        .GroupBy(x => x.MachineId)
    //        .ToDictionary(
    //            group => group.Key,
    //            group => new
    //            {
    //                availabilityPct = AverageOrNull(
    //                    group.Select(x => x.AvailabilityPct)),
    //                qualityPct = AverageOrNull(
    //                    group.Select(x => x.QualityPct)),
    //                oeePct = AverageOrNull(
    //                    group.Select(x => x.OeePct)),
    //                speedRpm = AverageOrNull(
    //                    group.Select(x => x.AverageSpeedRpm))
    //            });
    //    var lines = _uow.ProductionLines.Query()
    //        .Where(x => lineIds.Contains(x.Id))
    //        .ToDictionary(x => x.Id);
    //    var lineItems = lines.Values
    //        .OrderBy(x => x.Code)
    //        .Select(line =>
    //    {
    //        var lineMachines = machines
    //            .Where(x => x.LineId == line.Id)
    //            .Select(machine =>
    //            {
    //                stateByMachineId.TryGetValue(machine.Id, out var state);
    //                oeeByMachine.TryGetValue(
    //                    machine.Id,
    //                    out var oee);
    //                var periodState = state is not null &&
    //                                  state.Timestamp >= currentShift.From &&
    //                                  state.Timestamp <= periodEnd
    //                    ? state
    //                    : null;
    //                return new
    //                {
    //                    id = machine.Id,
    //                    machine.Code,
    //                    machine.Name,
    //                    type = machine.MachineType,
    //                    state = periodState is null
    //                        ? "unknown"
    //                        : NormalizeMachineState(periodState.State, periodState.Fault),
    //                    availabilityPct = oee?.availabilityPct,
    //                    qualityPct = oee?.qualityPct,
    //                    oeePct = oee?.oeePct,
    //                    speedRpm = oee?.speedRpm
    //                };
    //            })
    //            .ToArray();

    //        return new
    //        {
    //            lineId = line.Id,
    //            lineCode = line.Code,
    //            lineName = line.Name,
    //            machineCount = lineMachines.Length,
    //            machines = lineMachines
    //        };
    //    }).ToArray();

    //    return new
    //    {
    //        title = "PRODUCTION FLOOR",
    //        machineCount = machines.Count,
    //        selectedLineId = scope.LineId,
    //        lines = lineItems
    //    };
    //}
    private object BuildFactoryLayout(
        ScadaLiveViewScope scope,
        ResolvedTimeFilter currentShift)
    {
        var allowedLineIds = scope.AllowedLineIds.ToArray();
        var lines = _uow.ProductionLines.Query()
            .Where(x => allowedLineIds.Contains(x.Id) &&
                        x.FactoryId == scope.FactoryId &&
                        x.IsActive &&
                        !x.IsDeleted)
            .OrderBy(x => x.Code)
            .ToList();
        var lineIds = lines.Select(x => x.Id).ToArray();
        var machines = _uow.Machines.Query()
            .Where(x => lineIds.Contains(x.LineId) &&
                        x.IsActive &&
                        !x.IsDeleted)
            .OrderBy(x => x.LineId)
            .ThenBy(x => x.Code)
            .ToList();
        var periodEnd = EffectivePeriodEnd(currentShift);
        var readings = _uow.MachineStates.Query()
            .Where(x => x.CustomerId == scope.CustomerId &&
                        x.FactoryId == scope.FactoryId &&
                        lineIds.Contains(x.LineId) &&
                        x.Timestamp >= currentShift.From &&
                        x.Timestamp <= periodEnd)
            .ToList();

        var lineItems = lines.Select(line =>
        {
            var machineItems = machines
                .Where(x => x.LineId == line.Id)
                .Select(machine =>
                {
                    var machineReadings = readings
                        .Where(x => x.LineId == line.Id &&
                                    (string.Equals(x.MachineId, machine.Code, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(x.MachineId, machine.Name, StringComparison.OrdinalIgnoreCase)))
                        .ToArray();
                    var latest = machineReadings
                        .OrderByDescending(x => x.Timestamp)
                        .ThenByDescending(x => x.Id)
                        .FirstOrDefault();
                    var hasShiftData = latest is not null;

                    return new
                    {
                        machineId = machine.Id,
                        machine.Code,
                        machine.Name,
                        type = machine.MachineType,
                        status = !hasShiftData
                            ? "no_data"
                            : latest!.State == 1 && latest.Fault == 0
                                ? "running"
                                : "stopped",
                        hasShiftData,
                        speed = latest?.Speed ?? 0,
                        count = machineReadings.Sum(x => x.CountDiff),
                        timestamp = latest is null
                            ? (DateTime?)null
                            : DateTime.SpecifyKind(latest.Timestamp, DateTimeKind.Utc)
                    };
                })
                .ToArray();
            var lineStatus = machineItems.Any(x => x.status == "running")
                ? "running"
                : machineItems.Any(x => x.hasShiftData)
                    ? "stopped"
                    : "no_data";

            return new
            {
                lineId = line.Id,
                line.Code,
                line.Name,
                status = lineStatus,
                machineCount = machineItems.Length,
                machines = machineItems
            };
        }).ToArray();

        return new
        {
            factoryId = scope.FactoryId,
            selectedLineId = scope.LineId,
            lineCount = lineItems.Length,
            machineCount = machines.Count,
            currentShift = new
            {
                currentShift.ShiftId,
                currentShift.ShiftName,
                from = currentShift.From,
                to = currentShift.To
            },
            lines = lineItems
        };
    }

    private object BuildProductionZone(
ScadaLiveViewScope scope,
ResolvedTimeFilter currentShift)
    {
        var machines = ActiveMachines(scope);

        var machineIds = machines
            .Select(x => x.Id)
            .ToHashSet();

        var lineIds = machines
            .Select(x => x.LineId)
            .ToHashSet();

        var states = _uow.MachineStates.Query()
            .Where(x =>
                x.FactoryId == scope.FactoryId &&
                lineIds.Contains(x.LineId))
            .ToList();

        // Matches Machine.Name/Code with Production.MachineId
        // and returns the latest state for every machine.
        var stateByMachineId = MatchMachineStates(
            machines,
            states);

        // Get the latest available OEE snapshot for every machine.
        var oeeByMachine = _uow.OeeSnapshots.Query()
            .Where(x => machineIds.Contains(x.MachineId))
            .ToList()
            .GroupBy(x => x.MachineId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.ComputedAt)
                    .First());

        var lines = _uow.ProductionLines.Query()
            .Where(x =>
                lineIds.Contains(x.Id) &&
                x.FactoryId == scope.FactoryId &&
                x.IsActive &&
                !x.IsDeleted)
            .ToDictionary(x => x.Id);

        var now = DateTime.UtcNow;
        var liveThreshold = now.AddMinutes(-2);

        var lineItems = lines.Values
            .OrderBy(x => x.Code)
            .Select(line =>
            {
                var lineMachines = machines
                    .Where(x => x.LineId == line.Id)
                    .Select(machine =>
                    {
                        stateByMachineId.TryGetValue(
                            machine.Id,
                            out var latestState);

                        oeeByMachine.TryGetValue(
                            machine.Id,
                            out var latestOee);

                        var isLive =
                            latestState is not null &&
                            latestState.Timestamp >= liveThreshold;

                        return new
                        {
                            id = machine.Id,
                            machine.Code,
                            machine.Name,
                            type = machine.MachineType,

                            // Old telemetry means the machine is offline,
                            // not unknown.
                            state = latestState is null
                                ? "unknown"
                                : isLive
                                    ? NormalizeMachineState(
                                        latestState.State,
                                        latestState.Fault)
                                    : "offline",

                            isLive,

                            availabilityPct =
                                latestOee?.AvailabilityPct,

                            qualityPct =
                                latestOee?.QualityPct,

                            oeePct =
                                latestOee?.OeePct,

                            // Current speed comes from realtime state.
                            speedRpm = latestState?.Speed,

                            timestamp = latestState?.Timestamp,

                            oeeComputedAt = latestOee?.ComputedAt
                        };
                    })
                    .ToArray();

                return new
                {
                    lineId = line.Id,
                    lineCode = line.Code,
                    lineName = line.Name,
                    machineCount = lineMachines.Length,
                    machines = lineMachines
                };
            })
            .ToArray();

        return new
        {
            title = "PRODUCTION FLOOR",
            machineCount = machines.Count,
            selectedLineId = scope.LineId,
            lines = lineItems
        };
    }



    private object BuildFinishedGoodsZone(ScadaLiveViewScope scope, ResolvedTimeFilter currentShift)
    {
        var allowedLineIds = scope.AllowedLineIds.ToArray();
        var finishedBatches = _uow.Batches.Query()
            .Where(x => x.CustomerId == scope.CustomerId &&
                        !x.IsDeleted &&
                        allowedLineIds.Contains(x.LineId) &&
                        x.Status.ToLower() == "finished" &&
                        x.ActualEnd.HasValue);

        var batches = finishedBatches
            .Where(x => x.ActualEnd!.Value >= currentShift.From &&
                        x.ActualEnd.Value < currentShift.To)
            .OrderByDescending(x => x.ActualEnd)
            .ToList();

        // SCADA normally shows the batches completed in the current shift.
        // If the shift has no completed batch yet, keep Zone 4 useful by
        // displaying the latest completed batch within the requested scope.
        if (batches.Count == 0)
        {
            var latestFinishedBatch = finishedBatches
                .OrderByDescending(x => x.ActualEnd)
                .FirstOrDefault();

            if (latestFinishedBatch is not null)
            {
                batches.Add(latestFinishedBatch);
            }
        }

        var skuIds = batches.Select(x => x.SkuId).ToHashSet();
        var skus = _uow.Skus.Query()
            .Where(x => x.CustomerId == scope.CustomerId &&
                        !x.IsDeleted &&
                        skuIds.Contains(x.Id))
            .ToDictionary(x => x.Id);
        var outputQty = batches.Sum(x => x.ProducedQty);
        var rejectedQty = batches.Sum(x => x.RejectedQty);
        var totalProcessed = outputQty + rejectedQty;

        var items = batches.Select(batch =>
        {
            skus.TryGetValue(batch.SkuId, out var sku);
            return new
            {
                batch.Id,
                batch.Uid,
                batch.LineId,
                batch.SkuId,
                skuCode = sku?.Code,
                productName = sku?.Name,
                batch.ProducedQty,
                batch.RejectedQty,
                batch.ActualEnd
            };
        }).ToArray();

        return new
        {
            zone = new
            {
                key = "finished_goods_warehouse",
                title = "ZONE 4 - FINISHED GOODS WAREHOUSE",
                count = items.Length
            },
            summary = new
            {
                outputQty,
                rejectedQty,
                batchesCompleted = batches.Count,
                rejectionRatePct = Percent(rejectedQty, totalProcessed),
                averageYieldPct = totalProcessed <= 0 ? 0 : Math.Round((decimal)outputQty / totalProcessed * 100m, 2)
            },
            items
        };
    }

    private object BuildEnergy(ScadaLiveViewScope scope)
    {
        var machines = ActiveMachines(scope);
        var machineIds = machines.Select(x => x.Id).ToHashSet();
        var energyByMachine = _uow.OeeSnapshots.Query()
            .Where(x => machineIds.Contains(x.MachineId) && x.EnergyKwh.HasValue)
            .ToList()
            .GroupBy(x => x.MachineId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.ComputedAt).First());

        return machines.Select(machine =>
        {
            energyByMachine.TryGetValue(machine.Id, out var snapshot);
            return new
            {
                machineId = machine.Id,
                machine.LineId,
                machine.Code,
                machine.Name,
                energyKwh = snapshot?.EnergyKwh,
                timestamp = snapshot?.ComputedAt
            };
        }).ToArray();
    }

    public IReadOnlyList<ScadaMachineTimelineSegment> GetMachineTimeline(
        ScadaLiveViewScope scope,
        int machineId,
        ResolvedTimeFilter period)
    {
        var machine = _uow.Machines.Query().FirstOrDefault(x =>
            x.Id == machineId && x.IsActive && !x.IsDeleted && scope.AllowedLineIds.Contains(x.LineId));
        if (machine is null)
            return Array.Empty<ScadaMachineTimelineSegment>();

        var snapshot = _uow.MachineStates.Query()
            .Where(x => x.FactoryId == scope.FactoryId && x.LineId == machine.LineId &&
                        (x.MachineId == machine.Name || x.MachineId == machine.Code) &&
                        x.Timestamp >= period.From && x.Timestamp <= period.To)
            .OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id)
            .FirstOrDefault();
        return BuildMachineTimeline(machineId, snapshot, period);
    }

    private List<ScadaMachineTimelineSegment> BuildMachineTimeline(
        int machineId,
        MachineState? currentSnapshot,
        ResolvedTimeFilter period)
    {
        var now = DateTime.UtcNow;
        var periodEnd = EffectivePeriodEnd(period, now);
        if (periodEnd <= period.From)
            return new List<ScadaMachineTimelineSegment>();

        var previousTransition = _uow.MachineStateHistories.Query()
            .Where(x => x.MachineId == machineId &&
                        x.TransitionedAt < period.From)
            .OrderByDescending(x => x.TransitionedAt)
            .FirstOrDefault();
        var transitions = _uow.MachineStateHistories.Query()
            .Where(x => x.MachineId == machineId &&
                        x.TransitionedAt >= period.From &&
                        x.TransitionedAt < periodEnd)
            .OrderBy(x => x.TransitionedAt)
            .ToList();

        if (currentSnapshot is null && transitions.Count == 0)
            return new List<ScadaMachineTimelineSegment>();

        var intervals = new List<(string State, DateTime StartedAt, DateTime EndedAt)>();
        string? activeState = previousTransition is null
            ? null
            : NormalizeHistoryState(previousTransition.ToState);
        var cursor = period.From;

        foreach (var transition in transitions)
        {
            var nextState = NormalizeHistoryState(transition.ToState);
            if (activeState is null)
            {
                var fromState = NormalizeHistoryStateOrNull(transition.FromState);
                if (fromState is not null && transition.TransitionedAt > cursor)
                    intervals.Add((fromState, cursor, transition.TransitionedAt));

                activeState = nextState;
                cursor = transition.TransitionedAt;
                continue;
            }

            if (transition.TransitionedAt > cursor &&
                activeState != nextState)
            {
                intervals.Add((
                    activeState,
                    cursor,
                    transition.TransitionedAt));
            }

            if (activeState != nextState)
                cursor = transition.TransitionedAt;

            activeState = nextState;
        }

        if (activeState is null && currentSnapshot is not null)
        {
            activeState = NormalizeMachineState(
                currentSnapshot.State,
                currentSnapshot.Fault);
            cursor = currentSnapshot.Timestamp < period.From
                ? period.From
                : currentSnapshot.Timestamp;
        }

        if (!string.IsNullOrWhiteSpace(activeState) && cursor < periodEnd)
        {
            intervals.Add((
                activeState,
                cursor,
                periodEnd));
        }

        var totalMinutes = Math.Max(
            0m,
            (decimal)(periodEnd - period.From).TotalMinutes);
        var currentState = currentSnapshot is null
            ? "offline"
            : NormalizeMachineState(currentSnapshot.State, currentSnapshot.Fault);
        var isLivePeriod = period.To >= now.AddSeconds(-2);

        return intervals.Select((interval, index) =>
        {
            var durationMinutes = Math.Round(
                (decimal)(interval.EndedAt - interval.StartedAt).TotalMinutes,
                2);
            return new ScadaMachineTimelineSegment(
                interval.State,
                interval.StartedAt,
                interval.EndedAt,
                durationMinutes,
                totalMinutes <= 0
                    ? 0
                    : Math.Round(durationMinutes / totalMinutes * 100m, 2),
                isLivePeriod &&
                currentSnapshot is not null &&
                index == intervals.Count - 1 &&
                interval.State == currentState);
        }).ToList();
    }

    private static DateTime EffectivePeriodEnd(
        ResolvedTimeFilter period,
        DateTime? now = null)
    {
        var utcNow = now ?? DateTime.UtcNow;
        return period.To > utcNow ? utcNow : period.To;
    }

    private static decimal? AverageOrNull(
        IEnumerable<decimal?> values)
    {
        var actualValues = values
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();
        return actualValues.Length == 0
            ? null
            : Math.Round(actualValues.Average(), 2);
    }

    private List<Machine> ActiveMachines(ScadaLiveViewScope scope)
    {
        var allowedLineIds = scope.AllowedLineIds.ToArray();
        return _uow.Machines.Query()
            .Where(x => allowedLineIds.Contains(x.LineId) &&
                        x.IsActive &&
                        !x.IsDeleted)
            .OrderBy(x => x.LineId)
            .ThenBy(x => x.Code)
            .ToList();
    }

    private static object? MaterialSummary(Material? material)
        => material is null
            ? null
            : new
            {
                material.Id,
                material.Code,
                material.Name,
                material.MaterialType,
                material.Uom
            };

    private static object? BatchSummary(Batch? batch)
        => batch is null
            ? null
            : new
            {
                batch.Id,
                batch.Uid,
                batch.Status,
                batch.Stage
            };

    private static decimal Percent(decimal value, decimal total)
        => total <= 0 ? 0 : Math.Round(value / total * 100m, 2);

    private static string SiloStatus(decimal fillPct, decimal lowLevelPct, decimal criticalLevelPct)
    {
        if (fillPct <= criticalLevelPct) return "critical";
        if (fillPct <= lowLevelPct) return "low";
        if (fillPct >= 100m) return "full";
        return "normal";
    }

    private static string ScaleDisplayStatus(
        Scale scale,
        Batch? activeBatch,
        BatchMaterial? batchMaterial)
    {
        var state = NormalizeState(scale.ScaleState);
        if (state is "error" or "fault" or "offline" ||
            scale.CalibratedAt is null ||
            string.IsNullOrWhiteSpace(scale.CalibrationCert))
            return "error";
        if (activeBatch is null)
            return "ready";
        if (batchMaterial is not null &&
            MaterialDisplayStatus(batchMaterial.Status) == "done")
            return "done";
        if (batchMaterial is not null &&
            batchMaterial.DispensedQty > 0)
            return "error";
        if (scale.ActiveBatchId.HasValue)
            return "weighing";
        return "ready";
    }

    private static string MaterialDisplayStatus(string? status)
    {
        var normalized = NormalizeState(status);
        return normalized is "complete" or "completed"
            ? "done"
            : normalized;
    }

    private static string NormalizeState(string? state)
        => string.IsNullOrWhiteSpace(state)
            ? "offline"
            : state.Trim().ToLowerInvariant();

    private static string NormalizeHistoryState(string state)
        => int.TryParse(state, out var numericState)
            ? NormalizeMachineState(numericState, 0)
            : NormalizeState(state);

    private static string? NormalizeHistoryStateOrNull(string? state)
        => string.IsNullOrWhiteSpace(state)
            ? null
            : NormalizeHistoryState(state);

    private static string NormalizeMachineState(int state, int fault)
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

    private static Dictionary<int, MachineState> MatchMachineStates(
        IReadOnlyCollection<Machine> machines,
        IReadOnlyCollection<MachineState> states)
    {
        var result = new Dictionary<int, MachineState>();
        foreach (var machine in machines)
        {
            var state = states
                .Where(x => x.LineId == machine.LineId &&
                            (string.Equals(x.MachineId, machine.Code, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(x.MachineId, machine.Name, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(x => x.Timestamp)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
            if (state is not null)
                result[machine.Id] = state;
        }

        return result;
    }

}
