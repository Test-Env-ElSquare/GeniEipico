using Application.Abstractions.Services;
using Domain.Entities.Analytics;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class EnergyRecordWriter(DbContextOptions<MesDbContext> options) : IEnergyRecordWriter
{
    public async Task AppendAsync(int transformerId, EnergyReading reading, CancellationToken cancellationToken)
    {
        await using var strategyContext = new MesDbContext(options);
        await strategyContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var db = new MesDbContext(options);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var key = $"Analytics.EnergyReadings:transformer:{transformerId}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result=sys.sp_getapplock @Resource={key}, @LockMode=N'Exclusive',
                    @LockOwner=N'Transaction', @LockTimeout=10000;
                IF @result<0 THROW 51000,'Could not acquire energy append lock.',1;
                """, cancellationToken);
            if (await db.EnergyReadings.AnyAsync(x => x.TransformerId == transformerId && x.Timestamp == reading.Timestamp, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            var transformer = await db.Transformers.AsNoTracking().SingleAsync(x => x.Id == transformerId && !x.IsDeleted, cancellationToken);
            var factory = await db.Factories.AsNoTracking().SingleAsync(x => x.Id == transformer.FactoryId && x.IsActive && !x.IsDeleted, cancellationToken);
            // Transformer has no line relationship yet. Only infer when unambiguous.
            var lines = await db.ProductionLines.AsNoTracking()
                .Where(x => x.FactoryId == factory.Id && x.IsActive && !x.IsDeleted)
                .Select(x => x.Id).Take(2).ToListAsync(cancellationToken);
            // Fresh entity on each execution-strategy retry (no stale generated identity).
            var record = new EnergyReading
            {
                VoltageL1 = reading.VoltageL1,
                VoltageL2 = reading.VoltageL2,
                VoltageL3 = reading.VoltageL3,
                CurrentL1 = reading.CurrentL1,
                CurrentL2 = reading.CurrentL2,
                CurrentL3 = reading.CurrentL3,
                PowerFactorL1 = reading.PowerFactorL1,
                PowerFactorL2 = reading.PowerFactorL2,
                PowerFactorL3 = reading.PowerFactorL3,
                ActivePowerL1 = reading.ActivePowerL1,
                ActivePowerL2 = reading.ActivePowerL2,
                ActivePowerL3 = reading.ActivePowerL3,
                ReactivePowerL1 = reading.ReactivePowerL1,
                ReactivePowerL2 = reading.ReactivePowerL2,
                ReactivePowerL3 = reading.ReactivePowerL3,
                ApparentPowerL1 = reading.ApparentPowerL1,
                ApparentPowerL2 = reading.ApparentPowerL2,
                ApparentPowerL3 = reading.ApparentPowerL3,
                EnergyL1 = reading.EnergyL1,
                EnergyL2 = reading.EnergyL2,
                EnergyL3 = reading.EnergyL3,
                VoltageThdL1 = reading.VoltageThdL1,
                VoltageThdL2 = reading.VoltageThdL2,
                VoltageThdL3 = reading.VoltageThdL3,
                CurrentThdL1 = reading.CurrentThdL1,
                CurrentThdL2 = reading.CurrentThdL2,
                CurrentThdL3 = reading.CurrentThdL3,
            };
            record.TransformerId = transformerId;
            record.FactoryId = factory.Id;
            record.CustomerId = factory.CustomerId;
            record.LineId = lines.Count == 1 ? lines[0] : null;
            record.Quality = null;
            record.Timestamp = reading.Timestamp;
            db.EnergyReadings.Add(record);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
