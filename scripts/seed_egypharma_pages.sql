/* Egypt Pharma FRONTEND TEST DATA ONLY.
   Targets CustomerId=1 and ONLY EGYPHARMA-F01 / EGYPHARMA-F02 by code.
   No updates/deletes, no users/passwords/permissions, no views, no jobs or notifications.
   Review first with @Commit=0. Set @Commit=1 to persist all changes atomically.
   Demo data affects every screen reading these two test factories; master data/MRP
   is customer-wide because those tables have no factory field.
   Reruns skip existing natural keys. Missing old hours are filled, never overwritten.
   No telemetry mappings/rules are activated. Existing machines/lines/shifts are reused.
   The legacy readers for THESE TWO factories currently compare dates as UTC.
   This script respects that storage contract; printed local time is +03:00.
*/
USE [GeniePark];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Commit bit=0, @DaysBack int=7;
DECLARE @Now datetime2(0)=SYSUTCDATETIME(), @CustomerId int=1;
DECLARE @Marker nvarchar(40)=N'EGYPHARMA_UI_DEMO_V1';
IF @DaysBack NOT BETWEEN 2 AND 31 THROW 51000,'DaysBack must be between 2 and 31.',1;
IF OBJECT_ID('Analytics.OeeSnapshotsPerHour','U') IS NULL
    THROW 51000,'Apply the OeeSnapshotsPerHour migration first.',1;
IF COL_LENGTH('Planning.ProductionSchedules','Title') IS NULL
    THROW 51000,'Apply the Planning stoppage migration first.',1;
IF COL_LENGTH('RealTime.Production','CustomerId') IS NULL
    THROW 51000,'Apply the Production CustomerId migration first.',1;

DECLARE @Factories table(Id int PRIMARY KEY,Code nvarchar(30));
INSERT @Factories SELECT Id,Code FROM Org.Factories
WHERE CustomerId=@CustomerId AND Code IN ('EGYPHARMA-F01','EGYPHARMA-F02') AND IsActive=1 AND IsDeleted=0;
IF (SELECT COUNT(*) FROM @Factories)<>2 THROW 51000,'Both active Egypt Pharma factories must exist under CustomerId=1.',1;
DECLARE @Lines table(Id int PRIMARY KEY,FactoryId int);
INSERT @Lines SELECT L.Id,L.FactoryId FROM Org.ProductionLines L JOIN @Factories F ON F.Id=L.FactoryId
WHERE L.IsActive=1 AND L.IsDeleted=0;
IF EXISTS(SELECT 1 FROM @Factories F WHERE NOT EXISTS(SELECT 1 FROM @Lines L WHERE L.FactoryId=F.Id))
    THROW 51000,'Each target factory needs at least one existing active line.',1;
DECLARE @Machines table(Id int PRIMARY KEY,LineId int,FactoryId int,Code nvarchar(20),Name nvarchar(100),Speed int);
INSERT @Machines SELECT M.Id,M.LineId,L.FactoryId,M.Code,M.Name,COALESCE(M.RatedSpeedRpm,500)
FROM Scada.Machines M JOIN @Lines L ON L.Id=M.LineId WHERE M.IsActive=1 AND M.IsDeleted=0;
IF NOT EXISTS(SELECT 1 FROM @Machines) THROW 51000,'No active machines in the selected factories.',1;

DECLARE @N table(N int PRIMARY KEY);
DECLARE @I int=0;
WHILE @I<=(@DaysBack+3)*24 BEGIN INSERT @N VALUES(@I); SET @I+=1; END;
DECLARE @StartDay datetime2(0)=CONVERT(date,DATEADD(day,-@DaysBack-1,@Now));
DECLARE @Windows table(FactoryId int,ShiftId int,StartAt datetime2(0),EndAt datetime2(0),
    PRIMARY KEY(FactoryId,ShiftId,StartAt));
INSERT @Windows
SELECT F.Id,S.Id,W.StartAt,W.EndAt
FROM @Factories F JOIN Planning.Shifts S ON (S.FactoryId=F.Id OR EXISTS
    (SELECT 1 FROM Planning.ShiftFactories SF WHERE SF.FactoryId=F.Id AND SF.ShiftId=S.Id))
CROSS JOIN @N N
CROSS APPLY(SELECT DATEADD(day,N.N,@StartDay) AS DayAt) D
CROSS APPLY(SELECT DATEADD(second,DATEDIFF(second,CAST('00:00' AS time),S.StartTime),D.DayAt) AS StartAt,
    DATEADD(day,CASE WHEN S.EndTime<=S.StartTime THEN 1 ELSE 0 END,
        DATEADD(second,DATEDIFF(second,CAST('00:00' AS time),S.EndTime),D.DayAt)) AS EndAt) W
