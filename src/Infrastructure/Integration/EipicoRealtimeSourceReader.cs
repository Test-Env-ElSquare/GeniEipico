using Microsoft.Data.SqlClient;

namespace Infrastructure.Integration;

internal sealed class EipicoRealtimeSourceReader(string connectionString)
{
    public Task<long> GetMaxHandheldIdAsync(CancellationToken cancellationToken)
        => ReadMaxIdAsync("[RealTime].[HandheldMatarial]", cancellationToken);

    public Task<long> GetMaxSplitIdAsync(CancellationToken cancellationToken)
        => ReadMaxIdAsync("[RealTime].[SplittedMatarial]", cancellationToken);

    public Task<long> GetMaxMachineLoadIdAsync(CancellationToken cancellationToken)
        => ReadMaxIdAsync("[RealTime].[MachineLoads]", cancellationToken);

    public Task<long> GetMaxSignalIdAsync(CancellationToken cancellationToken)
        => ReadMaxIdAsync("[RealTime].[Signals]", cancellationToken);

    public Task<long> GetMaxScaleSignalIdAsync(CancellationToken cancellationToken)
        => ReadMaxIdAsync("[RealTime].[ScaleSignals]", cancellationToken);

    public Task<long> GetMaxScaleLiveMessageIdAsync(CancellationToken cancellationToken)
        => ReadMaxIdAsync("[RealTime].[ScaleLiveMessages]", cancellationToken);

    public Task<long> GetMaxEnergyIdAsync(CancellationToken cancellationToken)
        => ReadMaxIdAsync("[RealTime].[Energies]", cancellationToken);

    public Task<long> GetMaxHarmonicIdAsync(CancellationToken cancellationToken)
        => ReadMaxIdAsync("[RealTime].[Harmonics]", cancellationToken);

    public Task<IReadOnlyList<EipicoSignalRecord>> ReadSignalsAfterAsync(
        long sourceId,
        int batchSize,
        CancellationToken cancellationToken)
        => ReadSignalsAsync(sourceId, batchSize, RealtimeReadMode.AfterSourceId, cancellationToken);

    public async Task<EipicoSignalRecord?> ReadSignalByIdAsync(
        long sourceId,
        CancellationToken cancellationToken)
        => (await ReadSignalsAsync(sourceId, 1, RealtimeReadMode.ExactSourceId, cancellationToken))
            .SingleOrDefault();

    public Task<IReadOnlyList<EipicoScaleSignalRecord>> ReadScaleSignalsAfterAsync(
        long sourceId,
        int batchSize,
        CancellationToken cancellationToken)
        => ReadScaleSignalsAsync(sourceId, batchSize, RealtimeReadMode.AfterSourceId, cancellationToken);

    public async Task<EipicoScaleSignalRecord?> ReadScaleSignalByIdAsync(
        long sourceId,
        CancellationToken cancellationToken)
        => (await ReadScaleSignalsAsync(sourceId, 1, RealtimeReadMode.ExactSourceId, cancellationToken))
            .SingleOrDefault();

    public Task<IReadOnlyList<EipicoScaleLiveMessageRecord>> ReadScaleLiveMessagesAfterAsync(
        long sourceId,
        int batchSize,
        CancellationToken cancellationToken)
        => ReadScaleLiveMessagesAsync(sourceId, batchSize, RealtimeReadMode.AfterSourceId, cancellationToken);

    public async Task<EipicoScaleLiveMessageRecord?> ReadScaleLiveMessageByIdAsync(
        long sourceId,
        CancellationToken cancellationToken)
        => (await ReadScaleLiveMessagesAsync(sourceId, 1, RealtimeReadMode.ExactSourceId, cancellationToken))
            .SingleOrDefault();

    public Task<IReadOnlyList<EipicoEnergyRecord>> ReadEnergiesAfterAsync(
        long sourceId,
        int batchSize,
        CancellationToken cancellationToken)
        => ReadEnergiesAsync(sourceId, batchSize, RealtimeReadMode.AfterSourceId, cancellationToken);

    public async Task<EipicoEnergyRecord?> ReadEnergyByIdAsync(
        long sourceId,
        CancellationToken cancellationToken)
        => (await ReadEnergiesAsync(sourceId, 1, RealtimeReadMode.ExactSourceId, cancellationToken))
            .SingleOrDefault();

