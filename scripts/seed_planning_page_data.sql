/* Planning UI demo data ONLY. SQL Server / GeniePark.
   1. Apply AddPlanningStoppageSupport first.
   2. Set FactoryId / LineId / local DemoDate below. Both IDs are required.
   3. Dry run by default. Set @Commit = 1 to save, then use the returned Duration=5 URL.
   No deletes or updates of existing business data. Re-running the same date/line is idempotent.
   Writes demo SKU/BOM/materials/routes/batches/schedules/MRP, NOT Production telemetry or stock.
   Do not run on production without approval: demo batches are visible to other batch screens too.
*/
USE [GeniePark];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Example ONLY: Batterjee is FactoryId=5 / LineId=13. Demo rows affect its real dashboards!
DECLARE @FactoryId int = NULL, @LineId int = NULL;
DECLARE @LocalUtcOffsetHours int = 3;
DECLARE @DemoDate date = CONVERT(date, DATEADD(hour, @LocalUtcOffsetHours, SYSUTCDATETIME()));
DECLARE @Commit bit = 0;
DECLARE @CustomerId int, @SkuId int, @RouteId int, @DraftRouteId int, @MaterialId int, @BatchId int;
-- Planning currently stores/filter-compares schedule timestamps as UTC internally.
-- Convert the local day ONCE; returned URLs use an explicit +03:00 local offset.
DECLARE @Now datetime2(0) = SYSUTCDATETIME(),
        @FromLocal datetime2(0) = @DemoDate,
        @ToLocal datetime2(0) = DATEADD(day, 1, CONVERT(datetime2(0), @DemoDate));
DECLARE @From datetime2(0) = DATEADD(hour,-@LocalUtcOffsetHours,@FromLocal),
        @To datetime2(0) = DATEADD(hour,-@LocalUtcOffsetHours,@ToLocal);
DECLARE @Tag nvarchar(30) = CONCAT('PD3-', CONVERT(char(8), @DemoDate, 112), '-L', @LineId);

IF @FactoryId IS NULL OR @LineId IS NULL
    THROW 51000, 'Set FactoryId and LineId explicitly. Use a test factory if possible.', 1;

IF COL_LENGTH('Planning.ProductionSchedules', 'Title') IS NULL
    THROW 51000, 'Apply AddPlanningStoppageSupport before running this script.', 1;
SELECT @CustomerId = f.CustomerId FROM Org.Factories f
JOIN Org.ProductionLines l ON l.FactoryId = f.Id
WHERE f.Id = @FactoryId AND l.Id = @LineId AND f.IsDeleted = 0 AND l.IsDeleted = 0
  AND f.IsActive = 1 AND l.IsActive = 1;
IF @CustomerId IS NULL THROW 51000, 'Choose an active factory and a line belonging to it.', 1;

BEGIN TRY
BEGIN TRANSACTION;
DECLARE @LockResult int, @Resource nvarchar(255) = CONCAT('planning:line:', @LineId);
EXEC @LockResult = sys.sp_getapplock @Resource=@Resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
IF @LockResult < 0 THROW 51000, 'Line is busy. Retry later.', 1;
-- Serialize creation of shared demo master data when two different lines are seeded concurrently.
SET @Resource = CONCAT('planning:demo:customer:', @CustomerId);
EXEC @LockResult = sys.sp_getapplock @Resource=@Resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
IF @LockResult < 0 THROW 51000, 'Demo setup is busy. Retry later.', 1;

SELECT @SkuId = Id FROM MasterData.Skus WHERE CustomerId=@CustomerId AND Code='DEMO-PLANNING' AND IsDeleted=0;
IF @SkuId IS NULL
BEGIN
    INSERT MasterData.Skus (CustomerId,Code,Name,Uom,StandardBatchSize,ErpSource,IsActive,IsDeleted,CreatedAt)
    VALUES (@CustomerId,'DEMO-PLANNING','Planning demo product','pcs',1000,0,1,0,@Now);
    SET @SkuId = CONVERT(int,SCOPE_IDENTITY());
