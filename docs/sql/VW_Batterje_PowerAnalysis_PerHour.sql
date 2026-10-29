USE [GeniePark];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- VIEW ONLY. First generate/apply the EF migration for PowerAnalysisPerHour.
-- Schema and table are owned by the code-first model, not this script.
-- IMPORTANT provisional assumptions (the supplied EMS view does not define these):
-- EnergyL1/2/3 are independent cumulative phase counters. Unit is unchanged, NOT assumed kWh.
-- Negative deltas/reset and absent baseline contribute zero; no reset recovery/interpolation.
-- Immediate previous row is used, including BEFORE the shift boundary. A missing phase
-- interrupts its baseline; we do not bridge nulls. Gaps are attributed to the later reading.
-- Phase averages ignore null phases, then shift averages follow the supplied EMS view.
-- EnergyReadings.Timestamp is already LOCAL +03:00; never add another three hours.
-- Default bootstrap date is intentionally fixed per request, not "today".
-- Saving any row advances that transformer's watermark. Save only finalized shifts;
-- Hourly rows are archived together at shift end, as in the shift version.
-- Saving even ONE hour marks that entire transformer shift as saved: archive ALL hours atomically.
-- HourStartTime is the unique hourly bucket, NOT ShiftStartTime. Empty elapsed hours have zero counts.
CREATE OR ALTER VIEW [Calculated].[VW_Batterje_PowerAnalysis_PerHour]
AS
WITH Runtime AS
(
    SELECT CAST(DATEADD(hour,3,SYSUTCDATETIME()) AS datetime2(7)) AS LocalNow,
           CONVERT(datetime2(7),'2026-09-30T08:00:00',126) AS BootstrapStart
),
Scope AS
(
    SELECT T.Id AS TransformerId,T.TransformerName,F.Id AS FactoryId,F.CustomerId,
        (SELECT MAX(P.ShiftStartTime) FROM Calculated.PowerAnalysisPerHour P
         WHERE P.TransformerId=T.Id AND P.FactoryId=F.Id AND P.CustomerId=F.CustomerId) AS LastSavedShift,
        (SELECT CASE WHEN COUNT(*)=1 THEN MAX(L.Id) END FROM Org.ProductionLines L
         WHERE L.FactoryId=F.Id AND L.IsDeleted=0 AND L.IsActive=1) AS DefaultLineId
    FROM MasterData.Transformers T
    JOIN Org.Factories F ON F.Id=T.FactoryId AND F.IsDeleted=0 AND F.IsActive=1
    WHERE T.IsDeleted=0 AND T.FactoryId=5 AND F.CustomerId=3
),
FactoryShifts AS
(
    SELECT F.Id AS FactoryId,S.Id AS ShiftId,S.Name AS ShiftName,S.StartTime,S.EndTime
    FROM Org.Factories F JOIN Planning.Shifts S ON
        S.FactoryId=F.Id OR EXISTS(SELECT 1 FROM Planning.ShiftFactories SF WHERE SF.FactoryId=F.Id AND SF.ShiftId=S.Id)
    WHERE S.IsActive=1 AND S.IsDeleted=0
),
PreviousReadings AS
(
    -- LAG must run before filtering to unarchived shifts.
    SELECT E.*,
        LAG(E.EnergyL1) OVER(PARTITION BY E.TransformerId ORDER BY E.Timestamp,E.Id) AS PreviousL1,
        LAG(E.EnergyL2) OVER(PARTITION BY E.TransformerId ORDER BY E.Timestamp,E.Id) AS PreviousL2,
        LAG(E.EnergyL3) OVER(PARTITION BY E.TransformerId ORDER BY E.Timestamp,E.Id) AS PreviousL3
    FROM Analytics.EnergyReadings E JOIN Scope T ON T.TransformerId=E.TransformerId
        AND T.FactoryId=E.FactoryId AND T.CustomerId=E.CustomerId
    CROSS JOIN Runtime R WHERE E.Timestamp<=R.LocalNow
),
Samples AS
(
    SELECT E.*,S.ShiftId,S.ShiftName,S.StartTime,S.EndTime,B.ShiftStartTime,
        DATEADD(second,CASE WHEN S.EndTime>S.StartTime THEN DATEDIFF(second,S.StartTime,S.EndTime)
            ELSE 86400+DATEDIFF(second,S.StartTime,S.EndTime) END,B.ShiftStartTime) AS ShiftEndTime,
        CAST(CASE WHEN E.EnergyL1>=E.PreviousL1 THEN E.EnergyL1-E.PreviousL1 ELSE 0 END
           + CASE WHEN E.EnergyL2>=E.PreviousL2 THEN E.EnergyL2-E.PreviousL2 ELSE 0 END
           + CASE WHEN E.EnergyL3>=E.PreviousL3 THEN E.EnergyL3-E.PreviousL3 ELSE 0 END AS decimal(28,6)) AS ConsumptionDiff,
        A.PF,A.Voltage,A.[Current],A.THDv,A.THDi
    FROM PreviousReadings E
    CROSS APPLY
    (
        -- Deterministic precedence if legacy shift definitions overlap; never multiply a reading.
        SELECT TOP(1) S.* FROM FactoryShifts S WHERE S.FactoryId=E.FactoryId AND
           ((S.StartTime<S.EndTime AND CAST(E.Timestamp AS time)>=S.StartTime AND CAST(E.Timestamp AS time)<S.EndTime)
           OR (S.StartTime>=S.EndTime AND (CAST(E.Timestamp AS time)>=S.StartTime OR CAST(E.Timestamp AS time)<S.EndTime)))
        ORDER BY S.StartTime DESC,S.ShiftId
    ) S
    CROSS APPLY(SELECT DATEADD(second,DATEDIFF(second,CAST('00:00:00' AS time),S.StartTime),
        DATEADD(day,CASE WHEN S.StartTime>=S.EndTime AND CAST(E.Timestamp AS time)<S.EndTime THEN -1 ELSE 0 END,
            CAST(CAST(E.Timestamp AS date) AS datetime2(7)))) AS ShiftStartTime) B
    CROSS APPLY
    (
        SELECT AVG(V.PF) AS PF,AVG(V.Voltage) AS Voltage,AVG(V.[Current]) AS [Current],AVG(V.THDv) AS THDv,AVG(V.THDi) AS THDi
        FROM (VALUES(E.PowerFactorL1,E.VoltageL1,E.CurrentL1,E.VoltageThdL1,E.CurrentThdL1),
                    (E.PowerFactorL2,E.VoltageL2,E.CurrentL2,E.VoltageThdL2,E.CurrentThdL2),
                    (E.PowerFactorL3,E.VoltageL3,E.CurrentL3,E.VoltageThdL3,E.CurrentThdL3)) V(PF,Voltage,[Current],THDv,THDi)
    ) A
),
UnstoredSamples AS
(
    SELECT E.* FROM Samples E JOIN Scope T ON T.TransformerId=E.TransformerId CROSS JOIN Runtime R
    WHERE (T.LastSavedShift IS NOT NULL AND E.ShiftStartTime>T.LastSavedShift)
       OR (T.LastSavedShift IS NULL AND E.ShiftStartTime>=R.BootstrapStart)
),
CurrentShift AS
(
    SELECT T.TransformerId,S.ShiftId,S.ShiftName,B.ShiftStartTime,
        DATEADD(second,CASE WHEN S.EndTime>S.StartTime THEN DATEDIFF(second,S.StartTime,S.EndTime)
            ELSE 86400+DATEDIFF(second,S.StartTime,S.EndTime) END,B.ShiftStartTime) AS ShiftEndTime
    FROM Scope T CROSS JOIN Runtime R
    CROSS APPLY
    (
        SELECT TOP(1) S.* FROM FactoryShifts S WHERE S.FactoryId=T.FactoryId AND
           ((S.StartTime<S.EndTime AND CAST(R.LocalNow AS time)>=S.StartTime AND CAST(R.LocalNow AS time)<S.EndTime)
           OR (S.StartTime>=S.EndTime AND (CAST(R.LocalNow AS time)>=S.StartTime OR CAST(R.LocalNow AS time)<S.EndTime)))
        ORDER BY S.StartTime DESC,S.ShiftId
    ) S
    CROSS APPLY(SELECT DATEADD(second,DATEDIFF(second,CAST('00:00:00' AS time),S.StartTime),
        DATEADD(day,CASE WHEN S.StartTime>=S.EndTime AND CAST(R.LocalNow AS time)<S.EndTime THEN -1 ELSE 0 END,
            CAST(CAST(R.LocalNow AS date) AS datetime2(7)))) AS ShiftStartTime) B
    WHERE (T.LastSavedShift IS NOT NULL AND B.ShiftStartTime>T.LastSavedShift)
       OR (T.LastSavedShift IS NULL AND B.ShiftStartTime>=R.BootstrapStart)
),
ShiftGrid AS
(
    SELECT TransformerId,ShiftId,ShiftName,ShiftStartTime,ShiftEndTime FROM CurrentShift
    UNION
    SELECT TransformerId,ShiftId,ShiftName,ShiftStartTime,ShiftEndTime FROM UnstoredSamples
),
HourNumbers AS
(
    SELECT A.N+10*B.N AS N FROM (VALUES(0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) A(N)
    CROSS JOIN (VALUES(0),(1),(2)) B(N)
),
HourGrid AS
(
    SELECT G.*,CASE WHEN H.StartAt<G.ShiftStartTime THEN G.ShiftStartTime ELSE H.StartAt END AS HourStartTime,
        CASE WHEN DATEADD(hour,1,H.StartAt)>G.ShiftEndTime THEN G.ShiftEndTime ELSE DATEADD(hour,1,H.StartAt) END AS HourEndTime
    FROM ShiftGrid G CROSS JOIN HourNumbers N CROSS JOIN Runtime R
    CROSS APPLY(SELECT DATEADD(hour,DATEDIFF(hour,CONVERT(datetime2,'19000101',112),G.ShiftStartTime)+N.N,
        CONVERT(datetime2,'19000101',112)) AS StartAt) H
    WHERE H.StartAt<G.ShiftEndTime AND H.StartAt<=R.LocalNow
)
SELECT T.TransformerId,T.TransformerName,T.CustomerId,T.FactoryId,
    CASE WHEN COUNT_BIG(E.Id)=0 THEN T.DefaultLineId
         WHEN COUNT_BIG(E.LineId)=COUNT_BIG(E.Id) AND MIN(E.LineId)=MAX(E.LineId) THEN MAX(E.LineId)
         ELSE NULL END AS LineId,
    G.ShiftId,G.ShiftName,G.ShiftStartTime,G.ShiftEndTime,G.HourStartTime,G.HourEndTime,
    CAST(ISNULL(SUM(E.ConsumptionDiff),0) AS decimal(38,6)) AS TotalEnergyConsumption,
    CAST(CASE WHEN MAX(E.Timestamp)>=DATEADD(hour,-3,R.LocalNow) THEN 'Online' ELSE 'Offline' END AS varchar(7)) AS Status,
    CAST(ISNULL(AVG(E.PF),0) AS decimal(28,6)) AS PowerFactor,
    CAST(ISNULL(AVG(E.Voltage),0) AS decimal(28,6)) AS Voltage,
    CAST(ISNULL(AVG(E.[Current]),0) AS decimal(28,6)) AS [Current],
    CAST(ISNULL(AVG(ISNULL(E.THDv,0)),0) AS decimal(28,6)) AS Hermonics,
    CAST(ISNULL(AVG(E.THDv),0) AS decimal(28,6)) AS AvgTHDv,
    CAST(ISNULL(AVG(E.THDi),0) AS decimal(28,6)) AS AvgTHDi,
    MAX(E.Timestamp) AS LastReadingAt,COUNT_BIG(E.Id) AS ReadingCount,R.LocalNow AS CalculatedAt
FROM HourGrid G JOIN Scope T ON T.TransformerId=G.TransformerId CROSS JOIN Runtime R
LEFT JOIN UnstoredSamples E ON E.TransformerId=G.TransformerId AND E.ShiftId=G.ShiftId AND E.ShiftStartTime=G.ShiftStartTime
    AND E.Timestamp>=G.HourStartTime AND E.Timestamp<G.HourEndTime
GROUP BY T.TransformerId,T.TransformerName,T.CustomerId,T.FactoryId,T.DefaultLineId,
    G.ShiftId,G.ShiftName,G.ShiftStartTime,G.ShiftEndTime,G.HourStartTime,G.HourEndTime,R.LocalNow;
GO
-- Inspect only; no snapshot writes and no job creation.
SELECT * FROM Calculated.VW_Batterje_PowerAnalysis_PerHour
WHERE FactoryId=5 ORDER BY TransformerId,HourStartTime;