    public Task<IReadOnlyList<EipicoHarmonicRecord>> ReadHarmonicsAfterAsync(
        long sourceId,
        int batchSize,
        CancellationToken cancellationToken)
        => ReadHarmonicsAsync(sourceId, batchSize, RealtimeReadMode.AfterSourceId, cancellationToken);

    public async Task<EipicoHarmonicRecord?> ReadHarmonicByIdAsync(
        long sourceId,
        CancellationToken cancellationToken)
        => (await ReadHarmonicsAsync(sourceId, 1, RealtimeReadMode.ExactSourceId, cancellationToken))
            .SingleOrDefault();

    public Task<IReadOnlyList<EipicoHandheldRecord>> ReadHandheldAfterAsync(
        long sourceId,
        int batchSize,
        CancellationToken cancellationToken)
        => ReadHandheldAsync(sourceId, batchSize, RealtimeReadMode.AfterSourceId, cancellationToken);

    public async Task<EipicoHandheldRecord?> ReadHandheldByIdAsync(
        long sourceId,
        CancellationToken cancellationToken)
        => (await ReadHandheldAsync(sourceId, 1, RealtimeReadMode.ExactSourceId, cancellationToken))
            .SingleOrDefault();

    public Task<IReadOnlyList<EipicoHandheldRecord>> ReadRecentHandheldAsync(
        int batchSize,
        CancellationToken cancellationToken)
        => ReadHandheldAsync(0, batchSize, RealtimeReadMode.Recent, cancellationToken);

    public Task<IReadOnlyList<EipicoMaterialSplitRecord>> ReadSplitsAfterAsync(
        long sourceId,
        int batchSize,
        CancellationToken cancellationToken)
        => ReadSplitsAsync(sourceId, batchSize, RealtimeReadMode.AfterSourceId, cancellationToken);

    public async Task<EipicoMaterialSplitRecord?> ReadSplitByIdAsync(
        long sourceId,
        CancellationToken cancellationToken)
        => (await ReadSplitsAsync(sourceId, 1, RealtimeReadMode.ExactSourceId, cancellationToken))
            .SingleOrDefault();

    public Task<IReadOnlyList<EipicoMaterialSplitRecord>> ReadRecentSplitsAsync(
        int batchSize,
        CancellationToken cancellationToken)
        => ReadSplitsAsync(0, batchSize, RealtimeReadMode.Recent, cancellationToken);

    public Task<IReadOnlyList<EipicoMachineLoadRecord>> ReadMachineLoadsAfterAsync(
        long sourceId,
        int batchSize,
        CancellationToken cancellationToken)
        => ReadMachineLoadsAsync(sourceId, batchSize, RealtimeReadMode.AfterSourceId, cancellationToken);

    public async Task<EipicoMachineLoadRecord?> ReadMachineLoadByIdAsync(
        long sourceId,
        CancellationToken cancellationToken)
        => (await ReadMachineLoadsAsync(sourceId, 1, RealtimeReadMode.ExactSourceId, cancellationToken))
            .SingleOrDefault();

    public Task<IReadOnlyList<EipicoMachineLoadRecord>> ReadRecentMachineLoadsAsync(
        int batchSize,
        CancellationToken cancellationToken)
        => ReadMachineLoadsAsync(0, batchSize, RealtimeReadMode.Recent, cancellationToken);