END;

SELECT @DraftRouteId = Id FROM Planning.Routes WHERE CustomerId=@CustomerId AND Code='DEMO-PLANNING-DRAFT' AND IsDeleted=0;
IF @DraftRouteId IS NULL
BEGIN
    INSERT Planning.Routes (CustomerId,Code,Name,SkuId,CycleTimeHours,Status,Version,IsDeleted,CreatedAt)
    VALUES (@CustomerId,'DEMO-PLANNING-DRAFT','Planning demo draft route',@SkuId,0.5,'draft',1,0,@Now);
    SET @DraftRouteId = CONVERT(int,SCOPE_IDENTITY());
    INSERT Planning.RouteSteps (RouteId,StepNo,StepName,EstDurationMin,IsDeleted,CreatedAt)
    VALUES (@DraftRouteId,1,'Preparation',10,0,@Now),(@DraftRouteId,2,'Packaging',20,0,@Now);
END;
SELECT @RouteId = Id FROM Planning.Routes WHERE CustomerId=@CustomerId AND Code='DEMO-PLANNING-RT' AND IsDeleted=0;
IF @RouteId IS NULL
BEGIN
    INSERT Planning.Routes (CustomerId,Code,Name,SkuId,CycleTimeHours,Status,Version,IsDeleted,CreatedAt)
    VALUES (@CustomerId,'DEMO-PLANNING-RT','Planning demo route',@SkuId,0.25,'active',1,0,@Now);
    SET @RouteId = CONVERT(int,SCOPE_IDENTITY());
    INSERT Planning.RouteSteps (RouteId,StepNo,StepName,EstDurationMin,IsDeleted,CreatedAt)
    VALUES (@RouteId,1,'Mixing',5,0,@Now),(@RouteId,2,'Filling',5,0,@Now),(@RouteId,3,'Packaging',5,0,@Now);
END;

DECLARE @Materials table (N int PRIMARY KEY, MaterialId int NOT NULL);
DECLARE @N int = 1, @Code nvarchar(40);
WHILE @N <= 3
BEGIN
    SET @MaterialId = NULL;
    SET @Code = CONCAT('DEMO-PLAN-M',@N);
    SELECT @MaterialId=Id FROM MasterData.Materials WHERE CustomerId=@CustomerId AND Code=@Code AND IsDeleted=0;
    IF @MaterialId IS NULL
    BEGIN
        INSERT MasterData.Materials (CustomerId,Code,Name,MaterialType,Uom,IsActive,IsDeleted,CreatedAt)
        VALUES (@CustomerId,@Code,CONCAT('Planning demo material ',@N),'RM','kg',1,0,@Now);
        SET @MaterialId = CONVERT(int,SCOPE_IDENTITY());
    END;
    INSERT @Materials VALUES (@N,@MaterialId);
    IF NOT EXISTS (SELECT 1 FROM MasterData.BillOfMaterials WHERE SkuId=@SkuId AND MaterialId=@MaterialId AND IsDeleted=0)
        INSERT MasterData.BillOfMaterials (SkuId,MaterialId,QtyPerBatch,TolerancePct,SequenceNo,IsCritical,IsDeleted,CreatedAt)
        VALUES (@SkuId,@MaterialId,10*@N,2,@N,0,0,@Now);
    IF NOT EXISTS (SELECT 1 FROM Planning.MrpRequirements WHERE CustomerId=@CustomerId AND MaterialId=@MaterialId AND PlanDate=@DemoDate)
        INSERT Planning.MrpRequirements (CustomerId,MaterialId,PlanDate,RequiredQty,AvailableQty,GapQty,MrpStatus,CreatedAt)
        VALUES (@CustomerId,@MaterialId,@DemoDate,100,
            CASE @N WHEN 1 THEN 500 WHEN 2 THEN 80 ELSE 20 END,
            CASE @N WHEN 1 THEN 0 WHEN 2 THEN 20 ELSE 80 END,
            CASE @N WHEN 1 THEN 'ok' WHEN 2 THEN 'monitor' ELSE 'critical' END,@Now);
    SET @N += 1;
