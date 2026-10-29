/*
    Seeds /mes/signal readings into the new RealTime telemetry tables.

    Machine names are made unique using:
        {MachineName}_{FactoryId}_{LineId}

    The resulting Machine.Name is used verbatim as TelemetryTags.DeviceId.
    Three readings are inserted per active machine so later projection logic
    can test current/previous Count and calculate CountDiff.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'[RealTime].[TelemetryRaw]', N'U') IS NULL
    THROW 50001, 'RealTime.TelemetryRaw does not exist. Apply RefactorRealtimeBusinessTables first.', 1;

IF OBJECT_ID(N'[RealTime].[TelemetryTags]', N'U') IS NULL
    THROW 50002, 'RealTime.TelemetryTags does not exist. Apply RefactorRealtimeBusinessTables first.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE machine
    SET
        machine.[Name] = CONCAT(RTRIM(machine.[Name]), N'_', line.[FactoryId], N'_', machine.[LineId]),
        machine.[UpdatedAt] = SYSUTCDATETIME()
    FROM [Scada].[Machines] machine
    INNER JOIN [Org].[ProductionLines] line
        ON line.[Id] = machine.[LineId]
    CROSS APPLY
    (
        SELECT CONCAT(N'_', line.[FactoryId], N'_', machine.[LineId]) AS [ExpectedSuffix]
    ) suffix
    WHERE machine.[IsDeleted] = 0
      AND RIGHT(RTRIM(machine.[Name]), LEN(suffix.[ExpectedSuffix])) <> suffix.[ExpectedSuffix];

    IF EXISTS
    (
        SELECT machine.[Name]
        FROM [Scada].[Machines] machine
        WHERE machine.[IsDeleted] = 0
        GROUP BY machine.[Name]
        HAVING COUNT_BIG(*) > 1
    )
    BEGIN
        THROW 50003, 'Machine names are still duplicated after adding FactoryId and LineId.', 1;
    END;

    DECLARE @SeedAt datetime2(7) = SYSUTCDATETIME();

    DECLARE @Signals TABLE
    (
        [MachineRecordId] int NOT NULL,
        [DeviceId] nvarchar(150) NOT NULL,
        [RecordedAt] datetime2(7) NOT NULL,
        [State] int NOT NULL,
        [Fault] int NOT NULL,
        [Speed] int NOT NULL,
        [Count] int NOT NULL,
        [Qreject] int NOT NULL,
        [StateType] int NOT NULL,
        [StateReason] int NOT NULL,
        [Hours] int NOT NULL,
        [SKU] int NOT NULL,
        [RawPayload] nvarchar(max) NOT NULL
    );

    ;WITH SampleNumbers AS
    (
        SELECT 1 AS [SampleNo]
        UNION ALL SELECT 2
        UNION ALL SELECT 3
    )
    INSERT INTO @Signals
    (
        [MachineRecordId],
        [DeviceId],
        [RecordedAt],
        [State],
        [Fault],
        [Speed],
        [Count],
        [Qreject],
        [StateType],
        [StateReason],
        [Hours],
        [SKU],
        [RawPayload]
    )
    SELECT
        machine.[Id],
        machine.[Name],
        DATEADD(SECOND, sample.[SampleNo] - 3, @SeedAt),
        calculated.[State],
        calculated.[Fault],
        calculated.[Speed],
        calculated.[Count],
        calculated.[Qreject],
        -1,
        calculated.[StateReason],
        -1,
        1,
        CONCAT(
            N'&ID=', REPLACE(machine.[Name], N' ', N'+'),
            N'&State=', calculated.[State],
            N'&Fault=', calculated.[Fault],
            N'&Speed=', calculated.[Speed],
            N'&Count=', calculated.[Count],
            N'&Qreject=', calculated.[Qreject],
            N'&StateType=-1',
            N'&StateReason=', calculated.[StateReason],
            N'&Hours=-1',
            N'&SKU=1'
        )
    FROM [Scada].[Machines] machine
    CROSS JOIN SampleNumbers sample
    CROSS APPLY
    (
        SELECT
            CASE
                WHEN sample.[SampleNo] = 1 THEN 0
                WHEN sample.[SampleNo] = 3 AND machine.[Id] % 7 = 0 THEN 0
                ELSE 1
            END AS [State],
            CASE
                WHEN sample.[SampleNo] = 3 AND machine.[Id] % 7 = 0 THEN 1
                ELSE 0
            END AS [Fault],
            CASE
                WHEN sample.[SampleNo] = 1 THEN 0
                WHEN sample.[SampleNo] = 3 AND machine.[Id] % 7 = 0 THEN 0
                ELSE CONVERT(int, ROUND(COALESCE(machine.[RatedSpeedRpm], 1000) * (0.65 + (machine.[Id] % 20) / 100.0), 0))
            END AS [Speed],
            10000 + (machine.[Id] * 100) +
                CASE
                    WHEN sample.[SampleNo] = 1 THEN 0
                    WHEN sample.[SampleNo] = 2 THEN 25 + (machine.[Id] % 10)
                    WHEN machine.[Id] % 7 = 0 THEN 25 + (machine.[Id] % 10)
                    ELSE 55 + (machine.[Id] % 15)
                END AS [Count],
            CASE
                WHEN sample.[SampleNo] = 1 THEN 0
                ELSE (machine.[Id] + sample.[SampleNo]) % 4
            END AS [Qreject],
            CASE
                WHEN sample.[SampleNo] = 3 AND machine.[Id] % 7 = 0 THEN 5
                ELSE -1
            END AS [StateReason]
    ) calculated
    WHERE machine.[IsActive] = 1
      AND machine.[IsDeleted] = 0;

    INSERT INTO [RealTime].[TelemetryRaw]
    (
        [SourcePath],
        [RawPayload],
        [ReceivedAt]
    )
    SELECT
        N'/mes/signal',
        signal.[RawPayload],
        signal.[RecordedAt]
    FROM @Signals signal;

    INSERT INTO [RealTime].[TelemetryTags]
    (
        [DeviceId],
        [TagName],
        [ValueNum],
        [ValueStr],
        [RecordedAt]
    )
    SELECT
        signal.[DeviceId],
        tag.[TagName],
        tag.[ValueNum],
        NULL,
        signal.[RecordedAt]
    FROM @Signals signal
    CROSS APPLY
    (
        VALUES
            (N'State',       CONVERT(decimal(18,6), signal.[State])),
            (N'Fault',       CONVERT(decimal(18,6), signal.[Fault])),
            (N'Speed',       CONVERT(decimal(18,6), signal.[Speed])),
            (N'Count',       CONVERT(decimal(18,6), signal.[Count])),
            (N'Qreject',     CONVERT(decimal(18,6), signal.[Qreject])),
            (N'StateType',   CONVERT(decimal(18,6), signal.[StateType])),
            (N'StateReason', CONVERT(decimal(18,6), signal.[StateReason])),
            (N'Hours',       CONVERT(decimal(18,6), signal.[Hours])),
            (N'SKU',         CONVERT(decimal(18,6), signal.[SKU]))
    ) tag ([TagName], [ValueNum]);

    DECLARE @RawRowsInserted int = (SELECT COUNT(*) FROM @Signals);
    DECLARE @TagRowsInserted int = @RawRowsInserted * 9;

    COMMIT TRANSACTION;

    SELECT
        @RawRowsInserted AS [TelemetryRawInserted],
        @TagRowsInserted AS [TelemetryTagsInserted],
        @RawRowsInserted / 3 AS [MachinesSeeded],
        @SeedAt AS [SeededAtUtc];

    SELECT
        machine.[Id] AS [MachineId],
        line.[FactoryId],
        machine.[LineId],
        machine.[Name]
    FROM [Scada].[Machines] machine
    INNER JOIN [Org].[ProductionLines] line
        ON line.[Id] = machine.[LineId]
    WHERE machine.[IsActive] = 1
      AND machine.[IsDeleted] = 0
    ORDER BY line.[FactoryId], machine.[LineId], machine.[Id];
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
