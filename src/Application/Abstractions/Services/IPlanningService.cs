using Application.Common.Time;
using Application.Common.Workflow;
using Domain.Entities.Planning;
using System.ComponentModel.DataAnnotations;

namespace Application.Abstractions.Services;

public sealed record PlanningScope(int CustomerId, IReadOnlyDictionary<int, int> LineFactories,
    IReadOnlyDictionary<int, ResolvedTimeFilter> Periods);

public sealed class ScheduleBatchCommand
{
    [Range(1, int.MaxValue)] public int BatchId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
}

public sealed class PlanningStoppageCommand
{
    [Range(1, int.MaxValue)] public int LineId { get; set; }
    [Required, StringLength(200, MinimumLength = 1)] public string Title { get; set; } = "";
    [Required] public string Type { get; set; } = "maintenance";
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
}

public sealed class PlanningRouteCommand
{
    [Range(1, int.MaxValue)] public int SkuId { get; set; }
    [Range(typeof(decimal), "0.01", "9999.99")] public decimal CycleHours { get; set; }
    [Required] public string Status { get; set; } = "draft";
    [Required, MinLength(1), MaxLength(100)] public string[] Steps { get; set; } = [];
}

public interface IPlanningService
{
    Task<object> ReadAsync(string section, PlanningScope scope, int page = 1, int pageSize = 10);
    Task<object> FormOptionsAsync(PlanningScope scope);
    Task<WorkflowResult<object>> ScheduleAsync(PlanningScope scope, ScheduleBatchCommand command);
    Task<WorkflowResult<object>> StoppageAsync(PlanningScope scope, PlanningStoppageCommand command);
    Task<WorkflowResult<object>> RouteAsync(PlanningScope scope, PlanningRouteCommand command);
}

public static class PlanningRules
{
    public static IReadOnlyList<Shift> SelectFactoryShifts(
        IEnumerable<Shift> shifts,
        IEnumerable<ShiftFactory> links,
        int factoryId)
    {
        var candidates = shifts.ToArray();
        var direct = candidates.Where(x => x.FactoryId == factoryId).ToArray();
        if (direct.Length > 0)
            return direct;

        var linkedIds = links.Where(x => x.FactoryId == factoryId).Select(x => x.ShiftId).ToHashSet();
        return candidates.Where(x => linkedIds.Contains(x.Id)).ToArray();
    }
    public static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd)
        => start < otherEnd && end > otherStart;
    public static bool IncludedInTotals(string status) => status is not ("cancelled" or "split");
    public static decimal? Attainment(long produced, long planned)
        => planned <= 0 ? null : Math.Round(100m * produced / planned, 2);
    public static string DisplayStatus(string status, string stage)
        => status == "in_progress" && stage == "dispense" ? "dispensing" : status;
}