END;

-- Find real free 15-minute slots. Never overwrite or overlap an existing reservation.
-- Six batches, then maintenance/cleaning/calibration. Fifth batch stays in New Schedule.
DECLARE @Start datetime2(0), @End datetime2(0), @Uid nvarchar(30), @Status nvarchar(20), @Stage nvarchar(20);
SET @N = 1;
WHILE @N <= 9
BEGIN
    SET @Uid = CONCAT(@Tag,'-',@N);
    IF (@N <= 6 AND EXISTS (SELECT 1 FROM Production.Batches WHERE Uid=@Uid AND IsDeleted=0)) OR
       (@N >= 7 AND EXISTS (SELECT 1 FROM Planning.ProductionSchedules WHERE CustomerId=@CustomerId AND LineId=@LineId AND Notes=@Uid AND IsDeleted=0))
    BEGIN
        SET @N += 1;
        CONTINUE;
    END;
    SET @Start = @From;
    -- Make planned demo batches upcoming when the selected day is today.
    IF @N IN (3,5) AND @Now >= @From AND @Now < @To
        SET @Start = DATEADD(minute,15,@Now);
    WHILE 1=1
    BEGIN
        SET @End = DATEADD(minute,15,@Start);
        IF @End > @To THROW 51000, 'Not enough free slots on this date/line. Choose another DemoDate or line; nothing was saved.', 1;
        IF NOT EXISTS (
            SELECT 1 FROM Planning.ProductionSchedules s
            LEFT JOIN Production.Batches b ON b.Id=s.BatchId
            WHERE s.CustomerId=@CustomerId AND s.LineId=@LineId AND s.IsDeleted=0
              AND (s.IsStoppage=1 OR (b.IsDeleted=0 AND b.Status NOT IN ('cancelled','split')))
              AND s.ScheduledStart < @End AND s.ScheduledEnd > @Start
        ) AND NOT EXISTS (
            SELECT 1 FROM Production.Batches b
            WHERE b.CustomerId=@CustomerId AND b.LineId=@LineId AND b.IsDeleted=0
              AND b.Status NOT IN ('cancelled','split') AND b.PlannedStart < @End AND b.PlannedEnd > @Start
              AND NOT EXISTS (SELECT 1 FROM Planning.ProductionSchedules s WHERE s.BatchId=b.Id AND s.IsDeleted=0 AND s.IsStoppage=0)
        ) BREAK;
        SET @Start=DATEADD(minute,15,@Start);
    END;
    IF @N >= 7
        INSERT Planning.ProductionSchedules (CustomerId,BatchId,LineId,ScheduledStart,ScheduledEnd,IsStoppage,Title,StoppageType,Notes,IsDeleted,CreatedAt)
        VALUES (@CustomerId,NULL,@LineId,@Start,@End,1,
            CASE @N WHEN 7 THEN 'Demo planned maintenance' WHEN 8 THEN 'Demo planned cleaning' ELSE 'Demo planned calibration' END,
            CASE @N WHEN 7 THEN 'maintenance' WHEN 8 THEN 'cleaning' ELSE 'calibration' END,@Uid,0,@Now);
    ELSE
    BEGIN
        SET @Status=CASE @N WHEN 1 THEN 'finished' WHEN 2 THEN 'production' WHEN 4 THEN 'cancelled' WHEN 6 THEN 'in_progress' ELSE 'planned' END;
        SET @Stage=CASE @N WHEN 1 THEN 'finished' WHEN 2 THEN 'production' WHEN 6 THEN 'dispense' ELSE 'warehouse' END;
        INSERT Production.Batches (Uid,CustomerId,SkuId,LineId,RouteId,Status,Stage,PlannedQty,ProducedQty,RejectedQty,
            PlannedStart,PlannedEnd,ActualStart,ActualEnd,Priority,Notes,IsDeleted,CreatedAt)
        VALUES (@Uid,@CustomerId,@SkuId,@LineId,@RouteId,@Status,@Stage,1000,
            CASE @N WHEN 1 THEN 950 WHEN 2 THEN 250 WHEN 6 THEN 100 ELSE 0 END,CASE @N WHEN 1 THEN 50 ELSE 0 END,
            @Start,@End,CASE WHEN @N IN (1,2,6) THEN @Start END,CASE WHEN @N=1 THEN @End END,2,
            'Planning UI demo only',0,@Now);
        SET @BatchId=CONVERT(int,SCOPE_IDENTITY());
        INSERT Production.BatchMaterials (BatchId,MaterialId,PlannedQty,DispensedQty,Status,IsDeleted,CreatedAt)
        SELECT @BatchId,MaterialId,10*N,CASE WHEN @N IN (1,2) THEN 10*N ELSE 0 END,
            CASE WHEN @N IN (1,2) THEN 'complete' ELSE 'pending' END,0,@Now FROM @Materials;
        IF @N <> 5
            INSERT Planning.ProductionSchedules (CustomerId,BatchId,LineId,ScheduledStart,ScheduledEnd,IsStoppage,Notes,IsDeleted,CreatedAt)
            VALUES (@CustomerId,@BatchId,@LineId,@Start,@End,0,@Uid,0,@Now);
    END;
    SET @N += 1;