WHERE S.IsActive=1 AND S.IsDeleted=0 AND N.N<=@DaysBack+2;
IF EXISTS(SELECT 1 FROM @Factories F WHERE NOT EXISTS
    (SELECT 1 FROM @Windows W WHERE W.FactoryId=F.Id AND W.StartAt<=@Now AND W.EndAt>@Now))
    THROW 51000,'Every factory needs configured shifts covering the current time.',1;

DECLARE @Hours table(FactoryId int,ShiftId int,ShiftStart datetime2(0),HourAt datetime2(0),PRIMARY KEY(FactoryId,ShiftId,HourAt));
INSERT @Hours SELECT W.FactoryId,W.ShiftId,W.StartAt,H.HourAt
FROM @Windows W CROSS JOIN @N N
CROSS APPLY(SELECT DATEADD(hour,N.N,CONVERT(datetime2(0),CONVERT(date,W.StartAt))) AS HourAt) H
WHERE N.N<48 AND H.HourAt>=W.StartAt AND H.HourAt<W.EndAt
  AND H.HourAt>=DATEADD(day,-@DaysBack,@Now) AND DATEADD(minute,1,H.HourAt)<@Now;

BEGIN TRY
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource='EGYPHARMA_UI_DEMO_V1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 51000,'Another demo script is running. Retry later.',1;

-- Shared customer demo SKU/materials/BOM/routes. Do not change existing rows.
IF NOT EXISTS(SELECT 1 FROM MasterData.Skus WHERE CustomerId=1 AND Code='UI-DEMO-SKU' AND IsDeleted=0)
    INSERT MasterData.Skus(CustomerId,Code,Name,Uom,StandardBatchSize,ErpSource,IsActive,IsDeleted,CreatedAt)
    VALUES(1,'UI-DEMO-SKU','TEST ONLY - Egypt Pharma demo product','pcs',1000,0,1,0,@Now);
DECLARE @Sku int=(SELECT Id FROM MasterData.Skus WHERE CustomerId=1 AND Code='UI-DEMO-SKU' AND IsDeleted=0);
SET @I=1;
WHILE @I<=3
BEGIN
    DECLARE @Material int=NULL,@MaterialCode nvarchar(30)=CONCAT('UI-DEMO-M',@I);
    SELECT @Material=Id FROM MasterData.Materials WHERE CustomerId=1 AND Code=@MaterialCode AND IsDeleted=0;
    IF @Material IS NULL
    BEGIN
        INSERT MasterData.Materials(CustomerId,Code,Name,MaterialType,Uom,IsActive,IsDeleted,CreatedAt)
        VALUES(1,@MaterialCode,CONCAT('TEST ONLY - Demo material ',@I),'RM','kg',1,0,@Now);
        SET @Material=CONVERT(int,SCOPE_IDENTITY());
    END;
    IF NOT EXISTS(SELECT 1 FROM MasterData.BillOfMaterials WHERE SkuId=@Sku AND MaterialId=@Material AND IsDeleted=0)
        INSERT MasterData.BillOfMaterials(SkuId,MaterialId,QtyPerBatch,TolerancePct,SequenceNo,IsCritical,IsDeleted,CreatedAt)
        VALUES(@Sku,@Material,10*@I,2,@I,0,0,@Now);
    INSERT Planning.MrpRequirements(CustomerId,MaterialId,PlanDate,RequiredQty,AvailableQty,GapQty,MrpStatus,CreatedAt)
    SELECT 1,@Material,DATEADD(day,N.N,@StartDay),100,
        CASE @I WHEN 1 THEN 500 WHEN 2 THEN 80 ELSE 20 END,
        CASE @I WHEN 1 THEN 0 WHEN 2 THEN 20 ELSE 80 END,
        CASE @I WHEN 1 THEN 'ok' WHEN 2 THEN 'monitor' ELSE 'critical' END,@Now
    FROM @N N WHERE N.N<=@DaysBack+2 AND NOT EXISTS(SELECT 1 FROM Planning.MrpRequirements R
        WHERE R.CustomerId=1 AND R.MaterialId=@Material AND R.PlanDate=DATEADD(day,N.N,@StartDay));
    SET @I+=1;
