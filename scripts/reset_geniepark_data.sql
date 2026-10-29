/*
    DANGER: This script permanently deletes all application and Identity data.
    It intentionally preserves dbo.__EFMigrationsHistory so EF Core still knows
    which migrations are applied.

    Expected database: GeniePark
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'GeniePark'
BEGIN
    THROW 51010, 'Safety check failed: select the GeniePark database before running this script.', 1;
END;

DECLARE @sql nvarchar(max);

BEGIN TRY
    BEGIN TRANSACTION;

    ---------------------------------------------------------------------------
    -- Disable foreign-key and check constraints on every application table.
    ---------------------------------------------------------------------------
    SELECT @sql = STRING_AGG(
        CAST(
            N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) +
            N' NOCHECK CONSTRAINT ALL;'
            AS nvarchar(max)),
        NCHAR(10))
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE t.is_ms_shipped = 0
      AND NOT (s.name = N'dbo' AND t.name = N'__EFMigrationsHistory');

    IF NULLIF(@sql, N'') IS NOT NULL
        EXEC sys.sp_executesql @sql;

    ---------------------------------------------------------------------------
    -- Delete all rows while preserving EF Core migration history.
    ---------------------------------------------------------------------------
    SELECT @sql = STRING_AGG(
        CAST(
            N'DELETE FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
            AS nvarchar(max)),
        NCHAR(10))
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE t.is_ms_shipped = 0
      AND NOT (s.name = N'dbo' AND t.name = N'__EFMigrationsHistory');

    IF NULLIF(@sql, N'') IS NOT NULL
        EXEC sys.sp_executesql @sql;

    ---------------------------------------------------------------------------
    -- Reset every identity column so the next inserted Id starts at 1.
    ---------------------------------------------------------------------------
    SELECT @sql = STRING_AGG(
        CAST(
            N'DBCC CHECKIDENT (N''' +
            REPLACE(QUOTENAME(s.name) + N'.' + QUOTENAME(t.name), N'''', N'''''') +
            N''', RESEED, 0) WITH NO_INFOMSGS;'
            AS nvarchar(max)),
        NCHAR(10))
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE t.is_ms_shipped = 0
      AND EXISTS
      (
          SELECT 1
          FROM sys.identity_columns ic
          WHERE ic.object_id = t.object_id
      )
      AND NOT (s.name = N'dbo' AND t.name = N'__EFMigrationsHistory');

    IF NULLIF(@sql, N'') IS NOT NULL
        EXEC sys.sp_executesql @sql;

    ---------------------------------------------------------------------------
    -- Re-enable constraints and validate the now-empty tables.
    ---------------------------------------------------------------------------
    SELECT @sql = STRING_AGG(
        CAST(
            N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) +
            N' WITH CHECK CHECK CONSTRAINT ALL;'
            AS nvarchar(max)),
        NCHAR(10))
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE t.is_ms_shipped = 0
      AND NOT (s.name = N'dbo' AND t.name = N'__EFMigrationsHistory');

    IF NULLIF(@sql, N'') IS NOT NULL
        EXEC sys.sp_executesql @sql;

    COMMIT TRANSACTION;

    SELECT
        N'Database cleared and all identity values reset.' AS [Status],
        DB_NAME() AS [DatabaseName];
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