    private async Task<IReadOnlyList<EipicoSignalRecord>> ReadSignalsAsync(
        long sourceId,
        int batchSize,
        RealtimeReadMode mode,
        CancellationToken cancellationToken)
    {
        var query = $"""
            SELECT TOP (@BatchSize)
                s.Id,
                s.MachineId,
                s.MachineName,
                TRY_CONVERT(int, s.State),
                TRY_CONVERT(int, s.Speed),
                TRY_CONVERT(int, s.[Count]),
                TRY_CONVERT(decimal(18, 6), s.CountDiff),
                TRY_CONVERT(int, s.Qreject),
                TRY_CONVERT(decimal(18, 6), s.QrejectDiff),
                TRY_CONVERT(int, s.StateReason),
                TRY_CONVERT(int, s.SKU),
                TRY_CONVERT(int, s.Fault),
                s.[TimeStamp],
                s.ShiftStartTime,
                s.FactoryId,
                s.LineId,
                m.UID,
                m.Faunctionality,
                m.RatedSpeed,
                l.Name,
                f.Name
            FROM [RealTime].[Signals] AS s
            LEFT JOIN [Definitions].[Machines] AS m ON m.Id = s.MachineId
            LEFT JOIN [Definitions].[Lines] AS l ON l.Id = s.LineId
            LEFT JOIN [Definitions].[Factories] AS f ON f.Id = l.FactoryId
            {WhereClause(mode, "s.Id")}
            ORDER BY s.Id {OrderDirection(mode)};
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, query, sourceId, batchSize, mode);
        var records = new List<EipicoSignalRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new EipicoSignalRecord
            {
                Id = reader.GetInt32(0),
                SourceMachineId = GetNullableInt32(reader, 1),
                MachineName = GetNullableString(reader, 2),
                State = GetNullableInt32(reader, 3),
                Speed = GetNullableInt32(reader, 4),
                Count = GetNullableInt32(reader, 5),
                CountDiff = GetNullableDecimal(reader, 6),
                QReject = GetNullableInt32(reader, 7),
                QRejectDiff = GetNullableDecimal(reader, 8),
                StateReason = GetNullableInt32(reader, 9),
                Sku = GetNullableInt32(reader, 10),
                Fault = GetNullableInt32(reader, 11),
                Timestamp = reader.GetDateTime(12),
                ShiftStartTime = GetNullableDateTime(reader, 13),
                SourceFactoryId = GetNullableInt32(reader, 14),
                SourceLineId = GetNullableInt32(reader, 15),
                MachineUid = GetNullableString(reader, 16),
                MachineType = GetNullableString(reader, 17),
                RatedSpeed = GetNullableDecimal(reader, 18),
                LineCode = GetNullableString(reader, 19),
                SourceFactoryName = GetNullableString(reader, 20)
            });
        }

        return records.OrderBy(x => x.Id).ToArray();
    }

    private async Task<IReadOnlyList<EipicoScaleSignalRecord>> ReadScaleSignalsAsync(
        long sourceId,
        int batchSize,
        RealtimeReadMode mode,
        CancellationToken cancellationToken)
    {
        var query = $"""
            SELECT TOP (@BatchSize)
                s.Id,
                s.Name,
                TRY_CONVERT(decimal(18, 6), s.Weight),
                TRY_CONVERT(int, s.ScaleId),
                s.[TimeStamp],
                s.ShiftStartTime
            FROM [RealTime].[ScaleSignals] AS s
            {WhereClause(mode, "s.Id")}
            ORDER BY s.Id {OrderDirection(mode)};
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, query, sourceId, batchSize, mode);
        var records = new List<EipicoScaleSignalRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new EipicoScaleSignalRecord
            {
                Id = reader.GetInt32(0),
                Name = GetNullableString(reader, 1),
                Weight = GetNullableDecimal(reader, 2),
                SourceScaleId = GetNullableInt32(reader, 3),
                Timestamp = reader.GetDateTime(4),
                ShiftStartTime = GetNullableDateTime(reader, 5)
            });
        }

        return records.OrderBy(x => x.Id).ToArray();
    }

    private async Task<IReadOnlyList<EipicoScaleLiveMessageRecord>> ReadScaleLiveMessagesAsync(
        long sourceId,
        int batchSize,
        RealtimeReadMode mode,
        CancellationToken cancellationToken)
    {
        var query = $"""
            SELECT TOP (@BatchSize)
                m.Id,
                CONVERT(nvarchar(100), m.ScaleId),
                m.TimeSTamp,
                scaleInfo.Id
            FROM [RealTime].[ScaleLiveMessages] AS m
            OUTER APPLY
            (
                SELECT TOP (1) s.Id
                FROM [Definitions].[Scales] AS s
                WHERE LTRIM(RTRIM(s.Name)) = LTRIM(RTRIM(CONVERT(nvarchar(100), m.ScaleId)))
                ORDER BY s.Id
            ) AS scaleInfo
            {WhereClause(mode, "m.Id")}
            ORDER BY m.Id {OrderDirection(mode)};
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, query, sourceId, batchSize, mode);
        var records = new List<EipicoScaleLiveMessageRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new EipicoScaleLiveMessageRecord
            {
                Id = reader.GetInt32(0),
                ScaleReference = GetNullableString(reader, 1),
                Timestamp = reader.GetDateTime(2),
                SourceScaleId = GetNullableInt32(reader, 3)
            });
        }

        return records.OrderBy(x => x.Id).ToArray();
    }

    private async Task<IReadOnlyList<EipicoEnergyRecord>> ReadEnergiesAsync(
        long sourceId,
        int batchSize,
        RealtimeReadMode mode,
        CancellationToken cancellationToken)
    {
        var query = $"""
            SELECT TOP (@BatchSize)
                e.Id, e.SourceId,
                e.V1, e.V2, e.V3, e.I1, e.I2, e.I3,
                e.PF1, e.PF2, e.PF3,
                e.Papp1, e.Papp2, e.Papp3,
                e.Pact1, e.Pact2, e.Pact3,
                e.Preact1, e.Preact2, e.Preact3,
                e.Energy1, e.Energy2, e.Energy3,
                e.THDv1, e.THDv2, e.THDv3,
                e.THDi1, e.THDi2, e.THDi3,
                e.Fault, e.[TimeStamp]
            FROM [RealTime].[Energies] AS e
            {WhereClause(mode, "e.Id")}
            ORDER BY e.Id {OrderDirection(mode)};
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, query, sourceId, batchSize, mode);
        var records = new List<EipicoEnergyRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new EipicoEnergyRecord
            {
                Id = reader.GetInt32(0),
                SourceId = GetNullableString(reader, 1),
                VoltageL1 = GetNullableDecimal(reader, 2),
                VoltageL2 = GetNullableDecimal(reader, 3),
                VoltageL3 = GetNullableDecimal(reader, 4),
                CurrentL1 = GetNullableDecimal(reader, 5),
                CurrentL2 = GetNullableDecimal(reader, 6),
                CurrentL3 = GetNullableDecimal(reader, 7),
                PowerFactorL1 = GetNullableDecimal(reader, 8),
                PowerFactorL2 = GetNullableDecimal(reader, 9),
                PowerFactorL3 = GetNullableDecimal(reader, 10),
                ApparentPowerL1 = GetNullableDecimal(reader, 11),
                ApparentPowerL2 = GetNullableDecimal(reader, 12),
                ApparentPowerL3 = GetNullableDecimal(reader, 13),
                ActivePowerL1 = GetNullableDecimal(reader, 14),
                ActivePowerL2 = GetNullableDecimal(reader, 15),
                ActivePowerL3 = GetNullableDecimal(reader, 16),
                ReactivePowerL1 = GetNullableDecimal(reader, 17),
                ReactivePowerL2 = GetNullableDecimal(reader, 18),
                ReactivePowerL3 = GetNullableDecimal(reader, 19),
                EnergyL1 = GetNullableDecimal(reader, 20),
                EnergyL2 = GetNullableDecimal(reader, 21),
                EnergyL3 = GetNullableDecimal(reader, 22),
                VoltageThdL1 = GetNullableDecimal(reader, 23),
                VoltageThdL2 = GetNullableDecimal(reader, 24),
                VoltageThdL3 = GetNullableDecimal(reader, 25),
                CurrentThdL1 = GetNullableDecimal(reader, 26),
                CurrentThdL2 = GetNullableDecimal(reader, 27),
                CurrentThdL3 = GetNullableDecimal(reader, 28),
                Fault = GetNullableInt32(reader, 29),
                Timestamp = reader.GetDateTime(30)
            });
        }

        return records.OrderBy(x => x.Id).ToArray();
    }

    private async Task<IReadOnlyList<EipicoHarmonicRecord>> ReadHarmonicsAsync(
        long sourceId,
        int batchSize,
        RealtimeReadMode mode,
        CancellationToken cancellationToken)
    {
        var query = $"""
            SELECT TOP (@BatchSize)
                h.Id, h.SourceId,
                h.HarmonicI31, h.HarmonicI32, h.HarmonicI33,
                h.HarmonicV31, h.HarmonicV32, h.HarmonicV33,
                h.HarmonicI51, h.HarmonicI52, h.HarmonicI53,
                h.HarmonicV51, h.HarmonicV52, h.HarmonicV53,
                h.HarmonicI71, h.HarmonicI72, h.HarmonicI73,
                h.HarmonicV71, h.HarmonicV72, h.HarmonicV73,
                h.HarmonicI91, h.HarmonicI92, h.HarmonicI93,
                h.HarmonicV91, h.HarmonicV92, h.HarmonicV93,
                h.HarmonicI111, h.HarmonicI112, h.HarmonicI113,
                h.HarmonicV111, h.HarmonicV112, h.HarmonicV113,
                h.HarmonicI131, h.HarmonicI132, h.HarmonicI133,
                h.HarmonicV131, h.HarmonicV132, h.HarmonicV133,
                h.[TimeStamp]
            FROM [RealTime].[Harmonics] AS h
            {WhereClause(mode, "h.Id")}
            ORDER BY h.Id {OrderDirection(mode)};
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, query, sourceId, batchSize, mode);
        var records = new List<EipicoHarmonicRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new EipicoHarmonicRecord
            {
                Id = reader.GetInt32(0),
                SourceId = GetNullableString(reader, 1),
                HarmonicI31 = reader.GetDecimal(2),
                HarmonicI32 = reader.GetDecimal(3),
                HarmonicI33 = reader.GetDecimal(4),
                HarmonicV31 = reader.GetDecimal(5),
                HarmonicV32 = reader.GetDecimal(6),
                HarmonicV33 = reader.GetDecimal(7),
                HarmonicI51 = reader.GetDecimal(8),
                HarmonicI52 = reader.GetDecimal(9),
                HarmonicI53 = reader.GetDecimal(10),
                HarmonicV51 = reader.GetDecimal(11),
                HarmonicV52 = reader.GetDecimal(12),
                HarmonicV53 = reader.GetDecimal(13),
                HarmonicI71 = reader.GetDecimal(14),
                HarmonicI72 = reader.GetDecimal(15),
                HarmonicI73 = reader.GetDecimal(16),
                HarmonicV71 = reader.GetDecimal(17),
                HarmonicV72 = reader.GetDecimal(18),
                HarmonicV73 = reader.GetDecimal(19),
                HarmonicI91 = reader.GetDecimal(20),
                HarmonicI92 = reader.GetDecimal(21),
                HarmonicI93 = reader.GetDecimal(22),
                HarmonicV91 = reader.GetDecimal(23),
                HarmonicV92 = reader.GetDecimal(24),
                HarmonicV93 = reader.GetDecimal(25),
                HarmonicI111 = reader.GetDecimal(26),
                HarmonicI112 = reader.GetDecimal(27),
                HarmonicI113 = reader.GetDecimal(28),
                HarmonicV111 = reader.GetDecimal(29),
                HarmonicV112 = reader.GetDecimal(30),
                HarmonicV113 = reader.GetDecimal(31),
                HarmonicI131 = reader.GetDecimal(32),
                HarmonicI132 = reader.GetDecimal(33),
                HarmonicI133 = reader.GetDecimal(34),
                HarmonicV131 = reader.GetDecimal(35),
                HarmonicV132 = reader.GetDecimal(36),
                HarmonicV133 = reader.GetDecimal(37),
                Timestamp = reader.GetDateTime(38)
            });
        }

        return records.OrderBy(x => x.Id).ToArray();
    }

    private async Task<long> ReadMaxIdAsync(string tableName, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand($"SELECT COALESCE(MAX(Id), 0) FROM {tableName};", connection)
        {
            CommandTimeout = 30
        };
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
    }

    private async Task<IReadOnlyList<EipicoHandheldRecord>> ReadHandheldAsync(
        long sourceId,
        int batchSize,
        RealtimeReadMode mode,
        CancellationToken cancellationToken)
    {
        var query = $"""
            SELECT TOP (@BatchSize)
                h.Id,
                h.Name,
                h.[TimeStamp],
                h.ScaleId,
                h.UID,
                h.Weight,
                h.Dispatched,
                h.SAPOrderId,
                material.Id AS SAPOrderMaterialId,
                scaleInfo.Id AS SourceScaleId,
                scaleInfo.Name AS SourceScaleName,
                scaleInfo.Room AS SourceScaleRoom
            FROM [RealTime].[HandheldMatarial] AS h
            OUTER APPLY
            (
                SELECT TOP (1) m.Id
                FROM [RealTime].[SAPOrderMaterials] AS m
                WHERE m.SAPOrderId = h.SAPOrderId
                  AND m.UID = h.UID
                  AND m.IsDeleted = 0
                ORDER BY m.Id DESC
            ) AS material
            OUTER APPLY
            (
                SELECT TOP (1) s.Id, s.Name, s.Room
                FROM [Definitions].[Scales] AS s
                WHERE s.Name = h.ScaleId
                ORDER BY s.Id
            ) AS scaleInfo
            {WhereClause(mode, "h.Id")}
            ORDER BY h.Id {OrderDirection(mode)};
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, query, sourceId, batchSize, mode);

        var records = new List<EipicoHandheldRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new EipicoHandheldRecord
            {
                Id = reader.GetInt32(0),
                Name = GetNullableString(reader, 1),
                RecordedAt = reader.GetDateTime(2),
                ScaleReference = GetNullableString(reader, 3),
                MaterialUid = GetNullableString(reader, 4),
                Weight = reader.GetDecimal(5),
                Dispatched = reader.GetBoolean(6),
                SapOrderId = GetNullableInt32(reader, 7),
                SapOrderMaterialId = GetNullableInt32(reader, 8),
                SourceScaleId = GetNullableInt32(reader, 9),
                SourceScaleName = GetNullableString(reader, 10),
                SourceScaleRoom = GetNullableString(reader, 11)
            });
        }

        return records.OrderBy(x => x.Id).ToArray();
    }

    private async Task<IReadOnlyList<EipicoMaterialSplitRecord>> ReadSplitsAsync(
        long sourceId,
        int batchSize,
        RealtimeReadMode mode,
        CancellationToken cancellationToken)
    {
        var query = $"""
            SELECT TOP (@BatchSize)
                s.Id,
                s.SplittedUid,
                s.Weight,
                s.SplitDate,
                s.SAPOrderMaterialId
            FROM [RealTime].[SplittedMatarial] AS s
            {WhereClause(mode, "s.Id")}
            ORDER BY s.Id {OrderDirection(mode)};
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, query, sourceId, batchSize, mode);

        var records = new List<EipicoMaterialSplitRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new EipicoMaterialSplitRecord
            {
                Id = reader.GetInt32(0),
                SplitUid = GetNullableString(reader, 1),
                WeightKg = reader.GetDecimal(2),
                SplitAt = GetNullableDateTime(reader, 3),
                SapOrderMaterialId = GetNullableInt32(reader, 4)
            });
        }

        return records.OrderBy(x => x.Id).ToArray();
    }

    private async Task<IReadOnlyList<EipicoMachineLoadRecord>> ReadMachineLoadsAsync(
        long sourceId,
        int batchSize,
        RealtimeReadMode mode,
        CancellationToken cancellationToken)
    {
        var query = $"""
            SELECT TOP (@BatchSize)
                load.Id,
                load.MaterialUID,
                load.MachineId,
                load.Weight,
                load.[TimeStamp],
                load.BatchId,
                load.ShiftStartTime,
                load.ActivationDate,
                load.FinishingDate,
                load.IsPending,
                load.[Count],
                machine.Name,
                machine.UID,
                machine.Faunctionality,
                machine.RatedSpeed,
                scheduledLine.Name, scheduledFactory.Name,
                physicalLine.Name, physicalFactory.Name
            FROM [RealTime].[MachineLoads] AS load
            LEFT JOIN [Definitions].[Machines] AS machine
                ON machine.Id = load.MachineId
            LEFT JOIN [Definitions].[Lines] scheduledLine ON scheduledLine.Id = load.LineId
            LEFT JOIN [Definitions].[Factories] scheduledFactory ON scheduledFactory.Id = scheduledLine.FactoryId
            LEFT JOIN [Definitions].[Lines] physicalLine ON physicalLine.Id = machine.LineId
            LEFT JOIN [Definitions].[Factories] physicalFactory ON physicalFactory.Id = physicalLine.FactoryId
            {WhereClause(mode, "load.Id")}
            ORDER BY load.Id {OrderDirection(mode)};
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, query, sourceId, batchSize, mode);

        var records = new List<EipicoMachineLoadRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new EipicoMachineLoadRecord
            {
                Id = reader.GetInt32(0),
                MaterialUid = GetNullableString(reader, 1),
                SourceMachineId = GetNullableInt32(reader, 2),
                Weight = GetNullableDecimal(reader, 3),
                LoadedAt = reader.GetDateTime(4),
                BatchUid = GetNullableString(reader, 5),
                ShiftStartTime = reader.GetDateTime(6),
                ActivationDate = GetNullableDateTime(reader, 7),
                FinishingDate = GetNullableDateTime(reader, 8),
                IsPending = GetNullableBoolean(reader, 9),
                Count = GetNullableInt32(reader, 10),
                MachineName = GetNullableString(reader, 11),
                MachineUid = GetNullableString(reader, 12),
                MachineType = GetNullableString(reader, 13),
                RatedSpeed = GetNullableDecimal(reader, 14),
                ScheduledLineName = GetNullableString(reader, 15),
                ScheduledFactoryName = GetNullableString(reader, 16),
                PhysicalLineName = GetNullableString(reader, 17),
                PhysicalFactoryName = GetNullableString(reader, 18)
            });
        }

        return records.OrderBy(x => x.Id).ToArray();
    }

    private static SqlCommand CreateCommand(
        SqlConnection connection,
        string query,
        long sourceId,
        int batchSize,
        RealtimeReadMode mode)
    {
        var command = new SqlCommand(query, connection) { CommandTimeout = 30 };
        command.Parameters.AddWithValue("@BatchSize", Math.Clamp(batchSize, 1, 1000));
        if (mode != RealtimeReadMode.Recent)
            command.Parameters.AddWithValue("@SourceId", sourceId);
        return command;
    }

    private static string WhereClause(RealtimeReadMode mode, string idColumn)
        => mode switch
        {
            RealtimeReadMode.ExactSourceId => $"WHERE {idColumn} = @SourceId",
            RealtimeReadMode.AfterSourceId => $"WHERE {idColumn} > @SourceId",
            _ => string.Empty
        };

    private static string OrderDirection(RealtimeReadMode mode)
        => mode == RealtimeReadMode.Recent ? "DESC" : "ASC";

    private static string? GetNullableString(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static int? GetNullableInt32(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static decimal? GetNullableDecimal(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);

    private static DateTime? GetNullableDateTime(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);

    private static bool? GetNullableBoolean(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);

    private enum RealtimeReadMode
    {
        AfterSourceId,
        ExactSourceId,
        Recent
    }
}

internal interface IEipicoRealtimeRecord
{
    int Id { get; }
}

internal sealed class EipicoSignalRecord : IEipicoRealtimeRecord
{
    public int Id { get; init; }
    public int? SourceMachineId { get; init; }
    public string? MachineName { get; init; }
    public int? State { get; init; }
    public int? Speed { get; init; }
    public int? Count { get; init; }
    public decimal? CountDiff { get; init; }
    public int? QReject { get; init; }
    public decimal? QRejectDiff { get; init; }
    public int? StateReason { get; init; }
    public int? Sku { get; init; }
    public int? Fault { get; init; }
    public DateTime Timestamp { get; init; }
    public DateTime? ShiftStartTime { get; init; }
    public int? SourceFactoryId { get; init; }
    public int? SourceLineId { get; init; }
    public string? MachineUid { get; init; }
    public string? MachineType { get; init; }
    public decimal? RatedSpeed { get; init; }
    public string? LineCode { get; init; }
    public string? SourceFactoryName { get; init; }
}

internal sealed class EipicoScaleSignalRecord : IEipicoRealtimeRecord
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public decimal? Weight { get; init; }
    public int? SourceScaleId { get; init; }
    public DateTime Timestamp { get; init; }
    public DateTime? ShiftStartTime { get; init; }
}