END;
SET @I=1;
WHILE @I<=2
BEGIN
    DECLARE @Route int=NULL,@RouteCode nvarchar(30)=CONCAT('UI-DEMO-RT',@I);
    SELECT @Route=Id FROM Planning.Routes WHERE CustomerId=1 AND Code=@RouteCode AND IsDeleted=0;
    IF @Route IS NULL
    BEGIN
        INSERT Planning.Routes(CustomerId,Code,Name,SkuId,CycleTimeHours,Status,Version,IsDeleted,CreatedAt)
        VALUES(1,@RouteCode,CONCAT('TEST ONLY - Demo route ',@I),@Sku,1,CASE @I WHEN 1 THEN 'active' ELSE 'draft' END,1,0,@Now);
        SET @Route=CONVERT(int,SCOPE_IDENTITY());
        INSERT Planning.RouteSteps(RouteId,StepNo,StepName,EstDurationMin,IsDeleted,CreatedAt)
        VALUES(@Route,1,'Mixing',20,0,@Now),(@Route,2,'Filling',20,0,@Now),(@Route,3,'Packaging',20,0,@Now);
    END;
    SET @I+=1;
END;
SET @Route=(SELECT Id FROM Planning.Routes WHERE CustomerId=1 AND Code='UI-DEMO-RT1' AND IsDeleted=0);
INSERT Planning.RouteStepParameters(RouteStepId,Name,Value,Unit,DataType,IsDeleted,CreatedAt)
SELECT S.Id,'Demo speed','500','rpm','number',0,@Now FROM Planning.RouteSteps S
JOIN Planning.Routes R ON R.Id=S.RouteId
WHERE R.CustomerId=1 AND R.Code IN('UI-DEMO-RT1','UI-DEMO-RT2') AND R.IsDeleted=0 AND S.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM Planning.RouteStepParameters P WHERE P.RouteStepId=S.Id AND P.Name='Demo speed' AND P.IsDeleted=0);
IF NOT EXISTS(SELECT 1 FROM MasterData.SkuProcessParameters WHERE SkuId=@Sku AND Name='Demo speed' AND IsDeleted=0)
    INSERT MasterData.SkuProcessParameters(SkuId,Name,Value,Unit,DataType,IsDeleted,CreatedAt)
    VALUES(@Sku,'Demo speed','500','rpm','number',0,@Now);

-- Raw production is append-only, one test signal per machine/hour.
INSERT RealTime.Production(MachineId,[State],Speed,[Count],CountDiff,CustomerId,LineId,FactoryId,Temp,Qreject,QrejectDiff,StateReason,SKU,Fault,[TimeStamp])
SELECT M.Code,1,M.Speed,100000+DATEDIFF(hour,@StartDay,H.HourAt)*300,300,1,M.LineId,M.FactoryId,35,2,2,-1,@Sku,0,DATEADD(minute,1,H.HourAt)
FROM @Machines M JOIN (SELECT DISTINCT FactoryId,HourAt FROM @Hours) H ON H.FactoryId=M.FactoryId
WHERE NOT EXISTS(SELECT 1 FROM RealTime.Production P WHERE P.CustomerId=1 AND P.FactoryId=M.FactoryId
    AND P.LineId=M.LineId AND P.MachineId=M.Code AND P.TimeStamp=DATEADD(minute,1,H.HourAt));

-- Fractional ratios: frontend multiplies by 100. Snapshot timestamp is the bucket start.
INSERT Analytics.OeeSnapshotsPerHour(MachineId,MachineName,CustomerId,FactoryId,LineId,ShiftId,ShiftDate,
    AvailabilityPct,PerformancePct,QualityPct,OeePct,AverageSpeedRpm,LastState,TotalOutput,RejectedUnits,DowntimeMin,EnergyKwh,ComputedAt,CreatedAt)
SELECT M.Id,M.Name,1,M.FactoryId,M.LineId,H.ShiftId,H.HourAt,.90,.90,.99,.80,M.Speed,1,300,2,6,12,
    CASE WHEN DATEADD(hour,1,H.HourAt)<@Now THEN DATEADD(hour,1,H.HourAt) ELSE @Now END,@Now
FROM @Machines M JOIN @Hours H ON H.FactoryId=M.FactoryId
WHERE NOT EXISTS(SELECT 1 FROM Analytics.OeeSnapshotsPerHour O WHERE O.MachineId=M.Id AND O.ShiftId=H.ShiftId AND O.ShiftDate=H.HourAt);

INSERT Analytics.OeeSnapshots(MachineId,MachineName,CustomerId,FactoryId,LineId,ShiftId,ShiftDate,
    AvailabilityPct,PerformancePct,QualityPct,OeePct,AverageSpeedRpm,LastState,TotalOutput,RejectedUnits,DowntimeMin,EnergyKwh,ComputedAt,CreatedAt)
SELECT M.Id,M.Name,1,M.FactoryId,M.LineId,H.ShiftId,H.ShiftStart,.90,.90,.99,.80,M.Speed,1,
    COUNT(*)*300,COUNT(*)*2,COUNT(*)*6,COUNT(*)*12,
    MAX(DATEADD(minute,1,H.HourAt)),@Now
