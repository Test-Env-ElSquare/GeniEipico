/*
    Optional pre-migration cleanup for RefactorRealtimeBusinessTables.

    The migration drops and recreates these volatile tables, so this script is
    only useful when you want to clear their old rows explicitly before running
    Update-Database. It deliberately does not delete Scales or Silos because
    their definitions and identifiers are preserved and moved to RealTime.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'[Analytics].[EnergyReadings]', N'U') IS NOT NULL
       AND EXISTS
       (
           SELECT 1
           FROM [Analytics].[EnergyReadings]
           WHERE [TelemetryRawId] IS NOT NULL
       )
    BEGIN
        THROW 50001, 'EnergyReadings contains TelemetryRaw references. No referenced data was deleted.', 1;
    END;

    IF OBJECT_ID(N'[IoT].[TelemetryTags]', N'U') IS NOT NULL
        DELETE FROM [IoT].[TelemetryTags];

    IF OBJECT_ID(N'[IoT].[TelemetryRaw]', N'U') IS NOT NULL
        DELETE FROM [IoT].[TelemetryRaw];

    IF OBJECT_ID(N'[Scada].[MachineStates]', N'U') IS NOT NULL
        DELETE FROM [Scada].[MachineStates];

    COMMIT TRANSACTION;

    SELECT
        N'Real-time volatile data cleared. Scales and Silos were preserved.' AS [Result];
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