internal sealed class EipicoScaleLiveMessageRecord : IEipicoRealtimeRecord
{
    public int Id { get; init; }
    public string? ScaleReference { get; init; }
    public DateTime Timestamp { get; init; }
    public int? SourceScaleId { get; init; }
}

internal sealed class EipicoEnergyRecord : IEipicoRealtimeRecord
{
    public int Id { get; init; }
    public string? SourceId { get; init; }
    public decimal? VoltageL1 { get; init; }
    public decimal? VoltageL2 { get; init; }
    public decimal? VoltageL3 { get; init; }
    public decimal? CurrentL1 { get; init; }
    public decimal? CurrentL2 { get; init; }
    public decimal? CurrentL3 { get; init; }
    public decimal? PowerFactorL1 { get; init; }
    public decimal? PowerFactorL2 { get; init; }
    public decimal? PowerFactorL3 { get; init; }
    public decimal? ApparentPowerL1 { get; init; }
    public decimal? ApparentPowerL2 { get; init; }
    public decimal? ApparentPowerL3 { get; init; }
    public decimal? ActivePowerL1 { get; init; }
    public decimal? ActivePowerL2 { get; init; }
    public decimal? ActivePowerL3 { get; init; }
    public decimal? ReactivePowerL1 { get; init; }
    public decimal? ReactivePowerL2 { get; init; }
    public decimal? ReactivePowerL3 { get; init; }
    public decimal? EnergyL1 { get; init; }
    public decimal? EnergyL2 { get; init; }
    public decimal? EnergyL3 { get; init; }
    public decimal? VoltageThdL1 { get; init; }
    public decimal? VoltageThdL2 { get; init; }
    public decimal? VoltageThdL3 { get; init; }
    public decimal? CurrentThdL1 { get; init; }
    public decimal? CurrentThdL2 { get; init; }
    public decimal? CurrentThdL3 { get; init; }
    public int? Fault { get; init; }
    public DateTime Timestamp { get; init; }
}