FROM @Machines M JOIN @Hours H ON H.FactoryId=M.FactoryId
WHERE NOT EXISTS(SELECT 1 FROM Analytics.OeeSnapshots O WHERE O.MachineId=M.Id AND O.ShiftId=H.ShiftId AND O.ShiftDate=H.ShiftStart)
GROUP BY M.Id,M.Name,M.FactoryId,M.LineId,M.Speed,H.ShiftId,H.ShiftStart;

-- Timeline, utilization and stop counts: transitions, not just a single latest state.
INSERT Scada.MachineStateHistories(MachineId,FromState,ToState,TransitionedAt,TriggerSource,Notes,CreatedAt)
SELECT M.Id,E.FromState,E.ToState,DATEADD(minute,E.MinuteAt,H.HourAt),'ui_demo_v1',@Marker,DATEADD(minute,E.MinuteAt,H.HourAt)
FROM @Machines M JOIN (SELECT DISTINCT FactoryId,HourAt FROM @Hours) H ON H.FactoryId=M.FactoryId
CROSS JOIN(VALUES(0,'idle','running'),(20,'running','idle'),(30,'idle','stopped'),(40,'stopped','running')) E(MinuteAt,FromState,ToState)
WHERE DATEADD(minute,E.MinuteAt,H.HourAt)<@Now AND NOT EXISTS(SELECT 1 FROM Scada.MachineStateHistories S
    WHERE S.MachineId=M.Id AND S.TransitionedAt=DATEADD(minute,E.MinuteAt,H.HourAt) AND S.ToState=E.ToState);

-- Active and resolved alarms, deliberately no notification recipients or rule execution.
INSERT [Alarm].AlarmEvents(CustomerId,MachineId,Title,Severity,TagName,TriggerValue,TriggeredAt,ResolvedAt,NotificationSent,Notes,ProbableCause,CreatedAt)
SELECT 1,M.Id,'TEST ONLY - Demo temperature alarm',
    CASE DATEPART(hour,H.HourAt)%4 WHEN 0 THEN 'critical' WHEN 1 THEN 'high' WHEN 2 THEN 'medium' ELSE 'low' END,
    'temperature',77,DATEADD(minute,5,H.HourAt),
    CASE WHEN DATEPART(hour,H.HourAt)%2=0 AND DATEADD(minute,15,H.HourAt)<@Now THEN DATEADD(minute,15,H.HourAt) END,
    1,@Marker,'TEST ONLY - simulated high temperature',DATEADD(minute,5,H.HourAt)
FROM @Machines M JOIN (SELECT DISTINCT FactoryId,HourAt FROM @Hours) H ON H.FactoryId=M.FactoryId
WHERE DATEADD(minute,5,H.HourAt)<@Now AND NOT EXISTS(SELECT 1 FROM [Alarm].AlarmEvents A
    WHERE A.CustomerId=1 AND A.MachineId=M.Id AND A.TriggeredAt=DATEADD(minute,5,H.HourAt) AND A.Notes=@Marker);

-- Planning and batch cards, on real lines/shifts; skip occupied slots rather than overlap them.
DECLARE @F int,@L int,@S int,@Start datetime2(0),@End datetime2(0),@B int,@Uid varchar(30),@Status varchar(20),@Stage varchar(20),@K int;
DECLARE BatchWindows CURSOR LOCAL FAST_FORWARD FOR
    SELECT L.FactoryId,L.Id,W.ShiftId,W.StartAt,W.EndAt FROM @Lines L JOIN @Windows W ON W.FactoryId=L.FactoryId
    WHERE W.StartAt>=DATEADD(day,-@DaysBack,@Now) AND W.StartAt<DATEADD(day,1,@Now) ORDER BY L.Id,W.StartAt;
