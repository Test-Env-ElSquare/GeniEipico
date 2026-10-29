/*
   Destructive reset for the test database only.
   It preserves the EF migration history/schema, removes application data,
   and reseeds every identity column so the next inserted row starts at 1.
   Stop IIS/the API before running this script.
*/
USE [GenieEipico_Test];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    /* The Eipico LineTransformers relation is many-to-many. */
    IF OBJECT_ID(N'[Org].[ProductionLineTransformers]', N'U') IS NULL
    BEGIN
        CREATE TABLE [Org].[ProductionLineTransformers]
        (
            [ProductionLineId] int NOT NULL,
            [TransformerId] int NOT NULL,
            CONSTRAINT [PK_ProductionLineTransformers]
                PRIMARY KEY ([ProductionLineId], [TransformerId]),
            CONSTRAINT [FK_ProductionLineTransformers_ProductionLines]
                FOREIGN KEY ([ProductionLineId])
                REFERENCES [Org].[ProductionLines] ([Id]),
            CONSTRAINT [FK_ProductionLineTransformers_Transformers]
                FOREIGN KEY ([TransformerId])
                REFERENCES [MasterData].[Transformers] ([Id])
        );

        CREATE INDEX [IX_ProductionLineTransformers_TransformerId]
            ON [Org].[ProductionLineTransformers] ([TransformerId]);
    END;

    /* The table above is the same schema as the pending EF migration. If the
       table had to be created here, mark that migration as applied so a later
       database-update command does not try to create it a second time. */
    IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1
           FROM [dbo].[__EFMigrationsHistory]
           WHERE [MigrationId] = N'20261028090000_AddProductionLineTransformerLinks'
       )
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20261028090000_AddProductionLineTransformerLinks', N'9.0.0');
    END;

    /* Keep the target line definition fields in sync with the Eipico schema.
       Each guard lets this reset run whether the migration was applied or not. */
    IF COL_LENGTH(N'Org.ProductionLines', N'Number') IS NULL
        ALTER TABLE [Org].[ProductionLines] ADD [Number] int NULL;
    IF COL_LENGTH(N'Org.ProductionLines', N'Area') IS NULL
        ALTER TABLE [Org].[ProductionLines] ADD [Area] nvarchar(100) NULL;
    IF COL_LENGTH(N'Org.ProductionLines', N'ViewName') IS NULL
        ALTER TABLE [Org].[ProductionLines] ADD [ViewName] nvarchar(100) NULL;
    IF COL_LENGTH(N'Org.ProductionLines', N'AliasName') IS NULL
        ALTER TABLE [Org].[ProductionLines] ADD [AliasName] nvarchar(200) NULL;

    IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1
           FROM [dbo].[__EFMigrationsHistory]
           WHERE [MigrationId] = N'20261028100000_AddEipicoLineDefinitionFields'
       )
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20261028100000_AddEipicoLineDefinitionFields', N'9.0.0');
    END;

    DECLARE @sql nvarchar(max) = N'';

    /* SQL Server cannot TRUNCATE tables referenced by foreign keys. */
    SELECT @sql +=
        N'ALTER TABLE ' + QUOTENAME(s.[name]) + N'.' + QUOTENAME(t.[name]) +
        N' NOCHECK CONSTRAINT ALL;' + NCHAR(13) + NCHAR(10)
    FROM sys.tables AS t
    INNER JOIN sys.schemas AS s ON s.[schema_id] = t.[schema_id]
    WHERE t.is_ms_shipped = 0
      AND NOT (s.[name] = N'dbo' AND t.[name] = N'__EFMigrationsHistory');
    EXECUTE (@sql);

    SET @sql = N'';
    SELECT @sql +=
        N'DELETE FROM ' + QUOTENAME(s.[name]) + N'.' + QUOTENAME(t.[name]) + N';' +
        NCHAR(13) + NCHAR(10)
    FROM sys.tables AS t
    INNER JOIN sys.schemas AS s ON s.[schema_id] = t.[schema_id]
    WHERE t.is_ms_shipped = 0
      AND NOT (s.[name] = N'dbo' AND t.[name] = N'__EFMigrationsHistory');
    EXECUTE (@sql);

    DECLARE identity_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT s.[name], t.[name]
        FROM sys.tables AS t
        INNER JOIN sys.schemas AS s ON s.[schema_id] = t.[schema_id]
        INNER JOIN sys.identity_columns AS ic
            ON ic.[object_id] = t.[object_id]
        WHERE t.is_ms_shipped = 0
          AND NOT (s.[name] = N'dbo' AND t.[name] = N'__EFMigrationsHistory');

    DECLARE @schema sysname;
    DECLARE @table sysname;
    DECLARE @checkIdentSql nvarchar(1000);
    OPEN identity_cursor;
    FETCH NEXT FROM identity_cursor INTO @schema, @table;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @checkIdentSql =
            N'DBCC CHECKIDENT (' + QUOTENAME(@schema) + N'.' +
            QUOTENAME(@table) + N', RESEED, 0) WITH NO_INFOMSGS;';
        EXECUTE (@checkIdentSql);
        FETCH NEXT FROM identity_cursor INTO @schema, @table;
    END;
    CLOSE identity_cursor;
    DEALLOCATE identity_cursor;

    SET @sql = N'';
    SELECT @sql +=
        N'ALTER TABLE ' + QUOTENAME(s.[name]) + N'.' + QUOTENAME(t.[name]) +
        N' WITH CHECK CHECK CONSTRAINT ALL;' + NCHAR(13) + NCHAR(10)
    FROM sys.tables AS t
    INNER JOIN sys.schemas AS s ON s.[schema_id] = t.[schema_id]
    WHERE t.is_ms_shipped = 0
      AND NOT (s.[name] = N'dbo' AND t.[name] = N'__EFMigrationsHistory');
    EXECUTE (@sql);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT [SchemaName] = s.[name], [TableName] = t.[name], [Rows] = SUM(p.[rows])
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.[schema_id] = t.[schema_id]
INNER JOIN sys.partitions AS p ON p.[object_id] = t.[object_id] AND p.index_id IN (0, 1)
WHERE t.is_ms_shipped = 0
GROUP BY s.[name], t.[name]
ORDER BY s.[name], t.[name];