internal sealed class EipicoHarmonicRecord : IEipicoRealtimeRecord
{
    public int Id { get; init; }
    public string? SourceId { get; init; }
    public decimal HarmonicI31 { get; init; }
    public decimal HarmonicI32 { get; init; }
    public decimal HarmonicI33 { get; init; }
    public decimal HarmonicV31 { get; init; }
    public decimal HarmonicV32 { get; init; }
    public decimal HarmonicV33 { get; init; }
    public decimal HarmonicI51 { get; init; }
    public decimal HarmonicI52 { get; init; }
    public decimal HarmonicI53 { get; init; }
    public decimal HarmonicV51 { get; init; }
    public decimal HarmonicV52 { get; init; }
    public decimal HarmonicV53 { get; init; }
    public decimal HarmonicI71 { get; init; }
    public decimal HarmonicI72 { get; init; }
    public decimal HarmonicI73 { get; init; }
    public decimal HarmonicV71 { get; init; }
    public decimal HarmonicV72 { get; init; }
    public decimal HarmonicV73 { get; init; }
    public decimal HarmonicI91 { get; init; }
    public decimal HarmonicI92 { get; init; }
    public decimal HarmonicI93 { get; init; }
    public decimal HarmonicV91 { get; init; }
    public decimal HarmonicV92 { get; init; }
    public decimal HarmonicV93 { get; init; }
    public decimal HarmonicI111 { get; init; }
    public decimal HarmonicI112 { get; init; }
    public decimal HarmonicI113 { get; init; }
    public decimal HarmonicV111 { get; init; }
    public decimal HarmonicV112 { get; init; }
    public decimal HarmonicV113 { get; init; }
    public decimal HarmonicI131 { get; init; }
    public decimal HarmonicI132 { get; init; }
    public decimal HarmonicI133 { get; init; }
    public decimal HarmonicV131 { get; init; }
    public decimal HarmonicV132 { get; init; }
    public decimal HarmonicV133 { get; init; }
    public DateTime Timestamp { get; init; }
}

