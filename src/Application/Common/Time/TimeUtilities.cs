using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Domain.Entities.Planning;

namespace Application.Common.Time;

public enum DurationFilter
{
    CurrentShift = 0,
    LastShift = 1,
    LastDay = 2,
    LastWeek = 3,
    LastMonth = 4,
    Custom = 5
}

public sealed record TimeFilterInput(
    int Duration = 0,
    int? FactoryId = null,
    int? LineId = null,
    DateTime? From = null,
    DateTime? To = null);

public sealed record ResolvedTimeFilter(
    int Duration,
    int? FactoryId,
    int? LineId,
    DateTime From,
    DateTime To,
    int? ShiftId,
    string? ShiftName,
    string Mode,
    string Note);

public interface ITimeUtilities
{
    Task<ResolvedTimeFilter> ResolveAsync(TimeFilterInput input);
}

public sealed class TimeUtilities : ITimeUtilities
{
    private readonly IUnitOfWork _uow;

    public TimeUtilities(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ResolvedTimeFilter> ResolveAsync(TimeFilterInput input)
    {
        var now = DateTime.UtcNow;
        var duration = input.Duration < 0 || input.Duration > 5 ? 0 : input.Duration;

        if (input.From.HasValue &&
            input.To.HasValue &&
            input.From.Value < input.To.Value)
        {
            return new ResolvedTimeFilter(duration, input.FactoryId, input.LineId, input.From.Value, input.To.Value, null, null, "custom", "Custom from/to supplied by request.");
        }

        return duration switch
        {
            0 => await ResolveCurrentShiftAsync(input, now),
            1 => await ResolveLastShiftAsync(input, now),
            2 => new ResolvedTimeFilter(duration, input.FactoryId, input.LineId, now.AddDays(-1), now, null, null, "last_day", "Last 24 hours."),
            3 => new ResolvedTimeFilter(duration, input.FactoryId, input.LineId, now.AddDays(-7), now, null, null, "last_week", "Last 7 days."),
            4 => new ResolvedTimeFilter(duration, input.FactoryId, input.LineId, now.AddMonths(-1), now, null, null, "last_month", "Last calendar month window from now."),
            5 => new ResolvedTimeFilter(duration, input.FactoryId, input.LineId, now, now, null, null, "invalid_custom", "Custom duration requires a valid from/to range."),
            _ => new ResolvedTimeFilter(0, input.FactoryId, input.LineId, now.Date, now, null, null, "fallback", "Fallback current UTC day.")
        };
    }

    private async Task<ResolvedTimeFilter> ResolveCurrentShiftAsync(TimeFilterInput input, DateTime now)
    {
        var shifts = await GetActiveShiftsAsync(input.FactoryId);

        var timeZone = await GetFactoryTimeZoneAsync(input.FactoryId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), timeZone);

        if (shifts.Count == 0)
        {
            return new ResolvedTimeFilter(0, input.FactoryId, input.LineId, now.AddHours(-8), now, null, null, "current_shift_fallback", "No configured factory shifts; using last 8 hours.");
        }

        var current = FindShiftForTime(shifts, localNow.TimeOfDay) ?? shifts[0];
        var (localFrom, localTo) = BuildWindowForShift(current, localNow);
        var from = ToUtc(localFrom, timeZone);
        var to = ToUtc(localTo, timeZone);

        return new ResolvedTimeFilter(0, input.FactoryId, input.LineId, from, to, current.Id, current.Name, "current_shift", "Current factory shift resolved from Planning.Shifts.");
    }

    private async Task<ResolvedTimeFilter> ResolveLastShiftAsync(TimeFilterInput input, DateTime now)
    {
        var shifts = await GetActiveShiftsAsync(input.FactoryId);

        var timeZone = await GetFactoryTimeZoneAsync(input.FactoryId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), timeZone);

        if (shifts.Count == 0)
        {
            return new ResolvedTimeFilter(1, input.FactoryId, input.LineId, now.AddHours(-16), now.AddHours(-8), null, null, "last_shift_fallback", "No configured factory shifts; using previous 8-hour window.");
        }

        var current = FindShiftForTime(shifts, localNow.TimeOfDay) ?? shifts[0];
        var currentIndex = shifts.FindIndex(x => x.Id == current.Id);
        var previousIndex = currentIndex <= 0 ? shifts.Count - 1 : currentIndex - 1;
        var previous = shifts[previousIndex];

        var anchor = previousIndex > currentIndex ? localNow.AddDays(-1) : localNow;
        var (localFrom, localTo) = BuildWindowForShift(previous, anchor);

        if (localTo > localNow)
        {
            localFrom = localFrom.AddDays(-1);
            localTo = localTo.AddDays(-1);
        }

        var from = ToUtc(localFrom, timeZone);
        var to = ToUtc(localTo, timeZone);
        return new ResolvedTimeFilter(1, input.FactoryId, input.LineId, from, to, previous.Id, previous.Name, "last_shift", "Previous factory shift resolved from Planning.Shifts.");
    }

    private async Task<List<Shift>> GetActiveShiftsAsync(int? factoryId)
    {
        var all = await _uow.Shifts.GetAllAsync();
        var active = all.Where(x => x.IsActive && !x.IsDeleted).ToArray();
        if (!factoryId.HasValue)
            return active.OrderBy(x => x.StartTime).ToList();

        var links = await _uow.ShiftFactories.FindAsync(x => x.FactoryId == factoryId.Value);
        return PlanningRules.SelectFactoryShifts(active, links, factoryId.Value)
            .OrderBy(x => x.StartTime)
            .ToList();
    }

    private static Shift? FindShiftForTime(IReadOnlyList<Shift> shifts, TimeSpan timeOfDay)
    {
        return shifts.FirstOrDefault(shift => IsTimeInsideShift(timeOfDay, shift.StartTime, shift.EndTime));
    }

    private static bool IsTimeInsideShift(TimeSpan time, TimeSpan start, TimeSpan end)
    {
        if (start == end) return true;
        return start < end
            ? time >= start && time < end
            : time >= start || time < end;
    }

    private async Task<TimeZoneInfo> GetFactoryTimeZoneAsync(int? factoryId)
    {
        if (!factoryId.HasValue)
            return TimeZoneInfo.Utc;

        var factory = await _uow.Factories.GetByIdAsync(factoryId.Value);
        return ResolveTimeZone(factory?.TimeZone);
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (timeZoneId.Equals("Africa/Cairo", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
        catch (TimeZoneNotFoundException) when (timeZoneId.Equals("Egypt Standard Time", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone);

    private static (DateTime From, DateTime To) BuildWindowForShift(Shift shift, DateTime anchorLocal)
    {
        var day = anchorLocal.Date;
        var from = day.Add(shift.StartTime);
        var to = day.Add(shift.EndTime);

        if (shift.StartTime >= shift.EndTime)
        {
            to = to.AddDays(1);
            if (anchorLocal.TimeOfDay < shift.EndTime)
            {
                from = from.AddDays(-1);
                to = to.AddDays(-1);
            }
        }

        return (from, to);
    }
}
