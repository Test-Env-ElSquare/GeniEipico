using Application.Abstractions.Services;
using Application.Dtos.EnergyDashboard;
using Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public sealed class EnergyDashboardService(
    MesDbContext db,
    TimeProvider clock,
    IMemoryCache cache) : IEnergyDashboardService
{
    private static readonly TimeZoneInfo CairoTimeZone = ResolveCairoTimeZone();

    public async Task<TransformerSummaryDto> GetSummaryAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        var period = ResolvePeriod(filter);
        var transformerRows = period.IsCurrent
            ? await ReadTransformerCurrentAsync(cancellationToken)
            : await ReadTransformerShiftHistoryAsync(period, cancellationToken);
        transformerRows = Scope(transformerRows, filter.Scope).ToList();

        var online = transformerRows.Where(x => IsOnline(x.Status)).ToArray();
        var factoryIds = filter.Scope.FactoryIds.ToArray();

        var totalTransformers = await db.Transformers
            .IgnoreQueryFilters()
            .CountAsync(x => factoryIds.Contains(x.FactoryId) &&
                             x.EmsEnergySourceId.HasValue &&
                             !x.IsDeleted,
                cancellationToken);
        var totalZones = await db.EnergyZones
            .CountAsync(x => factoryIds.Contains(x.FactoryId) && !x.IsDeleted,
                cancellationToken);
        var totalLines = await db.ProductionLines
            .IgnoreQueryFilters()
            .CountAsync(x => factoryIds.Contains(x.FactoryId) &&
                             x.EmsEnergySourceId.HasValue &&
                             !x.IsDeleted,
                cancellationToken);

        return new TransformerSummaryDto
        {
            TotalEnergy = transformerRows.Sum(x => x.Energy),
            Transformers = new EntityCountDto { TotalCount = totalTransformers },
            Zones = new EntityCountDto { TotalCount = totalZones },
            Lines = new EntityCountDto { TotalCount = totalLines },
            AvgPowerFactor = online.Length == 0 ? 0 : online.Average(x => x.PowerFactor),
            Level = filter.Scope.SelectedFactoryId.HasValue ? "Factory" : "Main"
        };
    }

    public async Task<OnlineStatusDto> GetOnlineStatusAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        var period = ResolvePeriod(filter);
        var transformers = Scope(
            period.IsCurrent
                ? await ReadTransformerCurrentAsync(cancellationToken)
                : await ReadTransformerShiftHistoryAsync(period, cancellationToken),
            filter.Scope).ToArray();
        var zones = Scope(
            period.IsCurrent
                ? await ReadZoneCurrentAsync(cancellationToken)
                : await ReadZoneShiftHistoryAsync(period, cancellationToken),
            filter.Scope).ToArray();
        var lines = Scope(
            period.IsCurrent
                ? await ReadLineCurrentAsync(cancellationToken)
                : await ReadLineShiftHistoryAsync(period, cancellationToken),
            filter.Scope).ToArray();

        return new OnlineStatusDto
        {
            Transformers = StatusCounts(transformers.Select(x => x.Status)),
            Zones = StatusCounts(zones.Select(x => x.Status)),
            Lines = StatusCounts(lines.Select(x => x.Status)),
            Level = filter.Scope.SelectedFactoryId.HasValue ? "Factory" : "Main"
        };
    }

    public async Task<IReadOnlyList<VoltageGraphDto>> GetVoltageGraphAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        var rows = await ScopedTransformerHourlyAsync(filter, cancellationToken);
        return rows
            .Where(x => IsOnline(x.Status))
            .GroupBy(x => x.HourStartTime!.Value)
            .Select(x => new VoltageGraphDto
            {
                FactoryId = filter.Scope.ResponseFactoryId,
                HourStartTime = ToCairo(x.Key),
                AvgVoltage = x.Average(y => y.Voltage)
            })
            .OrderBy(x => x.HourStartTime)
            .ToArray();
    }

    public async Task<IReadOnlyList<CurrentGraphDto>> GetCurrentGraphAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        var rows = await ScopedTransformerHourlyAsync(filter, cancellationToken);
        return rows
            .Where(x => IsOnline(x.Status))
            .GroupBy(x => x.HourStartTime!.Value)
            .Select(x => new CurrentGraphDto
            {
                FactoryId = filter.Scope.ResponseFactoryId,
                HourStartTime = ToCairo(x.Key),
                AvgCurrent = x.Average(y => y.Current)
            })
            .OrderBy(x => x.HourStartTime)
            .ToArray();
    }

    public async Task<IReadOnlyList<HarmonicsGraphDto>> GetHarmonicsGraphAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        var rows = await ScopedTransformerHourlyAsync(filter, cancellationToken);
        return rows
            .GroupBy(x => x.HourStartTime!.Value)
            .Select(x => new HarmonicsGraphDto
            {
                FactoryId = filter.Scope.ResponseFactoryId,
                HourStartTime = ToCairo(x.Key),
                AvgHarmonics = x.Average(y => y.Harmonics)
            })
            .OrderBy(x => x.HourStartTime)
            .ToArray();
    }

    public async Task<IReadOnlyList<EnergyConsumptionDto>> GetTotalEnergyAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        ValidateDrilldown(filter);
        var period = ResolvePeriod(filter);

        if (!filter.TransformerId.HasValue && !filter.ZoneId.HasValue)
        {
            var rows = Scope(
                period.IsCurrent
                    ? await ReadTransformerCurrentAsync(cancellationToken)
                    : await ReadTransformerShiftHistoryAsync(period, cancellationToken),
                filter.Scope);
            return rows
                .GroupBy(x => new { x.TransformerId, x.TransformerName })
                .Select(x => new EnergyConsumptionDto
                {
                    Level = "Transformer",
                    TransformerId = x.Key.TransformerId,
                    TransformerName = x.Key.TransformerName,
                    Energy = x.Sum(y => y.Energy)
                })
                .OrderBy(x => x.TransformerName)
                .ToArray();
        }

        if (filter.TransformerId.HasValue && !filter.ZoneId.HasValue)
        {
            var rows = Scope(
                    period.IsCurrent
                        ? await ReadZoneCurrentAsync(cancellationToken)
                        : await ReadZoneShiftHistoryAsync(period, cancellationToken),
                    filter.Scope)
                .Where(x => x.TargetTransformerId == filter.TransformerId.Value);
            return rows
                .GroupBy(x => new { x.ZoneId, x.ZoneName })
                .Select(x => new EnergyConsumptionDto
                {
                    Level = "Zone",
                    TransformerId = x.First().TransformerId,
                    ZoneId = x.Key.ZoneId,
                    ZoneName = x.Key.ZoneName,
                    Energy = x.Sum(y => y.Energy)
                })
                .OrderBy(x => x.ZoneName)
                .ToArray();
        }

        var lineRows = Scope(
                period.IsCurrent
                    ? await ReadLineCurrentAsync(cancellationToken)
                    : await ReadLineShiftHistoryAsync(period, cancellationToken),
                filter.Scope)
            .Where(x => x.TargetZoneId == filter.ZoneId!.Value);
        return lineRows
            .GroupBy(x => new { x.LineId, x.LineName })
            .Select(x => new EnergyConsumptionDto
            {
                Level = "Line",
                TransformerId = x.First().TransformerId,
                ZoneId = x.First().ZoneId,
                LineId = x.Key.LineId,
                LineName = x.Key.LineName,
                Energy = x.Sum(y => y.Energy)
            })
            .OrderBy(x => x.LineName)
            .ToArray();
    }

    public async Task<IReadOnlyList<HourlyEnergyConsumptionDto>> GetHourlyEnergyAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        ValidateDrilldown(filter);
        var period = ResolvePeriod(filter);
        var groupByHour = period.IsCurrent || (period.ToUtc - period.FromUtc).TotalHours <= 48;

        if (!filter.TransformerId.HasValue && !filter.ZoneId.HasValue)
        {
            var rows = Scope(
                groupByHour
                    ? period.IsCurrent
                        ? await ReadTransformerHourlyCurrentAsync(cancellationToken)
                        : await ReadTransformerHourlyHistoryAsync(period, cancellationToken)
                    : await ReadTransformerShiftHistoryAsync(period, cancellationToken),
                filter.Scope);
            return rows
                .Select(x => new HourlyEnergyConsumptionDto
                {
                    FactoryId = x.FactoryId,
                    HourStartTime = ToCairo(groupByHour ? x.HourStartTime!.Value : x.ShiftStartTime),
                    Level = "Transformer",
                    TransformerId = x.TransformerId,
                    TransformerName = x.TransformerName,
                    Energy = x.Energy
                })
                .OrderBy(x => x.HourStartTime)
                .ThenBy(x => x.TransformerName)
                .ToArray();
        }

        if (filter.TransformerId.HasValue && !filter.ZoneId.HasValue)
        {
            var rows = Scope(
                    groupByHour
                        ? period.IsCurrent
                            ? await ReadZoneHourlyCurrentAsync(cancellationToken)
                            : await ReadZoneHourlyHistoryAsync(period, cancellationToken)
                        : await ReadZoneShiftHistoryAsync(period, cancellationToken),
                    filter.Scope)
                .Where(x => x.TargetTransformerId == filter.TransformerId.Value);
            return rows
                .Select(x => new HourlyEnergyConsumptionDto
                {
                    FactoryId = x.FactoryId,
                    HourStartTime = ToCairo(groupByHour ? x.HourStartTime!.Value : x.ShiftStartTime),
                    Level = "Zone",
                    TransformerId = x.TransformerId,
                    ZoneId = x.ZoneId,
                    ZoneName = x.ZoneName,
                    Energy = x.Energy
                })
                .OrderBy(x => x.HourStartTime)
                .ThenBy(x => x.ZoneName)
                .ToArray();
        }

        var lineRows = Scope(
                groupByHour
                    ? period.IsCurrent
                        ? await ReadLineHourlyCurrentAsync(cancellationToken)
                        : await ReadLineHourlyHistoryAsync(period, cancellationToken)
                    : await ReadLineShiftHistoryAsync(period, cancellationToken),
                filter.Scope)
            .Where(x => x.TargetZoneId == filter.ZoneId!.Value);
        return lineRows
            .Select(x => new HourlyEnergyConsumptionDto
            {
                FactoryId = x.FactoryId,
                HourStartTime = ToCairo(groupByHour ? x.HourStartTime!.Value : x.ShiftStartTime),
                Level = "Line",
                TransformerId = x.TransformerId,
                ZoneId = x.ZoneId,
                LineId = x.LineId,
                LineName = x.LineName,
                Energy = x.Energy
            })
            .OrderBy(x => x.HourStartTime)
            .ThenBy(x => x.LineName)
            .ToArray();
    }

    public async Task<IReadOnlyList<TopEnergyConsumerDto>> GetTopEnergyConsumersAsync(
        EnergyDashboardFilter filter,
        int top,
        CancellationToken cancellationToken = default)
    {
        if (top <= 0)
            throw new ArgumentException("Top must be greater than zero.");

        var rows = await GetTotalEnergyAsync(filter, cancellationToken);
        var total = rows.Sum(x => x.Energy);
        return rows
            .OrderByDescending(x => x.Energy)
            .Take(top)
            .Select((x, index) => new TopEnergyConsumerDto
            {
                Rank = index + 1,
                Level = x.Level,
                TransformerId = x.TransformerId,
                TransformerName = x.TransformerName,
                ZoneId = x.ZoneId,
                ZoneName = x.ZoneName,
                LineId = x.LineId,
                LineName = x.LineName,
                Energy = x.Energy,
                PercentageOfTotal = total > 0
                    ? Math.Round(x.Energy / total * 100, 0)
                    : 0
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<EnergyHeatmapDto>> GetEnergyHeatmapAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        var period = ResolvePeriod(filter);
        var groupByHour = period.IsCurrent || (period.ToUtc - period.FromUtc).TotalHours <= 48;

        if (filter.TransformerId == 0 && filter.ZoneId == 0)
            return await LineHeatmapAsync(filter, period, groupByHour, allFactoryLines: true, cancellationToken);

        if (filter.TransformerId == 0 && !filter.ZoneId.HasValue)
            return await ZoneHeatmapAsync(filter, period, groupByHour, allFactoryZones: true, cancellationToken);

        if (!filter.TransformerId.HasValue && !filter.ZoneId.HasValue)
            return await TransformerHeatmapAsync(filter, period, groupByHour, cancellationToken);

        if (filter.TransformerId is > 0 && !filter.ZoneId.HasValue)
            return await ZoneHeatmapAsync(filter, period, groupByHour, allFactoryZones: false, cancellationToken);

        if (filter.TransformerId is > 0 && filter.ZoneId is > 0)
            return await LineHeatmapAsync(filter, period, groupByHour, allFactoryLines: false, cancellationToken);

        throw new ArgumentException("Invalid filters.");
    }

    private async Task<IReadOnlyList<EnergyHeatmapDto>> TransformerHeatmapAsync(
        EnergyDashboardFilter filter,
        EnergyPeriod period,
        bool groupByHour,
        CancellationToken cancellationToken)
    {
        var threshold = await ReadThresholdAsync("Transformer", cancellationToken);
        var rows = Scope(
            groupByHour
                ? period.IsCurrent
                    ? await ReadTransformerHourlyCurrentAsync(cancellationToken)
                    : await ReadTransformerHourlyHistoryAsync(period, cancellationToken)
                : await ReadTransformerShiftHistoryAsync(period, cancellationToken),
            filter.Scope);
        return rows.Select(x => new EnergyHeatmapDto
            {
                Date = ToCairo(groupByHour ? x.HourStartTime!.Value : x.ShiftStartTime),
                PeriodType = groupByHour ? "Hour" : "Shift",
                Level = "Transformer",
                TransformerId = x.TransformerId,
                TransformerName = x.TransformerName,
                Energy = x.Energy,
                Status = EnergyStatus(x.Energy, threshold)
            })
            .OrderBy(x => x.Date)
            .ThenBy(x => x.TransformerName)
            .ToArray();
    }

    private async Task<IReadOnlyList<EnergyHeatmapDto>> ZoneHeatmapAsync(
        EnergyDashboardFilter filter,
        EnergyPeriod period,
        bool groupByHour,
        bool allFactoryZones,
        CancellationToken cancellationToken)
    {
        var threshold = await ReadThresholdAsync("Zone", cancellationToken);
        var rows = Scope(
            groupByHour
                ? period.IsCurrent
                    ? await ReadZoneHourlyCurrentAsync(cancellationToken)
                    : await ReadZoneHourlyHistoryAsync(period, cancellationToken)
                : await ReadZoneShiftHistoryAsync(period, cancellationToken),
            filter.Scope);
        if (!allFactoryZones)
            rows = rows.Where(x => x.TargetTransformerId == filter.TransformerId).ToArray();

        return rows.Select(x => new EnergyHeatmapDto
            {
                Date = ToCairo(groupByHour ? x.HourStartTime!.Value : x.ShiftStartTime),
                PeriodType = groupByHour ? "Hour" : "Shift",
                Level = "Zone",
                TransformerId = x.TransformerId,
                ZoneId = x.ZoneId,
                ZoneName = x.ZoneName,
                Energy = x.Energy,
                Status = EnergyStatus(x.Energy, threshold)
            })
            .OrderBy(x => x.Date)
            .ThenBy(x => x.ZoneName)
            .ToArray();
    }

    private async Task<IReadOnlyList<EnergyHeatmapDto>> LineHeatmapAsync(
        EnergyDashboardFilter filter,
        EnergyPeriod period,
        bool groupByHour,
        bool allFactoryLines,
        CancellationToken cancellationToken)
    {
        var threshold = await ReadThresholdAsync("Line", cancellationToken);
        var rows = Scope(
            groupByHour
                ? period.IsCurrent
                    ? await ReadLineHourlyCurrentAsync(cancellationToken)
                    : await ReadLineHourlyHistoryAsync(period, cancellationToken)
                : await ReadLineShiftHistoryAsync(period, cancellationToken),
            filter.Scope);
        if (!allFactoryLines)
            rows = rows.Where(x => x.TargetZoneId == filter.ZoneId).ToArray();

        return rows.Select(x => new EnergyHeatmapDto
            {
                Date = ToCairo(groupByHour ? x.HourStartTime!.Value : x.ShiftStartTime),
                PeriodType = groupByHour ? "Hour" : "Shift",
                Level = "Line",
                TransformerId = allFactoryLines ? null : x.TransformerId,
                ZoneId = x.ZoneId,
                LineId = x.LineId,
                LineName = x.LineName,
                Energy = x.Energy,
                Status = EnergyStatus(x.Energy, threshold)
            })
            .OrderBy(x => x.Date)
            .ThenBy(x => x.LineName)
            .ToArray();
    }

    private async Task<List<TransformerReadRow>> ScopedTransformerHourlyAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken)
    {
        var period = ResolvePeriod(filter);
        var rows = period.IsCurrent
            ? await ReadTransformerHourlyCurrentAsync(cancellationToken)
            : await ReadTransformerHourlyHistoryAsync(period, cancellationToken);
        return Scope(rows, filter.Scope).ToList();
    }

    private EnergyPeriod ResolvePeriod(EnergyDashboardFilter filter)
    {
        if (filter.Duration is < 0 or > 5)
            throw new ArgumentException("Invalid duration type.");

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, CairoTimeZone);
        var currentShiftStart = localNow.TimeOfDay switch
        {
            var value when value >= TimeSpan.FromHours(8) && value < TimeSpan.FromHours(20)
                => localNow.Date.AddHours(8),
            var value when value >= TimeSpan.FromHours(20)
                => localNow.Date.AddHours(20),
            _ => localNow.Date.AddDays(-1).AddHours(20)
        };

        DateTime fromLocal;
        DateTime toLocal;
        switch (filter.Duration)
        {
            case 0:
                fromLocal = currentShiftStart;
                toLocal = localNow;
                break;
            case 1:
                fromLocal = currentShiftStart.AddHours(-12);
                toLocal = currentShiftStart;
                break;
            case 2:
                fromLocal = localNow.Date.AddDays(-1).AddHours(8);
                toLocal = localNow.Date.AddHours(8);
                break;
            case 3:
                fromLocal = localNow.Date.AddDays(-7).AddHours(8);
                toLocal = localNow.Date.AddHours(8);
                break;
            case 4:
                fromLocal = localNow.Date.AddDays(-30).AddHours(8);
                toLocal = localNow.Date.AddHours(8);
                break;
            case 5:
                if (!filter.From.HasValue || !filter.To.HasValue)
                    throw new ArgumentException("Custom duration requires both from and to datetime.");
                if (filter.To.Value < filter.From.Value)
                    throw new ArgumentException("Duration Start Time must be less than Duration End Time.");
                fromLocal = AsCairoLocal(filter.From.Value);
                toLocal = AsCairoLocal(filter.To.Value);
                break;
            default:
                throw new ArgumentException("Invalid duration type.");
        }

        return new EnergyPeriod(
            filter.Duration == 0,
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fromLocal, DateTimeKind.Unspecified), CairoTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(toLocal, DateTimeKind.Unspecified), CairoTimeZone));
    }

    private static DateTime AsCairoLocal(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return TimeZoneInfo.ConvertTimeFromUtc(value, CairoTimeZone);
        if (value.Kind == DateTimeKind.Local)
            return TimeZoneInfo.ConvertTime(value, CairoTimeZone);
        return value;
    }

    private static DateTime ToCairo(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            CairoTimeZone);

    private static TimeZoneInfo ResolveCairoTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
    }

    private static void ValidateDrilldown(EnergyDashboardFilter filter)
    {
        if (filter.ZoneId.HasValue && !filter.TransformerId.HasValue)
            throw new ArgumentException("TransformerId is required when ZoneId is provided.");
        if (filter.TransformerId is <= 0 || filter.ZoneId is <= 0)
            throw new ArgumentException("TransformerId and ZoneId must be greater than zero when provided.");
    }

    private static EntityStatusDto StatusCounts(IEnumerable<string> statuses)
    {
        var values = statuses.ToArray();
        return new EntityStatusDto
        {
            OnlineCount = values.Count(IsOnline),
            OfflineCount = values.Count(x => string.Equals(x, "Offline", StringComparison.OrdinalIgnoreCase))
        };
    }

    private static bool IsOnline(string status)
        => string.Equals(status, "Online", StringComparison.OrdinalIgnoreCase);

    private static int EnergyStatus(decimal energy, ThresholdReadRow threshold)
    {
        if (energy >= threshold.LowFrom && energy <= threshold.LowTo) return 1;
        if (energy >= threshold.MediumFrom && energy <= threshold.MediumTo) return 2;
        if (energy >= threshold.HighFrom && energy <= threshold.HighTo) return 3;
        return 0;
    }

    private async Task<ThresholdReadRow> ReadThresholdAsync(
        string level,
        CancellationToken cancellationToken)
    {
        var thresholds = await cache.GetOrCreateAsync(
            "energy-dashboard:thresholds",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return await QueryAsync<ThresholdReadRow>(ThresholdSql, cancellationToken);
            }) ?? [];
        return thresholds.FirstOrDefault(x =>
                   string.Equals(x.Level, level, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException(
                   $"Energy heatmap threshold for level '{level}' is not configured.");
    }

    private Task<List<TransformerReadRow>> ReadTransformerCurrentAsync(CancellationToken ct)
        => ReadCurrentCachedAsync(
            "energy-dashboard:transformer-current",
            () => QueryAsync<TransformerReadRow>(TransformerCurrentSql, ct));

    private Task<List<TransformerReadRow>> ReadTransformerHourlyCurrentAsync(CancellationToken ct)
        => ReadCurrentCachedAsync(
            "energy-dashboard:transformer-hourly-current",
            async () =>
            {
                var rows = await QueryAsync<TransformerReadRow>(TransformerHourlyCurrentSql, ct);
                return rows.Count > 0
                    ? rows
                    : await QueryAsync<TransformerReadRow>(TransformerHourlyLiveSql, ct);
            });

    private Task<List<ZoneReadRow>> ReadZoneCurrentAsync(CancellationToken ct)
        => ReadCurrentCachedAsync(
            "energy-dashboard:zone-current",
            () => QueryAsync<ZoneReadRow>(ZoneCurrentSql, ct));

    private Task<List<ZoneReadRow>> ReadZoneHourlyCurrentAsync(CancellationToken ct)
        => ReadCurrentCachedAsync(
            "energy-dashboard:zone-hourly-current",
            async () =>
            {
                var rows = await QueryAsync<ZoneReadRow>(ZoneHourlyCurrentSql, ct);
                return rows.Count > 0
                    ? rows
                    : await QueryAsync<ZoneReadRow>(ZoneHourlyLiveSql, ct);
            });

    private Task<List<LineReadRow>> ReadLineCurrentAsync(CancellationToken ct)
        => ReadCurrentCachedAsync(
            "energy-dashboard:line-current",
            () => QueryAsync<LineReadRow>(LineCurrentSql, ct));

    private Task<List<LineReadRow>> ReadLineHourlyCurrentAsync(CancellationToken ct)
        => ReadCurrentCachedAsync(
            "energy-dashboard:line-hourly-current",
            async () =>
            {
                var rows = await QueryAsync<LineReadRow>(LineHourlyCurrentSql, ct);
                return rows.Count > 0
                    ? rows
                    : await QueryAsync<LineReadRow>(LineHourlyLiveSql, ct);
            });

    private async Task<List<TransformerReadRow>> ReadTransformerShiftHistoryAsync(EnergyPeriod period, CancellationToken ct)
        => await ReadHistoryAsync<TransformerReadRow>(TransformerShiftHistorySql, period, ct);

    private async Task<List<TransformerReadRow>> ReadTransformerHourlyHistoryAsync(EnergyPeriod period, CancellationToken ct)
        => await ReadHistoryAsync<TransformerReadRow>(TransformerHourlyHistorySql, period, ct);

    private async Task<List<ZoneReadRow>> ReadZoneShiftHistoryAsync(EnergyPeriod period, CancellationToken ct)
        => await ReadHistoryAsync<ZoneReadRow>(ZoneShiftHistorySql, period, ct);

    private async Task<List<ZoneReadRow>> ReadZoneHourlyHistoryAsync(EnergyPeriod period, CancellationToken ct)
        => await ReadHistoryAsync<ZoneReadRow>(ZoneHourlyHistorySql, period, ct);

    private async Task<List<LineReadRow>> ReadLineShiftHistoryAsync(EnergyPeriod period, CancellationToken ct)
        => await ReadHistoryAsync<LineReadRow>(LineShiftHistorySql, period, ct);

    private async Task<List<LineReadRow>> ReadLineHourlyHistoryAsync(EnergyPeriod period, CancellationToken ct)
        => await ReadHistoryAsync<LineReadRow>(LineHourlyHistorySql, period, ct);

    private async Task<List<T>> ReadHistoryAsync<T>(
        string sql,
        EnergyPeriod period,
        CancellationToken cancellationToken)
        where T : class
        => await QueryAsync<T>(
            sql,
            cancellationToken,
            new SqlParameter("@FromUtc", period.FromUtc),
            new SqlParameter("@ToUtc", period.ToUtc));

    private async Task<List<T>> ReadCurrentCachedAsync<T>(
        string key,
        Func<Task<List<T>>> factory)
    {
        return await cache.GetOrCreateAsync(
            key,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5);
                return await factory();
            }) ?? [];
    }

    private async Task<List<T>> QueryAsync<T>(
        string sql,
        CancellationToken cancellationToken,
        params object[] parameters)
        where T : class
    {
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));
        return await db.Database.SqlQueryRaw<T>(sql, parameters)
            .ToListAsync(cancellationToken);
    }

    private static IEnumerable<T> Scope<T>(IEnumerable<T> rows, EnergyDashboardScope scope)
        where T : FactoryScopedReadRow
        => rows.Where(x => scope.FactoryIds.Contains(x.TargetFactoryId));

    private sealed record EnergyPeriod(bool IsCurrent, DateTime FromUtc, DateTime ToUtc);

    private const string UtcShiftExpression =
        "CONVERT(datetime2, ([source].[ShiftStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')";
    private const string UtcHourExpression =
        "CONVERT(datetime2, ([source].[HourStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')";

    private const string TransformerCurrentSql = """
        SELECT
            [view].[FactoryId] AS [TargetFactoryId],
            [factory].[EmsEnergySourceId] AS [FactoryId],
            [view].[TransformerId] AS [TargetTransformerId],
            [view].[EmsEnergySourceId] AS [TransformerId],
            [view].[TransformerName],
            [view].[ShiftStartTime],
            CAST(NULL AS datetime2) AS [HourStartTime],
            [view].[TotalEnergyConsumption] AS [Energy],
            [view].[Status],
            [view].[PowerFactor],
            [view].[Voltage],
            [view].[Current],
            [view].[AvgTHDv] AS [Harmonics]
        FROM [Calculated].[VW_EnergyTransformerCurrent] AS [view] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[Id] = [view].[FactoryId]
        WHERE [factory].[EmsEnergySourceId] IS NOT NULL
        """;

    private const string TransformerHourlyCurrentSql = """
        SELECT
            [factory].[Id] AS [TargetFactoryId],
            [source].[FactoryId],
            [transformer].[Id] AS [TargetTransformerId],
            [source].[TransformerId],
            [transformer].[TransformerName],
            CONVERT(datetime2,
                ([source].[ShiftStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                AS [ShiftStartTime],
            CONVERT(datetime2,
                ([source].[HourStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                AS [HourStartTime],
            CAST([source].[TotalEnergyConsumption] AS decimal(38, 6)) AS [Energy],
            [source].[Status],
            [source].[PowerFactor],
            [source].[Voltage],
            [source].[Current],
            [source].[Hermonics] AS [Harmonics]
        FROM [EMS].[Calculated].[TransformerHourlyAnalysisCurrent] AS [source] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[EmsEnergySourceId] = [source].[FactoryId]
        INNER JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[EmsEnergySourceId] = [source].[TransformerId]
           AND [transformer].[FactoryId] = [factory].[Id]
        """;

    private const string TransformerHourlyLiveSql = """
        SELECT
            [factory].[Id] AS [TargetFactoryId],
            [source].[FactoryId],
            [transformer].[Id] AS [TargetTransformerId],
            [source].[TransformerId],
            [transformer].[TransformerName],
            CONVERT(datetime2,
                ([source].[ShiftStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                AS [ShiftStartTime],
            CONVERT(datetime2,
                ([source].[HourStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                AS [HourStartTime],
            CAST([source].[TotalEnergyConsumption] AS decimal(38, 6)) AS [Energy],
            [source].[Status],
            [source].[PowerFactor],
            [source].[Voltage],
            [source].[Current],
            [source].[Hermonics] AS [Harmonics]
        FROM [EMS].[Calculated].[VW_TransformerHourlyAnalysis] AS [source] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[EmsEnergySourceId] = [source].[FactoryId]
        INNER JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[EmsEnergySourceId] = [source].[TransformerId]
           AND [transformer].[FactoryId] = [factory].[Id]
        """;

    private const string ZoneCurrentSql = """
        SELECT
            [view].[FactoryId] AS [TargetFactoryId],
            [factory].[EmsEnergySourceId] AS [FactoryId],
            [view].[TransformerId] AS [TargetTransformerId],
            [transformer].[EmsEnergySourceId] AS [TransformerId],
            [view].[ZoneId] AS [TargetZoneId],
            [view].[EmsEnergySourceId] AS [ZoneId],
            [view].[ZoneName],
            [view].[ShiftStartTime],
            CAST(NULL AS datetime2) AS [HourStartTime],
            [view].[EnergyConsumption] AS [Energy],
            [view].[Status]
        FROM [Calculated].[VW_EnergyZoneCurrent] AS [view] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[Id] = [view].[FactoryId]
        LEFT JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[Id] = [view].[TransformerId]
        WHERE [factory].[EmsEnergySourceId] IS NOT NULL
        """;

    private const string ZoneHourlyCurrentSql = """
        SELECT
            [view].[FactoryId] AS [TargetFactoryId],
            [factory].[EmsEnergySourceId] AS [FactoryId],
            [view].[TransformerId] AS [TargetTransformerId],
            [transformer].[EmsEnergySourceId] AS [TransformerId],
            [view].[ZoneId] AS [TargetZoneId],
            [view].[EmsEnergySourceId] AS [ZoneId],
            [view].[ZoneName],
            [view].[ShiftStartTime],
            [view].[HourStartTime],
            [view].[EnergyConsumption] AS [Energy],
            [view].[Status]
        FROM [Calculated].[VW_EnergyZoneHourly] AS [view] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[Id] = [view].[FactoryId]
        LEFT JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[Id] = [view].[TransformerId]
        WHERE [factory].[EmsEnergySourceId] IS NOT NULL
        """;

    private const string ZoneHourlyLiveSql = """
        SELECT
            [factory].[Id] AS [TargetFactoryId],
            [source].[FactoryId],
            [transformer].[Id] AS [TargetTransformerId],
            [source].[TransformerId],
            [zone].[Id] AS [TargetZoneId],
            [source].[ZoneId],
            [zone].[Name] AS [ZoneName],
            CONVERT(datetime2,
                ([source].[ShiftStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                AS [ShiftStartTime],
            CONVERT(datetime2,
                ([source].[HourStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                AS [HourStartTime],
            CAST([source].[EnergyConsumption] AS decimal(38, 6)) AS [Energy],
            ISNULL([source].[Status], 'Offline') AS [Status]
        FROM [EMS].[Calculated].[VW_ZoneHourlyAnalysis] AS [source] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[EmsEnergySourceId] = [source].[FactoryId]
        INNER JOIN [Org].[EnergyZones] AS [zone]
            ON [zone].[EmsSourceId] = [source].[ZoneId]
           AND [zone].[FactoryId] = [factory].[Id]
        LEFT JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[EmsEnergySourceId] = [source].[TransformerId]
           AND [transformer].[FactoryId] = [factory].[Id]
        """;

    private const string LineCurrentSql = """
        SELECT
            [view].[FactoryId] AS [TargetFactoryId],
            [factory].[EmsEnergySourceId] AS [FactoryId],
            [view].[TransformerId] AS [TargetTransformerId],
            [transformer].[EmsEnergySourceId] AS [TransformerId],
            [view].[ZoneId] AS [TargetZoneId],
            [zone].[EmsSourceId] AS [ZoneId],
            [view].[LineId] AS [TargetLineId],
            [view].[EmsEnergySourceId] AS [LineId],
            [view].[LineName],
            [view].[ShiftStartTime],
            CAST(NULL AS datetime2) AS [HourStartTime],
            [view].[EnergyConsumption] AS [Energy],
            [view].[Status]
        FROM [Calculated].[VW_EnergyLineCurrent] AS [view] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[Id] = [view].[FactoryId]
        LEFT JOIN [Org].[EnergyZones] AS [zone]
            ON [zone].[Id] = [view].[ZoneId]
        LEFT JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[Id] = [view].[TransformerId]
        WHERE [factory].[EmsEnergySourceId] IS NOT NULL
        """;

    private const string LineHourlyCurrentSql = """
        SELECT
            [view].[FactoryId] AS [TargetFactoryId],
            [factory].[EmsEnergySourceId] AS [FactoryId],
            [view].[TransformerId] AS [TargetTransformerId],
            [transformer].[EmsEnergySourceId] AS [TransformerId],
            [view].[ZoneId] AS [TargetZoneId],
            [zone].[EmsSourceId] AS [ZoneId],
            [view].[LineId] AS [TargetLineId],
            [view].[EmsEnergySourceId] AS [LineId],
            [view].[LineName],
            [view].[ShiftStartTime],
            [view].[HourStartTime],
            [view].[EnergyConsumption] AS [Energy],
            [view].[Status]
        FROM [Calculated].[VW_EnergyLineHourly] AS [view] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[Id] = [view].[FactoryId]
        LEFT JOIN [Org].[EnergyZones] AS [zone]
            ON [zone].[Id] = [view].[ZoneId]
        LEFT JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[Id] = [view].[TransformerId]
        WHERE [factory].[EmsEnergySourceId] IS NOT NULL
        """;

    private const string LineHourlyLiveSql = """
        SELECT
            [factory].[Id] AS [TargetFactoryId],
            [source].[FactoryId],
            [transformer].[Id] AS [TargetTransformerId],
            [transformer].[EmsEnergySourceId] AS [TransformerId],
            [zone].[Id] AS [TargetZoneId],
            [source].[ZoneId],
            [line].[Id] AS [TargetLineId],
            [source].[LineId],
            [line].[Name] AS [LineName],
            CONVERT(datetime2,
                ([source].[ShiftStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                AS [ShiftStartTime],
            CONVERT(datetime2,
                ([source].[HourStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                AS [HourStartTime],
            CAST([source].[EnergyConsumption] AS decimal(38, 6)) AS [Energy],
            ISNULL([source].[Status], 'Offline') AS [Status]
        FROM [EMS].[Calculated].[VW_LineHourlyAnalysis] AS [source] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[EmsEnergySourceId] = [source].[FactoryId]
        INNER JOIN [Org].[ProductionLines] AS [line]
            ON [line].[EmsEnergySourceId] = [source].[LineId]
           AND [line].[FactoryId] = [factory].[Id]
        LEFT JOIN [Org].[EnergyZones] AS [zone]
            ON [zone].[EmsSourceId] = [source].[ZoneId]
           AND [zone].[FactoryId] = [factory].[Id]
        LEFT JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[Id] = [zone].[TransformerId]
        """;

    private static readonly string TransformerShiftHistorySql = $$"""
        SELECT
            [factory].[Id] AS [TargetFactoryId],
            [source].[FactoryId],
            [transformer].[Id] AS [TargetTransformerId],
            [source].[TransformerId],
            [transformer].[TransformerName],
            {{UtcShiftExpression}} AS [ShiftStartTime],
            CAST(NULL AS datetime2) AS [HourStartTime],
            CAST([source].[TotalEnergyConsumption] AS decimal(38, 6)) AS [Energy],
            [source].[Status],
            [source].[PowerFactor],
            [source].[Voltage],
            [source].[Current],
            [source].[Hermonics] AS [Harmonics]
        FROM [EMS].[Calculated].[TransformerAnalysis] AS [source] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[EmsEnergySourceId] = [source].[FactoryId]
        INNER JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[EmsEnergySourceId] = [source].[TransformerId]
           AND [transformer].[FactoryId] = [factory].[Id]
        WHERE {{UtcShiftExpression}} >= @FromUtc
          AND {{UtcShiftExpression}} <= @ToUtc
        """;

    private static readonly string TransformerHourlyHistorySql = $$"""
        SELECT
            [factory].[Id] AS [TargetFactoryId],
            [source].[FactoryId],
            [transformer].[Id] AS [TargetTransformerId],
            [source].[TransformerId],
            [transformer].[TransformerName],
            {{UtcShiftExpression}} AS [ShiftStartTime],
            {{UtcHourExpression}} AS [HourStartTime],
            CAST([source].[TotalEnergyConsumption] AS decimal(38, 6)) AS [Energy],
            [source].[Status],
            [source].[PowerFactor],
            [source].[Voltage],
            [source].[Current],
            [source].[Hermonics] AS [Harmonics]
        FROM [EMS].[Calculated].[TransformerHourlyAnalysis] AS [source] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[EmsEnergySourceId] = [source].[FactoryId]
        INNER JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[EmsEnergySourceId] = [source].[TransformerId]
           AND [transformer].[FactoryId] = [factory].[Id]
        WHERE {{UtcHourExpression}} >= @FromUtc
          AND {{UtcHourExpression}} <= @ToUtc
        """;

    private static readonly string ZoneShiftHistorySql = $$"""
        SELECT
            [factory].[Id] AS [TargetFactoryId],
            [source].[FactoryId],
            [transformer].[Id] AS [TargetTransformerId],
            [source].[TransformerId],
            [zone].[Id] AS [TargetZoneId],
            [source].[ZoneId],
            [zone].[Name] AS [ZoneName],
            {{UtcShiftExpression}} AS [ShiftStartTime],
            CAST(NULL AS datetime2) AS [HourStartTime],
            CAST([source].[EnergyConsumption] AS decimal(38, 6)) AS [Energy],
            [source].[Status]
        FROM [EMS].[Calculated].[ZoneAnalysis] AS [source] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[EmsEnergySourceId] = [source].[FactoryId]
        INNER JOIN [Org].[EnergyZones] AS [zone]
            ON [zone].[EmsSourceId] = [source].[ZoneId]
           AND [zone].[FactoryId] = [factory].[Id]
        LEFT JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[EmsEnergySourceId] = [source].[TransformerId]
           AND [transformer].[FactoryId] = [factory].[Id]
        WHERE {{UtcShiftExpression}} >= @FromUtc
          AND {{UtcShiftExpression}} <= @ToUtc
        """;

    private static readonly string ZoneHourlyHistorySql = $$"""
        SELECT
            [factory].[Id] AS [TargetFactoryId],
            [source].[FactoryId],
            [transformer].[Id] AS [TargetTransformerId],
            [source].[TransformerId],
            [zone].[Id] AS [TargetZoneId],
            [source].[ZoneId],
            [zone].[Name] AS [ZoneName],
            {{UtcShiftExpression}} AS [ShiftStartTime],
            {{UtcHourExpression}} AS [HourStartTime],
            CAST([source].[EnergyConsumption] AS decimal(38, 6)) AS [Energy],
            ISNULL([source].[Status], 'Offline') AS [Status]
        FROM [EMS].[Calculated].[ZoneHourlyAnalysis] AS [source] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[EmsEnergySourceId] = [source].[FactoryId]
        INNER JOIN [Org].[EnergyZones] AS [zone]
            ON [zone].[EmsSourceId] = [source].[ZoneId]
           AND [zone].[FactoryId] = [factory].[Id]
        LEFT JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[EmsEnergySourceId] = [source].[TransformerId]
           AND [transformer].[FactoryId] = [factory].[Id]
        WHERE {{UtcHourExpression}} >= @FromUtc
          AND {{UtcHourExpression}} <= @ToUtc
        """;

    private static readonly string LineShiftHistorySql = $$"""
        SELECT
            [factory].[Id] AS [TargetFactoryId],
            [source].[FactoryId],
            [transformer].[Id] AS [TargetTransformerId],
            [transformer].[EmsEnergySourceId] AS [TransformerId],
            [zone].[Id] AS [TargetZoneId],
            [source].[ZoneId],
            [line].[Id] AS [TargetLineId],
            [source].[LineId],
            [line].[Name] AS [LineName],
            {{UtcShiftExpression}} AS [ShiftStartTime],
            CAST(NULL AS datetime2) AS [HourStartTime],
            CAST([source].[EnergyConsumption] AS decimal(38, 6)) AS [Energy],
            [source].[Status]
        FROM [EMS].[Calculated].[LineAnalysis] AS [source] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[EmsEnergySourceId] = [source].[FactoryId]
        INNER JOIN [Org].[ProductionLines] AS [line]
            ON [line].[EmsEnergySourceId] = [source].[LineId]
           AND [line].[FactoryId] = [factory].[Id]
        LEFT JOIN [Org].[EnergyZones] AS [zone]
            ON [zone].[EmsSourceId] = [source].[ZoneId]
           AND [zone].[FactoryId] = [factory].[Id]
        LEFT JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[Id] = [zone].[TransformerId]
        WHERE {{UtcShiftExpression}} >= @FromUtc
          AND {{UtcShiftExpression}} <= @ToUtc
        """;

    private static readonly string LineHourlyHistorySql = $$"""
        SELECT
            [factory].[Id] AS [TargetFactoryId],
            [source].[FactoryId],
            [transformer].[Id] AS [TargetTransformerId],
            [transformer].[EmsEnergySourceId] AS [TransformerId],
            [zone].[Id] AS [TargetZoneId],
            [source].[ZoneId],
            [line].[Id] AS [TargetLineId],
            [source].[LineId],
            [line].[Name] AS [LineName],
            {{UtcShiftExpression}} AS [ShiftStartTime],
            {{UtcHourExpression}} AS [HourStartTime],
            CAST([source].[EnergyConsumption] AS decimal(38, 6)) AS [Energy],
            ISNULL([source].[Status], 'Offline') AS [Status]
        FROM [EMS].[Calculated].[LineHourlyAnalysis] AS [source] WITH (NOLOCK)
        INNER JOIN [Org].[Factories] AS [factory]
            ON [factory].[EmsEnergySourceId] = [source].[FactoryId]
        INNER JOIN [Org].[ProductionLines] AS [line]
            ON [line].[EmsEnergySourceId] = [source].[LineId]
           AND [line].[FactoryId] = [factory].[Id]
        LEFT JOIN [Org].[EnergyZones] AS [zone]
            ON [zone].[EmsSourceId] = [source].[ZoneId]
           AND [zone].[FactoryId] = [factory].[Id]
        LEFT JOIN [MasterData].[Transformers] AS [transformer]
            ON [transformer].[Id] = [zone].[TransformerId]
        WHERE {{UtcHourExpression}} >= @FromUtc
          AND {{UtcHourExpression}} <= @ToUtc
        """;

    private const string ThresholdSql = """
        SELECT
            [Level], [LowFrom], [LowTo], [MediumFrom], [MediumTo], [HighFrom], [HighTo]
        FROM [EMS].[dbo].[EnergyHeatmapThresholds]
        WHERE [IsActive] = 1
        """;
}

