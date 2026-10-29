using Application.Abstractions.Services;
using Application.Common.Workflow;
using Domain.Entities.Planning;
using Domain.Entities.Production;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

// SQL persistence and cross-request locking live here; the controller contains no planning calculations.
public sealed class PlanningService(MesDbContext db, IRouteWorkflowService routes) : IPlanningService
{
    public async Task<object> ReadAsync(string section, PlanningScope scope, int page = 1, int pageSize = 10)
    {
        var lineIds = scope.LineFactories.Keys.ToArray();
        var periods = scope.Periods.Values.OrderBy(x => x.FactoryId).ToArray();
        if (section == "routes") return await RouteRows(scope);
        if (section == "mrp") return await MrpRows(scope);
        var schedules = await db.ProductionSchedules.AsNoTracking()
            .Where(x => x.CustomerId == scope.CustomerId && lineIds.Contains(x.LineId) && !x.IsDeleted).ToListAsync();
        var selectedSchedules = schedules.Where(x => InWindow(scope, x.LineId, x.ScheduledStart, x.ScheduledEnd)).ToArray();
        var allBatches = await db.Batches.AsNoTracking().Include(x => x.Sku).Include(x => x.Line)
            .Where(x => x.CustomerId == scope.CustomerId && lineIds.Contains(x.LineId) && !x.IsDeleted).ToListAsync();
        // A batch with a schedule is selected by that schedule, not also by a stale planned/actual date.
        var scheduledIds = schedules.Where(x => !x.IsStoppage && x.BatchId.HasValue).Select(x => x.BatchId!.Value).ToHashSet();
        var selectedIds = selectedSchedules.Where(x => !x.IsStoppage && x.BatchId.HasValue).Select(x => x.BatchId!.Value).ToHashSet();
        var batches = allBatches.Where(x => scheduledIds.Contains(x.Id) ? selectedIds.Contains(x.Id) :
            InWindow(scope, x.LineId, x.PlannedStart, x.PlannedEnd)).OrderBy(x => x.PlannedStart).ThenBy(x => x.Id).ToArray();
        var cards = batches.Select(BatchRow).ToArray();
        var stops = selectedSchedules.Where(x => x.IsStoppage).OrderBy(x => x.ScheduledStart).Select(ScheduleRow).ToArray();
        if (section == "batches") return new { periods, items = cards.Skip((int)Math.Min(int.MaxValue, (page - 1L) * pageSize)).Take(pageSize),
            pagination = new { page, pageSize, total = cards.Length, totalPages = (int)Math.Ceiling(cards.Length / (double)pageSize),
                hasPrevious = page > 1, hasNext = (long)page * pageSize < cards.Length } };
        if (section == "stoppages") return new { periods, count = stops.Length, items = stops };
        var counted = batches.Where(x => PlanningRules.IncludedInTotals(x.Status)).ToArray();
        if (section == "planned-vs-produced") return new { periods, items = counted.Select(x => new
            { batchId = x.Id, x.Uid, productName = x.Sku.Name, x.PlannedQty, x.ProducedQty }).ToArray() };
        if (section == "summary")
        {
            var planned = counted.Sum(x => (long)x.PlannedQty.GetValueOrDefault());
            var produced = counted.Sum(x => (long)x.ProducedQty);
            var routeList = await db.Routes.AsNoTracking().Where(x => x.CustomerId == scope.CustomerId && !x.IsDeleted).ToListAsync();
            var mrp = await MrpData(scope);
            return new { periods, plannedQty = planned, producedQty = produced,
                attainmentPct = PlanningRules.Attainment(produced, planned),
                activeCount = counted.Count(x => x.Status is "in_progress" or "production"),
                upcomingCount = counted.Count(x => x.Status == "planned" && x.PlannedStart.HasValue && x.PlannedStart.Value > DateTime.UtcNow),
                finishedCount = counted.Count(x => x.Status == "finished"),
                routeReadiness = new { active = routeList.Count(x => x.Status == "active"), total = routeList.Count, scope = "customer" },
                mrp = MrpSummary(mrp), stoppagesCount = stops.Length };
        }
        var lines = await db.ProductionLines.AsNoTracking().Where(x => lineIds.Contains(x.Id))
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Code, x.FactoryId }).ToArrayAsync();
        var byId = allBatches.ToDictionary(x => x.Id);
        var bars = selectedSchedules.Where(x => !x.IsStoppage && x.BatchId.HasValue &&
            byId.TryGetValue(x.BatchId.Value, out var b) && PlanningRules.IncludedInTotals(b.Status))
            .Select(x => (object)new { schedule = ScheduleRow(x), batch = BatchRow(byId[x.BatchId!.Value]) }).ToList();
        // Existing batches with planned dates are visible before explicit scheduling, without fabricated end times.
        bars.AddRange(batches.Where(x => !scheduledIds.Contains(x.Id) && PlanningRules.IncludedInTotals(x.Status))
            .Select(x => (object)new { schedule = (object)new { id = (int?)null, batchId = x.Id, x.LineId,
                start = Utc(x.PlannedStart), end = Utc(x.PlannedEnd), isStoppage = false, source = "batch_planned_dates" }, batch = BatchRow(x) }));
        var mode = periods.Select(x => x.Mode).Distinct().ToArray();
        var title = mode.Length == 1 ? "Production schedule - " + mode[0].Replace('_', ' ') : "Production schedule - selected periods";
        return new { periods, title, nowUtc = DateTime.UtcNow, lines,
            shifts = await ShiftRows(scope), batches = bars, stoppages = stops };
    }

    public async Task<object> FormOptionsAsync(PlanningScope scope)
    {
        var ids = scope.LineFactories.Keys.ToArray();
        var factories = scope.LineFactories.Values.Distinct().ToArray();
        var planned = await db.Batches.AsNoTracking().Include(x => x.Sku)
            .Where(x => x.CustomerId == scope.CustomerId && ids.Contains(x.LineId) && x.Status == "planned" && !x.IsDeleted &&
                !db.ProductionSchedules.Any(s => !s.IsDeleted && !s.IsStoppage && s.BatchId == x.Id))
            .OrderBy(x => x.PlannedStart).ToListAsync();
        return new {
            factories = await db.Factories.AsNoTracking().Where(x => factories.Contains(x.Id))
                .Select(x => new { value = x.Id, label = x.Name }).ToArrayAsync(),
            lines = await db.ProductionLines.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new { value = x.Id, label = x.Name, x.FactoryId }).ToArrayAsync(),
            batches = planned.Select(x => new { value = x.Id, label = x.Uid, productName = x.Sku.Name,
                x.LineId, x.PlannedQty, start = Utc(x.PlannedStart), end = Utc(x.PlannedEnd) }),
            skus = await db.Skus.AsNoTracking().Where(x => x.CustomerId == scope.CustomerId && x.IsActive && !x.IsDeleted)
                .Select(x => new { value = x.Id, label = x.Name, x.Code }).ToArrayAsync(),
            routeStatuses = new[] { "draft", "active" },
            stoppageTypes = new[] { "maintenance", "cleaning", "calibration" }
        };
    }

    public async Task<WorkflowResult<object>> ScheduleAsync(PlanningScope scope, ScheduleBatchCommand command)
    {
        if (!ValidRange(command.Start, command.End)) return Fail("invalid_period", "Start must be set and earlier than End.");
        var batch = await db.Batches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == command.BatchId &&
            x.CustomerId == scope.CustomerId && !x.IsDeleted);
        if (batch == null || !scope.LineFactories.ContainsKey(batch.LineId)) return Fail("forbidden", "Batch is outside your scope.");
        return await LockedWrite($"planning:line:{batch.LineId}", async () =>
        {
            var current = await db.Batches.SingleAsync(x => x.Id == command.BatchId);
            await db.Entry(current).ReloadAsync();
            if (current.IsDeleted || current.CustomerId != scope.CustomerId || current.LineId != batch.LineId ||
                current.Status != "planned") return Fail("invalid_batch", "Only an accessible planned batch can be scheduled.");
            if (await db.ProductionSchedules.AnyAsync(x => !x.IsDeleted && !x.IsStoppage && x.BatchId == current.Id))
                return Fail("already_scheduled", "This batch already has a schedule.");
            var start = command.Start.UtcDateTime;
            var end = command.End.UtcDateTime;
            if (await HasOverlap(scope.CustomerId, current.LineId, start, end, current.Id))
                return Fail("overlap", "The line already has production or a stoppage in this window.");
            var schedule = new ProductionSchedule { CustomerId = scope.CustomerId, BatchId = current.Id,
                LineId = current.LineId, ScheduledStart = start, ScheduledEnd = end, Notes = command.Notes };
            db.ProductionSchedules.Add(schedule);
            current.PlannedStart = start;
            current.PlannedEnd = end;
            // A previously selected shift may no longer match the new dates.
            current.ShiftId = await MatchingShift(current.LineId, start, end);
            await db.SaveChangesAsync();
            return WorkflowResult<object>.Ok(ScheduleRow(schedule));
        });
    }

    public async Task<WorkflowResult<object>> StoppageAsync(PlanningScope scope, PlanningStoppageCommand command)
    {
        if (!scope.LineFactories.ContainsKey(command.LineId)) return Fail("forbidden", "Line is outside your scope.");
        if (!ValidRange(command.Start, command.End)) return Fail("invalid_period", "Start must be set and earlier than End.");
        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Length > 200) return Fail("invalid_title", "Title is required (max 200).");
        var type = command.Type?.Trim().ToLowerInvariant();
        if (type is not ("maintenance" or "cleaning" or "calibration")) return Fail("invalid_type", "Choose a type from form-options.");
        return await LockedWrite($"planning:line:{command.LineId}", async () =>
        {
            var start = command.Start.UtcDateTime;
            var end = command.End.UtcDateTime;
            if (await HasOverlap(scope.CustomerId, command.LineId, start, end, null))
                return Fail("overlap", "The line already has production or a stoppage in this window.");
            var schedule = new ProductionSchedule { CustomerId = scope.CustomerId, LineId = command.LineId,
                IsStoppage = true, Title = command.Title.Trim(), StoppageType = type,
                ScheduledStart = start, ScheduledEnd = end, Notes = command.Notes };
            db.ProductionSchedules.Add(schedule);
            await db.SaveChangesAsync();
            return WorkflowResult<object>.Ok(ScheduleRow(schedule));
        });
    }

    public async Task<WorkflowResult<object>> RouteAsync(PlanningScope scope, PlanningRouteCommand command)
    {
        var status = command.Status?.Trim().ToLowerInvariant();
        if (status is not ("draft" or "active") || command.CycleHours <= 0 || command.CycleHours > 9999.99m ||
            command.Steps == null || command.Steps.Length is < 1 or > 100 ||
            command.Steps.Any(x => string.IsNullOrWhiteSpace(x) || x.Trim().Length > 80))
            return Fail("invalid_route", "Supply draft/active, positive cycle hours, and 1-100 steps of at most 80 characters.");
        return await LockedWrite($"planning:sku:{command.SkuId}", async () =>
        {
            var sku = await db.Skus.AsNoTracking().FirstOrDefaultAsync(x => x.Id == command.SkuId &&
                x.CustomerId == scope.CustomerId && x.IsActive && !x.IsDeleted);
            if (sku == null) return Fail("forbidden", "SKU is outside your scope or inactive.");
            var version = (await db.Routes.Where(x => x.CustomerId == scope.CustomerId && x.SkuId == sku.Id)
                .Select(x => (int?)x.Version).MaxAsync() ?? 0) + 1;
            if (version > short.MaxValue) return Fail("invalid_version", "Route version limit reached.");
            var route = new ProductionRoute { CustomerId = scope.CustomerId, SkuId = sku.Id,
                Code = "RT-" + Guid.NewGuid().ToString("N")[..24], Name = sku.Name.Length > 150 ? sku.Name[..150] : sku.Name,
                CycleTimeHours = command.CycleHours, Version = (short)version, Status = "draft" };
            db.Routes.Add(route);
            await db.SaveChangesAsync();
            var steps = command.Steps.Select((name, index) => new RouteStep { RouteId = route.Id,
                StepNo = (short)(index + 1), StepName = name.Trim() }).ToArray();
            var replaced = await routes.ReplaceStepsAsync(route.Id, steps);
            if (!replaced.Succeeded) return Fail(replaced.ErrorCode!, replaced.Message!);
            if (status == "active")
            {
                var activated = await routes.UpdateStatusAsync(route.Id, "active");
                if (!activated.Succeeded) return Fail(activated.ErrorCode!, activated.Message!);
            }
            return WorkflowResult<object>.Ok(new { route.Id, route.Code, route.SkuId, productName = sku.Name,
                route.Status, route.Version, cycleHours = route.CycleTimeHours,
                steps = steps.Select(x => new { x.Id, x.StepNo, x.StepName }) });
        });
    }

    private async Task<WorkflowResult<object>> LockedWrite(string resource, Func<Task<WorkflowResult<object>>> action)
    {
        // Transaction-owned SQL Server application locks also work across multiple API instances.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51000, 'Planning resource is busy; retry the request.', 1;");
                var result = await action();
                if (result.Succeeded) await transaction.CommitAsync();
                else { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); }
                return result;
            }
            catch { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); throw; }
        });
    }

    private async Task<bool> HasOverlap(int customerId, int lineId, DateTime start, DateTime end, int? excludeBatch)
    {
        if (await db.ProductionSchedules.AnyAsync(x => x.CustomerId == customerId && x.LineId == lineId && !x.IsDeleted &&
            (x.IsStoppage || x.Batch != null && !x.Batch.IsDeleted && x.Batch.Status != "cancelled" && x.Batch.Status != "split") &&
            x.ScheduledStart < end && x.ScheduledEnd > start)) return true;
        // Also reserve dates of existing, unscheduled batches; do not silently overwrite their planned slots.
        return await db.Batches.AnyAsync(x => x.CustomerId == customerId && x.LineId == lineId && !x.IsDeleted &&
            (!excludeBatch.HasValue || x.Id != excludeBatch.Value) && x.Status != "cancelled" && x.Status != "split" &&
            x.PlannedEnd.HasValue && x.PlannedStart < end && x.PlannedEnd > start &&
            !db.ProductionSchedules.Any(s => !s.IsDeleted && !s.IsStoppage && s.BatchId == x.Id));
    }

    private async Task<int?> MatchingShift(int lineId, DateTime start, DateTime end)
    {
        var factory = await db.ProductionLines.Where(x => x.Id == lineId)
            .Select(x => new { x.FactoryId, TimeZone = x.Factory.TimeZone })
            .SingleAsync();
        var candidates = await db.Shifts.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).ToListAsync();
        var links = await db.ShiftFactories.AsNoTracking().Where(x => x.FactoryId == factory.FactoryId).ToListAsync();
        var shifts = PlanningRules.SelectFactoryShifts(candidates, links, factory.FactoryId);
        var timeZone = ResolveTimeZone(factory.TimeZone);
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(start, DateTimeKind.Utc), timeZone);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(end, DateTimeKind.Utc), timeZone);
        foreach (var shift in shifts)
            for (var day = localStart.Date.AddDays(-1); day <= localEnd.Date; day = day.AddDays(1))
            {
                var localFrom = day + shift.StartTime;
                var localTo = day + shift.EndTime + (shift.EndTime <= shift.StartTime ? TimeSpan.FromDays(1) : TimeSpan.Zero);
                var from = ToUtc(localFrom, timeZone);
                var to = ToUtc(localTo, timeZone);
                if (start >= from && end <= to) return shift.Id;
            }
        return null;
    }

    private async Task<object[]> ShiftRows(PlanningScope scope)
    {
        var factories = scope.Periods.Keys.ToArray();
        var links = await db.ShiftFactories.AsNoTracking().Where(x => factories.Contains(x.FactoryId)).ToListAsync();
        var linked = links.Select(x => x.ShiftId).ToArray();
        var shifts = await db.Shifts.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive &&
            (factories.Contains(x.FactoryId) || linked.Contains(x.Id))).ToListAsync();
        var timeZones = await db.Factories.AsNoTracking()
            .Where(x => factories.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => ResolveTimeZone(x.TimeZone));
        var result = new List<object>();
        foreach (var (factoryId, period) in scope.Periods)
        {
            var timeZone = timeZones.TryGetValue(factoryId, out var configuredTimeZone)
                ? configuredTimeZone
                : TimeZoneInfo.Utc;
            var localFrom = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(period.From, DateTimeKind.Utc), timeZone);
            var localTo = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(period.To, DateTimeKind.Utc), timeZone);
            foreach (var shift in PlanningRules.SelectFactoryShifts(shifts, links, factoryId))
                for (var day = localFrom.Date.AddDays(-1); day <= localTo.Date; day = day.AddDays(1))
                {
                    var localShiftFrom = day + shift.StartTime;
                    var localShiftTo = day + shift.EndTime + (shift.EndTime <= shift.StartTime ? TimeSpan.FromDays(1) : TimeSpan.Zero);
                    var from = ToUtc(localShiftFrom, timeZone);
                    var to = ToUtc(localShiftTo, timeZone);
                    if (PlanningRules.Overlaps(from, to, period.From, period.To))
                        result.Add(new { shiftId = shift.Id, shift.Name, factoryId, from = Utc(from), to = Utc(to) });
                }
        }
        return result.ToArray();
    }
    private async Task<object> RouteRows(PlanningScope scope)
    {
        var rows = await db.Routes.AsNoTracking().Include(x => x.Sku).Include(x => x.Steps)
            .Where(x => x.CustomerId == scope.CustomerId && !x.IsDeleted).OrderBy(x => x.Code).ToListAsync();
        return new { scope = "customer", count = rows.Count, items = rows.Select(x => new {
            x.Id, x.Code, x.Name, x.SkuId, productName = x.Sku.Name, x.Status, x.Version, cycleHours = x.CycleTimeHours,
            steps = x.Steps.Where(s => !s.IsDeleted).OrderBy(s => s.StepNo).Select(s => new { s.Id, s.StepNo, s.StepName, s.MachineType, s.EstDurationMin }) }) };
    }
    private async Task<List<MrpRequirement>> MrpData(PlanningScope scope)
    {
        if (scope.Periods.Count == 0) return [];
        var from = scope.Periods.Values.Min(x => x.From).Date;
        var to = scope.Periods.Values.Max(x => x.To);
        var endDate = to.TimeOfDay == TimeSpan.Zero ? to.Date : to.Date.AddDays(1);
        var rows = await db.MrpRequirements.AsNoTracking().Include(x => x.Material)
            .Where(x => x.CustomerId == scope.CustomerId && x.PlanDate >= from && x.PlanDate < endDate).ToListAsync();
        return rows.Where(x => scope.Periods.Values.Any(p => x.PlanDate.Date < p.To && x.PlanDate.Date.AddDays(1) > p.From)).ToList();
    }
    private async Task<object> MrpRows(PlanningScope scope)
    {
        var rows = await MrpData(scope);
        return new { scope = "customer", factoryAndLineFilterApplied = false, dateGranularity = "day",
            periods = scope.Periods.Values, summary = MrpSummary(rows), items = rows.OrderBy(x => x.PlanDate).ThenBy(x => x.MaterialId)
                .Select(x => new { x.Id, x.MaterialId, materialName = x.Material.Name, unit = x.Material.Uom,
                    x.PlanDate, x.RequiredQty, x.AvailableQty, x.GapQty, status = x.MrpStatus, x.PoReference, x.PoExpectedDate }) };
    }
    private static object MrpSummary(IEnumerable<MrpRequirement> rows)
    {
        var items = rows.ToArray();
        // Preserve unknown stored statuses; do not invent stock thresholds or reinterpret the gap.
        var sufficient = items.Count(x => x.MrpStatus.ToLowerInvariant() is "ok" or "sufficient");
        var monitor = items.Count(x => x.MrpStatus.ToLowerInvariant() == "monitor");
        var critical = items.Count(x => x.MrpStatus.ToLowerInvariant() == "critical");
        return new { scope = "customer", total = items.Length, sufficient, monitor, critical,
            other = items.Length - sufficient - monitor - critical, exceptions = monitor + critical };
    }
    private static bool InWindow(PlanningScope scope, int lineId, DateTime? start, DateTime? end)
    {
        if (!start.HasValue) return false;
        var period = scope.Periods[scope.LineFactories[lineId]];
        return end.HasValue ? PlanningRules.Overlaps(start.Value, end.Value, period.From, period.To) : start.Value >= period.From && start.Value < period.To;
    }
    private static object BatchRow(Batch x) => new { batchId = x.Id, x.Uid, x.SkuId, skuCode = x.Sku.Code,
        productName = x.Sku.Name, x.LineId, lineName = x.Line.Name, x.Status, x.Stage,
        displayStatus = PlanningRules.DisplayStatus(x.Status, x.Stage), x.PlannedQty, x.ProducedQty, x.RejectedQty,
        attainmentPct = PlanningRules.Attainment(x.ProducedQty, x.PlannedQty.GetValueOrDefault()),
        plannedStart = Utc(x.PlannedStart), plannedEnd = Utc(x.PlannedEnd), actualStart = Utc(x.ActualStart), actualEnd = Utc(x.ActualEnd) };
    private static object ScheduleRow(ProductionSchedule x) => new { x.Id, x.BatchId, x.LineId, x.Title,
        start = Utc(x.ScheduledStart), end = Utc(x.ScheduledEnd), durationHours = (x.ScheduledEnd - x.ScheduledStart).TotalHours,
        x.IsStoppage, type = x.StoppageType, x.Notes };
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;
    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone);
    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (TimeZoneNotFoundException) when (timeZoneId.Equals("Africa/Cairo", StringComparison.OrdinalIgnoreCase))
        { return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
        catch (TimeZoneNotFoundException) when (timeZoneId.Equals("Egypt Standard Time", StringComparison.OrdinalIgnoreCase))
        { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }
    private static bool ValidRange(DateTimeOffset start, DateTimeOffset end) => start != default && end > start && end - start <= TimeSpan.FromDays(366);
    private static WorkflowResult<object> Fail(string code, string message) => WorkflowResult<object>.Fail(code, message);
}