END;

DECLARE @Filter nvarchar(500) = CONCAT('?FactoryId=',@FactoryId,'&LineId=',@LineId,
    '&Duration=5&From=',CONVERT(varchar(19),@FromLocal,126),'%2B03:00&To=',CONVERT(varchar(19),@ToLocal,126),'%2B03:00');
SELECT @CustomerId AS CustomerId,@FactoryId AS FactoryId,@LineId AS LineId,
    @FromLocal AS FromLocal,@ToLocal AS ToLocal,@Commit AS WillCommit,
    CONCAT('/v1/planning/summary',@Filter) AS TestUrl;
SELECT Endpoint, CONCAT('/v1/planning/',Endpoint,@Filter) AS TestUrl
FROM (VALUES ('summary'),('schedule'),('planned-vs-produced'),('batches'),('routes'),('mrp'),('stoppages'),('form-options')) E(Endpoint);
SELECT Id,Uid,Status,Stage,PlannedStart,PlannedEnd,PlannedQty,ProducedQty FROM Production.Batches
WHERE CustomerId=@CustomerId AND LineId=@LineId AND Uid LIKE @Tag+'-%' AND IsDeleted=0 ORDER BY Id;
SELECT Id,Title,StoppageType,ScheduledStart,ScheduledEnd FROM Planning.ProductionSchedules
WHERE CustomerId=@CustomerId AND LineId=@LineId AND Notes LIKE @Tag+'-%' AND IsStoppage=1 AND IsDeleted=0;
SELECT Id,Code,Name,Status FROM Planning.Routes
WHERE CustomerId=@CustomerId AND Id IN (@RouteId,@DraftRouteId);
SELECT M.Name,R.PlanDate,R.RequiredQty,R.AvailableQty,R.GapQty,R.MrpStatus
FROM Planning.MrpRequirements R JOIN MasterData.Materials M ON M.Id=R.MaterialId
WHERE R.CustomerId=@CustomerId AND R.PlanDate=@DemoDate AND R.MaterialId IN (SELECT MaterialId FROM @Materials);
IF @Commit=1 BEGIN COMMIT; PRINT 'Saved planning demo rows. Reuse the exact TestUrl filter on all time-filtered endpoints.'; END
ELSE BEGIN ROLLBACK; PRINT 'DRY RUN: nothing saved. Set @Commit=1 and rerun to save.'; END;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
