using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Overview;
using Domain.Entities.Planning;
using Microsoft.Extensions.Options;

namespace Application.Services;

// Only Overview uses this adapter; other pages retain their existing time semantics.
public sealed class OverviewPeriodResolver(IUnitOfWork uow, ITimeUtilities time,
    IOptions<EipicoOverviewOptions> settings, TimeProvider clock) : IOverviewPeriodResolver
{
    public int SnapshotUtcOffsetHours(int customerId, int factoryId)
        => settings.Value.UtcOffsetHours;

    public async Task<ResolvedTimeFilter> ResolveAsync(int customerId, TimeFilterInput input)
    {
        var offset = SnapshotUtcOffsetHours(customerId, input.FactoryId!.Value);
        // Unzoned custom dates supplied by Overview are factory-local, like its response.
        // Keep internal periods UTC for querying existing event timestamps.
        if (input.From.HasValue && input.To.HasValue)
            input = input with { From = ToUtc(input.From.Value, offset), To = ToUtc(input.To.Value, offset) };
        if (input.Duration is not (0 or 1) || input.From.HasValue || input.To.HasValue || offset == 0)
            return await time.ResolveAsync(input);

        var links = await uow.ShiftFactories.FindAsync(x => x.FactoryId == input.FactoryId);
        var candidates = await uow.Shifts.FindAsync(x => x.IsActive && !x.IsDeleted);
        var shifts = PlanningRules.SelectFactoryShifts(candidates, links, input.FactoryId.Value);
        return input.Duration == 0
            ? CurrentShift(shifts, clock.GetUtcNow().UtcDateTime, offset, input.FactoryId.Value, input.LineId)
            : LastShift(shifts, clock.GetUtcNow().UtcDateTime, offset, input.FactoryId.Value, input.LineId);
    }

    public static DateTime ToUtc(DateTime value, int offset)
        => value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.AddHours(-offset), DateTimeKind.Utc)
            : value.ToUniversalTime();

    public static ResolvedTimeFilter CurrentShift(
        IEnumerable<Shift> shifts,
        DateTime nowUtc,
        int offset,
        int factoryId,
        int? lineId)
    {
        var localNow = nowUtc.AddHours(offset);
        var current = shifts.FirstOrDefault(shift =>
            shift.StartTime == shift.EndTime ||
            (shift.StartTime < shift.EndTime
                ? localNow.TimeOfDay >= shift.StartTime && localNow.TimeOfDay < shift.EndTime
                : localNow.TimeOfDay >= shift.StartTime || localNow.TimeOfDay < shift.EndTime));

        if (current == null)
            return new(0, factoryId, lineId, nowUtc.AddHours(-8), nowUtc, null, null,
                "current_shift_fallback", "No active configured shift; using the last eight hours.");

        var localFrom = localNow.Date + current.StartTime;
        var localTo = localNow.Date + current.EndTime;
        if (current.StartTime >= current.EndTime)
        {
            localTo = localTo.AddDays(1);
            if (localNow.TimeOfDay < current.EndTime)
            {
                localFrom = localFrom.AddDays(-1);
                localTo = localTo.AddDays(-1);
            }
        }

        return new(0, factoryId, lineId,
            DateTime.SpecifyKind(localFrom.AddHours(-offset), DateTimeKind.Utc),
            DateTime.SpecifyKind(localTo.AddHours(-offset), DateTimeKind.Utc),
            current.Id, current.Name, "current_shift",
            "Current local factory shift expressed in UTC.");
    }

    public static ResolvedTimeFilter LastShift(IEnumerable<Shift> shifts, DateTime nowUtc, int offset, int factoryId, int? lineId)
    {
        var localNow = nowUtc.AddHours(offset);
        var windows = shifts.SelectMany(s => Enumerable.Range(-2, 3).Select(day => {
            var from = localNow.Date.AddDays(day) + s.StartTime;
            var to = localNow.Date.AddDays(day) + s.EndTime;
            if (s.EndTime <= s.StartTime) to = to.AddDays(1);
            return new { Shift = s, From = from, To = to };
        }));
        var last = windows.Where(x => x.To <= localNow).OrderByDescending(x => x.To).FirstOrDefault();
        if (last == null) return new(1, factoryId, lineId, nowUtc.AddHours(-16), nowUtc.AddHours(-8), null, null,
            "last_shift_fallback", "No completed configured shift; using previous eight-hour window.");
        return new(1, factoryId, lineId, DateTime.SpecifyKind(last.From.AddHours(-offset), DateTimeKind.Utc),
            DateTime.SpecifyKind(last.To.AddHours(-offset), DateTimeKind.Utc), last.Shift.Id, last.Shift.Name,
            "last_shift", "Previous local factory shift expressed in UTC.");
    }
}