internal sealed class EipicoHandheldRecord : IEipicoRealtimeRecord
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public DateTime RecordedAt { get; init; }
    public string? ScaleReference { get; init; }
    public string? MaterialUid { get; init; }
    public decimal Weight { get; init; }
    public bool Dispatched { get; init; }
    public int? SapOrderId { get; init; }
    public int? SapOrderMaterialId { get; init; }
    public int? SourceScaleId { get; init; }
    public string? SourceScaleName { get; init; }
    public string? SourceScaleRoom { get; init; }
}

internal sealed class EipicoMaterialSplitRecord : IEipicoRealtimeRecord
{
    public int Id { get; init; }
    public string? SplitUid { get; init; }
    public decimal WeightKg { get; init; }
    public DateTime? SplitAt { get; init; }
    public int? SapOrderMaterialId { get; init; }
}

internal sealed class EipicoMachineLoadRecord : IEipicoRealtimeRecord
{
    public int Id { get; init; }
    public string? MaterialUid { get; init; }
    public int? SourceMachineId { get; init; }
    public decimal? Weight { get; init; }
    public DateTime LoadedAt { get; init; }
    public string? BatchUid { get; init; }
    public DateTime ShiftStartTime { get; init; }
    public DateTime? ActivationDate { get; init; }
    public DateTime? FinishingDate { get; init; }
    public bool? IsPending { get; init; }
    public int? Count { get; init; }
    public string? MachineName { get; init; }
    public string? MachineUid { get; init; }
    public string? MachineType { get; init; }
    public decimal? RatedSpeed { get; init; }
    public string? ScheduledLineName { get; init; }
    public string? ScheduledFactoryName { get; init; }
    public string? PhysicalLineName { get; init; }
    public string? PhysicalFactoryName { get; init; }
}