public abstract class FactoryScopedReadRow
{
    public int TargetFactoryId { get; set; }
    public int FactoryId { get; set; }
    public DateTime ShiftStartTime { get; set; }
    public DateTime? HourStartTime { get; set; }
    public decimal Energy { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class TransformerReadRow : FactoryScopedReadRow
{
    public int TargetTransformerId { get; set; }
    public int TransformerId { get; set; }
    public string TransformerName { get; set; } = string.Empty;
    public decimal PowerFactor { get; set; }
    public decimal Voltage { get; set; }
    public decimal Current { get; set; }
    public decimal Harmonics { get; set; }
}

public sealed class ZoneReadRow : FactoryScopedReadRow
{
    public int? TargetTransformerId { get; set; }
    public int? TransformerId { get; set; }
    public int TargetZoneId { get; set; }
    public int ZoneId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
}

public sealed class LineReadRow : FactoryScopedReadRow
{
    public int? TargetTransformerId { get; set; }
    public int? TransformerId { get; set; }
    public int? TargetZoneId { get; set; }
    public int? ZoneId { get; set; }
    public int TargetLineId { get; set; }
    public int LineId { get; set; }
    public string LineName { get; set; } = string.Empty;
}

public sealed class ThresholdReadRow
{
    public string Level { get; set; } = string.Empty;
    public decimal LowFrom { get; set; }
    public decimal LowTo { get; set; }
    public decimal MediumFrom { get; set; }
    public decimal MediumTo { get; set; }
    public decimal HighFrom { get; set; }
    public decimal HighTo { get; set; }
}
