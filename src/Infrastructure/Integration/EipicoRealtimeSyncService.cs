using System.Security.Cryptography;
using System.Text;
using Domain.Common;
using Domain.Entities.Analytics;
using Domain.Entities.Integration;
using Domain.Entities.Organization;
using Domain.Entities.Production;
using Domain.Entities.Scada;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Integration;

internal sealed class EipicoRealtimeSyncService(
    MesDbContext db,
    EipicoRealtimeSourceReader source,
    EipicoSapSyncService sapSync,
    EipicoSourceTime sourceTime,
    IOptions<EipicoSyncOptions> configuredOptions,
    ILogger<EipicoRealtimeSyncService> logger)
{
    private const string SourceSystem = "Eipico";
    private const string HandheldStream = "HandheldMatarial";
    private const string SplitStream = "SplittedMatarial";
    private const string MachineLoadStream = "MachineLoads";
    private const string SignalStream = "Signals";
    private const string ScaleSignalStream = "ScaleSignals";
    private const string ScaleLiveMessageStream = "ScaleLiveMessages";
    private const string EnergyStream = "Energies";
    private const string HarmonicStream = "Harmonics";
    private const string HandheldSource = "handheld";
    private const string SapSource = "sap";
    private readonly EipicoSyncOptions options = configuredOptions.Value;

    public async Task<EipicoRealtimeSyncResult> SyncNextPagesAsync(
        CancellationToken cancellationToken)
    {
        var signals = await SyncStreamAsync(
            SignalStream,
            (lastId, size, token) => source.ReadSignalsAfterAsync(lastId, size, token),
            (id, token) => source.ReadSignalByIdAsync(id, token),
            token => source.GetMaxSignalIdAsync(token),
            UpsertSignalAsync,
            cancellationToken);

        var scaleSignals = await SyncStreamAsync(
            ScaleSignalStream,
            (lastId, size, token) => source.ReadScaleSignalsAfterAsync(lastId, size, token),
            (id, token) => source.ReadScaleSignalByIdAsync(id, token),
            token => source.GetMaxScaleSignalIdAsync(token),
            UpsertScaleSignalAsync,
            cancellationToken);

        var scaleLiveMessages = await SyncStreamAsync(
            ScaleLiveMessageStream,
            (lastId, size, token) => source.ReadScaleLiveMessagesAfterAsync(lastId, size, token),
            (id, token) => source.ReadScaleLiveMessageByIdAsync(id, token),
            token => source.GetMaxScaleLiveMessageIdAsync(token),
            UpsertScaleLiveMessageAsync,
            cancellationToken);

        var energies = await SyncStreamAsync(
            EnergyStream,
            (lastId, size, token) => source.ReadEnergiesAfterAsync(lastId, size, token),
            (id, token) => source.ReadEnergyByIdAsync(id, token),
            token => source.GetMaxEnergyIdAsync(token),
            UpsertEnergyAsync,
            cancellationToken);

        var harmonics = await SyncStreamAsync(
            HarmonicStream,
            (lastId, size, token) => source.ReadHarmonicsAfterAsync(lastId, size, token),
            (id, token) => source.ReadHarmonicByIdAsync(id, token),
            token => source.GetMaxHarmonicIdAsync(token),
            UpsertHarmonicAsync,
            cancellationToken);

        var handheld = await SyncStreamAsync(
            HandheldStream,
            (lastId, size, token) => source.ReadHandheldAfterAsync(lastId, size, token),
            (id, token) => source.ReadHandheldByIdAsync(id, token),
            token => source.GetMaxHandheldIdAsync(token),
            UpsertHandheldAsync,
            cancellationToken);

        var splits = await SyncStreamAsync(
            SplitStream,
            (lastId, size, token) => source.ReadSplitsAfterAsync(lastId, size, token),
            (id, token) => source.ReadSplitByIdAsync(id, token),
            token => source.GetMaxSplitIdAsync(token),
            UpsertSplitAsync,
            cancellationToken);

        var machineLoads = await SyncStreamAsync(
            MachineLoadStream,
            (lastId, size, token) => source.ReadMachineLoadsAfterAsync(lastId, size, token),
            (id, token) => source.ReadMachineLoadByIdAsync(id, token),
            token => source.GetMaxMachineLoadIdAsync(token),
            UpsertMachineLoadAsync,
            cancellationToken);

        return new EipicoRealtimeSyncResult(
            handheld,
            splits,
            machineLoads,
            signals,
            scaleSignals,
            scaleLiveMessages,
            energies,
            harmonics);
    }

    public async Task<EipicoRealtimeSyncResult> ReconcileRecentAsync(
        CancellationToken cancellationToken)
    {
        var batchSize = Math.Clamp(options.ReconciliationBatchSize, 1, 1000);
        var handheld = await ReconcileAsync(
            HandheldStream,
            await source.ReadRecentHandheldAsync(batchSize, cancellationToken),
            ids => db.WeighingEvents
                .AsNoTracking()
                .Where(x =>
                    x.WeighingSource == HandheldSource &&
                    x.SourceRecordId.HasValue &&
                    ids.Contains(x.SourceRecordId.Value))
                .Select(x => x.SourceRecordId!.Value),
            UpsertHandheldAsync,
            cancellationToken);

        var splits = await ReconcileAsync(
            SplitStream,
            await source.ReadRecentSplitsAsync(batchSize, cancellationToken),
            ids => db.MaterialSplits
                .AsNoTracking()
                .Where(x => x.SourceRecordId.HasValue && ids.Contains(x.SourceRecordId.Value))
                .Select(x => x.SourceRecordId!.Value),
            UpsertSplitAsync,
            cancellationToken);

        var machineLoads = await ReconcileAsync(
            MachineLoadStream,
            await source.ReadRecentMachineLoadsAsync(batchSize, cancellationToken),
            ids => db.MachineMaterialLoads
                .AsNoTracking()
                .Where(x => x.SourceRecordId.HasValue && ids.Contains(x.SourceRecordId.Value))
                .Select(x => x.SourceRecordId!.Value),
            UpsertMachineLoadAsync,
            cancellationToken);

        return new EipicoRealtimeSyncResult(handheld, splits, machineLoads, 0, 0, 0, 0, 0);
    }

    private async Task<int> SyncStreamAsync<TRecord>(
        string streamName,
        Func<long, int, CancellationToken, Task<IReadOnlyList<TRecord>>> readAfter,
        Func<long, CancellationToken, Task<TRecord?>> readById,
        Func<CancellationToken, Task<long>> readCurrentMax,
        Func<TRecord, CancellationToken, Task> upsert,
        CancellationToken cancellationToken)
        where TRecord : class, IEipicoRealtimeRecord
    {
        await RetryErrorsAsync(streamName, readById, upsert, cancellationToken);

        var checkpoint = await db.SyncCheckpoints
            .SingleOrDefaultAsync(
                x => x.SourceSystem == SourceSystem && x.StreamName == streamName,
                cancellationToken);
        if (checkpoint is null)
        {
            if (options.InitializeMissingRealtimeCheckpoints)
            {
                checkpoint = new SyncCheckpoint
                {
                    SourceSystem = SourceSystem,
                    StreamName = streamName,
                    LastSourceId = await readCurrentMax(cancellationToken),
                    UpdatedAt = DateTime.UtcNow
                };
                db.SyncCheckpoints.Add(checkpoint);
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Initialized Eipico realtime checkpoint {StreamName} at source id {SourceId}; historical rows were not imported.",
                    streamName,
                    checkpoint.LastSourceId);
                return 0;
            }

            checkpoint = new SyncCheckpoint
            {
                SourceSystem = SourceSystem,
                StreamName = streamName,
                LastSourceId = 0,
                UpdatedAt = DateTime.UtcNow
            };
            db.SyncCheckpoints.Add(checkpoint);
            await db.SaveChangesAsync(cancellationToken);
        }

        var lastSourceId = checkpoint.LastSourceId;

        var records = await readAfter(
            lastSourceId,
            Math.Clamp(options.BatchSize, 1, 1000),
            cancellationToken);

        foreach (var record in records)
        {
            await ProcessRecordAsync(
                streamName,
                record,
                upsert,
                advanceCheckpoint: true,
                cancellationToken);
        }

        return records.Count;
    }

    private async Task<int> ReconcileAsync<TRecord>(
        string streamName,
        IReadOnlyList<TRecord> records,
        Func<long[], IQueryable<long>> existingTargetIds,
        Func<TRecord, CancellationToken, Task> upsert,
        CancellationToken cancellationToken)
        where TRecord : IEipicoRealtimeRecord
    {
        if (records.Count == 0)
            return 0;

        var sourceIds = records.Select(x => (long)x.Id).ToArray();
        var synchronizedIds = await existingTargetIds(sourceIds)
            .ToHashSetAsync(cancellationToken);

        var reconciled = 0;
        foreach (var record in records.Where(x => synchronizedIds.Contains(x.Id)))
        {
            if (await ProcessRecordAsync(
                    streamName,
                    record,
                    upsert,
                    advanceCheckpoint: false,
                    cancellationToken))
            {
                reconciled++;
            }
        }

        return reconciled;
    }

    private async Task RetryErrorsAsync<TRecord>(
        string streamName,
        Func<long, CancellationToken, Task<TRecord?>> readById,
        Func<TRecord, CancellationToken, Task> upsert,
        CancellationToken cancellationToken)
        where TRecord : class, IEipicoRealtimeRecord
    {
        var retryBefore = DateTime.UtcNow.AddSeconds(-Math.Max(1, options.ErrorRetryIntervalSeconds));
        var failedIds = await db.SyncErrors
            .AsNoTracking()
            .Where(x =>
                x.SourceSystem == SourceSystem &&
                x.StreamName == streamName &&
                x.SourceRecordId.HasValue &&
                x.ResolvedAt == null &&
                x.LastOccurredAt <= retryBefore)
            .Select(x => x.SourceRecordId!.Value)
            .Distinct()
            .OrderBy(x => x)
            .Take(Math.Clamp(options.RetryBatchSize, 1, 100))
            .ToArrayAsync(cancellationToken);

        foreach (var sourceId in failedIds)
        {
            var record = await readById(sourceId, cancellationToken);
            if (record is null)
            {
                await ResolveErrorsAsync(streamName, sourceId, cancellationToken);
                continue;
            }

            await ProcessRecordAsync(
                streamName,
                record,
                upsert,
                advanceCheckpoint: false,
                cancellationToken);
        }
    }

    private async Task<bool> ProcessRecordAsync<TRecord>(
        string streamName,
        TRecord record,
        Func<TRecord, CancellationToken, Task> upsert,
        bool advanceCheckpoint,
        CancellationToken cancellationToken)
        where TRecord : IEipicoRealtimeRecord
    {
        try
        {
            await ApplyRecordAsync(streamName, record, upsert, advanceCheckpoint, cancellationToken);
            return true;
        }
        catch (InvalidDataException exception)
        {
            await ResetDatabaseAfterFailureAsync();
            await RecordFailureAsync(
                streamName,
                record.Id,
                exception,
                advanceCheckpoint,
                terminal: true,
                cancellationToken);

            logger.LogWarning(
                "Skipped invalid Eipico {StreamName} record {SourceRecordId}: {Reason}",
                streamName,
                record.Id,
                exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await ResetDatabaseAfterFailureAsync();
            await RecordFailureAsync(
                streamName,
                record.Id,
                exception,
                advanceCheckpoint,
                terminal: false,
                cancellationToken);

            logger.LogWarning(
                exception,
                "Could not synchronize Eipico {StreamName} record {SourceRecordId}; it remains retryable.",
                streamName,
                record.Id);
        }

        return false;
    }

    private async Task ApplyRecordAsync<TRecord>(
        string streamName,
        TRecord record,
        Func<TRecord, CancellationToken, Task> upsert,
        bool advanceCheckpoint,
        CancellationToken cancellationToken)
        where TRecord : IEipicoRealtimeRecord
    {
        var executionStrategy = db.Database.CreateExecutionStrategy();
        try
        {
            await executionStrategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await upsert(record, cancellationToken);
                    await ResolveErrorsAsync(streamName, record.Id, cancellationToken);
                    if (advanceCheckpoint)
                        await AdvanceCheckpointAsync(streamName, record.Id, cancellationToken);

                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    try
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                    }
                    catch (Exception rollbackException)
                    {
                        logger.LogWarning(
                            rollbackException,
                            "Could not roll back the failed Eipico {StreamName} synchronization transaction. The connection will be reset.",
                            streamName);
                    }

                    logger.LogDebug(
                        exception,
                        "Rolling back Eipico {StreamName} record {SourceRecordId}.",
                        streamName,
                        record.Id);
                    throw;
                }
            });
        }
        catch
        {
            await ResetDatabaseAfterFailureAsync();
            throw;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task ResetDatabaseAfterFailureAsync()
    {
        db.ChangeTracker.Clear();

        if (db.Database.CurrentTransaction is { } transaction)
        {
            try
            {
                await transaction.DisposeAsync();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not dispose the failed Eipico realtime synchronization transaction.");
            }
        }

        try
        {
            await db.Database.CloseConnectionAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not close the failed Eipico realtime synchronization database connection.");
        }

        db.ChangeTracker.Clear();
    }

    private async Task UpsertSignalAsync(
        EipicoSignalRecord sourceRecord,
        CancellationToken cancellationToken)
    {
        var machineIdentity = FirstValue(
            sourceRecord.MachineUid,
            sourceRecord.MachineName,
            sourceRecord.SourceMachineId?.ToString());
        if (string.IsNullOrWhiteSpace(machineIdentity))
            throw new InvalidDataException($"Signal record {sourceRecord.Id} has no machine identity.");

        var (factory, line, machine) = await EnsureRealtimeMachineAsync(
            sourceRecord,
            machineIdentity,
            cancellationToken);
        var timestampUtc = sourceTime.ToUtc(sourceRecord.Timestamp);

        var machineNames = new[] { machine.Name, machine.Code, machine.Id.ToString() };
        var existing = await db.MachineStates
            .Where(x => x.CustomerId == factory.CustomerId &&
                        x.FactoryId == factory.Id &&
                        x.LineId == line.Id &&
                        machineNames.Contains(x.MachineId) &&
                        x.Timestamp == timestampUtc)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return;

        var previous = await db.MachineStates
            .Where(x => x.CustomerId == factory.CustomerId &&
                        x.FactoryId == factory.Id &&
                        x.LineId == line.Id &&
                        machineNames.Contains(x.MachineId) &&
                        x.Timestamp <= timestampUtc)
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var count = sourceRecord.Count ?? 0;
        var qReject = sourceRecord.QReject ?? 0;
        var record = new MachineState
        {
            MachineId = machine.Name,
            State = sourceRecord.State ?? 0,
            Speed = sourceRecord.Speed ?? 0,
            Count = count,
            CountDiff = sourceRecord.CountDiff ??
                        (previous is null ? 0m : Math.Max(0m, count - previous.Count)),
            CustomerId = factory.CustomerId,
            FactoryId = factory.Id,
            LineId = line.Id,
            QReject = qReject,
            QRejectDiff = sourceRecord.QRejectDiff ??
                          (previous is null ? 0m : Math.Max(0m, qReject - previous.QReject)),
            StateReason = sourceRecord.StateReason ?? 0,
            Sku = sourceRecord.Sku ?? 0,
            Fault = sourceRecord.Fault ?? 0,
            Timestamp = timestampUtc
        };

        db.MachineStates.Add(record);

        if (previous is not null &&
            previous.State != record.State &&
            !await db.MachineStateHistories.AnyAsync(
                x => x.MachineId == machine.Id && x.TransitionedAt == record.Timestamp,
                cancellationToken))
        {
            db.MachineStateHistories.Add(new MachineStateHistory
            {
                MachineId = machine.Id,
                FromState = previous.State.ToString(),
                ToState = record.State.ToString(),
                TransitionedAt = record.Timestamp,
                TriggerSource = $"eipico:{SignalStream}",
                Notes = $"SourceRecordId={sourceRecord.Id}"
            });
        }
    }

    private async Task UpsertScaleSignalAsync(
        EipicoScaleSignalRecord sourceRecord,
        CancellationToken cancellationToken)
    {
        var scale = await EnsureRealtimeScaleAsync(
            sourceRecord.SourceScaleId,
            sourceRecord.Name,
            cancellationToken);

        scale.WeightKg = sourceRecord.Weight;
        scale.ScaleState = "online";
        scale.TelemetryAt = sourceTime.ToUtc(sourceRecord.Timestamp);
    }

    private async Task UpsertScaleLiveMessageAsync(
        EipicoScaleLiveMessageRecord sourceRecord,
        CancellationToken cancellationToken)
    {
        var sourceScaleId = sourceRecord.SourceScaleId ??
                            (int.TryParse(sourceRecord.ScaleReference, out var parsed)
                                ? parsed
                                : (int?)null);
        var scale = await EnsureRealtimeScaleAsync(
            sourceScaleId,
            sourceRecord.ScaleReference,
            cancellationToken);

        scale.ScaleState = "online";
        scale.TelemetryAt = sourceTime.ToUtc(sourceRecord.Timestamp);
    }

    private async Task UpsertEnergyAsync(
        EipicoEnergyRecord sourceRecord,
        CancellationToken cancellationToken)
    {
        var transformerName = Required(sourceRecord.SourceId, "Energy.SourceId", 150);
        var transformer = await db.Transformers
            .SingleOrDefaultAsync(
                x => !x.IsDeleted && x.TransformerName == transformerName,
                cancellationToken);
        if (transformer is null)
        {
            throw new EipicoDependencyNotReadyException(
                $"Transformer '{transformerName}' is not synchronized yet.");
        }

        var factory = await db.Factories
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => !x.IsDeleted && x.IsActive && x.Id == transformer.FactoryId,
                cancellationToken);
        if (factory is null)
        {
            throw new EipicoDependencyNotReadyException(
                $"Transformer '{transformerName}' has no active synchronized factory.");
        }

        var target = await db.EnergyReadings
            .SingleOrDefaultAsync(x => x.SourceRecordId == sourceRecord.Id, cancellationToken);
        if (target is null)
        {
            target = new EnergyReading { SourceRecordId = sourceRecord.Id };
            db.EnergyReadings.Add(target);
        }

        target.CustomerId = factory.CustomerId;
        target.FactoryId = factory.Id;
        target.LineId = null;
        target.TransformerId = transformer.Id;
        target.Quality = sourceRecord.Fault is >= short.MinValue and <= short.MaxValue
            ? (short?)sourceRecord.Fault.Value
            : null;
        target.VoltageL1 = sourceRecord.VoltageL1;
        target.VoltageL2 = sourceRecord.VoltageL2;
        target.VoltageL3 = sourceRecord.VoltageL3;
        target.CurrentL1 = sourceRecord.CurrentL1;
        target.CurrentL2 = sourceRecord.CurrentL2;
        target.CurrentL3 = sourceRecord.CurrentL3;
        target.PowerFactorL1 = sourceRecord.PowerFactorL1;
        target.PowerFactorL2 = sourceRecord.PowerFactorL2;
        target.PowerFactorL3 = sourceRecord.PowerFactorL3;
        target.ApparentPowerL1 = sourceRecord.ApparentPowerL1;
        target.ApparentPowerL2 = sourceRecord.ApparentPowerL2;
        target.ApparentPowerL3 = sourceRecord.ApparentPowerL3;
        target.ActivePowerL1 = sourceRecord.ActivePowerL1;
        target.ActivePowerL2 = sourceRecord.ActivePowerL2;
        target.ActivePowerL3 = sourceRecord.ActivePowerL3;
        target.ReactivePowerL1 = sourceRecord.ReactivePowerL1;
        target.ReactivePowerL2 = sourceRecord.ReactivePowerL2;
        target.ReactivePowerL3 = sourceRecord.ReactivePowerL3;
        target.EnergyL1 = sourceRecord.EnergyL1;
        target.EnergyL2 = sourceRecord.EnergyL2;
        target.EnergyL3 = sourceRecord.EnergyL3;
        target.VoltageThdL1 = sourceRecord.VoltageThdL1;
        target.VoltageThdL2 = sourceRecord.VoltageThdL2;
        target.VoltageThdL3 = sourceRecord.VoltageThdL3;
        target.CurrentThdL1 = sourceRecord.CurrentThdL1;
        target.CurrentThdL2 = sourceRecord.CurrentThdL2;
        target.CurrentThdL3 = sourceRecord.CurrentThdL3;
        target.Timestamp = sourceTime.ToUtc(sourceRecord.Timestamp);
    }

    private async Task UpsertHarmonicAsync(
        EipicoHarmonicRecord sourceRecord,
        CancellationToken cancellationToken)
    {
        var transformerName = Required(sourceRecord.SourceId, "Harmonic.SourceId", 150);
        var transformer = await db.Transformers
            .SingleOrDefaultAsync(
                x => !x.IsDeleted && x.TransformerName == transformerName,
                cancellationToken);
        if (transformer is null)
        {
            throw new EipicoDependencyNotReadyException(
                $"Transformer '{transformerName}' is not synchronized yet.");
        }

        var target = await db.EnergyHarmonicReadings
            .SingleOrDefaultAsync(x => x.SourceRecordId == sourceRecord.Id, cancellationToken);
        if (target is null)
        {
            target = new EnergyHarmonicReading { SourceRecordId = sourceRecord.Id };
            db.EnergyHarmonicReadings.Add(target);
        }

        target.TransformerId = transformer.Id;
        target.Timestamp = sourceTime.ToUtc(sourceRecord.Timestamp);
        target.HarmonicI31 = sourceRecord.HarmonicI31;
        target.HarmonicI32 = sourceRecord.HarmonicI32;
        target.HarmonicI33 = sourceRecord.HarmonicI33;
        target.HarmonicV31 = sourceRecord.HarmonicV31;
        target.HarmonicV32 = sourceRecord.HarmonicV32;
        target.HarmonicV33 = sourceRecord.HarmonicV33;
        target.HarmonicI51 = sourceRecord.HarmonicI51;
        target.HarmonicI52 = sourceRecord.HarmonicI52;
        target.HarmonicI53 = sourceRecord.HarmonicI53;
        target.HarmonicV51 = sourceRecord.HarmonicV51;
        target.HarmonicV52 = sourceRecord.HarmonicV52;
        target.HarmonicV53 = sourceRecord.HarmonicV53;
        target.HarmonicI71 = sourceRecord.HarmonicI71;
        target.HarmonicI72 = sourceRecord.HarmonicI72;
        target.HarmonicI73 = sourceRecord.HarmonicI73;
        target.HarmonicV71 = sourceRecord.HarmonicV71;
        target.HarmonicV72 = sourceRecord.HarmonicV72;
        target.HarmonicV73 = sourceRecord.HarmonicV73;
        target.HarmonicI91 = sourceRecord.HarmonicI91;
        target.HarmonicI92 = sourceRecord.HarmonicI92;
        target.HarmonicI93 = sourceRecord.HarmonicI93;
        target.HarmonicV91 = sourceRecord.HarmonicV91;
        target.HarmonicV92 = sourceRecord.HarmonicV92;
        target.HarmonicV93 = sourceRecord.HarmonicV93;
        target.HarmonicI111 = sourceRecord.HarmonicI111;
        target.HarmonicI112 = sourceRecord.HarmonicI112;
        target.HarmonicI113 = sourceRecord.HarmonicI113;
        target.HarmonicV111 = sourceRecord.HarmonicV111;
        target.HarmonicV112 = sourceRecord.HarmonicV112;
        target.HarmonicV113 = sourceRecord.HarmonicV113;
        target.HarmonicI131 = sourceRecord.HarmonicI131;
        target.HarmonicI132 = sourceRecord.HarmonicI132;
        target.HarmonicI133 = sourceRecord.HarmonicI133;
        target.HarmonicV131 = sourceRecord.HarmonicV131;
        target.HarmonicV132 = sourceRecord.HarmonicV132;
        target.HarmonicV133 = sourceRecord.HarmonicV133;
    }

    private async Task<(Factory Factory, ProductionLine Line, Machine Machine)> EnsureRealtimeMachineAsync(
        EipicoSignalRecord sourceRecord,
        string machineIdentity,
        CancellationToken cancellationToken)
    {
        var lineCode = FirstValue(sourceRecord.LineCode);
        var factoryName = FirstValue(sourceRecord.SourceFactoryName);
        if (lineCode is null || factoryName is null)
            throw new EipicoDependencyNotReadyException(
                $"Signal {sourceRecord.Id} has no synchronized Eipico line/factory definition.");

        var line = await db.ProductionLines
            .IgnoreQueryFilters()
            .Include(x => x.Factory)
            .SingleOrDefaultAsync(x =>
                !x.IsDeleted && x.Code == lineCode &&
                x.Factory.Name == factoryName,
                cancellationToken)
            ?? throw new EipicoDependencyNotReadyException(
                $"Signal {sourceRecord.Id} line '{lineCode}' in factory '{factoryName}' is not synchronized yet.");
        var factory = line.Factory;

        var machineCode = SourceCode("M", sourceRecord.SourceMachineId, machineIdentity);
        var machine = await db.Machines
            .IgnoreQueryFilters()
            .Where(x => x.LineId == line.Id && x.Code == machineCode)
            .OrderBy(x => x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (machine is null)
        {
            machine = new Machine
            {
                LineId = line.Id,
                Code = machineCode,
                IsActive = true
            };
            db.Machines.Add(machine);
        }
        else
        {
            Restore(machine);
            machine.IsActive = true;
        }

        machine.Name = TruncateRequired(
            FirstValue(sourceRecord.MachineName, machineIdentity)!,
            100);
        machine.MachineType = TruncateRequired(
            FirstValue(sourceRecord.MachineType, "Eipico")!,
            60);
        machine.RatedSpeedRpm = ToNullableInt(sourceRecord.RatedSpeed);

        if (machine.Id == 0)
            await db.SaveChangesAsync(cancellationToken);

        return (factory, line, machine);
    }

    private async Task<Scale> EnsureRealtimeScaleAsync(
        int? sourceScaleId,
        string? sourceName,
        CancellationToken cancellationToken)
    {
        if (sourceScaleId.HasValue)
        {
            var code = SourceCode("S", sourceScaleId, sourceScaleId.Value.ToString());
            return await db.Scales
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => !x.IsDeleted && x.Code == code, cancellationToken)
                ?? throw new EipicoDependencyNotReadyException(
                    $"Scale definition {sourceScaleId.Value} is not synchronized yet.");
        }

        var targetName = FirstValue(sourceName);
        if (targetName is null)
            throw new EipicoDependencyNotReadyException("Scale message has no definition identifier.");

        var matches = await db.Scales
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.Name == targetName)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (matches.Count != 1)
            throw new EipicoDependencyNotReadyException(
                $"Scale name '{targetName}' does not identify exactly one synchronized scale.");
        return matches[0];
    }

    private async Task<Factory> EnsureRealtimeFactoryAsync(CancellationToken cancellationToken)
    {
        var customer = await EnsureRealtimeCustomerAsync(cancellationToken);
        if (customer.Id == 0)
            await db.SaveChangesAsync(cancellationToken);

        var factory = await db.Factories
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == customer.Id && x.Code == options.FactoryCode)
            .OrderBy(x => x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (factory is null)
        {
            factory = new Factory
            {
                CustomerId = customer.Id,
                Code = Required(options.FactoryCode, "EipicoSync.FactoryCode", 30),
                Name = TruncateRequired(options.FactoryName, 150),
                CountryCode = "EGY",
                TimeZone = "Africa/Cairo",
                IsActive = true
            };
            db.Factories.Add(factory);
        }
        else
        {
            Restore(factory);
            factory.IsActive = true;
        }

        return factory;
    }

    private async Task<Customer> EnsureRealtimeCustomerAsync(CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .IgnoreQueryFilters()
            .Where(x => x.Code == options.CustomerCode)
            .OrderBy(x => x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            customer = new Customer
            {
                Code = Required(options.CustomerCode, "EipicoSync.CustomerCode", 30),
                Name = TruncateRequired(options.CustomerName, 150),
                Tier = "enterprise",
                CountryCode = "EGY",
                Industry = "Pharmaceutical",
                ErpSystem = "SAP",
                IsActive = true
            };
            db.Customers.Add(customer);
        }
        else
        {
            Restore(customer);
            customer.IsActive = true;
        }

        return customer;
    }

    private async Task UpsertHandheldAsync(
        EipicoHandheldRecord sourceRecord,
        CancellationToken cancellationToken)
    {
        var materialUid = Required(sourceRecord.MaterialUid, "Handheld.UID", 30);
        if (sourceRecord.Weight < 0)
            throw new InvalidDataException($"Handheld record {sourceRecord.Id} has a negative weight.");

        var batchMaterial = await FindBatchMaterialAsync(
            sourceRecord.SapOrderMaterialId,
            sourceRecord.SapOrderId,
            materialUid,
            cancellationToken);
        if (batchMaterial is null)
        {
            await sapSync.ImportDependencyAsync(
                sourceRecord.SapOrderId, sourceRecord.SapOrderMaterialId,
                null, materialUid, cancellationToken);
            batchMaterial = await FindBatchMaterialAsync(
                sourceRecord.SapOrderMaterialId, sourceRecord.SapOrderId,
                materialUid, cancellationToken);
        }
        if (batchMaterial is null)
        {
            throw new EipicoDependencyNotReadyException(
                $"No synchronized SAP material matches handheld UID '{materialUid}'.");
        }

        var batch = await db.Batches
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(x => x.Id == batchMaterial.BatchId, cancellationToken);

        Scale? scale = null;
        var scaleName = FirstValue(sourceRecord.SourceScaleName, sourceRecord.ScaleReference);
        if (!string.IsNullOrWhiteSpace(scaleName))
        {
            var factoryId = await db.ProductionLines
                .IgnoreQueryFilters()
                .Where(x => x.Id == batch.LineId)
                .Select(x => (int?)x.FactoryId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!factoryId.HasValue)
                throw new EipicoDependencyNotReadyException("The SAP batch line/factory is not synchronized yet.");

            scale = await EnsureScaleAsync(
                factoryId.Value,
                sourceRecord.SourceScaleId,
                scaleName,
                sourceRecord.SourceScaleRoom,
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var target = await db.WeighingEvents.SingleOrDefaultAsync(
            x => x.WeighingSource == HandheldSource && x.SourceRecordId == sourceRecord.Id,
            cancellationToken);
        if (target is null)
        {
            target = new WeighingEvent
            {
                WeighingSource = HandheldSource,
                SourceRecordId = sourceRecord.Id
            };
            db.WeighingEvents.Add(target);
        }

        target.BatchId = batchMaterial.BatchId;
        target.BatchMaterialId = batchMaterial.Id;
        target.ScaleId = scale?.Id;
        target.MaterialUid = materialUid;
        target.Weight = sourceRecord.Weight;
        var recordedAtUtc = sourceTime.ToUtc(sourceRecord.RecordedAt);
        target.RecordedAt = recordedAtUtc;
        target.Dispatched = sourceRecord.Dispatched;

        ApplyDispenseSummary(batchMaterial, sourceRecord.Weight, recordedAtUtc, scale?.Id);
    }

    private async Task UpsertSplitAsync(
        EipicoMaterialSplitRecord sourceRecord,
        CancellationToken cancellationToken)
    {
        var splitUid = Required(sourceRecord.SplitUid, "Split.SplittedUid", 30);
        if (!sourceRecord.SapOrderMaterialId.HasValue)
            throw new InvalidDataException($"Split record {sourceRecord.Id} has no SAPOrderMaterialId.");
        if (sourceRecord.WeightKg < 0)
            throw new InvalidDataException($"Split record {sourceRecord.Id} has a negative weight.");

        var batchMaterial = await db.BatchMaterials
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                x => !x.IsDeleted && x.SourceRecordId == sourceRecord.SapOrderMaterialId.Value,
                cancellationToken);
        if (batchMaterial is null)
        {
            await sapSync.ImportDependencyAsync(
                null, sourceRecord.SapOrderMaterialId, null, null, cancellationToken);
            batchMaterial = await db.BatchMaterials.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => !x.IsDeleted &&
                    x.SourceRecordId == sourceRecord.SapOrderMaterialId.Value, cancellationToken);
        }
        if (batchMaterial is null)
        {
            throw new EipicoDependencyNotReadyException(
                $"SAP material {sourceRecord.SapOrderMaterialId.Value} is not synchronized yet.");
        }

        var normalizedWeight = sourceRecord.WeightKg * options.SplitWeightToTargetFactor;
        var target = await db.MaterialSplits.SingleOrDefaultAsync(
            x => x.SourceRecordId == sourceRecord.Id,
            cancellationToken);
        if (target is null)
        {
            target = new MaterialSplit { SourceRecordId = sourceRecord.Id };
            db.MaterialSplits.Add(target);
        }

        target.BatchMaterialId = batchMaterial.Id;
        target.SplitUid = splitUid;
        target.Weight = normalizedWeight;
        var splitAtUtc = sourceTime.ToUtc(sourceRecord.SplitAt);
        target.SplitAt = splitAtUtc;

        await db.SaveChangesAsync(cancellationToken);

        var hasHandheldWeight = await db.WeighingEvents.AsNoTracking().AnyAsync(
            x => x.BatchMaterialId == batchMaterial.Id && x.WeighingSource == HandheldSource,
            cancellationToken);
        if (!hasHandheldWeight)
        {
            var totalSplitWeight = await db.MaterialSplits
                .Where(x => x.BatchMaterialId == batchMaterial.Id)
                .SumAsync(x => x.Weight, cancellationToken);
            ApplyDispenseSummary(
                batchMaterial,
                totalSplitWeight,
                splitAtUtc,
                scaleId: null);
        }
    }

    private async Task UpsertMachineLoadAsync(
        EipicoMachineLoadRecord sourceRecord,
        CancellationToken cancellationToken)
    {
        var batchUid = Required(sourceRecord.BatchUid, "MachineLoad.BatchId", 30);
        var materialUid = Required(sourceRecord.MaterialUid, "MachineLoad.MaterialUID", 30);
        if (sourceRecord.Weight < 0)
            throw new InvalidDataException($"Machine load {sourceRecord.Id} has a negative weight.");

        var batch = await db.Batches
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                x => !x.IsDeleted && x.SourceType == SapSource && x.Uid == batchUid,
                cancellationToken);
        if (batch is null)
        {
            await sapSync.ImportDependencyAsync(null, null, batchUid, null, cancellationToken);
            batch = await db.Batches.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => !x.IsDeleted &&
                    x.SourceType == SapSource && x.Uid == batchUid, cancellationToken);
        }
        if (batch is null)
            throw new EipicoDependencyNotReadyException($"SAP batch '{batchUid}' is not synchronized yet.");

        var materialSplit = await db.MaterialSplits
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.SplitUid == materialUid, cancellationToken);

        BatchMaterial? batchMaterial;
        if (materialSplit is not null)
        {
            batchMaterial = await db.BatchMaterials
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    x => !x.IsDeleted &&
                         x.Id == materialSplit.BatchMaterialId &&
                         x.BatchId == batch.Id,
                    cancellationToken);
        }
        else
        {
            batchMaterial = await db.BatchMaterials
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    x => !x.IsDeleted && x.BatchId == batch.Id && x.MaterialUid == materialUid,
                    cancellationToken);
        }

        if (batchMaterial is null)
        {
            throw new EipicoDependencyNotReadyException(
                $"No synchronized material '{materialUid}' exists on batch '{batchUid}'.");
        }

        var machine = await EnsureMachineAsync(batch.LineId, sourceRecord, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var target = await db.MachineMaterialLoads.SingleOrDefaultAsync(
            x => x.SourceRecordId == sourceRecord.Id,
            cancellationToken);
        if (target is null)
        {
            target = new MachineMaterialLoad { SourceRecordId = sourceRecord.Id };
            db.MachineMaterialLoads.Add(target);
        }

        target.BatchId = batch.Id;
        target.BatchMaterialId = batchMaterial.Id;
        target.MaterialSplitId = materialSplit?.Id;
        target.MachineId = machine.Id;
        target.MaterialUid = materialUid;
        target.Weight = sourceRecord.Weight;
        target.LoadedAt = sourceTime.ToUtc(sourceRecord.LoadedAt);
        target.ShiftStartTime = sourceTime.ToUtc(sourceRecord.ShiftStartTime);
        target.ActivationDate = sourceTime.ToUtc(sourceRecord.ActivationDate);
        target.FinishingDate = sourceTime.ToUtc(sourceRecord.FinishingDate);
        target.IsPending = sourceRecord.IsPending;
        target.Count = sourceRecord.Count;
    }

    private async Task<BatchMaterial?> FindBatchMaterialAsync(
        int? sourceMaterialId,
        int? sourceOrderId,
        string materialUid,
        CancellationToken cancellationToken)
    {
        if (sourceMaterialId.HasValue)
        {
            var exactMaterial = await db.BatchMaterials
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    x => !x.IsDeleted && x.SourceRecordId == sourceMaterialId.Value,
                    cancellationToken);
            if (exactMaterial is not null)
                return exactMaterial;
        }

        if (sourceOrderId.HasValue)
        {
            return await db.BatchMaterials
                .IgnoreQueryFilters()
                .Where(x => !x.IsDeleted && x.MaterialUid == materialUid)
                .Where(x => db.Batches.IgnoreQueryFilters().Any(b =>
                    !b.IsDeleted &&
                    b.Id == x.BatchId &&
                    b.SourceType == SapSource &&
                    b.SourceRecordId == sourceOrderId.Value))
                .SingleOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    private async Task<Scale> EnsureScaleAsync(
        int factoryId,
        int? sourceScaleId,
        string sourceName,
        string? sourceRoom,
        CancellationToken cancellationToken)
    {
        var code = SourceCode("S", sourceScaleId, sourceName);
        var scale = await db.Scales
            .IgnoreQueryFilters()
            .Where(x => x.FactoryId == factoryId && x.Code == code)
            .OrderBy(x => x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (scale is null)
        {
            scale = new Scale
            {
                FactoryId = factoryId,
                Code = code,
                TareKg = 0,
                ScaleState = "offline"
            };
            db.Scales.Add(scale);
        }
        else
        {
            Restore(scale);
        }

        scale.Name = TruncateRequired(sourceName.Trim(), 100);
        scale.Zone = Truncate(sourceRoom?.Trim(), 20);
        return scale;
    }

    private async Task<Machine> EnsureMachineAsync(
        int lineId,
        EipicoMachineLoadRecord sourceRecord,
        CancellationToken cancellationToken)
    {
        var identity = FirstValue(sourceRecord.MachineUid, sourceRecord.MachineName)
            ?? sourceRecord.SourceMachineId?.ToString()
            ?? throw new InvalidDataException($"Machine load {sourceRecord.Id} has no machine identity.");
        var code = SourceCode("M", sourceRecord.SourceMachineId, identity);
        var machine = await db.Machines
            .IgnoreQueryFilters()
            .Where(x => x.LineId == lineId && x.Code == code)
            .OrderBy(x => x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (machine is null)
        {
            machine = new Machine
            {
                LineId = lineId,
                Code = code,
                IsActive = true
            };
            db.Machines.Add(machine);
        }
        else
        {
            Restore(machine);
            machine.IsActive = true;
        }

        machine.Name = TruncateRequired(FirstValue(sourceRecord.MachineName, identity)!, 100);
        machine.MachineType = TruncateRequired(FirstValue(sourceRecord.MachineType, "Eipico")!, 60);
        machine.RatedSpeedRpm = ToNullableInt(sourceRecord.RatedSpeed);
        return machine;
    }

    private void ApplyDispenseSummary(
        BatchMaterial material,
        decimal actualWeight,
        DateTime? completedAt,
        int? scaleId)
    {
        material.DispensedQty = actualWeight;
        material.ScaleId = scaleId ?? material.ScaleId;
        material.DispenseStartedAt ??= completedAt;
        material.DispenseCompletedAt = completedAt;

        if (material.PlannedQty <= 0)
        {
            material.DeviationPct = null;
            material.Status = actualWeight > 0 ? "complete" : "pending";
            return;
        }

        var deviation = Math.Abs(actualWeight - material.PlannedQty) / material.PlannedQty * 100m;
        material.DeviationPct = Math.Round(deviation, 4);
        material.Status = deviation <= options.DispenseTolerancePct ? "complete" : "pending";
    }

    private async Task AdvanceCheckpointAsync(
        string streamName,
        long sourceId,
        CancellationToken cancellationToken)
    {
        var checkpoint = await db.SyncCheckpoints.SingleOrDefaultAsync(
            x => x.SourceSystem == SourceSystem && x.StreamName == streamName,
            cancellationToken);

        if (checkpoint is null)
        {
            checkpoint = new SyncCheckpoint
            {
                SourceSystem = SourceSystem,
                StreamName = streamName,
                LastSourceId = sourceId,
                UpdatedAt = DateTime.UtcNow
            };
            db.SyncCheckpoints.Add(checkpoint);
        }
        else if (sourceId > checkpoint.LastSourceId)
        {
            checkpoint.LastSourceId = sourceId;
            checkpoint.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task RecordFailureAsync(
        string streamName,
        long sourceId,
        Exception exception,
        bool advanceCheckpoint,
        bool terminal,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var error = await db.SyncErrors
            .Where(x =>
                x.SourceSystem == SourceSystem &&
                x.StreamName == streamName &&
                x.SourceRecordId == sourceId &&
                x.ResolvedAt == null)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (error is null)
        {
            error = new SyncError
            {
                SourceSystem = SourceSystem,
                StreamName = streamName,
                SourceRecordId = sourceId,
                FirstOccurredAt = utcNow
            };
            db.SyncErrors.Add(error);
        }

        error.ErrorMessage = DescribeException(exception);
        error.AttemptCount++;
        error.LastOccurredAt = utcNow;
        error.ResolvedAt = terminal ? utcNow : null;
        if (advanceCheckpoint)
            await AdvanceCheckpointAsync(streamName, sourceId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    private static string DescribeException(Exception exception)
    {
        var rootCause = exception.GetBaseException();
        return ReferenceEquals(rootCause, exception)
            ? $"{exception.GetType().Name}: {exception.Message}"
            : $"{exception.GetType().Name}: {exception.Message} | " +
              $"{rootCause.GetType().Name}: {rootCause.Message}";
    }

    private async Task ResolveErrorsAsync(
        string streamName,
        long sourceId,
        CancellationToken cancellationToken)
    {
        var errors = await db.SyncErrors
            .Where(x =>
                x.SourceSystem == SourceSystem &&
                x.StreamName == streamName &&
                x.SourceRecordId == sourceId &&
                x.ResolvedAt == null)
            .ToListAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;
        foreach (var error in errors)
            error.ResolvedAt = utcNow;

        if (errors.Count > 0 && db.Database.CurrentTransaction is null)
            await db.SaveChangesAsync(cancellationToken);
    }

    private static string Required(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidDataException($"{fieldName} is required.");
        if (normalized.Length > maxLength)
        {
            throw new InvalidDataException(
                $"{fieldName} exceeds the target maximum length of {maxLength} characters.");
        }

        return normalized;
    }

    private static string SourceCode(string kind, int? sourceId, string identity)
    {
        if (sourceId.HasValue)
            return $"EIP-{kind}{sourceId.Value}";

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.Trim())));
        return $"EIP-{kind}{hash[..12]}";
    }

    private static string? FirstValue(params string?[] values)
        => values.Select(x => x?.Trim()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private static string? Truncate(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];

    private static string TruncateRequired(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static int? ToNullableInt(decimal? value)
    {
        if (!value.HasValue || value < 0 || value > int.MaxValue)
            return null;
        return decimal.ToInt32(decimal.Round(value.Value, 0, MidpointRounding.AwayFromZero));
    }

    private static void Restore(SoftDeleteEntity entity)
    {
        entity.IsDeleted = false;
        entity.DeletedAt = null;
    }
}

internal sealed class EipicoDependencyNotReadyException(string message) : Exception(message);

internal readonly record struct EipicoRealtimeSyncResult(
    int HandheldCount,
    int SplitCount,
    int MachineLoadCount,
    int SignalCount,
    int ScaleSignalCount,
    int ScaleLiveMessageCount,
    int EnergyCount,
    int HarmonicCount)
{
    public int TotalCount => HandheldCount + SplitCount + MachineLoadCount +
                             SignalCount + ScaleSignalCount + ScaleLiveMessageCount +
                             EnergyCount + HarmonicCount;
}
