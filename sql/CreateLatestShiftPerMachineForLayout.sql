USE [GenieEipico_Test];
GO
IF SCHEMA_ID(N'Calculated') IS NULL EXEC(N'CREATE SCHEMA [Calculated]');
GO
CREATE OR ALTER VIEW [Calculated].[VW_LatestShiftPerMachineForLayout]
AS
-- Eipico semantics: latest STORED shift per external machine, not current-shift filtering.
-- Production timestamps are UTC. The original GETDATE date rule uses Cairo local dates.
WITH LatestShift AS
(
    SELECT CustomerId, FactoryId, MachineId AS MachineName,
           MAX(ShiftStartTime) AS LatestShiftStartTime
    FROM RealTime.Production
    WHERE ShiftStartTime IS NOT NULL
    GROUP BY CustomerId, FactoryId, MachineId
)
SELECT ls.CustomerId, ls.FactoryId, sig.LineId, ls.MachineName,
       ls.LatestShiftStartTime AS ShiftStartTime,
       CASE WHEN CONVERT(date, ls.LatestShiftStartTime AT TIME ZONE 'UTC' AT TIME ZONE 'Egypt Standard Time')
                    = CONVERT(date, SYSDATETIMEOFFSET() AT TIME ZONE 'Egypt Standard Time')
            THEN tot.TotalCountDiff ELSE CONVERT(decimal(38,4), 0) END AS TotalCountDiff,
       sig.[TimeStamp] AS LatestTimeStamp,
       sig.Id AS SignalId, sig.State, sig.[Count], sig.Qreject, sig.Speed,
       sig.StateReason, sig.SKU, sig.CountDiff AS SignalCountDiff, sig.Fault, sig.QrejectDiff
FROM LatestShift ls
CROSS APPLY
(
    SELECT SUM(p.CountDiff) AS TotalCountDiff
    FROM RealTime.Production p
    WHERE p.CustomerId = ls.CustomerId AND p.FactoryId = ls.FactoryId
      AND p.MachineId = ls.MachineName AND p.ShiftStartTime = ls.LatestShiftStartTime
) tot
CROSS APPLY
(
    SELECT TOP (1) p.*
    FROM RealTime.Production p
    WHERE p.CustomerId = ls.CustomerId AND p.FactoryId = ls.FactoryId
      AND p.MachineId = ls.MachineName AND p.ShiftStartTime = ls.LatestShiftStartTime
    ORDER BY p.[TimeStamp] DESC, p.Id DESC
) sig;
GO
-- Optional read-only verification after creating the view:
SELECT FactoryId, COUNT(*) AS MachinesWithShiftData
FROM Calculated.VW_LatestShiftPerMachineForLayout
GROUP BY FactoryId;