OPEN BatchWindows;
FETCH NEXT FROM BatchWindows INTO @F,@L,@S,@Start,@End;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @K=1;
    WHILE @K<=6
    BEGIN
        DECLARE @A datetime2(0)=DATEADD(minute,(@K-1)*20,@Start),@Z datetime2(0)=DATEADD(minute,@K*20,@Start);
        SET @Uid=CONCAT('E',@L,'-',CONVERT(char(8),@Start,112),REPLACE(CONVERT(char(5),@Start,108),':',''),'-',@K);
        IF @Z<=@End AND NOT EXISTS(SELECT 1 FROM Production.Batches WHERE Uid=@Uid AND IsDeleted=0)
          AND NOT EXISTS(SELECT 1 FROM Planning.ProductionSchedules WHERE LineId=@L AND IsDeleted=0 AND ScheduledStart<@Z AND ScheduledEnd>@A)
          AND NOT EXISTS(SELECT 1 FROM Production.Batches WHERE LineId=@L AND CustomerId=1 AND IsDeleted=0
              AND Status NOT IN('cancelled','split') AND PlannedStart<@Z AND PlannedEnd>@A)
        BEGIN
            SET @Status=CASE WHEN @K=4 THEN 'cancelled' WHEN @K=5 OR @A>@Now THEN 'planned'
                WHEN @K=1 THEN 'finished' WHEN @K=6 THEN 'in_progress' ELSE 'production' END;
            SET @Stage=CASE @Status WHEN 'finished' THEN 'finished' WHEN 'production' THEN 'production'
                WHEN 'in_progress' THEN 'dispense' ELSE 'warehouse' END;
            INSERT Production.Batches(Uid,CustomerId,SkuId,LineId,RouteId,ShiftId,Status,Stage,PlannedQty,ProducedQty,RejectedQty,
                PlannedStart,PlannedEnd,ActualStart,ActualEnd,Priority,Notes,IsDeleted,CreatedAt)
            VALUES(@Uid,1,@Sku,@L,@Route,@S,@Status,@Stage,1000,
                CASE @Status WHEN 'finished' THEN 980 WHEN 'production' THEN 400 WHEN 'in_progress' THEN 100 ELSE 0 END,
                CASE WHEN @Status='finished' THEN 20 ELSE 0 END,@A,@Z,
                CASE WHEN @Status IN('finished','production','in_progress') THEN @A END,
                CASE WHEN @Status='finished' THEN @Z END,2,@Marker,0,@A);
            SET @B=CONVERT(int,SCOPE_IDENTITY());
            INSERT Production.BatchMaterials(BatchId,MaterialId,PlannedQty,DispensedQty,Status,IsDeleted,CreatedAt)
            SELECT @B,MaterialId,QtyPerBatch,CASE WHEN @Status='finished' THEN QtyPerBatch ELSE 0 END,
                CASE WHEN @Status='finished' THEN 'complete' ELSE 'pending' END,0,@A
            FROM MasterData.BillOfMaterials WHERE SkuId=@Sku AND IsDeleted=0;
            INSERT Production.BatchStageLogs(BatchId,FromStage,ToStage,TransitionedAt,Notes,CreatedAt)
            VALUES(@B,NULL,@Stage,@A,@Marker,@A);
            IF @K<>5 -- keep one planned batch selectable in New Schedule
                INSERT Planning.ProductionSchedules(CustomerId,BatchId,LineId,ScheduledStart,ScheduledEnd,IsStoppage,Notes,IsDeleted,CreatedAt)
                VALUES(1,@B,@L,@A,@Z,0,@Marker,0,@A);
        END;
        SET @K+=1;
    END;
    SET @A=DATEADD(minute,140,@Start); SET @Z=DATEADD(minute,160,@Start);
    IF @Z<=@End AND NOT EXISTS(SELECT 1 FROM Planning.ProductionSchedules WHERE LineId=@L AND IsDeleted=0 AND ScheduledStart<@Z AND ScheduledEnd>@A)
       AND NOT EXISTS(SELECT 1 FROM Production.Batches WHERE CustomerId=1 AND LineId=@L AND IsDeleted=0
          AND Status NOT IN('cancelled','split') AND PlannedStart<@Z AND PlannedEnd>@A)
        INSERT Planning.ProductionSchedules(CustomerId,LineId,ScheduledStart,ScheduledEnd,IsStoppage,Title,StoppageType,Notes,IsDeleted,CreatedAt)
        VALUES(1,@L,@A,@Z,1,'TEST ONLY - Planned maintenance','maintenance',@Marker,0,@A);
    FETCH NEXT FROM BatchWindows INTO @F,@L,@S,@Start,@End;
END;
CLOSE BatchWindows; DEALLOCATE BatchWindows;

-- SCADA warehouse/dispensing examples: NEW clearly labelled demo equipment only.
DECLARE @EquipmentCode varchar(20)=CONCAT('UI-',CONVERT(char(8),@Now,112));
SET @Material=(SELECT Id FROM MasterData.Materials WHERE CustomerId=1 AND Code='UI-DEMO-M1' AND IsDeleted=0);
INSERT RealTime.Silos(FactoryId,Code,Name,SiloType,Zone,CapacityKg,LowLevelPct,CriticalLevelPct,CurrentQtyKg,
    CurrentMaterialId,CurrentLotUid,TelemetryAt,IsDeleted,CreatedAt)
