using Application.Abstractions.Services;
using Application.Dtos.Overview;

namespace Application.Services;

public sealed class FactorySnapshotService(IEnumerable<IOverviewLiveProvider> liveProviders) : IFactorySnapshotService
{
    private static readonly FactorySnapshotDto Empty = new(0, 0m, 0m, 0);

    public async Task<FactorySnapshotReadResult> ReadCurrentAsync(
        ScadaLiveViewScope scope,
        CancellationToken cancellationToken = default)
    {
        var provider = liveProviders.SingleOrDefault(x => x.Supports(scope.CustomerId, scope.FactoryId));
        if (provider == null)
            return new(false, null, Empty);

        var result = await provider.ReadOeeAsync(
            scope.CustomerId,
            scope.FactoryId,
            scope.AllowedLineIds.OrderBy(x => x).ToArray(),
            cancellationToken);

        return result.Available
            ? new(true, result.Source, Build(result.Rows))
            : new(false, result.Source, Empty);
    }

    public static FactorySnapshotDto Build(IEnumerable<OverviewOee> rows)
    {
        var values = rows.ToArray();
        var runningLines = values
            .Where(x => x.State == 1)
            .Select(x => x.LineId)
            .Distinct()
            .Count();
        var totalProduction = values.Sum(x => x.Output ?? 0m);
        var totalRejected = values.Sum(x => x.Rejected ?? 0m);
        var rejectRate = totalProduction > 0m
            ? Math.Round(totalRejected / totalProduction, 6)
            : 0m;

        return new(runningLines, totalProduction, rejectRate, 0);
    }
}
