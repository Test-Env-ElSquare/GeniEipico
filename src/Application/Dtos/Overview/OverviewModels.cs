using Application.Common.Time;
using Domain.Entities.Alarms;
using Domain.Entities.Analytics;

namespace Application.Dtos.Overview;

public enum OverviewSection { Page, Kpis, OeeTrend, ProductionOutput, EnergyByMachine, ActiveAlarms, FactoriesProduction, EnergyEfficiency, EnergyBySku }
public sealed record OverviewQuery(int Duration = 0, int? FactoryId = null, int? LineId = null, DateTime? From = null, DateTime? To = null);
public sealed record OverviewAccess(int CustomerId, IReadOnlySet<int> FactoryIds, IReadOnlyDictionary<int, int> LineFactories);
public sealed record OverviewPartition(int CustomerId, int FactoryId, int[] LineIds, ResolvedTimeFilter Period,
    bool IncludeFactoryWide = false, int SnapshotUtcOffsetHours = 0);
public sealed record OverviewMachine(int Id, string Name, int LineId, string LineName, int FactoryId, string FactoryName);
public sealed record OverviewOee(int MachineId, string MachineName, int LineId, string LineName, int FactoryId, string FactoryName,
    int ShiftId, DateTime ShiftStartLocal, DateTime? ShiftEndLocal, decimal? OeePct, decimal? AvailabilityPct,
    decimal? PerformancePct, decimal? QualityPct, decimal? Output, decimal? EnergyKwh,
    DateTime? HourStartLocal = null, string? ShiftName = null, decimal? Rejected = null, int? State = null);
public sealed record OverviewLiveResult(bool Available, string Source, IReadOnlyList<OverviewOee> Rows);
public sealed record FactorySnapshotDto(int RunningLines, decimal TotalProduction, decimal RejectRate, int ActiveBatches);
public sealed record FactorySnapshotReadResult(bool Available, string? Source, FactorySnapshotDto Data);
public sealed record OverviewResult(object? Data, object? Meta = null, int StatusCode = 200, string? ErrorCode = null, string? ErrorMessage = null)
{
    public static OverviewResult Fail(int status, string code, string message) => new(null, null, status, code, message);
}

public sealed class EipicoOverviewOptions
{
    public int UtcOffsetHours { get; set; } = 3;
    public int LiveWindowHours { get; set; } = 8;
}
