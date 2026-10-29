using System.Globalization;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;

namespace Application.Services;

/// <summary>Retains Eipico's layout response shape. Shift aggregates belong to the SQL view.</summary>
public sealed class SignageLayoutService(IUnitOfWork uow, IFactoryLayoutSignalReader signals) : ISignageLayoutService
{
    public async Task<IReadOnlyList<SignageLineSignals>> GetFactorySignalsAsync(ScadaLiveViewScope scope)
    {
        var lines = AllowedLines(scope);
        var lineIds = lines.Select(x => x.Id).ToArray();
        var machines = uow.Machines.Query().Where(x => lineIds.Contains(x.LineId) && x.IsActive && !x.IsDeleted)
            .ToList().ToDictionary(x => (x.LineId, x.Name));
        var rows = await signals.ReadAsync(scope.CustomerId, scope.FactoryId, lineIds);
        // Keep a defense in depth check even for a custom reader implementation.
        return rows.Where(x => x.CustomerId == scope.CustomerId && x.FactoryId == scope.FactoryId &&
                               lineIds.Contains(x.LineId) && machines.ContainsKey((x.LineId, x.MachineName)))
            .OrderBy(x => x.LineId).ThenBy(x => x.MachineName)
            .GroupBy(x => x.LineId)
            .Select(group => new SignageLineSignals(group.Key, group.Count(), group.Select(x =>
            {
                var machine = machines[(x.LineId, x.MachineName)];
                var shift = Utc(x.ShiftStartTime);
                var at = Utc(x.LatestTimeStamp);
                return new SignageMachineSignal(x.MachineName, shift, x.TotalCountDiff, at,
                    new SignageLatestSignal(x.SignalId, x.MachineName, x.State, x.TotalCountDiff,
                        x.Qreject, x.Speed, null, x.StateReason.ToString(CultureInfo.InvariantCulture),
                        x.SKU.ToString(CultureInfo.InvariantCulture), x.SignalCountDiff, at, shift,
                        machine.Id, scope.FactoryId, group.Key, x.Fault, machine.MachineType, x.QrejectDiff));
            }).ToArray())).ToArray();
    }

    public IReadOnlyList<SignageLineDefinition> GetFactoryLines(ScadaLiveViewScope scope)
    {
        var machines = GetMachines(scope).ToLookup(x => x.LineId);
        return AllowedLines(scope).OrderBy(x => x.ViewName ?? x.Name).Select(x =>
            new SignageLineDefinition(x.Id, string.IsNullOrWhiteSpace(x.ViewName) ? x.Name : x.ViewName,
                x.AliasName, x.Number ?? 0, x.FactoryId, x.ProductType, x.Area, machines[x.Id].ToArray())).ToArray();
    }

    public IReadOnlyList<SignageMachineDefinition> GetMachines(ScadaLiveViewScope scope)
    {
        var lineIds = AllowedLines(scope).Select(x => x.Id).ToArray();
        return uow.Machines.Query().Where(x => lineIds.Contains(x.LineId) && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Id).Select(x => new SignageMachineDefinition(x.Id, x.Name, x.LineId,
                x.MachineType, x.RatedSpeedRpm, x.IsDominant, null)).ToArray();
    }

    public IReadOnlyList<SignageScaleStatus> GetScales(ScadaLiveViewScope scope)
        => uow.Scales.Query().Where(x => x.FactoryId == scope.FactoryId && !x.IsDeleted)
            .OrderBy(x => x.Id).ToList().Select(x => new SignageScaleStatus(x.Id, x.Name,
                $"Room {x.Zone}", x.FactoryId, x.TelemetryAt.HasValue ? Utc(x.TelemetryAt.Value) : null)).ToArray();

    public IReadOnlyList<SignageCurrentBatch> GetCurrentBatches(ScadaLiveViewScope scope)
    {
        var lineIds = AllowedLines(scope).Select(x => x.Id).ToArray();
        var candidates = (
            from load in uow.MachineMaterialLoads.Query()
            join batch in uow.Batches.Query() on load.BatchId equals batch.Id
            join sku in uow.Skus.Query() on batch.SkuId equals sku.Id
            join machine in uow.Machines.Query() on load.MachineId equals machine.Id
            where load.LineId.HasValue && lineIds.Contains(load.LineId.Value) &&
                  batch.CustomerId == scope.CustomerId && load.ActivationDate != null && load.IsPending == false
            select new { Load = load, batch.Uid, batch.JobOrderId, SkuName = sku.Name, MachineName = machine.Name }
        ).ToArray();
        return candidates.GroupBy(x => new { x.Load.LineId, x.Load.BatchId })
            .Where(group => group.All(x => x.Load.FinishingDate == null))
            .Select(group => group.OrderByDescending(x => x.Load.SourceRecordId ?? x.Load.Id).First())
            .OrderBy(x => x.Load.LineId).ThenBy(x => x.MachineName)
            .Select(x => new SignageCurrentBatch(x.Load.LineId!.Value, x.SkuName, x.JobOrderId, x.Uid,
                x.Load.ActivationDate.HasValue ? Utc(x.Load.ActivationDate.Value) : null,
                null, x.Load.Count, x.Load.MachineId, x.MachineName)).ToArray();
    }
    private Domain.Entities.Organization.ProductionLine[] AllowedLines(ScadaLiveViewScope scope)
    {
        var allowed = scope.AllowedLineIds.ToArray();
        return uow.ProductionLines.Query().Where(x => x.FactoryId == scope.FactoryId && allowed.Contains(x.Id) &&
            (!scope.LineId.HasValue || x.Id == scope.LineId.Value) && x.IsActive && !x.IsDeleted).ToArray();
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}