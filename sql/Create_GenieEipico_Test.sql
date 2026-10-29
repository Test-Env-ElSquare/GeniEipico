USE [master];
GO

IF DB_ID(N'GenieEipico_Test') IS NULL
BEGIN
    CREATE DATABASE [GenieEipico_Test];
    PRINT N'Created database GenieEipico_Test.';
END
ELSE
BEGIN
    PRINT N'Database GenieEipico_Test already exists; no action was taken.';
END;
GO
