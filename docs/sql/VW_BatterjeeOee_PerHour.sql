USE [GeniePark];
GO
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- Read model only. Does not insert snapshots or modify the existing shift view.
-- All exposed times are LOCAL UTC+3, just like VW_BatterjeeOee.
-- Snapshot writes must save ALL machine/hour rows for a factory shift atomically.
-- Any saved row marks its factory shift as stored; no partially saved shifts.
CREATE OR ALTER VIEW [Analytics].[VW_BatterjeeOee_PerHour]
AS
WITH Runtime AS
(
    SELECT DATEADD(HOUR, 3, SYSUTCDATETIME()) AS LocalNow
),
LastFactoryShift AS
(
    -- ShiftDate stores an HOUR start. Recover its parent shift start (including
    -- after-midnight hours) before determining the latest stored factory shift.
    SELECT F.Id AS FactoryId,
           MAX(DATEADD(DAY,
               DATEDIFF(DAY, CONVERT(datetime2, '19000101', 112), OS.ShiftDate)
               - CASE WHEN SH.StartTime >= SH.EndTime
                       AND CAST(OS.ShiftDate AS time) < SH.EndTime THEN 1 ELSE 0 END,
               CAST(SH.StartTime AS datetime2))) AS LastShiftStartTime
    FROM [Analytics].[OeeSnapshotsPerHour] OS
    INNER JOIN [Scada].[Machines] M ON M.Id = OS.MachineId
    INNER JOIN [Org].[ProductionLines] L ON L.Id = M.LineId
    INNER JOIN [Org].[Factories] F ON F.Id = L.FactoryId AND F.CustomerId = 3
    INNER JOIN [Planning].[Shifts] SH ON SH.Id = OS.ShiftId AND SH.FactoryId = F.Id
    WHERE (OS.CustomerId IS NULL OR OS.CustomerId = F.CustomerId)
      AND (OS.FactoryId IS NULL OR OS.FactoryId = F.Id)
      AND (OS.LineId IS NULL OR OS.LineId = L.Id)
     
    GROUP BY F.Id
),
ProductionSignals AS
(
    SELECT P.Id, P.CustomerId, P.FactoryId, P.LineId, P.MachineId,
           P.[State], P.Speed, P.CountDiff, P.Qreject,
           DATEADD(HOUR, 3, P.TimeStamp) AS LocalTimeStamp
    FROM [RealTime].[Production] P
    WHERE P.CustomerId = 3
),
AssignedShiftBase AS
(
    SELECT S.*, M.Id AS NumericMachineId, M.Name AS MachineName,
           SH.Id AS ShiftId, SH.Name AS ShiftName, SH.StartTime, SH.EndTime,
           DATEADD(DAY,
               DATEDIFF(DAY, CONVERT(datetime2, '19000101', 112), S.LocalTimeStamp)
               - CASE WHEN SH.StartTime >= SH.EndTime
                       AND CAST(S.LocalTimeStamp AS time) < SH.EndTime THEN 1 ELSE 0 END,
               CAST(SH.StartTime AS datetime2)) AS ShiftStartTime,
           DATEADD(HOUR, DATEDIFF(HOUR, CONVERT(datetime2, '19000101', 112), S.LocalTimeStamp),
               CONVERT(datetime2, '19000101', 112)) AS HourStartTime
    FROM ProductionSignals S
    INNER JOIN [Org].[Factories] F ON F.Id = S.FactoryId AND F.CustomerId = S.CustomerId
        AND F.CustomerId = 3 AND F.IsActive = 1 AND F.IsDeleted = 0
    INNER JOIN [Org].[ProductionLines] L ON L.Id = S.LineId AND L.FactoryId = F.Id
        AND L.IsActive = 1 AND L.IsDeleted = 0
    INNER JOIN [Scada].[Machines] M ON M.LineId = L.Id
        AND (M.Name = S.MachineId OR M.Code = S.MachineId)
        AND M.IsActive = 1 AND M.IsDeleted = 0
    INNER JOIN [Planning].[Shifts] SH ON SH.FactoryId = F.Id
        AND SH.IsActive = 1 AND SH.IsDeleted = 0
        AND ((SH.StartTime < SH.EndTime AND CAST(S.LocalTimeStamp AS time) >= SH.StartTime
                AND CAST(S.LocalTimeStamp AS time) < SH.EndTime)
          OR (SH.StartTime >= SH.EndTime AND (CAST(S.LocalTimeStamp AS time) >= SH.StartTime
                OR CAST(S.LocalTimeStamp AS time) < SH.EndTime)))
),
AssignedShift AS
(
    SELECT S.*,
           DATEADD(SECOND, CASE WHEN S.StartTime < S.EndTime
               THEN DATEDIFF(SECOND, S.StartTime, S.EndTime)
               ELSE 86400 + DATEDIFF(SECOND, S.StartTime, S.EndTime) END,
               S.ShiftStartTime) AS ShiftEndTime
    FROM AssignedShiftBase S
),
LevelZero AS
(
    SELECT S.NumericMachineId AS MachineId, S.MachineName, S.CustomerId, S.FactoryId, S.LineId,
           S.ShiftId, S.ShiftName, S.ShiftStartTime, S.ShiftEndTime, S.HourStartTime,
           SUM(ISNULL(S.CountDiff, 0)) AS [Count],
           SUM(CAST(ISNULL(S.Qreject, 0) AS decimal(38,4))) AS Qreject,
           CAST(SUM(CAST(S.[State] AS decimal(38,6))) / NULLIF(COUNT_BIG(S.[State]), 0)
               AS decimal(7,4)) AS Availability,
           CAST(AVG(CASE WHEN S.Speed < 0 THEN NULL ELSE CAST(S.Speed AS decimal(18,6)) END)
               AS decimal(20,2)) AS AVGSpeed,
           AVG(CASE WHEN S.CountDiff > 0 THEN CAST(S.CountDiff AS decimal(18,6)) ELSE NULL END)
               AS AveragePositiveCountDiff
    FROM AssignedShift S
    LEFT JOIN LastFactoryShift LFS ON LFS.FactoryId = S.FactoryId
    CROSS JOIN Runtime R
    WHERE S.ShiftStartTime > ISNULL(LFS.LastShiftStartTime, CONVERT(datetime2, '19000101', 112))
      AND S.ShiftStartTime <= R.LocalNow AND S.LocalTimeStamp <= R.LocalNow
    GROUP BY S.NumericMachineId, S.MachineName, S.CustomerId, S.FactoryId, S.LineId,
             S.ShiftId, S.ShiftName, S.ShiftStartTime, S.ShiftEndTime, S.HourStartTime
),
LevelOne AS
(
    SELECT L.*,
           CAST(CASE WHEN L.AVGSpeed IS NULL OR L.AVGSpeed = 0 THEN 0
               WHEN L.AveragePositiveCountDiff * 240 / L.AVGSpeed > 1 THEN 1
               WHEN L.AveragePositiveCountDiff > 0 THEN L.AveragePositiveCountDiff * 240 / L.AVGSpeed
               ELSE 0 END AS decimal(20,2)) AS Performance,
           CAST(1 AS decimal(18,6)) AS Quality
    FROM LevelZero L
)
SELECT MachineId, MachineName, CustomerId, FactoryId, LineId, ShiftId, ShiftName,
       ShiftStartTime, ShiftEndTime, HourStartTime,
       CASE WHEN DATEADD(HOUR, 1, HourStartTime) > ShiftEndTime THEN ShiftEndTime
            ELSE DATEADD(HOUR, 1, HourStartTime) END AS HourEndTime,
       [Count], Qreject, Availability, AVGSpeed, Quality, Performance,
       CAST(Availability * Performance * Quality AS decimal(20,4)) AS OEE
FROM LevelOne;
GO