SELECT F.Id,@EquipmentCode,'TEST ONLY - Demo silo','raw_material','warehouse',1000,20,10,650,@Material,
    CONCAT('UI-LOT-',CONVERT(char(8),@Now,112)),@Now,0,@Now FROM @Factories F
WHERE NOT EXISTS(SELECT 1 FROM RealTime.Silos S WHERE S.FactoryId=F.Id AND S.Code=@EquipmentCode AND S.IsDeleted=0);
INSERT RealTime.Scales(FactoryId,Code,Name,Zone,CapacityKg,ResolutionG,TareKg,WeightKg,ScaleState,
    ActiveBatchId,ActiveMaterialId,CalibratedAt,TelemetryAt,IsDeleted,CreatedAt)
SELECT F.Id,@EquipmentCode,'TEST ONLY - Demo scale','dispensing',500,.1,0,25,'stable',
    (SELECT TOP(1) B.Id FROM Production.Batches B JOIN @Lines L ON L.Id=B.LineId
        WHERE B.CustomerId=1 AND L.FactoryId=F.Id AND B.Notes=@Marker AND B.Status='in_progress' AND B.IsDeleted=0
        ORDER BY B.PlannedStart DESC),@Material,DATEADD(day,-1,@Now),@Now,0,@Now
FROM @Factories F WHERE NOT EXISTS(SELECT 1 FROM RealTime.Scales S WHERE S.FactoryId=F.Id AND S.Code=@EquipmentCode AND S.IsDeleted=0);

-- Alarm rule tabs: disabled examples, no actions, recipients, or automatic work orders.
INSERT [Alarm].AlarmRules(CustomerId,Name,RuleType,ScopeType,ScopeEntityId,TagName,MinimumValue,MaximumValue,
    Severity,CreateWorkOrder,IsActive,IsDeleted,CreatedAt)
SELECT 1,CONCAT('TEST ONLY - ',F.Code,' ',R.RuleType),R.RuleType,'factory',F.Id,'temperature',0,80,'high',0,0,0,@Now
FROM @Factories F CROSS JOIN(VALUES('user'),('system')) R(RuleType)
WHERE NOT EXISTS(SELECT 1 FROM [Alarm].AlarmRules A WHERE A.CustomerId=1 AND A.Name=CONCAT('TEST ONLY - ',F.Code,' ',R.RuleType) AND A.IsDeleted=0);

-- Assets/configuration: never replace a user's configured machine parameters.
INSERT Asset.Assets(CustomerId,FactoryId,Code,Name,AssetType,MachineId,HealthScore,Status,UtilizationPct,IsActive,IsDeleted,CreatedAt)
SELECT 1,M.FactoryId,CONCAT('UI-DEMO-M',M.Id),CONCAT('TEST ONLY - ',M.Name),'machine',M.Id,90,'running',80,1,0,@Now
FROM @Machines M
WHERE NOT EXISTS(SELECT 1 FROM Asset.Assets A WHERE A.CustomerId=1 AND A.Code=CONCAT('UI-DEMO-M',M.Id) AND A.IsDeleted=0)
-- The unique MachineId index applies across customers, not only demo asset codes.
AND NOT EXISTS(SELECT 1 FROM Asset.Assets A WHERE A.MachineId=M.Id AND A.IsDeleted=0);
INSERT Config.MachineConfigs(MachineId,RatedSpeedRpm,MinSpeedRpm,MaxTempCelsius,MaxVibrationMms,RatedPowerKw,CreatedAt)
SELECT Id,Speed,100,80,5,12,@Now FROM @Machines M
WHERE NOT EXISTS(SELECT 1 FROM Config.MachineConfigs C WHERE C.MachineId=M.Id);

-- Maintenance history and work order detail/parts.
IF NOT EXISTS(SELECT 1 FROM Maintenance.SpareParts WHERE CustomerId=1 AND Code='UI-DEMO-PART' AND IsDeleted=0)
    INSERT Maintenance.SpareParts(CustomerId,Code,Name,Uom,QtyOnHand,MinStock,UnitCost,StockStatus,IsDeleted,CreatedAt)
    VALUES(1,'UI-DEMO-PART','TEST ONLY - Demo bearing','pcs',20,5,100,'in_stock',0,@Now);
