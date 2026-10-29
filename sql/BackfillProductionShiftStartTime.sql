USE [GenieEipico_Test];
GO
-- Run after AddProductionShiftStartTime. Target Production timestamps and shifts are UTC.
-- Source Eipico timestamps and shifts are Cairo local time.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @minUtc datetime2 = (SELECT MIN([TimeStamp]) FROM RealTime.Production WHERE ShiftStartTime IS NULL);
DECLARE @maxUtc datetime2 = (SELECT MAX([TimeStamp]) FROM RealTime.Production WHERE ShiftStartTime IS NULL);
DECLARE @minLocal datetime2 = CAST(@minUtc AT TIME ZONE 'UTC' AT TIME ZONE 'Egypt Standard Time' AS datetime2);
DECLARE @maxLocal datetime2 = CAST(@maxUtc AT TIME ZONE 'UTC' AT TIME ZONE 'Egypt Standard Time' AS datetime2);

-- Match the original shift when the reading exists in Eipico; never join by numeric IDs across databases.
SELECT s.Id, s.MachineName, sl.Name AS LineName, sf.Name AS FactoryName,
    CAST(s.[TimeStamp] AT TIME ZONE 'Egypt Standard Time' AT TIME ZONE 'UTC' AS datetime2) AS TimestampUtc,
    CAST(s.ShiftStartTime AT TIME ZONE 'Egypt Standard Time' AT TIME ZONE 'UTC' AS datetime2) AS ShiftStartUtc
INTO #SourceShifts
FROM Eipico.RealTime.Signals s
JOIN Eipico.Definitions.Lines sl ON sl.Id = s.LineId
JOIN Eipico.Definitions.Factories sf ON sf.Id = s.FactoryId
WHERE s.[TimeStamp] BETWEEN @minLocal AND @maxLocal AND s.ShiftStartTime IS NOT NULL;
CREATE INDEX IX_SourceShifts ON #SourceShifts (MachineName, TimestampUtc);

UPDATE p SET ShiftStartTime = source.ShiftStartUtc
FROM RealTime.Production p
JOIN Org.ProductionLines l ON l.Id = p.LineId
JOIN Org.Factories f ON f.Id = p.FactoryId
JOIN Scada.Machines m ON m.LineId = l.Id AND (m.Name = p.MachineId OR m.Code = p.MachineId)
CROSS APPLY (
    SELECT TOP (1) s.ShiftStartUtc FROM #SourceShifts s
    WHERE s.MachineName = m.Name AND s.LineName = l.Name
      AND REPLACE(s.FactoryName, ' ', '') = REPLACE(f.Name, ' ', '')
      AND s.TimestampUtc = p.[TimeStamp]
    ORDER BY s.Id DESC
) source
WHERE p.ShiftStartTime IS NULL;
SELECT @@ROWCOUNT AS CopiedFromEipico;

-- Remaining local readings: reconstruct using the same 08:00 / 20:00 code rule.
UPDATE p SET ShiftStartTime = CAST(
    shiftValue.LocalStart AT TIME ZONE 'Egypt Standard Time' AT TIME ZONE 'UTC' AS datetime2)
FROM RealTime.Production p
CROSS APPLY (SELECT CAST(p.[TimeStamp] AT TIME ZONE 'UTC' AT TIME ZONE 'Egypt Standard Time' AS datetime2) AS LocalTime) localValue
CROSS APPLY (SELECT CASE
    WHEN DATEPART(HOUR, localValue.LocalTime) < 8 THEN DATEADD(HOUR, 20, DATEADD(DAY, -1, CAST(CAST(localValue.LocalTime AS date) AS datetime2)))
    WHEN DATEPART(HOUR, localValue.LocalTime) < 20 THEN DATEADD(HOUR, 8, CAST(CAST(localValue.LocalTime AS date) AS datetime2))
    ELSE DATEADD(HOUR, 20, CAST(CAST(localValue.LocalTime AS date) AS datetime2)) END AS LocalStart) shiftValue
WHERE p.ShiftStartTime IS NULL;
SELECT @@ROWCOUNT AS ReconstructedFromTimestamp;

SELECT COUNT_BIG(*) AS TotalReadings,
    SUM(CASE WHEN ShiftStartTime IS NULL THEN CONVERT(bigint,1) ELSE 0 END) AS MissingShiftStart
FROM RealTime.Production;
DROP TABLE #SourceShifts;
COMMIT TRANSACTION;