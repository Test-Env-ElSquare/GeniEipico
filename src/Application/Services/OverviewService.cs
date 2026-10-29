using System.Globalization;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Application.Dtos.Overview;
using Domain.Entities.Alarms;

namespace Application.Services;

public sealed class OverviewService(IOverviewRepository repository, IOverviewAccessProvider accessProvider,
    IEnumerable<IOverviewLiveProvider> liveProviders, IOverviewPeriodResolver time) : IOverviewService
{
    public async Task<OverviewResult> ReadAsync(OverviewSection section, OverviewQuery query, CancellationToken cancellationToken = default)
    {
        var access = accessProvider.GetAccess();
        if (access == null) return OverviewResult.Fail(401, "not_authenticated", "A valid user and customer scope is required.");
        if (query.Duration is < 0 or > 5 || query.FactoryId is <= 0 || query.LineId is <= 0)
            return OverviewResult.Fail(400, "invalid_filter", "Duration must be 0-5 and scope IDs must be positive.");
        if ((query.Duration == 5 || query.From.HasValue || query.To.HasValue) &&
            (!query.From.HasValue || !query.To.HasValue || query.From >= query.To))
            return OverviewResult.Fail(400, "invalid_period", "Supply From and To with From < To.");
        if (query.Duration == 0 && (query.From.HasValue || query.To.HasValue))
            return OverviewResult.Fail(400, "invalid_period", "Use Duration=5 for a custom period. Duration=0 always reads current views.");
        if (query.FactoryId.HasValue && !access.FactoryIds.Contains(query.FactoryId.Value))
            return OverviewResult.Fail(403, "factory_forbidden", "Factory is outside your access scope.");
        if (query.LineId.HasValue && (!access.LineFactories.TryGetValue(query.LineId.Value, out var factory) ||
            query.FactoryId.HasValue && factory != query.FactoryId.Value))
            return OverviewResult.Fail(403, "line_forbidden", "Line is outside your access scope or selected factory.");

        var rows = new List<OverviewOee>();
        var hourlyRows = new List<OverviewOee>();
        var unavailable = new List<string>();
        var alarms = new List<AlarmEvent>();
        var periods = new List<ResolvedTimeFilter>();
        var sources = new List<string>();
        var usedSnapshotOee = false;
        var usedSnapshotHourly = false;
        string? reason = null;
        var live = query.Duration == 0;
        var readsOee = section is OverviewSection.Page or OverviewSection.Kpis or
            OverviewSection.FactoriesProduction or OverviewSection.EnergyByMachine;
        var readsHourly = section is OverviewSection.Page or OverviewSection.ProductionOutput or OverviewSection.OeeTrend;
        var readsAlarms = section is OverviewSection.Page or OverviewSection.Kpis or OverviewSection.ActiveAlarms;
        if (live)
        {
            if (!query.FactoryId.HasValue) reason = "select_factory";
            else if ((!readsOee && !readsHourly) || section == OverviewSection.EnergyByMachine) reason = "live_view_not_configured";
            else
            {
                var provider = liveProviders.SingleOrDefault(x => x.Supports(access.CustomerId, query.FactoryId.Value));
                if (provider == null) reason = "live_view_not_configured";
                else
                {
                    var lineIds = Lines(access, query.FactoryId.Value, query.LineId);
                    if (readsOee)
                    {
                        var result = await provider.ReadOeeAsync(access.CustomerId, query.FactoryId.Value, lineIds, cancellationToken);
                        if (!result.Available) reason = "live_view_unavailable";
                        else { sources.Add(result.Source); rows.AddRange(result.Rows); }
                    }
                    if (readsHourly)
                    {
                        var result = await provider.ReadHourlyOeeAsync(access.CustomerId, query.FactoryId.Value, lineIds, cancellationToken);
                        if (!result.Available)
                        {
                            reason = "live_view_unavailable";
                            if (section is OverviewSection.Page or OverviewSection.ProductionOutput) unavailable.Add("productionOutput");
                            if (section is OverviewSection.Page or OverviewSection.OeeTrend) unavailable.Add("oeeTrend");
                        }
                        else { sources.Add(result.Source); hourlyRows.AddRange(result.Rows); }
                    }
                    if (reason == null && rows.Count == 0 && hourlyRows.Count == 0) reason = "no_data";
                    periods.AddRange(rows.Concat(hourlyRows).GroupBy(x => new { x.FactoryId, x.ShiftId, x.ShiftName, x.ShiftStartLocal, x.ShiftEndLocal })
                        .Select(g => new ResolvedTimeFilter(0, g.Key.FactoryId, query.LineId, g.Key.ShiftStartLocal,
                            g.Key.ShiftEndLocal ?? g.Key.ShiftStartLocal, g.Key.ShiftId, g.Key.ShiftName, "current_shift",
                            "Factory-local shift boundaries supplied by the live view.")));
                }
            }
        }
        else if (section is OverviewSection.EnergyEfficiency or OverviewSection.EnergyBySku)
            reason = "historical_calculation_source_not_configured";
        else
        {
            var factoryIds = query.FactoryId.HasValue ? new[] { query.FactoryId.Value } : query.LineId.HasValue
                ? new[] { access.LineFactories[query.LineId.Value] } : access.FactoryIds.Order().ToArray();
            foreach (var factoryId in factoryIds)
            {
                var period = await time.ResolveAsync(access.CustomerId, new(query.Duration, factoryId, query.LineId, query.From, query.To));
                var lineIds = Lines(access, factoryId, query.LineId);
                var offset = time.SnapshotUtcOffsetHours(access.CustomerId, factoryId);
                periods.Add(period with { From = Local(period.From.AddHours(offset)), To = Local(period.To.AddHours(offset)),
                    Note = "Selected period expressed in factory-local time." });
                var partition = new OverviewPartition(access.CustomerId, factoryId, lineIds, period, !query.LineId.HasValue, offset);
                if (readsOee || readsHourly)
                {
                    var machines = (await repository.MachinesAsync(access.CustomerId, factoryId, lineIds, cancellationToken)).ToDictionary(x => x.Id);
                    if (readsHourly)
                    {
                        var hourlySnapshots = await repository.HourlySnapshotsAsync(partition, cancellationToken);
                        if (hourlySnapshots.Count > 0)
                        {
                            usedSnapshotHourly = true;
                            hourlyRows.AddRange(hourlySnapshots.Where(x => machines.ContainsKey(x.MachineId))
                                .GroupBy(x => new { x.MachineId, x.ShiftId, x.ShiftDate })
                                .Select(g => g.OrderByDescending(x => x.ComputedAt).ThenByDescending(x => x.Id).First())
                                .Select(x => {
                                    var m = machines[x.MachineId];
                                    return new OverviewOee(x.MachineId, m.Name, m.LineId, m.LineName, factoryId, m.FactoryName,
                                        x.ShiftId, Local(x.ShiftDate), null, x.OeePct, x.AvailabilityPct, x.PerformancePct,
                                        x.QualityPct, x.TotalOutput, x.EnergyKwh, Local(x.ShiftDate));
                                }));
                        }
                        else if (query.Duration == 1)
                        {
                            var provider = liveProviders.SingleOrDefault(x => x.Supports(access.CustomerId, factoryId));
                            if (provider is not null)
                            {
                                var viewResult = await provider.ReadHourlyOeeAsync(
                                    access.CustomerId, factoryId, lineIds, cancellationToken);
                                if (viewResult.Available)
                                {
                                    var localFrom = Local(period.From.AddHours(offset));
                                    var localTo = Local(period.To.AddHours(offset));
                                    sources.Add(viewResult.Source);
                                    hourlyRows.AddRange(viewResult.Rows.Where(x =>
                                        x.HourStartLocal.HasValue &&
                                        x.HourStartLocal.Value >= localFrom &&
                                        x.HourStartLocal.Value < localTo));
                                }
                                else
                                {
                                    unavailable.Add(section is OverviewSection.OeeTrend ? "oeeTrend" : "productionOutput");
                                }
                            }
                        }
                    }
                    if (readsOee)
                    {
                        var snapshots = await repository.SnapshotsAsync(partition, cancellationToken);
                        if (snapshots.Count > 0)
                        {
                            usedSnapshotOee = true;
                            rows.AddRange(snapshots.Where(x => machines.ContainsKey(x.MachineId))
                                .GroupBy(x => new { x.MachineId, x.ShiftId, x.ShiftDate })
                                .Select(g => g.OrderByDescending(x => x.ComputedAt).ThenByDescending(x => x.Id).First()).Select(x => {
                                    var machine = machines[x.MachineId];
                                    return new OverviewOee(x.MachineId, machine.Name, machine.LineId, machine.LineName, factoryId,
                                        machine.FactoryName, x.ShiftId, Local(x.ShiftDate), null,
                                        x.OeePct, x.AvailabilityPct, x.PerformancePct, x.QualityPct, x.TotalOutput, x.EnergyKwh);
                                }));
                        }
                        else if (query.Duration == 1)
                        {
                            var provider = liveProviders.SingleOrDefault(x => x.Supports(access.CustomerId, factoryId));
                            if (provider is not null)
                            {
                                var viewResult = await provider.ReadOeeAsync(
                                    access.CustomerId, factoryId, lineIds, cancellationToken);
                                if (viewResult.Available)
                                {
                                    var localFrom = Local(period.From.AddHours(offset));
                                    var localTo = Local(period.To.AddHours(offset));
                                    sources.Add(viewResult.Source);
                                    rows.AddRange(viewResult.Rows.Where(x =>
                                        x.ShiftStartLocal < localTo &&
                                        (x.ShiftEndLocal ?? x.ShiftStartLocal) > localFrom));
                                }
                                else
                                {
                                    unavailable.Add("oee");
                                }
                            }
                        }
                    }
                }
                if (readsAlarms) alarms.AddRange(await repository.AlarmsAsync(partition, cancellationToken));
            }
            if (readsOee && usedSnapshotOee) sources.Add("Analytics.OeeSnapshots");
            if (readsHourly && usedSnapshotHourly) sources.Add("Analytics.OeeSnapshotsPerHour");
            if (readsAlarms) sources.Add("Alarm.AlarmEvents");
            if (reason is null && rows.Count == 0 && hourlyRows.Count == 0 && (readsOee || readsHourly))
                reason = "no_data";
        }
        if (section == OverviewSection.Page)
            unavailable.AddRange(live ? ["activeAlarms", "energyByMachine", "energyEfficiency", "energyBySku"]
                : new[] { "energyEfficiency", "energyBySku" });
        else if (section == OverviewSection.Kpis && live) unavailable.Add("activeAlarms");
        var data = Build(section, query, rows, alarms, live, hourlyRows);
        return new(data, new { query.Duration, query.FactoryId, query.LineId, mode = live ? "live_view" : "historical",
            sources = sources.Distinct().ToArray(), periods, reason, granularity = section is OverviewSection.ProductionOutput or OverviewSection.OeeTrend ? "hour" : section == OverviewSection.Page ? "mixed" : "shift",
            ratioScale = "0_to_1", timeBasis = "factory_local", unavailableSections = unavailable.ToArray() });
    }

    private static int[] Lines(OverviewAccess access, int factoryId, int? lineId)
        => access.LineFactories.Where(x => x.Value == factoryId && (!lineId.HasValue || x.Key == lineId)).Select(x => x.Key).ToArray();

    public static object Build(OverviewSection section, OverviewQuery query, IReadOnlyList<OverviewOee> rows,
        IReadOnlyList<AlarmEvent> alarms, bool live, IReadOnlyList<OverviewOee>? hourlyRows = null)
    {
        var title = "Production by hour";
        switch (section)
        {
            case OverviewSection.Kpis:
                return new[] { new { oee = Average(rows.Select(x => x.OeePct)), availability = Average(rows.Select(x => x.AvailabilityPct)),
                    performance = Average(rows.Select(x => x.PerformancePct)), qualityRate = Average(rows.Select(x => x.QualityPct)),
                    activeAlarms = live ? (int?)null : alarms.Count } };
            case OverviewSection.OeeTrend:
                var oee = (hourlyRows ?? []).Where(x => x.OeePct.HasValue && x.HourStartLocal.HasValue)
                    .GroupBy(x => x.HourStartLocal!.Value).OrderBy(x => x.Key).ToArray();
                return new { labels = oee.Select(x => Label(x.Key)).ToArray(), oee = oee.Select(x => Average(x.Select(y => y.OeePct))).ToArray() };
            case OverviewSection.ProductionOutput:
                var output = (hourlyRows ?? []).Where(x => x.Output.HasValue && x.HourStartLocal.HasValue)
                    .GroupBy(x => x.HourStartLocal!.Value).OrderBy(x => x.Key).ToArray();
                return new { title, labels = output.Select(x => Label(x.Key)).ToArray(),
                    series = new[] { new { name = title, values = output.Select(x => x.Sum(y => y.Output!.Value)).ToArray() } } };
            case OverviewSection.FactoriesProduction:
                return rows.GroupBy(x => query.LineId.HasValue ? (x.MachineId, x.MachineName, "machine") :
                        query.FactoryId.HasValue ? (x.LineId, x.LineName, "line") : (x.FactoryId, x.FactoryName, "factory"))
                    .Select(g => new { id = g.Key.Item1, name = g.Key.Item2, level = g.Key.Item3,
                        productionCount = g.Any(x => x.Output.HasValue) ? g.Sum(x => x.Output) : null }).OrderBy(x => x.id).ToArray();
            case OverviewSection.EnergyByMachine:
                return rows.Where(x => x.EnergyKwh.HasValue).GroupBy(x => new { x.MachineId, x.MachineName })
                    .Select(g => new { machineId = g.Key.MachineId, machine = g.Key.MachineName, kwh = g.Sum(x => x.EnergyKwh) }).ToArray();
            case OverviewSection.ActiveAlarms:
                return alarms.Where(x => x.AcknowledgedAt == null).OrderByDescending(x => x.TriggeredAt).Take(4).Select(x => new {
                    id = x.Id, title = x.Title, severity = x.Severity, tagName = x.TagName, triggerValue = x.TriggerValue,
                    triggeredAt = x.TriggeredAt, acknowledged = false, source = x.MachineId.HasValue ? $"Machine {x.MachineId}" :
                        x.SiloId.HasValue ? $"Silo {x.SiloId}" : $"Device {x.DeviceId}" }).ToArray();
            case OverviewSection.Page:
                var sections = new Dictionary<string, object?>();
                foreach (var (key, child) in new[] { ("kpis", OverviewSection.Kpis), ("oeeTrend", OverviewSection.OeeTrend),
                    ("productionOutput", OverviewSection.ProductionOutput), ("factoriesProduction", OverviewSection.FactoriesProduction),
                    ("energyByMachine", OverviewSection.EnergyByMachine), ("activeAlarms", OverviewSection.ActiveAlarms),
                    ("energyEfficiency", OverviewSection.EnergyEfficiency), ("energyBySku", OverviewSection.EnergyBySku) })
                    sections[key] = Build(child, query, rows, alarms, live, hourlyRows);
                return new UiPageDto("Overview Dashboard", "/v1/ui/overview", "Read Model", "database", [], sections);
            default: return Array.Empty<object>();
        }
    }
    private static decimal? Average(IEnumerable<decimal?> source)
    {
        var values = source.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return values.Length == 0 ? null : Math.Round(values.Average(), 6);
    }
    private static string Label(DateTime timestamp) => timestamp.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    private static DateTime Local(DateTime timestamp) => DateTime.SpecifyKind(timestamp, DateTimeKind.Unspecified);
}