DECLARE @Part int=(SELECT Id FROM Maintenance.SpareParts WHERE CustomerId=1 AND Code='UI-DEMO-PART' AND IsDeleted=0);
INSERT Maintenance.PmSchedules(CustomerId,MachineId,TaskName,Frequency,IntervalDays,LastDoneAt,NextDueAt,EstimatedDurationHours,Instructions,IsActive,IsDeleted,CreatedAt)
SELECT 1,M.Id,'TEST ONLY - Demo inspection','weekly',7,DATEADD(day,-7,@Now),DATEADD(day,1,@Now),1,@Marker,1,0,@Now
FROM @Machines M WHERE NOT EXISTS(SELECT 1 FROM Maintenance.PmSchedules P WHERE P.CustomerId=1 AND P.MachineId=M.Id AND P.Instructions=@Marker AND P.IsDeleted=0);
-- Different shift definitions can share a start time. NOT EXISTS alone does not
-- remove duplicates produced by the same INSERT SELECT: rank by the final key.
;WITH WorkOrderCandidates AS
(
    SELECT M.FactoryId,M.Id AS MachineId,W.StartAt,W.EndAt,
        K.Number,
        ROW_NUMBER() OVER(PARTITION BY K.Number ORDER BY W.StartAt,W.ShiftId,W.EndAt) AS KeyRank
    FROM @Machines M JOIN @Windows W ON W.FactoryId=M.FactoryId
    CROSS APPLY(SELECT CONCAT('E',CONVERT(char(8),CONVERT(binary(4),M.Id),2),CONVERT(char(6),W.StartAt,12),REPLACE(CONVERT(char(5),W.StartAt,108),':','')) AS Number) K
    WHERE DATEADD(minute,40,W.StartAt)<@Now AND W.StartAt>=DATEADD(day,-@DaysBack,@Now)
)
INSERT Maintenance.WorkOrders(Number,CustomerId,FactoryId,MachineId,WorkOrderType,Priority,Description,PlannedStart,
    PlannedDurationHours,ActualStart,ActualEnd,Status,FailureMode,IsDeleted,CreatedAt)
SELECT W.Number,
    1,W.FactoryId,W.MachineId,'corrective','medium',@Marker,DATEADD(minute,30,W.StartAt),1,DATEADD(minute,30,W.StartAt),
    CASE WHEN W.EndAt<@Now THEN DATEADD(minute,40,W.StartAt) END,
    CASE WHEN W.EndAt<@Now THEN 'resolved' ELSE 'open' END,'TEST ONLY - simulated stop',0,DATEADD(minute,30,W.StartAt)
FROM WorkOrderCandidates W
WHERE W.KeyRank=1
AND NOT EXISTS(SELECT 1 FROM Maintenance.WorkOrders O WHERE O.Number=W.Number);
INSERT Maintenance.BreakdownLogs(Reference,MachineId,WorkOrderId,BreakdownAt,ResolvedAt,MttrHours,RootCause,FailureMode,Status,CreatedAt)
SELECT CONCAT('B',O.Number),O.MachineId,O.Id,O.ActualStart,O.ActualEnd,
    CASE WHEN O.ActualEnd IS NOT NULL THEN .17 END,@Marker,O.FailureMode,O.Status,O.CreatedAt
FROM Maintenance.WorkOrders O JOIN @Machines M ON M.Id=O.MachineId
WHERE O.CustomerId=1 AND O.Description=@Marker AND O.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM Maintenance.BreakdownLogs B WHERE B.WorkOrderId=O.Id)
AND NOT EXISTS(SELECT 1 FROM Maintenance.BreakdownLogs B WHERE B.Reference=CONCAT('B',O.Number));
INSERT Maintenance.WoPartsUsed(WorkOrderId,SparePartId,QtyUsed,UnitCostAtUse,CreatedAt)
SELECT O.Id,@Part,1,100,O.CreatedAt FROM Maintenance.WorkOrders O JOIN @Machines M ON M.Id=O.MachineId
WHERE O.CustomerId=1 AND O.Description=@Marker AND O.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM Maintenance.WoPartsUsed P WHERE P.WorkOrderId=O.Id AND P.SparePartId=@Part);

-- Transformer energy samples. These do NOT invent energy efficiency or SKU allocation formulas.
INSERT MasterData.Transformers(TransformerName,FactoryId,CreatedAt,IsDeleted)
SELECT CONCAT('UI-DEMO-F',Id),Id,@Now,0 FROM @Factories F
WHERE NOT EXISTS(SELECT 1 FROM MasterData.Transformers T WHERE T.TransformerName=CONCAT('UI-DEMO-F',F.Id) AND T.IsDeleted=0);
INSERT Analytics.EnergyReadings(CustomerId,FactoryId,LineId,TransformerId,Quality,VoltageL1,VoltageL2,VoltageL3,
    CurrentL1,CurrentL2,CurrentL3,PowerFactorL1,PowerFactorL2,PowerFactorL3,ActivePowerL1,ActivePowerL2,ActivePowerL3,
    EnergyL1,EnergyL2,EnergyL3,Timestamp)
