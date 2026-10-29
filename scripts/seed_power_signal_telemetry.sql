/*
    Creates demo transformer definitions and seeds /power/api/source readings.

    Source id mapping:
        Raw payload id      = MasterData.Transformers.TransformerName
        Telemetry DeviceId  = MasterData.Transformers.TransformerName

    This script is idempotent. Existing transformer definitions and telemetry
    readings with the same deterministic timestamps are not duplicated.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'[MasterData].[Transformers]', N'U') IS NULL
    THROW 50001, 'MasterData.Transformers does not exist. Apply AddTransformerMasterData first.', 1;

IF OBJECT_ID(N'[RealTime].[TelemetryRaw]', N'U') IS NULL
    THROW 50002, 'RealTime.TelemetryRaw does not exist.', 1;

IF OBJECT_ID(N'[RealTime].[TelemetryTags]', N'U') IS NULL
    THROW 50003, 'RealTime.TelemetryTags does not exist.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Definitions TABLE
    (
        [TransformerName] nvarchar(150) NOT NULL PRIMARY KEY,
        [FactoryCode] nvarchar(50) NOT NULL
    );

    INSERT INTO @Definitions ([TransformerName], [FactoryCode])
    VALUES
        (N'EP1_TR1', N'EGYPHARMA-F01'),
        (N'EP1_TR2', N'EGYPHARMA-F01'),
        (N'EP1_TR3', N'EGYPHARMA-F01'),
        (N'EP1_TR4', N'EGYPHARMA-F01'),
        (N'EP1_TR5', N'EGYPHARMA-F01'),
        (N'EP1_TR6', N'EGYPHARMA-F01'),
        (N'EP1_TR7', N'EGYPHARMA-F01'),
        (N'EP1_TR8', N'EGYPHARMA-F01'),
        (N'EP2_TR1', N'EGYPHARMA-F02'),
        (N'EP2_TR2', N'EGYPHARMA-F02'),
        (N'EP2_TR3', N'EGYPHARMA-F02'),
        (N'EP2_TR4', N'EGYPHARMA-F02'),
        (N'NF1_TR1', N'NILEFOOD-F01'),
        (N'NF1_TR2', N'NILEFOOD-F01'),
        (N'NF1_TR3', N'NILEFOOD-F01'),
        (N'NF1_TR4', N'NILEFOOD-F01'),
        (N'NF2_TR1', N'NILEFOOD-F02'),
        (N'NF2_TR2', N'NILEFOOD-F02'),
        (N'NF2_TR3', N'NILEFOOD-F02'),
        (N'NF2_TR4', N'NILEFOOD-F02');

    IF EXISTS
    (
        SELECT 1
        FROM @Definitions definition
        LEFT JOIN [Org].[Factories] factory
            ON factory.[Code] = definition.[FactoryCode]
           AND factory.[IsActive] = 1
           AND factory.[IsDeleted] = 0
        WHERE factory.[Id] IS NULL
    )
    BEGIN
        THROW 50004, 'One or more target factory codes were not found or are inactive.', 1;
    END;

    UPDATE transformer
    SET
        transformer.[FactoryId] = factory.[Id],
        transformer.[IsDeleted] = 0
    FROM [MasterData].[Transformers] transformer
    INNER JOIN @Definitions definition
        ON definition.[TransformerName] = transformer.[TransformerName]
    INNER JOIN [Org].[Factories] factory
        ON factory.[Code] = definition.[FactoryCode]
       AND factory.[IsActive] = 1
       AND factory.[IsDeleted] = 0;

    DECLARE @DefinitionsCreatedAt datetime2(7) = SYSUTCDATETIME();

    INSERT INTO [MasterData].[Transformers]
    (
        [TransformerName],
        [FactoryId],
        [CreatedAt],
        [IsDeleted]
    )
    SELECT
        definition.[TransformerName],
        factory.[Id],
        @DefinitionsCreatedAt,
        0
    FROM @Definitions definition
    INNER JOIN [Org].[Factories] factory
        ON factory.[Code] = definition.[FactoryCode]
       AND factory.[IsActive] = 1
       AND factory.[IsDeleted] = 0
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM [MasterData].[Transformers] existing
        WHERE existing.[TransformerName] = definition.[TransformerName]
    );

    DECLARE @TransformersInserted int = @@ROWCOUNT;

    DECLARE @PowerSignals TABLE
    (
        [TransformerName] nvarchar(150) NOT NULL,
        [RecordedAt] datetime2(7) NOT NULL,
        [V1] decimal(18,6) NOT NULL,
        [V2] decimal(18,6) NOT NULL,
        [V3] decimal(18,6) NOT NULL,
        [I1] decimal(18,6) NOT NULL,
        [I2] decimal(18,6) NOT NULL,
        [I3] decimal(18,6) NOT NULL,
        [Pf1] decimal(18,6) NOT NULL,
        [Pf2] decimal(18,6) NOT NULL,
        [Pf3] decimal(18,6) NOT NULL,
        [Pact1] decimal(18,6) NOT NULL,
        [Pact2] decimal(18,6) NOT NULL,
        [Pact3] decimal(18,6) NOT NULL,
        [Preact1] decimal(18,6) NOT NULL,
        [Preact2] decimal(18,6) NOT NULL,
        [Preact3] decimal(18,6) NOT NULL,
        [Papp1] decimal(18,6) NOT NULL,
        [Papp2] decimal(18,6) NOT NULL,
        [Papp3] decimal(18,6) NOT NULL,
        [Energy1] decimal(18,6) NOT NULL,
        [Energy2] decimal(18,6) NOT NULL,
        [Energy3] decimal(18,6) NOT NULL,
        [THDv1] decimal(18,6) NOT NULL,
        [THDv2] decimal(18,6) NOT NULL,
        [THDv3] decimal(18,6) NOT NULL,
        [THDi1] decimal(18,6) NOT NULL,
        [THDi2] decimal(18,6) NOT NULL,
        [THDi3] decimal(18,6) NOT NULL,
        [RawPayload] nvarchar(max) NOT NULL,
        PRIMARY KEY ([TransformerName], [RecordedAt])
    );

    ;WITH SampleNumbers AS
    (
        SELECT 1 AS [SampleNo]
        UNION ALL SELECT 2
        UNION ALL SELECT 3
    )
    INSERT INTO @PowerSignals
    (
        [TransformerName], [RecordedAt],
        [V1], [V2], [V3], [I1], [I2], [I3], [Pf1], [Pf2], [Pf3],
        [Pact1], [Pact2], [Pact3], [Preact1], [Preact2], [Preact3],
        [Papp1], [Papp2], [Papp3], [Energy1], [Energy2], [Energy3],
        [THDv1], [THDv2], [THDv3], [THDi1], [THDi2], [THDi3],
        [RawPayload]
    )
    SELECT
        transformer.[TransformerName],
        DATEADD(SECOND, sample.[SampleNo] - 1, transformer.[CreatedAt]),
        reading.[V1], reading.[V2], reading.[V3],
        reading.[I1], reading.[I2], reading.[I3],
        reading.[Pf1], reading.[Pf2], reading.[Pf3],
        reading.[Pact1], reading.[Pact2], reading.[Pact3],
        reading.[Preact1], reading.[Preact2], reading.[Preact3],
        reading.[Papp1], reading.[Papp2], reading.[Papp3],
        reading.[Energy1], reading.[Energy2], reading.[Energy3],
        reading.[THDv1], reading.[THDv2], reading.[THDv3],
        reading.[THDi1], reading.[THDi2], reading.[THDi3],
        CONCAT(
            N'id=', transformer.[TransformerName],
            N'&V1=', reading.[V1], N'&V2=', reading.[V2], N'&V3=', reading.[V3],
            N'&I1=', reading.[I1], N'&I2=', reading.[I2], N'&I3=', reading.[I3],
            N'&Pf1=', reading.[Pf1], N'&Pf2=', reading.[Pf2], N'&Pf3=', reading.[Pf3],
            N'&Pact1=', reading.[Pact1], N'&Pact2=', reading.[Pact2], N'&Pact3=', reading.[Pact3],
            N'&Preact1=', reading.[Preact1], N'&Preact2=', reading.[Preact2], N'&Preact3=', reading.[Preact3],
            N'&Papp1=', reading.[Papp1], N'&Papp2=', reading.[Papp2], N'&Papp3=', reading.[Papp3],
            N'&Energy1=', reading.[Energy1], N'&Energy2=', reading.[Energy2], N'&Energy3=', reading.[Energy3],
            N'&THDv1=', reading.[THDv1], N'&THDv2=', reading.[THDv2], N'&THDv3=', reading.[THDv3],
            N'&THDi1=', reading.[THDi1], N'&THDi2=', reading.[THDi2], N'&THDi3=', reading.[THDi3]
        )
    FROM [MasterData].[Transformers] transformer
    INNER JOIN @Definitions definition
        ON definition.[TransformerName] = transformer.[TransformerName]
    CROSS JOIN SampleNumbers sample
    CROSS APPLY
    (
        SELECT
            CONVERT(decimal(18,6), 230.13 + (transformer.[Id] % 5) * 0.11 + (sample.[SampleNo] - 2) * 0.04) AS [V1],
            CONVERT(decimal(18,6), 231.96 + (transformer.[Id] % 4) * 0.09 + (sample.[SampleNo] - 2) * 0.03) AS [V2],
            CONVERT(decimal(18,6), 228.52 + (transformer.[Id] % 6) * 0.08 + (sample.[SampleNo] - 2) * 0.05) AS [V3],
            CONVERT(decimal(18,6), 900.00 + transformer.[Id] * 7.25 + sample.[SampleNo] * 2.10) AS [I1],
            CONVERT(decimal(18,6), 880.00 + transformer.[Id] * 6.80 + sample.[SampleNo] * 1.90) AS [I2],
            CONVERT(decimal(18,6), 910.00 + transformer.[Id] * 7.10 + sample.[SampleNo] * 2.20) AS [I3],
            CONVERT(decimal(18,6), 0.94 + (transformer.[Id] % 5) * 0.01) AS [Pf1],
            CONVERT(decimal(18,6), 0.93 + (transformer.[Id] % 6) * 0.01) AS [Pf2],
            CONVERT(decimal(18,6), 0.92 + (transformer.[Id] % 7) * 0.01) AS [Pf3],
            CONVERT(decimal(18,6), 220.00 + transformer.[Id] * 2.40 + sample.[SampleNo] * 0.70) AS [Pact1],
            CONVERT(decimal(18,6), 218.00 + transformer.[Id] * 2.30 + sample.[SampleNo] * 0.65) AS [Pact2],
            CONVERT(decimal(18,6), 219.00 + transformer.[Id] * 2.35 + sample.[SampleNo] * 0.68) AS [Pact3],
            CONVERT(decimal(18,6), 35.00 + transformer.[Id] * 0.80 + sample.[SampleNo] * 0.20) AS [Preact1],
            CONVERT(decimal(18,6), 34.00 + transformer.[Id] * 0.75 + sample.[SampleNo] * 0.18) AS [Preact2],
            CONVERT(decimal(18,6), 36.00 + transformer.[Id] * 0.82 + sample.[SampleNo] * 0.22) AS [Preact3],
            CONVERT(decimal(18,6), 230.00 + transformer.[Id] * 2.50 + sample.[SampleNo] * 0.75) AS [Papp1],
            CONVERT(decimal(18,6), 228.00 + transformer.[Id] * 2.45 + sample.[SampleNo] * 0.72) AS [Papp2],
            CONVERT(decimal(18,6), 229.00 + transformer.[Id] * 2.48 + sample.[SampleNo] * 0.74) AS [Papp3],
            CONVERT(decimal(18,6), 4500000.000 + transformer.[Id] * 10000.000 + sample.[SampleNo] * 125.500) AS [Energy1],
            CONVERT(decimal(18,6), 4495000.000 + transformer.[Id] * 9950.000 + sample.[SampleNo] * 123.000) AS [Energy2],
            CONVERT(decimal(18,6), 4510000.000 + transformer.[Id] * 10050.000 + sample.[SampleNo] * 127.250) AS [Energy3],
            CONVERT(decimal(18,6), 4.00 + (transformer.[Id] % 6) * 0.10 + sample.[SampleNo] * 0.02) AS [THDv1],
            CONVERT(decimal(18,6), 4.10 + (transformer.[Id] % 5) * 0.09 + sample.[SampleNo] * 0.02) AS [THDv2],
            CONVERT(decimal(18,6), 3.90 + (transformer.[Id] % 7) * 0.08 + sample.[SampleNo] * 0.03) AS [THDv3],
            CONVERT(decimal(18,6), 4.80 + (transformer.[Id] % 6) * 0.20 + sample.[SampleNo] * 0.04) AS [THDi1],
            CONVERT(decimal(18,6), 3.60 + (transformer.[Id] % 5) * 0.18 + sample.[SampleNo] * 0.03) AS [THDi2],
            CONVERT(decimal(18,6), 5.50 + (transformer.[Id] % 7) * 0.16 + sample.[SampleNo] * 0.05) AS [THDi3]
    ) reading
    WHERE transformer.[IsDeleted] = 0;

    INSERT INTO [RealTime].[TelemetryRaw]
    (
        [SourcePath],
        [RawPayload],
        [ReceivedAt]
    )
    SELECT
        N'/power/api/source',
        signal.[RawPayload],
        signal.[RecordedAt]
    FROM @PowerSignals signal
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM [RealTime].[TelemetryRaw] existing
        WHERE existing.[SourcePath] = N'/power/api/source'
          AND existing.[ReceivedAt] = signal.[RecordedAt]
          AND existing.[RawPayload] = signal.[RawPayload]
    );

    DECLARE @RawRowsInserted int = @@ROWCOUNT;

    INSERT INTO [RealTime].[TelemetryTags]
    (
        [DeviceId],
        [TagName],
        [ValueNum],
        [ValueStr],
        [RecordedAt]
    )
    SELECT
        signal.[TransformerName],
        tag.[TagName],
        tag.[ValueNum],
        NULL,
        signal.[RecordedAt]
    FROM @PowerSignals signal
    CROSS APPLY
    (
        VALUES
            (N'V1', signal.[V1]), (N'V2', signal.[V2]), (N'V3', signal.[V3]),
            (N'I1', signal.[I1]), (N'I2', signal.[I2]), (N'I3', signal.[I3]),
            (N'Pf1', signal.[Pf1]), (N'Pf2', signal.[Pf2]), (N'Pf3', signal.[Pf3]),
            (N'Pact1', signal.[Pact1]), (N'Pact2', signal.[Pact2]), (N'Pact3', signal.[Pact3]),
            (N'Preact1', signal.[Preact1]), (N'Preact2', signal.[Preact2]), (N'Preact3', signal.[Preact3]),
            (N'Papp1', signal.[Papp1]), (N'Papp2', signal.[Papp2]), (N'Papp3', signal.[Papp3]),
            (N'Energy1', signal.[Energy1]), (N'Energy2', signal.[Energy2]), (N'Energy3', signal.[Energy3]),
            (N'THDv1', signal.[THDv1]), (N'THDv2', signal.[THDv2]), (N'THDv3', signal.[THDv3]),
            (N'THDi1', signal.[THDi1]), (N'THDi2', signal.[THDi2]), (N'THDi3', signal.[THDi3])
    ) tag ([TagName], [ValueNum])
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM [RealTime].[TelemetryTags] existing
        WHERE existing.[DeviceId] = signal.[TransformerName]
          AND existing.[TagName] = tag.[TagName]
          AND existing.[RecordedAt] = signal.[RecordedAt]
    );

    DECLARE @TagRowsInserted int = @@ROWCOUNT;

    COMMIT TRANSACTION;

    SELECT
        @TransformersInserted AS [TransformersInserted],
        (SELECT COUNT(*) FROM @Definitions) AS [TransformersSeeded],
        @RawRowsInserted AS [TelemetryRawInserted],
        @TagRowsInserted AS [TelemetryTagsInserted];

    SELECT
        transformer.[Id],
        transformer.[TransformerName],
        transformer.[FactoryId],
        factory.[Code] AS [FactoryCode],
        transformer.[CreatedAt]
    FROM [MasterData].[Transformers] transformer
    INNER JOIN @Definitions definition
        ON definition.[TransformerName] = transformer.[TransformerName]
    INNER JOIN [Org].[Factories] factory
        ON factory.[Id] = transformer.[FactoryId]
    WHERE transformer.[IsDeleted] = 0
    ORDER BY factory.[Code], transformer.[TransformerName];
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
