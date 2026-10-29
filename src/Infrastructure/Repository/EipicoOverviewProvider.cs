using Application.Abstractions.Services;
using Application.Dtos.Overview;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Repository;

/// <summary>
/// Reads the live Overview read model from the Eipico-compatible SQL views.
/// The views keep the original GeniePark OEE calculation and shift/hour semantics,
/// while their source rows come from Eipico RealTime.Production.
/// </summary>
public sealed class EipicoOverviewProvider(MesDbContext db) : IOverviewLiveProvider
{
    // Kept for callers compiled against the previous provider constructor. The
    // live window/settings are now owned by the SQL views.
    public EipicoOverviewProvider(MesDbContext db, IOptions<EipicoOverviewOptions> _, TimeProvider __)
        : this(db)
    {
    }

    public bool Supports(int customerId, int factoryId)
        => customerId > 0 && factoryId > 0;

    public Task<OverviewLiveResult> ReadOeeAsync(
        int customerId,
        int factoryId,
        int[] lineIds,
        CancellationToken cancellationToken)
        => ReadOeeViewAsync(customerId, factoryId, lineIds, cancellationToken);

    public Task<OverviewLiveResult> ReadHourlyOeeAsync(
        int customerId,
        int factoryId,
        int[] lineIds,
        CancellationToken cancellationToken)
        => ReadHourlyViewAsync(customerId, factoryId, lineIds, cancellationToken);

    private async Task<OverviewLiveResult> ReadOeeViewAsync(
        int customerId, int factoryId, int[] lineIds, CancellationToken cancellationToken)
    {
        if (lineIds.Length == 0)
            return new(true, "Analytics.VW_EipicoOee", []);

        var viewRows = await db.EipicoOeeView
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.FactoryId == factoryId && lineIds.Contains(x.LineId))
            .ToArrayAsync(cancellationToken);

        var names = await MachineNamesAsync(viewRows.Select(x => x.MachineId), cancellationToken);
        var rows = viewRows.Select(x =>
        {
            names.TryGetValue(x.MachineId, out var machine);
            return new OverviewOee(
                x.MachineId, machine?.Name ?? x.MachineName, x.LineId, machine?.LineName ?? "",
                x.FactoryId, machine?.FactoryName ?? "", x.ShiftId, x.ShiftStartTime, x.ShiftEndTime,
                x.OEE, x.Availability, x.Performance, x.Quality, x.Count, null, null,
                x.ShiftName, x.Qreject, x.State);
        }).ToArray();

        return new(true, "Analytics.VW_EipicoOee", rows);
    }

    private async Task<OverviewLiveResult> ReadHourlyViewAsync(
        int customerId, int factoryId, int[] lineIds, CancellationToken cancellationToken)
    {
        if (lineIds.Length == 0)
            return new(true, "Analytics.VW_EipicoOee_PerHour", []);

        var viewRows = await db.EipicoOeePerHourView
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.FactoryId == factoryId && lineIds.Contains(x.LineId))
            .ToArrayAsync(cancellationToken);

        var names = await MachineNamesAsync(viewRows.Select(x => x.MachineId), cancellationToken);
        var rows = viewRows.Select(x =>
        {
            names.TryGetValue(x.MachineId, out var machine);
            return new OverviewOee(
                x.MachineId, machine?.Name ?? x.MachineName, x.LineId, machine?.LineName ?? "",
                x.FactoryId, machine?.FactoryName ?? "", x.ShiftId, x.ShiftStartTime, x.ShiftEndTime,
                x.OEE, x.Availability, x.Performance, x.Quality, x.Count, null, x.HourStartTime,
                x.ShiftName, x.Qreject, null);
        }).ToArray();

        return new(true, "Analytics.VW_EipicoOee_PerHour", rows);
    }

    private async Task<Dictionary<int, MachineNames>> MachineNamesAsync(
        IEnumerable<int> machineIds, CancellationToken cancellationToken)
    {
        var ids = machineIds.Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        return await db.Machines
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new MachineNames(x.Id, x.Name, x.Line.Name, x.Line.Factory.Name))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    private sealed record MachineNames(int Id, string Name, string LineName, string FactoryName);
}
