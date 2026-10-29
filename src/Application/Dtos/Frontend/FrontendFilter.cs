using Application.Common.Time;
using System.Text.Json.Serialization;

namespace Application.Dtos.Frontend;

public sealed record FrontendFilter(
    int Duration,
    int? FactoryId,
    int? LineId,
    DateTime From,
    DateTime To,
    int? ShiftId,
    string? ShiftName,
    string Mode,
    string Note,
    string? Search = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 10,
    bool ApplyTimeFilter = true,
    [property: JsonIgnore] int? CustomerId = null,
    [property: JsonIgnore] IReadOnlyCollection<int>? AllowedLineIds = null)
{
    public static FrontendFilter FromResolved(ResolvedTimeFilter resolved)
    {
        return new FrontendFilter(
            resolved.Duration,
            resolved.FactoryId,
            resolved.LineId,
            resolved.From,
            resolved.To,
            resolved.ShiftId,
            resolved.ShiftName,
            resolved.Mode,
            resolved.Note);
    }
}
