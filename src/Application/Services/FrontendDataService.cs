using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Dtos.Frontend;
using Domain.Entities.Alarms;
using Domain.Entities.Analytics;
using Domain.Entities.Maintenance;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;
using Domain.Entities.Planning;
using Domain.Entities.Production;
using Domain.Entities.Scada;
using Domain.Enums;

namespace Application.Services;

public sealed class FrontendDataService : IFrontendDataService
{
    private readonly IUnitOfWork _uow;

    public FrontendDataService(IUnitOfWork uow)
    {
        _uow = uow;
    }



    public async Task<object?> GetOverviewKpisAsyncV1(FrontendFilter? filter = null)
    {
        var alarms = await _uow.AlarmEvents.GetAllAsync();
        var oee = await _uow.OeeSnapshots.GetAllAsync();
        var production = await _uow.MachineStates.GetAllAsync();
        var machines = await _uow.Machines.GetAllAsync();

        if (filter is not null)
        {
            alarms = alarms.Where(x => InRange(x.TriggeredAt, filter)).ToList();
            oee = oee.Where(x => InRange(x.ComputedAt, filter)).ToList();
            production = production.Where(x => InRange(x.Timestamp, filter)).ToList();
        }

        var avgOee = AverageOrNull(oee.Select(x => x.OeePct));
        var avgAvail = AverageOrNull(oee.Select(x => x.AvailabilityPct));
        var avgPerf = AverageOrNull(oee.Select(x => x.PerformancePct));
        var avgQuality = AverageOrNull(oee.Select(x => x.QualityPct));
        if (avgOee is null && avgAvail is null && avgPerf is null && avgQuality is null)
        {
            var realtime = CalculateRealtimeKpis(production, machines);
            avgOee = realtime.Oee;
            avgAvail = realtime.Availability;
            avgPerf = realtime.Performance;
            avgQuality = realtime.Quality;
        }

        var activeAlarms = alarms.Count(x => x.ResolvedAt == null);

        return new[]
        {
        new
        {
            Oee = avgOee,
            availability = avgAvail,
            performance = avgPerf,
            qualityRate = avgQuality,
            activeAlarms = activeAlarms
        }
    };
    }






    public async Task<UiPageDto> GetOverviewAsync(FrontendFilter? filter = null)
    {
        var machines = await _uow.Machines.GetAllAsync();
        var states = await _uow.MachineStates.GetAllAsync();
        var batches = await _uow.Batches.GetAllAsync();
        var alarms = await _uow.AlarmEvents.GetAllAsync();
        var oee = await _uow.OeeSnapshots.GetAllAsync();

        if (filter is not null)
        {
            batches = ApplyBatchFilter(batches, filter).ToList();
            alarms = alarms.Where(x =>
                (!filter.CustomerId.HasValue || x.CustomerId == filter.CustomerId.Value) &&
                InRange(x.TriggeredAt, filter)).ToList();
            oee = oee.Where(x => InRange(x.ComputedAt, filter)).ToList();
        }

        var activeMachines = machines.Count(machine =>
            FindMachineState(states, machine) is { } latest && MachineStateLabel(latest) == "running");
        if (activeMachines == 0 && machines.Count > 0) activeMachines = machines.Count(x => x.IsActive);
        var todayOutput = batches.Where(x => x.ActualEnd?.Date == DateTime.UtcNow.Date || x.CreatedAt.Date == DateTime.UtcNow.Date).Sum(x => x.ProducedQty);

        var avgOee = AverageOrNull(oee.Select(x => x.OeePct));
        var avgAvail = AverageOrNull(oee.Select(x => x.AvailabilityPct));
        var avgPerf = AverageOrNull(oee.Select(x => x.PerformancePct));
        var avgQuality = AverageOrNull(oee.Select(x => x.QualityPct));

        var activeAlarms = alarms.Count(x => x.ResolvedAt == null);
        var energy = oee.Sum(x => x.EnergyKwh ?? 0m);
        var latestOeeByMachine = oee
            .GroupBy(x => x.MachineId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.ComputedAt).First());

