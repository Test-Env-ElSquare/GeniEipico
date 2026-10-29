using Application.Common.Time;

namespace API.Contracts;

public sealed class FrontendFilterQuery
{
    public int? Duration { get; set; }
    public int? FactoryId { get; set; }
    public int? LineId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public bool HasExplicitTimeFilter()
    {
        return Duration.HasValue ||
               From.HasValue ||
               To.HasValue;
    }

    public TimeFilterInput ToInput()
    {
        return new TimeFilterInput(Duration ?? 0, FactoryId, LineId, From, To);
    }
}
