using Application.Abstractions.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repository;

public sealed class FactoryLayoutSignalReader(MesDbContext db) : IFactoryLayoutSignalReader
{
    public async Task<IReadOnlyList<FactoryLayoutSignal>> ReadAsync(int customerId, int factoryId, int[] lineIds)
    {
        if (lineIds.Length == 0) return [];
        return await db.Database.SqlQuery<FactoryLayoutSignal>($"""
            SELECT CustomerId, FactoryId, LineId, MachineName, ShiftStartTime, TotalCountDiff,
                   LatestTimeStamp, SignalId, State, [Count], Qreject, Speed, StateReason,
                   SKU, SignalCountDiff, Fault, QrejectDiff
            FROM Calculated.VW_LatestShiftPerMachineForLayout
            WHERE CustomerId = {customerId} AND FactoryId = {factoryId}
            """)
            .Where(x => lineIds.Contains(x.LineId))
            .ToArrayAsync();
    }
}