        return Page(
            "Overview Dashboard",
            "/v1/ui/overview",
            "Type D - Read Model / Dashboard",
            UseCases(
                ("UC-T05", "C", "SCADA State Updater applies tags", "Manual/read fallback now", "Real-time updater later"),
                ("UC-T06", "C", "Alarm Evaluator checks rules", "Uses active AlarmEvent rows now", "Rule engine later"),
                ("UC-T07", "C", "OEE Aggregator updates snapshots", "Uses existing OEE rows or fallback", "Scheduled OEE aggregator later"),
                ("UC-B13", "D", "Track batch on Kanban board", "Summary from Batches now", "SQL view/push later")),
            new Dictionary<string, object?>
            {
                ["kpis"] = new[]
                {
                    new { key = "overallOee", label = "OVERALL OEE", value = FormatPercent(avgOee), change = (string?)null, status = "neutral" },
                    new { key = "availability", label = "AVAILABILITY", value = FormatPercent(avgAvail), change = (string?)null, status = "neutral" },
                    new { key = "performance", label = "PERFORMANCE", value = FormatPercent(avgPerf), change = (string?)null, status = "neutral" },
                    new { key = "qualityRate", label = "QUALITY RATE", value = FormatPercent(avgQuality), change = (string?)null, status = "neutral" },
                    new { key = "activeAlarms", label = "ACTIVE ALARMS", value = (string?)activeAlarms.ToString(), change = (string?)null, status = "neutral" }
                },
                ["oeeTrend"] = BuildOeeTrend(oee),
                ["productionOutput"] = BuildProductionTrend(states),
                ["energyByMachine"] = machines.Take(8).Select(x => new { machineId = x.Id, machine = x.Name, kwh = latestOeeByMachine.TryGetValue(x.Id, out var machineOee) ? machineOee.EnergyKwh : null }).ToArray<object>(),
                ["activeAlarms"] = AlarmCards(alarms).Take(4).ToArray(),
                ["tierModules"] = new[]
                {
                    new { tier = "Basic", enabled = true, features = new[] { "Machine Monitoring", "Energy Consumption", "Shift Reports", "OEE Dashboard" } },
                    new { tier = "Advanced", enabled = true, features = new[] { "Material Flow", "Batch Management", "Recipe Control", "MRP Planning" } },
                    new { tier = "Enterprise", enabled = true, features = new[] { "Power Quality", "AI Insights", "Predictive Maintenance", "Advanced Analytics" } },
                    new { tier = "Platinum", enabled = false, features = new[] { "Physical Control", "Custom Integrations", "White-label SDK", "API Gateway" } }
                }
            });
    }

    public async Task<UiPageDto> GetOperationsAsync(FrontendFilter? filter = null)
    {
        var machines = await _uow.Machines.GetAllAsync();
        var states = await _uow.MachineStates.GetAllAsync();
        var oeeSnapshots = await _uow.OeeSnapshots.GetAllAsync();
        var latestOeeByMachine = oeeSnapshots
            .GroupBy(x => x.MachineId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.ComputedAt)
                    .First());
        return Page("Operations", "/v1/ui/operations", "Type D - Read Model", UseCases(
            ("UC-T05", "C", "Machine state shown in operations", "Reads MachineState now", "Real-time updater later"),
            ("UC-T07", "C", "OEE values shown in operations", "Reads OeeSnapshot now", "Aggregator later")),
            new Dictionary<string, object?>
            {
                ["energyLoadLast8Hours"] = BuildEnergyTrend(oeeSnapshots),
                ["machineUtilization"] = machines.Select(m => new { machineId = m.Id, machine = m.Name, utilizationPct = latestOeeByMachine.TryGetValue(m.Id, out var oee) ? oee.OeePct : null }).ToArray<object>(),
                ["machineStatusTable"] = machines.Select(m =>
                {
                    var st = FindMachineState(states, m);
                    latestOeeByMachine.TryGetValue(m.Id, out var oee);
                    return new { id = m.Id, code = m.Code, machine = m.Name, state = st is null ? "idle" : MachineStateLabel(st), speedRpm = st?.Speed ?? 0, oeePct = oee?.OeePct, energyKwh = oee?.EnergyKwh ?? 0, lastTelemetryAt = st?.Timestamp };
                }).ToArray<object>()
            });
    }

    public async Task<UiPageDto> GetBatchManagementAsync(FrontendFilter? filter = null)
    {
        var batches = await _uow.Batches.GetAllAsync();
        var skus = await _uow.Skus.GetAllAsync();
        var lines = await _uow.ProductionLines.GetAllAsync();
        var alarms = await _uow.AlarmEvents.GetAllAsync();

        if (filter is not null)
        {
            batches = ApplyBatchFilter(batches, filter).ToList();
            alarms = alarms.Where(x =>
                (!filter.CustomerId.HasValue || x.CustomerId == filter.CustomerId.Value) &&
                InRange(x.TriggeredAt, filter)).ToList();
        }

        var cards = BatchCards(batches, skus, lines, alarms).ToArray();
        return Page("Batch Management", "/v1/ui/batches", "Type D - Read Model + Type B action targets", UseCases(
            ("UC-B03", "B", "Create batch manually", "POST /v1/batches/manual", "Factory-manager authorization/approval is enforced by the API authorization layer"),
            ("UC-B05", "B", "Start batch", "POST /v1/batches/{id}/start", "No real-time dependency"),
            ("UC-B06", "B", "Advance stage", "POST /v1/batches/{id}/advance-stage", "No real-time dependency"),
            ("UC-B07", "C", "Dispense with actual weight", "Manual qty endpoint now", "Scale telemetry later"),
            ("UC-B13", "D", "Kanban board", "DTO grouped cards now", "SQL view/live push later"),
            ("UC-B14", "D", "Batch detail", "DTO detail endpoint now", "PDF export/signatures later")),
            new Dictionary<string, object?>
            {
                ["uidLegend"] = new { format = "BT-YYMM-SSSSS-ORG", examples = new[] { "BT-2606-00001-EGY", "BT-2406-00033-EGY" }, types = new[] { "BT", "RM", "SF", "FG" } },
                ["kanban"] = GroupKanban(cards),
                ["table"] = cards,
                ["filters"] = new { statuses = new[] { "planned", "in_progress", "production", "finished", "cancelled", "split" }, searchFields = new[] { "uid", "skuName", "lineName" } },
                ["actionEndpoints"] = new[]
                {
                    new { action = "create", method = "POST", endpoint = "/v1/batches/manual", type = "B" },
                    new { action = "start", method = "POST", endpoint = "/v1/batches/{id}/start", type = "B" },
                    new { action = "advanceStage", method = "POST", endpoint = "/v1/batches/{id}/advance-stage", type = "B" },
                    new { action = "complete", method = "POST", endpoint = "/v1/batches/{id}/complete", type = "B" },
                    new { action = "dispense", method = "POST", endpoint = "/v1/batches/{id}/materials/{materialId}/dispense", type = "C manual now / RT later" }
                }
            });
    }

    public async Task<UiPageDto> GetBatchDetailAsync(int batchId, FrontendFilter? filter = null)
    {
        var batches = await _uow.Batches.GetAllAsync();
        var batch = batches.FirstOrDefault(x =>
            x.Id == batchId &&
            (filter?.CustomerId is null || x.CustomerId == filter.CustomerId.Value) &&
            (filter?.AllowedLineIds is null || filter.AllowedLineIds.Contains(x.LineId)));
        var skus = await _uow.Skus.GetAllAsync();
        var lines = await _uow.ProductionLines.GetAllAsync();
        var materials = await _uow.Materials.GetAllAsync();
        var batchMaterials = await _uow.BatchMaterials.GetAllAsync();
        var boms = await _uow.BillOfMaterials.GetAllAsync();
        var logs = await _uow.BatchStageLogs.GetAllAsync();
        var alarms = await _uow.AlarmEvents.GetAllAsync();
        if (filter?.CustomerId is not null)
        {
            alarms = alarms.Where(x => x.CustomerId == filter.CustomerId.Value).ToList();
        }

        var detail = batch is not null ? BatchDetail(batch, skus, lines, batchMaterials, materials, boms, logs, alarms) : null;
        return Page("Batch Detail", $"/v1/ui/batches/{batchId}/detail", "Type D - Read Model", UseCases(
            ("UC-B14", "D", "View batch detail with BOM actuals", "Unified DTO now", "PDF/hash export later"),
            ("UC-B09", "B/D", "Record/read stage timestamps", "Reads BatchStageLog now", "Immutable audit enforcement later"),
            ("UC-B10", "C", "Deviation alarms", "Displays alarm events now", "Alarm evaluator later")),
            new Dictionary<string, object?> { ["detail"] = detail });
    }

    public async Task<object> GetBatchMaterialsAsync(int batchId, FrontendFilter? filter = null)
    {
        var materials = await _uow.Materials.GetAllAsync();
        var lines = (await _uow.BatchMaterials.GetAllAsync()).Where(x => x.BatchId == batchId).ToList();
        if (!lines.Any()) return Array.Empty<object>();
        var batch = await _uow.Batches.GetByIdAsync(batchId);
        var bomToleranceByMaterial = batch is null
            ? new Dictionary<int, decimal>()
            : (await _uow.BillOfMaterials.GetAllAsync())
                .Where(x => x.SkuId == batch.SkuId)
                .ToDictionary(x => x.MaterialId, x => x.TolerancePct);
        return lines.Select(x => new
        {
            id = x.Id,
            batchId = x.BatchId,
            materialId = x.MaterialId,
            materialCode = materials.FirstOrDefault(m => m.Id == x.MaterialId)?.Code,
            materialName = MaterialName(materials, x.MaterialId),
            plannedQty = x.PlannedQty,
            dispensedQty = x.DispensedQty,
            deviationQty = x.DispensedQty > 0 ? Math.Abs(x.DispensedQty - x.PlannedQty) : (decimal?)null,
            deviationPct = x.DeviationPct,
            tolerancePct = ResolveTolerancePct(bomToleranceByMaterial, x.MaterialId),
            minAllowedQty = MinAllowedQty(x.PlannedQty, ResolveTolerancePct(bomToleranceByMaterial, x.MaterialId)),
            maxAllowedQty = MaxAllowedQty(x.PlannedQty, ResolveTolerancePct(bomToleranceByMaterial, x.MaterialId)),
            status = x.Status,
            scaleId = x.ScaleId,
            type = "D read model, C when used by dispense endpoint"
        }).ToArray();
    }

    public async Task<object> GetBatchStageLogAsync(int batchId, FrontendFilter? filter = null)
    {
        var logs = (await _uow.BatchStageLogs.GetAllAsync()).Where(x => x.BatchId == batchId).OrderBy(x => x.TransitionedAt).ToList();
        if (!logs.Any()) return Array.Empty<object>();
        return logs.Select(x => new { x.Id, x.BatchId, x.FromStage, x.ToStage, x.TransitionedAt, x.MachineId, x.Notes }).ToArray();
    }

    public async Task<UiPageDto> GetPlanningAsync(FrontendFilter? filter = null)
    {
        var schedules = await _uow.ProductionSchedules.GetAllAsync();
        var routes = await _uow.ProductionRoutes.GetAllAsync();
        var steps = await _uow.RouteSteps.GetAllAsync();
        var mrp = await _uow.MrpRequirements.GetAllAsync();

        if (filter is not null)
        {
            schedules = ApplyScheduleFilter(schedules, filter).ToList();
            mrp = mrp.Where(x => InRange(x.PlanDate, filter)).ToList();
        }

        return Page("Planning", "/v1/ui/planning", "Type D - Read Model + Type B route actions", UseCases(
            ("UC-R01", "B", "Create route", "Routes workflow now", "Versioning rules later"),
            ("UC-R03", "B", "Activate route", "PUT /v1/routes/{id}/status", "E-signature later"),
            ("UC-R05", "C", "Execute route step", "Shown as route status now", "Machine state validation later")),
            new Dictionary<string, object?>
            {
                ["gantt"] = schedules.Select(s => new { s.Id, s.BatchId, s.LineId, s.ScheduledStart, s.ScheduledEnd, s.IsStoppage, s.StoppageType, s.Notes }).ToArray<object>(),
                ["routing"] = routes.Select(r => new { r.Id, r.Code, r.Name, r.SkuId, r.Status, r.Version, stepsCount = steps.Count(s => s.RouteId == r.Id), steps = steps.Where(s => s.RouteId == r.Id).OrderBy(s => s.StepNo).Select(s => new { s.Id, s.StepNo, s.StepName, s.MachineType, s.EstDurationMin }).ToArray() }).ToArray<object>(),
                ["mrp"] = mrp.Select(x => new { x.Id, x.MaterialId, x.PlanDate, x.RequiredQty, x.AvailableQty, x.GapQty, x.MrpStatus, x.PoReference, x.PoExpectedDate }).ToArray<object>(),
                ["stoppages"] = schedules.Where(x => x.IsStoppage).Select(x => new { x.Id, x.ScheduledStart, x.ScheduledEnd, x.StoppageType, x.Notes }).ToArray<object>()
            });
    }

    public async Task<UiPageDto> GetAssetsAsync(FrontendFilter? filter = null)
    {
        var assets = await _uow.Assets.GetAllAsync();

        if (filter is not null)
        {
            assets = assets.Where(x => MatchesFactory(x.FactoryId, filter)).ToList();
        }

        return Page("Asset Registry", "/v1/ui/assets", "Type D - Read Model", UseCases(
            ("UC-MD08", "A", "Manage assets", "DTO list now", "Business rules later"),
            ("UC-V04", "B", "PM check before machine start", "Shown as health/maintenance status", "Validation service later")),
            new Dictionary<string, object?>
            {
                ["kpis"] = new[]
                {
                    new { label = "Running", value = (string?)assets.Count(x => Eq(x.Status, "running")).ToString(), status = "good" },
                    new { label = "Fault/Alarm", value = (string?)assets.Count(x => Eq(x.Status, "fault") || Eq(x.Status, "alarm")).ToString(), status = "alert" },
                    new { label = "Maintenance", value = (string?)assets.Count(x => Eq(x.Status, "maintenance")).ToString(), status = "info" },
                    new { label = "Avg Health", value = assets.Any(x => x.HealthScore.HasValue) ? $"{assets.Where(x => x.HealthScore.HasValue).Average(x => x.HealthScore!.Value):0}%" : null, status = "normal" }
                },
                ["assets"] = assets.Select(a => new { a.Id, a.Code, a.Name, a.AssetType, a.FactoryId, a.MachineId, a.SerialNumber, a.Manufacturer, a.HealthScore, a.Status, a.UtilizationPct, a.IsActive }).ToArray<object>()
            });
    }

    public async Task<UiPageDto> GetAlarmsAsync(FrontendFilter? filter = null)
    {
        var alarms = await _uow.AlarmEvents.GetAllAsync();
        var rules = await _uow.AlarmRules.GetAllAsync();
        var active = AlarmCards(alarms).ToArray();
        return Page("Alarms & Alerts", "/v1/ui/alarms", "Type D - Read Model + Type B alarm actions", UseCases(
            ("UC-T06", "C", "Alarm evaluator checks rules", "Manual/alarm rows now", "Threshold evaluator later"),
            ("UC-B10", "C", "Deviation alarm", "Created by manual dispense now", "Real-time weight later"),
            ("UC-MD09", "A", "Manage alarm rules", "Rules DTO now", "Rule conflict validation later")),
            new Dictionary<string, object?>
            {
                ["summary"] = new { critical = CountSeverity(alarms, AlarmSeverity.Critical), high = CountSeverity(alarms, AlarmSeverity.High), medium = CountSeverity(alarms, AlarmSeverity.Medium), low = CountSeverity(alarms, AlarmSeverity.Low) },
                ["activeAlarms"] = active,
                ["userRules"] = rules.Where(r => r.RuleType == AlarmRuleType.User).Select(r => new { r.Id, r.Name, r.RuleType, r.ScopeType, r.TagName, r.MinimumValue, r.MaximumValue, r.Severity, r.CreateWorkOrder, r.IsActive }).ToArray<object>(),
                ["systemRules"] = rules.Where(r => r.RuleType == AlarmRuleType.System).Select(r => new { r.Id, r.Name, r.TagName, r.MinimumValue, r.MaximumValue, r.Severity, r.IsActive }).ToArray<object>(),
                ["actionEndpoints"] = new[] { new { action = "acknowledge", endpoint = "/v1/alarms/{id}/acknowledge", type = "B" }, new { action = "resolve", endpoint = "/v1/alarms/{id}/resolve", type = "B" }, new { action = "createWo", endpoint = "/v1/alarms/{id}/create-wo", type = "B" } }
            });
    }

    public async Task<UiPageDto> GetAnalyticsAsync(FrontendFilter? filter = null)
    {
        var insights = await _uow.AiInsights.GetAllAsync();
        var oee = await _uow.OeeSnapshots.GetAllAsync();
        var assets = await _uow.Assets.GetAllAsync();
        return Page("Analytics & AI Insights", "/v1/ui/analytics", "Type D - Read Model / Future aggregate views", UseCases(
            ("UC-T07", "C", "OEE aggregation", "Reads OeeSnapshot/fallback", "Aggregator later"),
            ("UC-L12", "D", "Hourly telemetry aggregates", "Static/read fallback now", "Scheduled job later")),
            new Dictionary<string, object?>
            {
                ["insights"] = insights.Where(x => !x.IsDismissed).Take(8).Select(i => new { i.Id, i.InsightType, i.EntityType, i.EntityId, i.Title, i.Body, i.ImpactValue, i.ImpactCurrency, i.ConfidencePct, i.GeneratedAt }).ToArray<object>(),
                ["oeeBreakdown"] = BuildOeeBreakdown(oee),
                ["heatmap"] = BuildHeatmap(await _uow.MachineStates.GetAllAsync()),
                ["failureRisk"] = assets.Where(a => a.HealthScore.HasValue).Select(a => new { a.Id, asset = a.Name, riskPct = 100 - a.HealthScore!.Value, healthScore = a.HealthScore.Value }).ToArray<object>()
            });
    }

    public async Task<UiPageDto> GetSkuManagementAsync(FrontendFilter? filter = null)
    {
        var skus = await _uow.Skus.GetAllAsync();
        var boms = await _uow.BillOfMaterials.GetAllAsync();
        var materials = await _uow.Materials.GetAllAsync();
        var lastSapSync = skus
            .Where(x => x.LastErpSyncAt.HasValue)
            .OrderByDescending(x => x.LastErpSyncAt)
            .Select(x => x.LastErpSyncAt)
            .FirstOrDefault();
        return Page("SKU Management", "/v1/ui/sku-management", "Type D DTO for Type A master data", UseCases(
            ("UC-MD01", "A", "Manage SKUs", "DTO list now", "Unique/deactivate rules later"),
            ("UC-MD02", "A", "Manage materials", "DTO list now", "UOM immutability later"),
            ("UC-MD03", "A", "Manage BOM", "BOM DTO now", "Versioning later")),
            new Dictionary<string, object?>
            {
                ["erpBanner"] = new { active = skus.Any(x => x.ErpSource), source = "SAP", lastSync = lastSapSync, syncedSkus = skus.Count(x => x.ErpSource), totalSkus = skus.Count, note = "Values come from the Eipico SAP synchronization state." },
                ["skus"] = skus.Select(s => new { s.Id, s.Code, s.Name, s.Category, s.Uom, s.StandardBatchSize, s.ShelfLifeMonths, s.RatedSpeedRpm, s.UnitWeightG, s.ErpSource, s.ErpMaterialCode, s.LastErpSyncAt, s.IsActive, bomLines = boms.Count(b => b.SkuId == s.Id) }).ToArray<object>(),
                ["materials"] = materials.Select(m => new { m.Id, m.Code, m.Name, m.MaterialType, m.Uom, m.ReorderPointKg, m.MinStockKg, m.IsActive }).ToArray<object>(),
                ["bom"] = boms.Select(b => new { b.Id, b.SkuId, b.MaterialId, materialName = MaterialName(materials, b.MaterialId), b.QtyPerBatch, b.TolerancePct, b.SequenceNo, b.IsCritical }).ToArray<object>()
            });
    }

    public async Task<UiPageDto> GetConfigurationAsync(FrontendFilter? filter = null)
    {
        var settings = await _uow.PlatformSettings.GetAllAsync();
        var shifts = await _uow.Shifts.GetAllAsync();
        var configs = await _uow.MachineConfigs.GetAllAsync();
        var thresholds = await _uow.MetricThresholds.GetAllAsync();
        return Page("MES Configuration", "/v1/ui/configuration", "Type D form DTO + Type B save endpoints", UseCases(
            ("UC-S01", "B", "Configure platform settings", "GET/PUT /v1/config/platform-settings", "Cache refresh/audit later"),
            ("UC-S03", "B", "Manage rating thresholds", "GET/PUT /v1/config/rating-thresholds", "Non-overlap validation later"),
            ("UC-S04", "B", "Configure machine parameters", "GET/PUT /v1/config/machine/{id}", "Alarm/SCADA propagation later"),
            ("UC-S05", "B", "Manage shifts", "Shift DTO now", "No gap/overlap validation later")),
            new Dictionary<string, object?>
            {
                ["generalSettings"] = settings.Where(x => !x.IsSensitive).Select(x => new { x.Key, x.Value, editable = true }).ToArray<object>(),
                ["shifts"] = shifts.Select(s => new { s.Id, s.FactoryId, s.Name, startTime = s.StartTime.ToString(), endTime = s.EndTime.ToString(), s.IsActive }).ToArray<object>(),
                ["machineParameters"] = configs.Select(c => new { c.MachineId, c.RatedSpeedRpm, c.MinSpeedRpm, c.MaxTempCelsius, c.MaxVibrationMms, c.RatedPowerKw }).ToArray<object>(),
                ["colorCodes"] = DefaultColorCodes(),
                ["ratingThresholds"] = thresholds.Select(t => new { t.Id, t.Metric, t.Grade, t.MinValue, t.MaxValue }).ToArray<object>()
            });
    }

    public async Task<UiPageDto> GetHierarchyAsync(FrontendFilter? filter = null)
    {
        var customers = await _uow.Customers.GetAllAsync();
        var factories = await _uow.Factories.GetAllAsync();
        var lines = await _uow.ProductionLines.GetAllAsync();
        var machines = await _uow.Machines.GetAllAsync();
        var scales = await _uow.Scales.GetAllAsync();
        var silos = await _uow.Silos.GetAllAsync();
        return Page("Hierarchy Management", "/v1/ui/hierarchy", "Type D - Organization tree DTO", UseCases(
            ("UC-MD04", "A", "Manage machines", "Hierarchy DTO now", "Reference checks later"),
            ("UC-MD05", "A", "Manage silos", "Hierarchy DTO now", "Stock rules later"),
            ("UC-MD06", "A", "Manage scales", "Hierarchy DTO now", "Calibration alerts later")),
            new Dictionary<string, object?>
            {
                ["stats"] = new[]
                {
                    new { label = "Customers", value = customers.Count }, new { label = "Factories", value = factories.Count }, new { label = "Lines", value = lines.Count }, new { label = "Machines", value = machines.Count }, new { label = "Scales", value = scales.Count }, new { label = "Silos", value = silos.Count }
                },
                ["tree"] = customers.Select(c => new
                {
                    id = c.Id,
                    type = "customer",
                    name = c.Name,
                    code = c.Code,
                    factories = factories.Where(f => f.CustomerId == c.Id).Select(f => new
                    {
                        id = f.Id,
                        type = "factory",
                        name = f.Name,
                        code = f.Code,
                        lines = lines.Where(l => l.FactoryId == f.Id).Select(l => new { id = l.Id, type = "line", name = l.Name, code = l.Code, machines = machines.Where(m => m.LineId == l.Id).Select(m => new { id = m.Id, type = "machine", name = m.Name, code = m.Code, machineType = m.MachineType }).ToArray() }).ToArray(),
                        scales = scales.Where(s => s.FactoryId == f.Id).Select(s => new { id = s.Id, type = "scale", name = s.Name, code = s.Code, state = s.ScaleState }).ToArray(),
                        silos = silos.Where(s => s.FactoryId == f.Id).Select(s => new { id = s.Id, type = "silo", name = s.Name, code = s.Code, currentQtyKg = s.CurrentQtyKg }).ToArray()
                    }).ToArray()
                }).ToArray<object>()
            });
    }

    public async Task<UiPageDto> GetMaintenanceAsync(FrontendFilter? filter = null)
    {
        var workOrders = await _uow.WorkOrders.GetAllAsync();
        var pm = await _uow.PmSchedules.GetAllAsync();
        var breakdowns = await _uow.BreakdownLogs.GetAllAsync();
        var parts = await _uow.SpareParts.GetAllAsync();
        return Page("Maintenance Management", "/v1/ui/maintenance", "Type D dashboard DTO + Type B action endpoints", UseCases(
            ("UC-MD10", "A", "Manage spare parts", "DTO inventory now", "Issue/reorder rules later"),
            ("UC-MD11", "A", "Manage PM schedules", "DTO schedule now", "Overdue block later"),
            ("UC-V04", "B", "Check PM before machine start", "Status shown now", "Validation service later")),
            new Dictionary<string, object?>
            {
                ["kpis"] = new[]
                {
                    new { label = "Open WOs", value = workOrders.Count(x => x.Status == WorkOrderStatus.Open), status = "alert" },
                    new { label = "In Progress", value = workOrders.Count(x => x.Status == WorkOrderStatus.InProgress), status = "info" },
                    new { label = "Completed Today", value = workOrders.Count(x => x.Status is WorkOrderStatus.Resolved or WorkOrderStatus.Completed or WorkOrderStatus.Closed && x.ActualEnd?.Date == DateTime.UtcNow.Date), status = "good" },
                    new { label = "PM Due 7 Days", value = pm.Count(x => x.NextDueAt <= DateTime.UtcNow.AddDays(7)), status = "warning" },
                    new { label = "Parts Low Stock", value = parts.Count(IsLowStock), status = "warning" }
                },
                ["workOrders"] = workOrders.Select(w => new { w.Id, w.Number, w.WorkOrderType, w.Priority, w.MachineId, w.Description, w.Team, w.PlannedStart, w.ActualStart, w.ActualEnd, w.Status, w.EstimatedCost, w.ActualCost, w.FailureMode, w.ResolutionNotes }).ToArray<object>(),
                ["pmSchedules"] = pm.Select(p => new { p.Id, p.MachineId, p.AssetId, p.TaskName, p.Frequency, p.IntervalDays, p.LastDoneAt, p.NextDueAt, p.AssignedTeam, p.EstimatedDurationHours, status = p.NextDueAt < DateTime.UtcNow ? "overdue" : p.NextDueAt <= DateTime.UtcNow.AddDays(7) ? "due_soon" : "ok" }).ToArray<object>(),
                ["breakdownHistory"] = breakdowns.Select(b => new { b.Id, b.Reference, b.MachineId, b.BreakdownAt, b.ResolvedAt, b.MttrHours, b.RootCause, b.FailureMode, b.Status }).ToArray<object>(),
                ["spareParts"] = parts.Select(p => new { p.Id, p.Code, p.Name, p.Category, p.Uom, p.QtyOnHand, p.MinStock, p.UnitCost, p.SupplierName, p.LeadTimeDays, stockStatus = p.StockStatus ?? (IsLowStock(p) ? "low" : "ok") }).ToArray<object>(),
                ["actionEndpoints"] = new[] { new { action = "startWo", endpoint = "/v1/work-orders/{id}/start", type = "B" }, new { action = "completeWo", endpoint = "/v1/work-orders/{id}/complete", type = "B" }, new { action = "issuePart", endpoint = "/v1/spare-parts/{id}/issue", type = "B" } }
            });
    }

    public async Task<UiPageDto> GetSignageAsync(FrontendFilter? filter = null)
    {
        var machines = await _uow.Machines.GetAllAsync();
        var states = await _uow.MachineStates.GetAllAsync();
        return Page("Signage Management", "/v1/ui/signage", "Type D - Read model", UseCases(
            ("UC-T09", "D", "Signage screen refreshes display", "Reads current Eipico machine state", "SignalR/read-only token later")),
            new Dictionary<string, object?>
            {
                ["screens"] = Array.Empty<object>(),
                ["andonBoard"] = machines.Select(machine =>
                {
                    var state = FindMachineState(states, machine);
                    return new
                    {
                        id = machine.Id,
                        machine.Code,
                        name = machine.Name,
                        type = machine.MachineType,
                        machine.LineId,
                        state = state is null ? "offline" : MachineStateLabel(state),
                        speedRpm = state?.Speed,
                        lastTelemetryAt = state?.Timestamp
                    };
                }).ToArray<object>(),
                ["kpiDisplayConfig"] = new[] { new { widget = "OEE", enabled = true, refreshSec = 5 }, new { widget = "Active Alarms", enabled = true, refreshSec = 5 }, new { widget = "Batch Progress", enabled = true, refreshSec = 10 } },
                ["templates"] = Array.Empty<object>(),
                ["futureRealtimeNote"] = "SignalR/live push can consume the same Eipico machine-state read model."
            });
    }


    // Split UI/read-model methods.
    // These intentionally return one section each, so one endpoint maps to one frontend component / one use-case group.
    // Later, each method can be replaced internally with a SQL view, materialized projection, or real-time table without changing frontend contracts.
    private static object? Section(UiPageDto page, string key)
        => page.Sections.TryGetValue(key, out var value)
            ? value
            : new { section = key, message = $"Section '{key}' is not available yet." };

    public async Task<object?> GetOverviewKpisAsync(FrontendFilter? filter = null)
        => await GetOverviewKpisAsyncV1(filter);

    public async Task<object?> GetOverviewOeeTrendAsync(FrontendFilter? filter = null)
    {
        var snapshots = await _uow.OeeSnapshots.GetAllAsync();
        if (filter is not null)
        {
            snapshots = snapshots.Where(x => InRange(x.ComputedAt, filter)).ToList();
        }

        var daily = snapshots
            .Where(x => x.OeePct.HasValue)
            .GroupBy(x => x.ShiftDate.Date)
            .Select(x => new { Date = x.Key, Oee = Math.Round(x.Average(y => y.OeePct!.Value), 1) })
            .OrderBy(x => x.Date)
            .TakeLast(7)
            .ToArray();

        return new
        {
            labels = daily.Select(x => x.Date.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            oee = daily.Select(x => x.Oee).ToArray()
        };
    }

    public async Task<object?> GetOverviewProductionOutputAsync(FrontendFilter? filter = null)
    {
        var readings = await _uow.MachineStates.GetAllAsync();
        if (filter is not null)
            readings = readings.Where(x => InRange(x.Timestamp, filter)).ToList();
        return BuildProductionTrend(readings);
    }

    public async Task<object?> GetOverviewEnergyByMachineAsync(FrontendFilter? filter = null)
    {
        var machines = await _uow.Machines.GetAllAsync();
        var snapshots = await _uow.OeeSnapshots.GetAllAsync();
        if (filter is not null)
        {
            snapshots = snapshots.Where(x => InRange(x.ComputedAt, filter)).ToList();
        }

        return machines.Take(8).Select(x => new
            {
                machineId = x.Id,
                machine = x.Name,
                kwh = snapshots.Where(s => s.MachineId == x.Id)
                    .OrderByDescending(s => s.ComputedAt)
                    .Select(s => s.EnergyKwh)
                    .FirstOrDefault() ?? 0m
            }).ToArray<object>();
    }

    public async Task<object?> GetOverviewActiveAlarmsAsync(FrontendFilter? filter = null)
    {
        var alarms = await _uow.AlarmEvents.GetAllAsync();
        if (filter is not null)
        {
            alarms = alarms.Where(x =>
                (!filter.CustomerId.HasValue || x.CustomerId == filter.CustomerId.Value) &&
                InRange(x.TriggeredAt, filter)).ToList();
        }

        return AlarmCards(alarms).Take(4).ToArray();
    }

    public async Task<object?> GetOverviewFactoriesProductionAsync(int? factoryId = null, int? lineId = null, FrontendFilter? filter = null)
    {
        var factories = await _uow.Factories.GetAllAsync();
        var lines = await _uow.ProductionLines.GetAllAsync();
        var machines = await _uow.Machines.GetAllAsync();
        var readings = await _uow.MachineStates.GetAllAsync();

        if (filter is not null)
        {
            readings = readings.Where(x => InRange(x.Timestamp, filter)).ToList();
        }

        if (lineId.HasValue)
        {
            var lineMachines = machines.Where(m => m.LineId == lineId.Value).ToList();
            return lineMachines.Select(m =>
            {
                var prodCount = readings.Where(x => x.LineId == m.LineId &&
                    (string.Equals(x.MachineId, m.Code, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(x.MachineId, m.Name, StringComparison.OrdinalIgnoreCase)))
                    .Sum(x => x.CountDiff);
                return new
                {
                    id = m.Id,
                    name = m.Name,
                    level = "machine",
                    productionCount = prodCount
                };
            }).ToArray();
        }

        if (factoryId.HasValue)
        {
            var factoryLines = lines.Where(l => l.FactoryId == factoryId.Value).ToList();
            return factoryLines.Select(l =>
            {
                var prodCount = readings.Where(x => x.LineId == l.Id).Sum(x => x.CountDiff);
                return new
                {
                    id = l.Id,
                    name = l.Name,
                    level = "line",
                    productionCount = prodCount
                };
            }).ToArray();
        }

        return factories.Select(f =>
        {
            var factoryLineIds = lines.Where(l => l.FactoryId == f.Id).Select(l => l.Id).ToList();
            var prodCount = readings.Where(x => factoryLineIds.Contains(x.LineId)).Sum(x => x.CountDiff);
            return new
            {
                id = f.Id,
                name = f.Name,
                level = "factory",
                productionCount = prodCount
            };
        }).ToArray();
    }

    public Task<object?> GetOverviewEnergyEfficiencyAsync(FrontendFilter? filter = null)
        => Task.FromResult<object?>(Array.Empty<object>());

    public Task<object?> GetOverviewEnergyBySkuAsync(FrontendFilter? filter = null)
        => Task.FromResult<object?>(Array.Empty<object>());

    public async Task<object?> GetBatchHistoryAsync(FrontendFilter? filter = null)
    {
        var batches = await _uow.Batches.GetAllAsync();
        var skus = await _uow.Skus.GetAllAsync();
        var lines = await _uow.ProductionLines.GetAllAsync();
        var machines = await _uow.Machines.GetAllAsync();

        if (filter is not null)
        {
            batches = ApplyBatchFilter(batches, filter).ToList();
        }

        var historyBatches = batches.Where(x => Eq(x.Status, "finished") || Eq(x.Status, "cancelled")).ToList();

        var rows = historyBatches.Select(b =>
        {
            var sku = skus.FirstOrDefault(s => s.Id == b.SkuId);
            var line = lines.FirstOrDefault(l => l.Id == b.LineId);
            var machine = machines.FirstOrDefault(m => m.LineId == b.LineId);
            var timestamp = b.ActualEnd ?? b.ActualStart ?? b.CreatedAt;

            return new
            {
                id = b.Id,
                batchName = b.Uid,
                skuCode = sku?.Code ?? $"SK-{b.SkuId}",
                skuName = sku?.Name ?? $"SKU #{b.SkuId}",
                jobOrder = b.Id.ToString(),
                timestamp = timestamp,
                machineName = machine?.Name ?? "N/A"
            };
        })
        .OrderByDescending(x => x.timestamp)
        .ToArray();

        var page = filter?.Page <= 0 ? 1 : filter?.Page ?? 1;
        var pageSize = filter?.PageSize <= 0 ? 10 : filter?.PageSize ?? 10;
        pageSize = Math.Min(pageSize, 100);
        var total = rows.Length;
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        var items = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray<object>();

        return new
        {
            items,
            pagination = new
            {
                page,
                pageSize,
                total,
                totalPages,
                hasPrevious = page > 1,
                hasNext = totalPages > 0 && page < totalPages
            }
        };
    }

    public async Task<object?> GetBatchHistoryDetailAsync(int batchId, FrontendFilter? filter = null)
    {
        var batches = await _uow.Batches.GetAllAsync();
        var batch = batches.FirstOrDefault(x =>
            x.Id == batchId &&
            (filter?.CustomerId is null || x.CustomerId == filter.CustomerId.Value) &&
            (filter?.AllowedLineIds is null || filter.AllowedLineIds.Contains(x.LineId)));
        if (batch is null) return null;

        var skus = await _uow.Skus.GetAllAsync();
        var sku = skus.FirstOrDefault(x => x.Id == batch.SkuId);

        var materials = await _uow.Materials.GetAllAsync();
        var batchMaterials = (await _uow.BatchMaterials.GetAllAsync()).Where(x => x.BatchId == batchId).ToList();
        var logs = (await _uow.BatchStageLogs.GetAllAsync()).Where(x => x.BatchId == batchId).OrderBy(x => x.TransitionedAt).ToList();

        var timeline = logs.Select((x, index) => new
        {
            name = x.ToStage,
            fromStage = x.FromStage,
            start = x.TransitionedAt,
            end = index + 1 < logs.Count ? logs[index + 1].TransitionedAt : batch.ActualEnd,
            color = Eq(x.ToStage, "finished") ? "green" : Eq(x.ToStage, "production") ? "yellow" : "blue"
        }).ToArray();

        var materialDetails = batchMaterials.Select(x => new
        {
            materialName = MaterialName(materials, x.MaterialId),
            materialCode = materials.FirstOrDefault(m => m.Id == x.MaterialId)?.Code ?? $"RM-{x.MaterialId}",
            bagWeight = $"{x.PlannedQty} kg",
            actualWeight = x.DispensedQty > 0 ? $"{x.DispensedQty} kg" : "--",
            deviationPct = x.DeviationPct.HasValue ? $"{x.DeviationPct.Value:0.00}%" : "--",
            timestamp = x.DispenseCompletedAt.HasValue ? x.DispenseCompletedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "--"
        }).ToArray();

        return new
        {
            uid = batch.Uid,
            productName = sku?.Name ?? $"SKU #{batch.SkuId}",
            timeline,
            materials = materialDetails
        };
    }

    public async Task<object?> GetOperationsEnergyLoadAsync(FrontendFilter? filter = null) => Section(await GetOperationsAsync(filter), "energyLoadLast8Hours");
    public async Task<object?> GetOperationsMachineUtilizationAsync(FrontendFilter? filter = null) => Section(await GetOperationsAsync(filter), "machineUtilization");
    public async Task<object?> GetOperationsMachineStatusTableAsync(FrontendFilter? filter = null) => Section(await GetOperationsAsync(filter), "machineStatusTable");

    public async Task<object?> GetBatchUidLegendAsync(FrontendFilter? filter = null) => Section(await GetBatchManagementAsync(filter), "uidLegend");
    public async Task<object?>

        GetBatchTableAsync(FrontendFilter? filter = null) => Section(await GetBatchManagementAsync(filter), "table");
    public async Task<object?> GetBatchKanbanAsync(FrontendFilter? filter = null) => Section(await GetBatchManagementAsync(filter), "kanban");
    public async Task<object?> GetBatchDetailDtoAsync(int batchId, FrontendFilter? filter = null) => Section(await GetBatchDetailAsync(batchId, filter), "detail");

    public async Task<object?> GetPlanningGanttAsync(FrontendFilter? filter = null) => Section(await GetPlanningAsync(filter), "gantt");
    public async Task<object?> GetPlanningRoutesAsync(FrontendFilter? filter = null) => Section(await GetPlanningAsync(filter), "routing");
    public async Task<object?> GetPlanningMrpAsync(FrontendFilter? filter = null) => Section(await GetPlanningAsync(filter), "mrp");
    public async Task<object?> GetPlanningStoppagesAsync(FrontendFilter? filter = null) => Section(await GetPlanningAsync(filter), "stoppages");

    public async Task<object?> GetAssetsKpisAsync(FrontendFilter? filter = null) => Section(await GetAssetsAsync(filter), "kpis");
    public async Task<object?> GetAssetsTableAsync(FrontendFilter? filter = null) => Section(await GetAssetsAsync(filter), "assets");

    public async Task<object?> GetAlarmsSummaryAsync(FrontendFilter? filter = null) => Section(await GetAlarmsAsync(filter), "summary");
    public async Task<object?> GetActiveAlarmsAsync(FrontendFilter? filter = null) => Section(await GetAlarmsAsync(filter), "activeAlarms");
    public async Task<object?> GetUserAlarmRulesAsync(FrontendFilter? filter = null) => Section(await GetAlarmsAsync(filter), "userRules");
    public async Task<object?> GetSystemAlarmRulesAsync(FrontendFilter? filter = null) => Section(await GetAlarmsAsync(filter), "systemRules");

    public async Task<object?> GetAnalyticsInsightsAsync(FrontendFilter? filter = null) => Section(await GetAnalyticsAsync(filter), "insights");
    public async Task<object?> GetAnalyticsOeeBreakdownAsync(FrontendFilter? filter = null) => Section(await GetAnalyticsAsync(filter), "oeeBreakdown");
    public async Task<object?> GetAnalyticsHeatmapAsync(FrontendFilter? filter = null) => Section(await GetAnalyticsAsync(filter), "heatmap");
    public async Task<object?> GetAnalyticsFailureRiskAsync(FrontendFilter? filter = null) => Section(await GetAnalyticsAsync(filter), "failureRisk");

    public async Task<object?> GetSkuErpBannerAsync(FrontendFilter? filter = null) => Section(await GetSkuManagementAsync(filter), "erpBanner");
    public async Task<object?> GetSkuListAsync(FrontendFilter? filter = null) => Section(await GetSkuManagementAsync(filter), "skus");
    public async Task<object?> GetMaterialListAsync(FrontendFilter? filter = null) => Section(await GetSkuManagementAsync(filter), "materials");
    public async Task<object?> GetBomListAsync(FrontendFilter? filter = null) => Section(await GetSkuManagementAsync(filter), "bom");
    public async Task<object?> GetSkuBomAsync(int skuId, FrontendFilter? filter = null)
    {
        var bom = (await _uow.BillOfMaterials.GetAllAsync()).Where(x => x.SkuId == skuId).ToList();
        var materials = await _uow.Materials.GetAllAsync();
        if (!bom.Any()) return Array.Empty<object>();
        return bom.Select(x => new
        {
            x.Id,
            x.SkuId,
            x.MaterialId,
            materialName = MaterialName(materials, x.MaterialId),
            x.QtyPerBatch,
            x.TolerancePct,
            x.SequenceNo,
            x.IsCritical,
            type = "Type A/D DTO - BOM versioning rules can be added later"
        }).ToArray<object>();
    }

    public async Task<object?> GetConfigurationGeneralSettingsAsync(FrontendFilter? filter = null) => Section(await GetConfigurationAsync(filter), "generalSettings");
    public async Task<object?> GetConfigurationShiftsAsync(FrontendFilter? filter = null) => Section(await GetConfigurationAsync(filter), "shifts");
    public async Task<object?> GetConfigurationMachineParametersAsync(FrontendFilter? filter = null) => Section(await GetConfigurationAsync(filter), "machineParameters");
    public async Task<object?> GetConfigurationColorCodesAsync(FrontendFilter? filter = null) => Section(await GetConfigurationAsync(filter), "colorCodes");
    public async Task<object?> GetConfigurationRatingThresholdsAsync(FrontendFilter? filter = null) => Section(await GetConfigurationAsync(filter), "ratingThresholds");

    public async Task<object?> GetHierarchyStatsAsync(FrontendFilter? filter = null) => Section(await GetHierarchyAsync(filter), "stats");
    public async Task<object?> GetHierarchyTreeAsync(FrontendFilter? filter = null) => Section(await GetHierarchyAsync(filter), "tree");

    public async Task<object?> GetMaintenanceKpisAsync(FrontendFilter? filter = null) => Section(await GetMaintenanceAsync(filter), "kpis");
    public async Task<object?> GetMaintenanceWorkOrdersAsync(FrontendFilter? filter = null) => Section(await GetMaintenanceAsync(filter), "workOrders");
    public async Task<object?> GetMaintenancePmSchedulesAsync(FrontendFilter? filter = null) => Section(await GetMaintenanceAsync(filter), "pmSchedules");
    public async Task<object?> GetMaintenanceBreakdownHistoryAsync(FrontendFilter? filter = null) => Section(await GetMaintenanceAsync(filter), "breakdownHistory");
    public async Task<object?> GetMaintenanceSparePartsAsync(FrontendFilter? filter = null) => Section(await GetMaintenanceAsync(filter), "spareParts");

    public async Task<object?> GetSignageScreensAsync(FrontendFilter? filter = null) => Section(await GetSignageAsync(filter), "screens");
    public async Task<object?> GetSignageAndonBoardAsync(FrontendFilter? filter = null) => Section(await GetSignageAsync(filter), "andonBoard");
    public async Task<object?> GetSignageKpiDisplayConfigAsync(FrontendFilter? filter = null) => Section(await GetSignageAsync(filter), "kpiDisplayConfig");
    public async Task<object?> GetSignageTemplatesAsync(FrontendFilter? filter = null) => Section(await GetSignageAsync(filter), "templates");

    public Task<IReadOnlyList<UseCaseCoverageDto>> GetUseCaseCoverageAsync()
    {
        return Task.FromResult<IReadOnlyList<UseCaseCoverageDto>>(CoverageRows());
    }

    // Helper methods
    private static UiPageDto Page(string page, string endpoint, string type, IReadOnlyList<UseCaseLinkDto> useCases, IReadOnlyDictionary<string, object?> sections)
        => new(page, endpoint, type, "Frontend binds to this DTO endpoint. Entity CRUD endpoints are admin/internal and should not be used directly by the UI.", useCases, sections);

    private static IReadOnlyList<UseCaseLinkDto> UseCases(params (string Code, string Type, string Summary, string Implementation, string Future)[] items)
        => items.Select(x => new UseCaseLinkDto(x.Code, x.Type, x.Summary, x.Implementation, x.Future)).ToArray();


    private static bool InRange(DateTime? value, FrontendFilter? filter)
    {
        if (filter is null || value is null) return true;
        if (!filter.ApplyTimeFilter) return true;
        return value.Value >= filter.From && value.Value <= filter.To;
    }

    private static bool MatchesLine(int? lineId, FrontendFilter? filter)
    {
        return filter?.LineId is null || lineId == filter.LineId.Value;
    }

    private static bool MatchesFactory(int? factoryId, FrontendFilter? filter)
    {
        return filter?.FactoryId is null || factoryId == filter.FactoryId.Value;
    }

    private static IEnumerable<Batch> ApplyBatchFilter(IEnumerable<Batch> batches, FrontendFilter? filter)
    {
        return batches.Where(x =>
            (filter?.CustomerId is null || x.CustomerId == filter.CustomerId.Value) &&
            (filter?.AllowedLineIds is null || filter.AllowedLineIds.Contains(x.LineId)) &&
            MatchesLine(x.LineId, filter) &&
            InRange(x.ActualStart ?? x.PlannedStart, filter) &&
            (string.IsNullOrWhiteSpace(filter?.Status) || Eq(x.Status, filter.Status)) &&
            (string.IsNullOrWhiteSpace(filter?.Search) || x.Uid.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<ProductionSchedule> ApplyScheduleFilter(IEnumerable<ProductionSchedule> rows, FrontendFilter? filter)
    {
        return rows.Where(x => MatchesLine(x.LineId, filter) && InRange(x.ScheduledStart, filter));
    }

    private static IEnumerable<WorkOrder> ApplyWorkOrderFilter(IEnumerable<WorkOrder> rows, FrontendFilter? filter)
    {
        return rows.Where(x => MatchesFactory(x.FactoryId, filter) && InRange(x.ActualStart ?? x.PlannedStart ?? x.CreatedAt, filter));
    }

    private static bool Eq(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static MachineState? FindMachineState(IEnumerable<MachineState> states, Machine machine)
        => states
            .Where(x => x.LineId == machine.LineId &&
                        (string.Equals(x.MachineId, machine.Code, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.MachineId, machine.Name, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();

    private static string MachineStateLabel(MachineState state)
    {
        if (state.Fault != 0) return "fault";
        return state.State switch
        {
            1 => "running",
            0 => "idle",
            2 => "offline",
            3 => "maintenance",
            _ => "unknown"
        };
    }

    private static decimal Percent(decimal current, decimal total) => total <= 0 ? 0 : Math.Round(current / total * 100m, 2);
    private static string MaterialName(IEnumerable<Material> materials, int? materialId) => materialId.HasValue ? materials.FirstOrDefault(x => x.Id == materialId.Value)?.Name ?? $"Material #{materialId.Value}" : "Unassigned";
    private static bool IsLowStock(SparePart p) => p.MinStock.HasValue && p.QtyOnHand <= p.MinStock.Value;
    private static int CountSeverity(IEnumerable<AlarmEvent> alarms, AlarmSeverity severity)
        => alarms.Count(x => x.ResolvedAt == null && x.AcknowledgedAt == null && x.Severity == severity);

    private static object Trend(string title, IReadOnlyList<string> labels, IReadOnlyList<decimal> values) => new { title, labels, series = new[] { new { name = title, values } } };

    private static IEnumerable<object> AlarmCards(IEnumerable<AlarmEvent> alarms)
        => alarms.Where(x => x.ResolvedAt == null && x.AcknowledgedAt == null).OrderByDescending(x => x.TriggeredAt).Select(a => new { id = a.Id, title = a.Title, severity = a.Severity, tagName = a.TagName, triggerValue = a.TriggerValue, triggeredAt = a.TriggeredAt, acknowledged = false, source = a.MachineId.HasValue ? $"Machine {a.MachineId}" : a.SiloId.HasValue ? $"Silo {a.SiloId}" : a.DeviceId.HasValue ? $"Device {a.DeviceId}" : "System" });

    private static IEnumerable<object> BatchCards(IEnumerable<Batch> batches, IEnumerable<Sku> skus, IEnumerable<ProductionLine> lines, IEnumerable<AlarmEvent> alarms)
        => batches.OrderByDescending(x => x.Priority).ThenBy(x => x.PlannedStart).Select(b =>
        {
            var sku = skus.FirstOrDefault(s => s.Id == b.SkuId);
            var line = lines.FirstOrDefault(l => l.Id == b.LineId);
            var plannedQty = b.PlannedQty.GetValueOrDefault();
            var progress = plannedQty <= 0 ? 0 : Math.Round((decimal)b.ProducedQty / plannedQty * 100m, 2);
            return new
            {
                id = b.Id,
                uid = b.Uid,
                skuId = b.SkuId,
                skuCode = sku?.Code,
                skuName = sku?.Name ?? $"SKU #{b.SkuId}",
                factoryId = line?.FactoryId,
                lineId = b.LineId,
                lineName = line?.Name ?? $"Line #{b.LineId}",
                status = b.Status,
                stage = b.Stage,
                plannedQty = b.PlannedQty,
                producedQty = b.ProducedQty,
                rejectedQty = b.RejectedQty,
                progressPct = progress,
                plannedStart = b.PlannedStart,
                plannedEnd = b.PlannedEnd,
                actualStart = b.ActualStart,
                actualEnd = b.ActualEnd,
                priority = b.Priority,
                hasActiveAlarm = alarms.Any(a => a.ResolvedAt == null && (a.Notes ?? string.Empty).Contains(b.Uid, StringComparison.OrdinalIgnoreCase)),
                detailEndpoint = $"/v1/batches/{b.Id}/detail"
            };
        });

    private static object GroupKanban(IEnumerable<object> cards)
    {
        var cardList = cards.ToList();
        return new[]
        {
            new { key = "in_progress", title = "In Progress", cards = cardList.Where(x => Eq(GetProp(x, "status")?.ToString(), "in_progress")).ToArray() },
            new { key = "production", title = "Production", cards = cardList.Where(x => Eq(GetProp(x, "status")?.ToString(), "production")).ToArray() },
            new { key = "finished", title = "Finished", cards = cardList.Where(x => Eq(GetProp(x, "status")?.ToString(), "finished")).ToArray() },
            new { key = "planned", title = "Planned", cards = cardList.Where(x => Eq(GetProp(x, "status")?.ToString(), "planned")).ToArray() }
        };
    }

    private static object? GetProp(object item, string name) => item.GetType().GetProperty(name)?.GetValue(item);

    private static object BatchDetail(Batch b, IReadOnlyList<Sku> skus, IReadOnlyList<ProductionLine> lines, IReadOnlyList<BatchMaterial> batchMaterials, IReadOnlyList<Material> materials, IReadOnlyList<BillOfMaterial> boms, IReadOnlyList<BatchStageLog> logs, IReadOnlyList<AlarmEvent> alarms)
    {
        var sku = skus.FirstOrDefault(x => x.Id == b.SkuId);
        var line = lines.FirstOrDefault(x => x.Id == b.LineId);
        var bomToleranceByMaterial = boms
            .Where(x => x.SkuId == b.SkuId)
            .ToDictionary(x => x.MaterialId, x => x.TolerancePct);
        return new
        {
            batch = new { b.Id, b.Uid, b.CustomerId, b.SkuId, skuCode = sku?.Code, skuName = sku?.Name, factoryId = line?.FactoryId, b.LineId, lineName = line?.Name, b.RouteId, b.Status, b.Stage, b.PlannedQty, b.ProducedQty, b.RejectedQty, progressPct = b.PlannedQty.GetValueOrDefault() <= 0 ? 0 : Math.Round((decimal)b.ProducedQty / b.PlannedQty.GetValueOrDefault() * 100m, 2), b.PlannedStart, b.PlannedEnd, b.ActualStart, b.ActualEnd, b.Priority, b.Notes },
            materials = batchMaterials.Where(x => x.BatchId == b.Id).Select(x => new { x.Id, x.MaterialId, materialCode = materials.FirstOrDefault(m => m.Id == x.MaterialId)?.Code, materialName = MaterialName(materials, x.MaterialId), x.MaterialUid, x.SiloId, x.ScaleId, x.PlannedQty, x.DispensedQty, deviationQty = x.DispensedQty > 0 ? Math.Abs(x.DispensedQty - x.PlannedQty) : (decimal?)null, x.DeviationPct, tolerancePct = ResolveTolerancePct(bomToleranceByMaterial, x.MaterialId), minAllowedQty = MinAllowedQty(x.PlannedQty, ResolveTolerancePct(bomToleranceByMaterial, x.MaterialId)), maxAllowedQty = MaxAllowedQty(x.PlannedQty, ResolveTolerancePct(bomToleranceByMaterial, x.MaterialId)), x.Status, timestamp = x.DispenseCompletedAt, x.DispenseStartedAt, x.DispenseCompletedAt }).ToArray(),
            stageTimeline = logs.Where(x => x.BatchId == b.Id).OrderBy(x => x.TransitionedAt).Select(x => new { x.Id, x.FromStage, x.ToStage, x.TransitionedAt, x.MachineId, x.Notes }).ToArray(),
            alarms = alarms.Where(x => x.ResolvedAt == null && ((x.Notes ?? string.Empty).Contains(b.Uid, StringComparison.OrdinalIgnoreCase))).Select(x => new { x.Id, x.Title, x.Severity, x.TriggeredAt, x.AcknowledgedAt, x.ResolvedAt }).ToArray(),
            future = new { routeStepStatus = "Future UC-R05/R06 route execution model", exportPdf = "Future UC-B14 PDF with signatures/hash" }
        };
    }

    private static decimal ResolveTolerancePct(IReadOnlyDictionary<int, decimal> toleranceByMaterial, int materialId)
        => toleranceByMaterial.TryGetValue(materialId, out var tolerancePct) ? tolerancePct : 2m;

    private static decimal MinAllowedQty(decimal plannedQty, decimal tolerancePct)
        => Math.Round(plannedQty * (1m - tolerancePct / 100m), 4);

    private static decimal MaxAllowedQty(decimal plannedQty, decimal tolerancePct)
        => Math.Round(plannedQty * (1m + tolerancePct / 100m), 4);

    private static IReadOnlyList<UseCaseCoverageDto> CoverageRows()
    {
        var rows = new List<UseCaseCoverageDto>();
        void Add(string domain, string code, string type, string area, string endpoint, string service, string data, string mode, string future)
            => rows.Add(new UseCaseCoverageDto(domain, code, type, area, endpoint, ResolveController(endpoint), service, data, mode, future));

        foreach (var c in new[] { "UC-S01", "UC-S02", "UC-S03", "UC-S04", "UC-S05", "UC-S06" }) Add("D1 Settings", c, "B", "Configuration", "/v1/ui/configuration", "ConfigService", "Settings/Thresholds/Shifts/Users", "DTO + existing workflow targets", "Audit/cache/RBAC hardening later");
        foreach (var c in new[] { "UC-T01", "UC-T02", "UC-T03", "UC-T04", "UC-T05", "UC-T06", "UC-T07", "UC-T08" }) Add("D2 Telemetry", c, "C", "SCADA/Operations/Overview", "/v1/scada/dashboard", "ScadaLiveViewService", "Silo/Scale/MachineState/OEE", "Current database snapshots", "Integration runtime/SignalR later");
        Add("D2 Telemetry", "UC-T09", "D", "Signage", "/v1/ui/signage", "SignageQueryService", "OEE/Alarm/Batch", "Static DTO now", "SignalR signage client later");
        foreach (var c in new[] { "UC-MF01", "UC-MF02", "UC-MF03", "UC-MF04", "UC-MF10", "UC-MF11" }) Add("D3 Material Flow", c, "B", "SCADA/Operations", "/v1/scada/dashboard", "ScadaLiveViewService", "Silo/Material/Batch", "Current database snapshots + selected manual workflows", "Full lot/inventory movement later");
        foreach (var c in new[] { "UC-MF05", "UC-MF06", "UC-MF07", "UC-MF08", "UC-MF09" }) Add("D3 Material Flow", c, "C", "SCADA/Batch dispensing", "/v1/scada/dashboard", "ScadaLiveViewService", "BatchMaterial/Scale/MachineState", "Current database snapshots", "Stable scale/machine integration later");
        foreach (var c in new[] { "UC-B01", "UC-B02", "UC-B03", "UC-B04", "UC-B05", "UC-B06", "UC-B09", "UC-B11", "UC-B12" }) Add("D4 Batch", c, "B", "Batch Management", "/v1/ui/batches", "BatchWorkflowService", "Batch/BatchMaterial/BatchStageLog", "Workflow or DTO now", "ERP/audit/validation hardening later");
        foreach (var c in new[] { "UC-B07", "UC-B08", "UC-B10" }) Add("D4 Batch", c, "C", "Batch Dispensing/Alarms", "/v1/ui/batches", "BatchDispenseService", "BatchMaterial/Scale/AlarmEvent", "Manual weight fallback now", "Scale telemetry later");
        foreach (var c in new[] { "UC-B13", "UC-B14" }) Add("D4 Batch", c, "D", "Batch Management", "/v1/ui/batches", "BatchQueryService", "Batch/Sku/Line/Alarms/OEE", "DTO now", "SQL view/live push/PDF later");
        foreach (var c in new[] { "UC-R01", "UC-R02", "UC-R03", "UC-R04", "UC-R06", "UC-R07" }) Add("D5 Route Planning", c, "B", "Planning", "/v1/ui/planning", "RouteWorkflowService", "Route/RouteStep/Batch", "DTO + some workflow now", "Route execution detail later");
        Add("D5 Route Planning", "UC-R05", "C", "SCADA/Route Execution", "/v1/ui/planning", "RouteExecutionService", "RouteStepExecution/MachineState", "Static/DTO now", "Machine state validation later");
        for (int i = 1; i <= 14; i++) Add("D6 Master Data", $"UC-MD{i:00}", "A", "Management screens", "/v1/ui/sku-management or /v1/ui/hierarchy", "Entity services", "Master data tables", "DTO frontend endpoints now; entity CRUD hidden/internal", "Uniqueness/reference/audit rules later");
        foreach (var c in new[] { "UC-V01", "UC-V04", "UC-V05", "UC-V06", "UC-V07", "UC-V08" }) Add("D7 Verification", c, "B", "Cross-cutting", "/v1/ui/use-case-coverage", "Validation/RBAC/Audit services", "Batch/PM/Silo/User/Audit", "Partially represented", "Policy/interceptor implementation later");
        foreach (var c in new[] { "UC-V02", "UC-V03" }) Add("D7 Verification", c, "C", "Dispense/Telemetry", "/v1/ui/use-case-coverage", "Tolerance/DeviceAuth", "BatchMaterial/Device/Alarm", "Manual/fallback now", "Telemetry/auth pipeline later");
        foreach (var c in new[] { "UC-U01", "UC-U02", "UC-U03", "UC-U04", "UC-U05", "UC-U06" }) Add("D8 User Management", c, "B", "Users/Admin", "/v1/users or /v1/auth", "UserManagement/Identity services", "ApplicationUser/Role", "Static/Identity baseline", "Full UserManager/JWT/MFA later");
        foreach (var c in new[] { "UC-U07", "UC-U08" }) Add("D8 User Management", c, "D", "Audit", "/v1/ui/use-case-coverage", "AuditQuery/Export", "AuditLog", "Static/export placeholder", "Signed exports later");
        foreach (var c in new[] { "UC-L01", "UC-L10", "UC-L14" }) Add("D9 Log Adapter", c, "B", "Configuration/Log Adapters", "/v1/ui/use-case-coverage", "LogAdapterConfig/ParseError", "LogSource/FieldMapping/Errors", "Static coverage now", "Adapter config module later");
        foreach (var c in new[] { "UC-L02", "UC-L03", "UC-L04", "UC-L05", "UC-L06", "UC-L07", "UC-L08", "UC-L09", "UC-L11" }) Add("D9 Log Adapter", c, "C", "Adapter pipeline", "/v1/ui/use-case-coverage", "Log adapter worker", "TelemetryRaw/Tags/ParseErrors", "Future pipeline", "File watcher/parser/dedup later");
        foreach (var c in new[] { "UC-L12", "UC-L13" }) Add("D9 Log Adapter", c, "D", "Analytics/Adapter health", "/v1/ui/analytics", "TelemetryAggregate/AdapterHealth", "TelemetryAggregate", "Static/read fallback", "Scheduled aggregate/health monitor later");
        return rows;
    }

    private static string ResolveController(string endpoint)
    {
        if (endpoint.Contains("/ui/")) return "UiController";
        if (endpoint.Contains("/batches")) return "BatchesController";
        if (endpoint.Contains("/config")) return "ConfigController";
        if (endpoint.Contains("/users")) return "UsersController";
        if (endpoint.Contains("/auth")) return "AuthController";
        return "Mapped controller / future worker";
    }

    private static object BuildOeeTrend(IEnumerable<OeeSnapshot> snapshots)
    {
        var points = snapshots
            .Where(x => x.OeePct.HasValue)
            .GroupBy(x => x.ShiftDate.Date)
            .OrderBy(x => x.Key)
            .TakeLast(7)
            .Select(x => new
            {
                label = x.Key.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture),
                value = Math.Round(x.Average(y => y.OeePct!.Value), 2)
            })
            .ToArray();
        return new
        {
            labels = points.Select(x => x.label).ToArray(),
            oee = points.Select(x => x.value).ToArray()
        };
    }

    private static object BuildProductionTrend(IEnumerable<MachineState> readings)
    {
        var points = readings
            .GroupBy(x => new DateTime(x.Timestamp.Year, x.Timestamp.Month, x.Timestamp.Day, x.Timestamp.Hour, 0, 0))
            .OrderBy(x => x.Key)
            .TakeLast(8)
            .Select(x => new
            {
                label = x.Key.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                value = x.Sum(y => y.CountDiff)
            })
            .ToArray();
        return Trend("Production Trend", points.Select(x => x.label).ToArray(), points.Select(x => x.value).ToArray());
    }

    private static object BuildEnergyTrend(IEnumerable<OeeSnapshot> snapshots)
    {
        var points = snapshots
            .Where(x => x.EnergyKwh.HasValue)
            .GroupBy(x => new DateTime(x.ComputedAt.Year, x.ComputedAt.Month, x.ComputedAt.Day, x.ComputedAt.Hour, 0, 0))
            .OrderBy(x => x.Key)
            .TakeLast(8)
            .Select(x => new
            {
                label = x.Key.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                value = x.Sum(y => y.EnergyKwh ?? 0m)
            })
            .ToArray();
        return Trend("Energy Load", points.Select(x => x.label).ToArray(), points.Select(x => x.value).ToArray());
    }

    private static object BuildOeeBreakdown(IEnumerable<OeeSnapshot> snapshots)
    {
        var points = snapshots
            .GroupBy(x => x.ShiftDate.Date)
            .OrderBy(x => x.Key)
            .TakeLast(7)
            .Select(x => new
            {
                label = x.Key.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture),
                availability = AverageOrNull(x.Select(y => y.AvailabilityPct)),
                performance = AverageOrNull(x.Select(y => y.PerformancePct)),
                quality = AverageOrNull(x.Select(y => y.QualityPct))
            })
            .ToArray();
        return new
        {
            labels = points.Select(x => x.label).ToArray(),
            availability = points.Select(x => x.availability).ToArray(),
            performance = points.Select(x => x.performance).ToArray(),
            quality = points.Select(x => x.quality).ToArray()
        };
    }

    private static object[] BuildHeatmap(IEnumerable<MachineState> readings)
        => readings
            .GroupBy(x => new { day = (int)x.Timestamp.DayOfWeek, slot = x.Timestamp.Hour / 2 })
            .OrderBy(x => x.Key.day)
            .ThenBy(x => x.Key.slot)
            .Select(x => (object)new { x.Key.day, x.Key.slot, intensity = x.Sum(y => y.CountDiff) })
            .ToArray();

    private static decimal? AverageOrNull(IEnumerable<decimal?> values)
    {
        var actual = values.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return actual.Length == 0 ? null : Math.Round(actual.Average(), 2);
    }

    private static string? FormatPercent(decimal? value)
        => value.HasValue ? $"{value.Value:0.0}%" : null;

    private static (decimal? Oee, decimal? Availability, decimal? Performance, decimal? Quality)
        CalculateRealtimeKpis(
            IReadOnlyCollection<MachineState> readings,
            IReadOnlyCollection<Machine> machines)
    {
        if (readings.Count == 0)
            return (null, null, null, null);

        var availability = Math.Round(
            (decimal)readings.Count(x => x.State == 1 && x.Fault == 0) / readings.Count,
            6);
        var ratedSpeedByExternalId = machines
            .Where(x => x.RatedSpeedRpm is > 0)
            .SelectMany(x => new[] { (Key: x.Code, Value: x.RatedSpeedRpm!.Value), (Key: x.Name, Value: x.RatedSpeedRpm!.Value) })
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.Single().Value, StringComparer.OrdinalIgnoreCase);
        var performanceValues = readings
            .Where(x => ratedSpeedByExternalId.ContainsKey(x.MachineId))
            .Select(x => Math.Clamp((decimal)x.Speed / ratedSpeedByExternalId[x.MachineId], 0m, 1m))
            .ToArray();
        decimal? performance = performanceValues.Length == 0
            ? null
            : Math.Round(performanceValues.Average(), 6);
        var output = readings.Sum(x => x.CountDiff);
        var rejected = readings.Sum(x => x.QRejectDiff);
        decimal? quality = output <= 0m
            ? null
            : Math.Round(Math.Clamp((output - rejected) / output, 0m, 1m), 6);
        decimal? oee = performance.HasValue && quality.HasValue
            ? Math.Round(availability * performance.Value * quality.Value, 6)
            : null;
        return (oee, availability, performance, quality);
    }

    private static object[] DefaultColorCodes() =>
    [
        new { metric = "machineState", label = "Running", color = "#28A745" },
        new { metric = "machineState", label = "Alarm", color = "#DC3545" },
        new { metric = "oee", label = "Excellent", color = "#1872ED" },
        new { metric = "fallback", label = "Default", color = "#6C757D" }
    ];
}
