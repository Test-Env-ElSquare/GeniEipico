/*
    One-time test-data seed for Analytics.EnergyReadings.

    This is intentionally not an application/runtime projection. It copies
    the complete power samples that already exist in RealTime.TelemetryTags
    so the new business table can be tested. Running it again is safe because
    (TransformerId, Timestamp) is treated as the natural reading key.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'[Analytics].[EnergyReadings]', N'U') IS NULL
    THROW 50001, 'Analytics.EnergyReadings does not exist.', 1;

IF OBJECT_ID(N'[RealTime].[TelemetryTags]', N'U') IS NULL
    THROW 50002, 'RealTime.TelemetryTags does not exist.', 1;

IF OBJECT_ID(N'[MasterData].[Transformers]', N'U') IS NULL
    THROW 50003, 'MasterData.Transformers does not exist.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    ;WITH PowerSignals AS
    (
        SELECT
            transformer.[Id] AS [TransformerId],
            factory.[CustomerId],
            transformer.[FactoryId],
            tag.[RecordedAt] AS [Timestamp],
            TRY_CONVERT(smallint, MAX(CASE WHEN tag.[TagName] = N'Quality' THEN tag.[ValueNum] END)) AS [Quality],
            MAX(CASE WHEN tag.[TagName] = N'V1' THEN tag.[ValueNum] END) AS [VoltageL1],
            MAX(CASE WHEN tag.[TagName] = N'V2' THEN tag.[ValueNum] END) AS [VoltageL2],
            MAX(CASE WHEN tag.[TagName] = N'V3' THEN tag.[ValueNum] END) AS [VoltageL3],
            MAX(CASE WHEN tag.[TagName] = N'I1' THEN tag.[ValueNum] END) AS [CurrentL1],
            MAX(CASE WHEN tag.[TagName] = N'I2' THEN tag.[ValueNum] END) AS [CurrentL2],
            MAX(CASE WHEN tag.[TagName] = N'I3' THEN tag.[ValueNum] END) AS [CurrentL3],
            MAX(CASE WHEN tag.[TagName] = N'Pf1' THEN tag.[ValueNum] END) AS [PowerFactorL1],
            MAX(CASE WHEN tag.[TagName] = N'Pf2' THEN tag.[ValueNum] END) AS [PowerFactorL2],
            MAX(CASE WHEN tag.[TagName] = N'Pf3' THEN tag.[ValueNum] END) AS [PowerFactorL3],
            MAX(CASE WHEN tag.[TagName] = N'Pact1' THEN tag.[ValueNum] END) AS [ActivePowerL1],
            MAX(CASE WHEN tag.[TagName] = N'Pact2' THEN tag.[ValueNum] END) AS [ActivePowerL2],
            MAX(CASE WHEN tag.[TagName] = N'Pact3' THEN tag.[ValueNum] END) AS [ActivePowerL3],
            MAX(CASE WHEN tag.[TagName] = N'Preact1' THEN tag.[ValueNum] END) AS [ReactivePowerL1],
            MAX(CASE WHEN tag.[TagName] = N'Preact2' THEN tag.[ValueNum] END) AS [ReactivePowerL2],
            MAX(CASE WHEN tag.[TagName] = N'Preact3' THEN tag.[ValueNum] END) AS [ReactivePowerL3],
            MAX(CASE WHEN tag.[TagName] = N'Papp1' THEN tag.[ValueNum] END) AS [ApparentPowerL1],
            MAX(CASE WHEN tag.[TagName] = N'Papp2' THEN tag.[ValueNum] END) AS [ApparentPowerL2],
            MAX(CASE WHEN tag.[TagName] = N'Papp3' THEN tag.[ValueNum] END) AS [ApparentPowerL3],
            MAX(CASE WHEN tag.[TagName] = N'Energy1' THEN tag.[ValueNum] END) AS [EnergyL1],
            MAX(CASE WHEN tag.[TagName] = N'Energy2' THEN tag.[ValueNum] END) AS [EnergyL2],
            MAX(CASE WHEN tag.[TagName] = N'Energy3' THEN tag.[ValueNum] END) AS [EnergyL3],
            MAX(CASE WHEN tag.[TagName] = N'THDv1' THEN tag.[ValueNum] END) AS [VoltageThdL1],
            MAX(CASE WHEN tag.[TagName] = N'THDv2' THEN tag.[ValueNum] END) AS [VoltageThdL2],
            MAX(CASE WHEN tag.[TagName] = N'THDv3' THEN tag.[ValueNum] END) AS [VoltageThdL3],
            MAX(CASE WHEN tag.[TagName] = N'THDi1' THEN tag.[ValueNum] END) AS [CurrentThdL1],
            MAX(CASE WHEN tag.[TagName] = N'THDi2' THEN tag.[ValueNum] END) AS [CurrentThdL2],
            MAX(CASE WHEN tag.[TagName] = N'THDi3' THEN tag.[ValueNum] END) AS [CurrentThdL3]
        FROM [RealTime].[TelemetryTags] tag
        INNER JOIN [MasterData].[Transformers] transformer
            ON transformer.[TransformerName] = tag.[DeviceId]
           AND transformer.[IsDeleted] = 0
        INNER JOIN [Org].[Factories] factory
            ON factory.[Id] = transformer.[FactoryId]
           AND factory.[IsActive] = 1
           AND factory.[IsDeleted] = 0
        WHERE tag.[TagName] IN
        (
            N'V1', N'V2', N'V3',
            N'I1', N'I2', N'I3',
            N'Pf1', N'Pf2', N'Pf3',
            N'Pact1', N'Pact2', N'Pact3',
            N'Preact1', N'Preact2', N'Preact3',
            N'Papp1', N'Papp2', N'Papp3',
            N'Energy1', N'Energy2', N'Energy3',
            N'THDv1', N'THDv2', N'THDv3',
            N'THDi1', N'THDi2', N'THDi3',
            N'Quality'
        )
        GROUP BY
            transformer.[Id],
            factory.[CustomerId],
            transformer.[FactoryId],
            tag.[RecordedAt]
        HAVING COUNT(DISTINCT CASE
            WHEN tag.[TagName] <> N'Quality' AND tag.[ValueNum] IS NOT NULL
            THEN tag.[TagName]
        END) = 27
    )
    INSERT INTO [Analytics].[EnergyReadings]
    (
        [CustomerId], [FactoryId], [LineId], [TransformerId], [Quality],
        [VoltageL1], [VoltageL2], [VoltageL3],
        [CurrentL1], [CurrentL2], [CurrentL3],
        [PowerFactorL1], [PowerFactorL2], [PowerFactorL3],
        [ActivePowerL1], [ActivePowerL2], [ActivePowerL3],
        [ReactivePowerL1], [ReactivePowerL2], [ReactivePowerL3],
        [ApparentPowerL1], [ApparentPowerL2], [ApparentPowerL3],
        [EnergyL1], [EnergyL2], [EnergyL3],
        [VoltageThdL1], [VoltageThdL2], [VoltageThdL3],
        [CurrentThdL1], [CurrentThdL2], [CurrentThdL3],
        [Timestamp]
    )
    SELECT
        signal.[CustomerId], signal.[FactoryId], NULL, signal.[TransformerId], signal.[Quality],
        signal.[VoltageL1], signal.[VoltageL2], signal.[VoltageL3],
        signal.[CurrentL1], signal.[CurrentL2], signal.[CurrentL3],
        signal.[PowerFactorL1], signal.[PowerFactorL2], signal.[PowerFactorL3],
        signal.[ActivePowerL1], signal.[ActivePowerL2], signal.[ActivePowerL3],
        signal.[ReactivePowerL1], signal.[ReactivePowerL2], signal.[ReactivePowerL3],
        signal.[ApparentPowerL1], signal.[ApparentPowerL2], signal.[ApparentPowerL3],
        signal.[EnergyL1], signal.[EnergyL2], signal.[EnergyL3],
        signal.[VoltageThdL1], signal.[VoltageThdL2], signal.[VoltageThdL3],
        signal.[CurrentThdL1], signal.[CurrentThdL2], signal.[CurrentThdL3],
        signal.[Timestamp]
    FROM PowerSignals signal
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM [Analytics].[EnergyReadings] existing
        WHERE existing.[TransformerId] = signal.[TransformerId]
          AND existing.[Timestamp] = signal.[Timestamp]
    );

    DECLARE @InsertedRows int = @@ROWCOUNT;

    COMMIT TRANSACTION;

    SELECT
        @InsertedRows AS [InsertedRows],
        COUNT_BIG(*) AS [TotalEnergyReadings]
    FROM [Analytics].[EnergyReadings];
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