SELECT 1,F.Id,(SELECT MIN(Id) FROM @Lines WHERE FactoryId=F.Id),T.Id,192,230,231,229,25,24,26,.95,.96,.94,5,5,5,
    1000+DATEDIFF(hour,@StartDay,H.HourAt)*5,1000+DATEDIFF(hour,@StartDay,H.HourAt)*5,1000+DATEDIFF(hour,@StartDay,H.HourAt)*5,H.HourAt
FROM @Factories F JOIN MasterData.Transformers T ON T.FactoryId=F.Id AND T.TransformerName=CONCAT('UI-DEMO-F',F.Id) AND T.IsDeleted=0
JOIN(SELECT DISTINCT FactoryId,HourAt FROM @Hours) H ON H.FactoryId=F.Id
WHERE NOT EXISTS(SELECT 1 FROM Analytics.EnergyReadings E WHERE E.TransformerId=T.Id AND E.Timestamp=H.HourAt);

INSERT Analytics.AiInsights(CustomerId,InsightType,EntityType,EntityId,Title,Body,ConfidencePct,ModelVersion,GeneratedAt,ExpiresAt,IsDismissed,IsDeleted,CreatedAt)
SELECT 1,'maintenance','machine',M.Id,'TEST ONLY - Inspect bearing','Synthetic frontend test insight, not an actual prediction.',90,@Marker,
    CONVERT(date,@Now),DATEADD(day,7,@Now),0,0,@Now FROM @Machines M
WHERE NOT EXISTS(SELECT 1 FROM Analytics.AiInsights A WHERE A.CustomerId=1 AND A.EntityType='machine' AND A.EntityId=M.Id AND A.ModelVersion=@Marker AND A.GeneratedAt=CONVERT(date,@Now) AND A.IsDeleted=0);

-- Self-check results (counts include existing rows). No passwords, tokens or real-user changes.
SELECT F.Id AS FactoryId,F.Code,@CustomerId AS CustomerId,@Commit AS WillCommit,
    DATEADD(hour,3,@Now) AS GeneratedAtLocal,
    (SELECT COUNT(*) FROM @Lines L WHERE L.FactoryId=F.Id) AS Lines,
    (SELECT COUNT(*) FROM @Machines M WHERE M.FactoryId=F.Id) AS Machines,
    (SELECT COUNT(*) FROM Analytics.OeeSnapshots O JOIN @Machines M ON M.Id=O.MachineId WHERE M.FactoryId=F.Id AND O.ShiftDate>=DATEADD(day,-@DaysBack,@Now)) AS OeeRows,
    (SELECT COUNT(*) FROM Analytics.OeeSnapshotsPerHour O JOIN @Machines M ON M.Id=O.MachineId WHERE M.FactoryId=F.Id AND O.ShiftDate>=DATEADD(day,-@DaysBack,@Now)) AS HourlyOeeRows,
    (SELECT COUNT(*) FROM Production.Batches B JOIN @Lines L ON L.Id=B.LineId WHERE L.FactoryId=F.Id AND B.Notes=@Marker AND B.IsDeleted=0) AS DemoBatches,
    (SELECT COUNT(*) FROM [Alarm].AlarmEvents A JOIN @Machines M ON M.Id=A.MachineId WHERE M.FactoryId=F.Id AND A.Notes=@Marker) AS DemoAlarms,
    (SELECT COUNT(*) FROM Maintenance.WorkOrders O WHERE O.CustomerId=1 AND O.FactoryId=F.Id AND O.Description=@Marker AND O.IsDeleted=0) AS DemoWorkOrders
FROM @Factories F;
SELECT F.Id AS FactoryId,L.Id AS LineId,CONCAT('/v1/ui/overview/oee-trend?FactoryId=',F.Id,'&LineId=',L.Id,'&Duration=2') AS OverviewLastDay,
    CONCAT('/v1/planning/summary?FactoryId=',F.Id,'&LineId=',L.Id,'&Duration=1') AS PlanningLastShift,
    CONCAT('/v1/operation/summary?FactoryId=',F.Id,'&LineId=',L.Id,'&Duration=2') AS OperationLastDay
FROM @Factories F JOIN @Lines L ON L.FactoryId=F.Id;
IF @Commit=1 BEGIN COMMIT TRANSACTION; PRINT 'SAVED Egypt Pharma demo rows. No other customer/factory was targeted.'; END
ELSE BEGIN ROLLBACK TRANSACTION; PRINT 'DRY RUN ONLY: nothing saved. Set @Commit=1 and run again to save. Identity counters can still advance during a dry run.'; END;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
