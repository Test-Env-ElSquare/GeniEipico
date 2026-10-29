/*
    GeniePark - Current operational/demo data
    ------------------------------------------------------------
    Purpose
      Fill transactional and runtime tables by reusing the existing:
        - customers
        - factories
        - production lines
        - shifts
        - SKUs and BOMs
        - machines, devices, silos, and scales

    This script DOES NOT insert master/definition rows.

    It adds or refreshes:
      - batches in all main statuses
      - batch materials and stage logs
      - production schedules
      - current machine states and current-shift state history
      - OEE snapshots for the last 31 days
      - active, acknowledged, and resolved alarm events
      - alarm waveform telemetry
      - current silo and scale snapshots

    Re-running the script is safe:
      - deterministic batch UIDs are used per day
      - only rows carrying the marker below are refreshed
      - existing user/business rows are not deleted
      - telemetry may append newer readings, as a real stream would
      - telemetry may append newer readings, as a real stream would

    Required migration:
      MoveMachineOeeToSnapshots
*/

USE [GeniePark];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2(7) = SYSUTCDATETIME();
DECLARE @Today date = CONVERT(date, @Now);
DECLARE @TodayKey char(8) = CONVERT(char(8), @Today, 112);
DECLARE @YearMonth char(4) = SUBSTRING(CONVERT(char(8), @Today, 112), 3, 4);
DECLARE @Marker nvarchar(80) = N'SCADA_RUNTIME_SEED_V3|' + @TodayKey;
DECLARE @HistorySource varchar(30) = 'runtime_seed_v3';

IF DB_NAME() <> N'GeniePark'
    THROW 51000, 'Run this script only against the GeniePark database.', 1;

IF COL_LENGTH('Analytics.OeeSnapshots', 'AverageSpeedRpm') IS NULL
    THROW 51001, 'Apply migration MoveMachineOeeToSnapshots before running this script.', 1;

IF COL_LENGTH('Scada.MachineStates', 'OeePct') IS NOT NULL
    THROW 51002, 'The old Scada.MachineStates.OeePct column still exists. Apply the latest migration first.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM Org.Customers
    WHERE IsActive = 1 AND IsDeleted = 0
)
    THROW 51003, 'No active customers were found.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM Scada.Machines
    WHERE IsActive = 1 AND IsDeleted = 0
)
    THROW 51004, 'No active machines were found.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM MasterData.BillOfMaterials
    WHERE IsDeleted = 0
)
    THROW 51005, 'No active BOM rows were found.', 1;

DECLARE
    @BatchesInserted int = 0,
    @BatchesUpdated int = 0,
    @BatchMaterialsInserted int = 0,
    @BatchMaterialsUpdated int = 0,
    @StageLogsInserted int = 0,
    @SchedulesInserted int = 0,
    @MachineStatesInserted int = 0,
    @MachineStatesUpdated int = 0,
    @MachineHistoryInserted int = 0,
    @OeeInserted int = 0,
    @OeeSpeedBackfilled int = 0,
    @AlarmEventsInserted int = 0,
    @AlarmEventsUpdated int = 0,
    @TelemetryRawInserted int = 0,
    @TelemetryTagsInserted int = 0,
    @SilosUpdated int = 0,
    @ScalesUpdated int = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    --------------------------------------------------------------------------
    -- 1) Resolve the current shift independently for every existing factory
    --------------------------------------------------------------------------

    CREATE TABLE #CurrentShifts
    (
        FactoryId int NOT NULL PRIMARY KEY,
        ShiftId int NOT NULL,
        ShiftName nvarchar(200) NOT NULL,
        ShiftStart datetime2(7) NOT NULL,
        ShiftEnd datetime2(7) NOT NULL
    );

    ;WITH ShiftCandidates AS
    (
        SELECT
            sh.FactoryId,
            sh.Id AS ShiftId,
            sh.Name AS ShiftName,
            sh.StartTime,
            sh.EndTime,
            CASE
                WHEN sh.StartTime = sh.EndTime THEN 1
                WHEN sh.StartTime < sh.EndTime
                     AND CONVERT(time, @Now) >= sh.StartTime
                     AND CONVERT(time, @Now) < sh.EndTime THEN 1
                WHEN sh.StartTime > sh.EndTime
                     AND
                     (
                         CONVERT(time, @Now) >= sh.StartTime
                         OR CONVERT(time, @Now) < sh.EndTime
                     ) THEN 1
                ELSE 0
            END AS IsCurrent
        FROM Planning.Shifts sh
        JOIN Org.Factories factory ON factory.Id = sh.FactoryId
        WHERE sh.IsActive = 1
          AND sh.IsDeleted = 0
          AND factory.IsActive = 1
          AND factory.IsDeleted = 0
    ),
    Resolved AS
    (
        SELECT
            candidate.FactoryId,
            candidate.ShiftId,
            candidate.ShiftName,
            CASE
                WHEN candidate.StartTime > candidate.EndTime
                     AND CONVERT(time, @Now) < candidate.EndTime
                    THEN DATEADD
                    (
                        SECOND,
                        DATEDIFF(SECOND, CONVERT(time, '00:00:00'), candidate.StartTime),
                        CONVERT(datetime2, DATEADD(DAY, -1, @Today))
                    )
                ELSE DATEADD
                (
                    SECOND,
                    DATEDIFF(SECOND, CONVERT(time, '00:00:00'), candidate.StartTime),
                    CONVERT(datetime2, @Today)
                )
            END AS ShiftStart,
            CASE
                WHEN candidate.StartTime = candidate.EndTime
                    THEN DATEADD
                    (
                        DAY,
                        1,
                        DATEADD
                        (
                            SECOND,
                            DATEDIFF(SECOND, CONVERT(time, '00:00:00'), candidate.EndTime),
                            CONVERT(datetime2, @Today)
                        )
                    )
                WHEN candidate.StartTime > candidate.EndTime
                     AND CONVERT(time, @Now) >= candidate.StartTime
                    THEN DATEADD
                    (
                        DAY,
                        1,
                        DATEADD
                        (
                            SECOND,
                            DATEDIFF(SECOND, CONVERT(time, '00:00:00'), candidate.EndTime),
                            CONVERT(datetime2, @Today)
                        )
                    )
                ELSE DATEADD
                (
                    SECOND,
                    DATEDIFF(SECOND, CONVERT(time, '00:00:00'), candidate.EndTime),
                    CONVERT(datetime2, @Today)
                )
            END AS ShiftEnd
        FROM ShiftCandidates candidate
        WHERE candidate.IsCurrent = 1
    ),
    Numbered AS
    (
        SELECT
            resolved.*,
            ROW_NUMBER() OVER
            (
                PARTITION BY resolved.FactoryId
                ORDER BY resolved.ShiftStart DESC, resolved.ShiftId
            ) AS RowNumber
        FROM Resolved resolved
    )
    INSERT INTO #CurrentShifts
        (FactoryId, ShiftId, ShiftName, ShiftStart, ShiftEnd)
    SELECT FactoryId, ShiftId, ShiftName, ShiftStart, ShiftEnd
    FROM Numbered
    WHERE RowNumber = 1;

    IF NOT EXISTS (SELECT 1 FROM #CurrentShifts)
        THROW 51006, 'No current factory shift could be resolved.', 1;

    --------------------------------------------------------------------------
    -- 2) Pick an existing SKU with a BOM for every active production line
    --------------------------------------------------------------------------

    CREATE TABLE #LineSeedBase
    (
        CustomerId int NOT NULL,
        CountryCode varchar(3) NOT NULL,
        FactoryId int NOT NULL,
        LineId int NOT NULL PRIMARY KEY,
        ShiftId int NOT NULL,
        ShiftStart datetime2(7) NOT NULL,
        ShiftEnd datetime2(7) NOT NULL,
        SkuId int NOT NULL,
        StandardBatchSize int NOT NULL,
        RouteId int NULL
    );

    INSERT INTO #LineSeedBase
        (CustomerId, CountryCode, FactoryId, LineId, ShiftId, ShiftStart,
         ShiftEnd, SkuId, StandardBatchSize, RouteId)
    SELECT
        customer.Id,
        LEFT(COALESCE(NULLIF(customer.CountryCode, ''), 'EGY'), 3),
        factory.Id,
        line.Id,
        currentShift.ShiftId,
        currentShift.ShiftStart,
        currentShift.ShiftEnd,
        sku.Id,
        sku.StandardBatchSize,
        route.Id
    FROM Org.ProductionLines line
    JOIN Org.Factories factory
      ON factory.Id = line.FactoryId
     AND factory.IsActive = 1
     AND factory.IsDeleted = 0
    JOIN Org.Customers customer
      ON customer.Id = factory.CustomerId
     AND customer.IsActive = 1
     AND customer.IsDeleted = 0
    JOIN #CurrentShifts currentShift
      ON currentShift.FactoryId = factory.Id
    CROSS APPLY
    (
        SELECT TOP (1)
            candidateSku.Id,
            candidateSku.StandardBatchSize
        FROM MasterData.Skus candidateSku
        WHERE candidateSku.CustomerId = customer.Id
          AND candidateSku.IsActive = 1
          AND candidateSku.IsDeleted = 0
          AND candidateSku.StandardBatchSize > 0
          AND EXISTS
          (
              SELECT 1
              FROM MasterData.BillOfMaterials bom
              WHERE bom.SkuId = candidateSku.Id
                AND bom.IsDeleted = 0
          )
        ORDER BY
        (
            SELECT COUNT(*)
            FROM MasterData.BillOfMaterials bomCount
            WHERE bomCount.SkuId = candidateSku.Id
              AND bomCount.IsDeleted = 0
        ) DESC,
        candidateSku.Id
    ) sku
    OUTER APPLY
    (
        SELECT TOP (1) productionRoute.Id
        FROM Planning.Routes productionRoute
        WHERE productionRoute.CustomerId = customer.Id
          AND productionRoute.SkuId = sku.Id
          AND productionRoute.IsDeleted = 0
        ORDER BY
            CASE WHEN productionRoute.Status = 'active' THEN 0 ELSE 1 END,
            productionRoute.Version DESC,
            productionRoute.Id DESC
    ) route
    WHERE line.IsActive = 1
      AND line.IsDeleted = 0;

    IF NOT EXISTS (SELECT 1 FROM #LineSeedBase)
        THROW 51007, 'No active line has an active SKU with a BOM.', 1;

    IF EXISTS (SELECT 1 FROM #LineSeedBase WHERE LineId > 99)
        THROW 51009, 'The deterministic demo UID range supports LineId values up to 99.', 1;

    --------------------------------------------------------------------------
    -- 3) Add five current operational batches per eligible line
    --------------------------------------------------------------------------

    CREATE TABLE #BatchPlan
    (
        Uid varchar(30) NOT NULL PRIMARY KEY,
        CustomerId int NOT NULL,
        FactoryId int NOT NULL,
        LineId int NOT NULL,
        SkuId int NOT NULL,
        RouteId int NULL,
        ShiftId int NOT NULL,
        Status varchar(20) NOT NULL,
        Stage varchar(20) NOT NULL,
        PlannedQty int NOT NULL,
        ProducedQty int NOT NULL,
        RejectedQty int NOT NULL,
        PlannedStart datetime2(7) NOT NULL,
        PlannedEnd datetime2(7) NULL,
        ActualStart datetime2(7) NULL,
        ActualEnd datetime2(7) NULL,
        Priority smallint NOT NULL,
        Notes nvarchar(max) NOT NULL
    );

    ;WITH StatusSeed AS
    (
        SELECT *
        FROM
        (
            VALUES
                (1, 'planned',     'warehouse',  1.00, 3),
                (2, 'in_progress', 'dispense',   0.75, 2),
                (3, 'production',  'production', 1.10, 1),
                (4, 'finished',    'finished',   0.90, 4),
                (5, 'cancelled',   'warehouse',  0.60, 5)
        ) value(StatusOrder, Status, Stage, QuantityFactor, Priority)
    ),
    RawPlan AS
    (
        SELECT
            CONCAT
            (
                'BT-',
                @YearMonth,
                '-',
                RIGHT
                (
                    '00000' + CONVERT
                    (
                        varchar(10),
                        60000
                        + (DAY(@Today) * 1000)
                        + (lineSeed.LineId * 10)
                        + statusSeed.StatusOrder
                    ),
                    5
                ),
                '-',
                lineSeed.CountryCode
            ) AS Uid,
            lineSeed.CustomerId,
            lineSeed.FactoryId,
            lineSeed.LineId,
            lineSeed.SkuId,
            lineSeed.RouteId,
            lineSeed.ShiftId,
            lineSeed.ShiftStart,
            lineSeed.ShiftEnd,
            statusSeed.StatusOrder,
            statusSeed.Status,
            statusSeed.Stage,
            CONVERT
            (
                int,
                ROUND(lineSeed.StandardBatchSize * statusSeed.QuantityFactor, 0)
            ) AS PlannedQty,
            CONVERT(smallint, statusSeed.Priority) AS Priority,
            CASE
                WHEN DATEDIFF(MINUTE, lineSeed.ShiftStart, @Now) < 12 THEN 12
                ELSE DATEDIFF(MINUTE, lineSeed.ShiftStart, @Now)
            END AS ElapsedMinutes
        FROM #LineSeedBase lineSeed
        CROSS JOIN StatusSeed statusSeed
    ),
    TimedPlan AS
    (
        SELECT
            rawPlan.*,
            CASE rawPlan.Status
                WHEN 'planned'
                    THEN DATEADD(MINUTE, 30 + (rawPlan.LineId % 20), @Now)
                WHEN 'in_progress'
                    THEN DATEADD
                    (
                        MINUTE,
                        rawPlan.ElapsedMinutes / 3,
                        rawPlan.ShiftStart
                    )
                WHEN 'production'
                    THEN DATEADD
                    (
                        MINUTE,
                        rawPlan.ElapsedMinutes / 4,
                        rawPlan.ShiftStart
                    )
                WHEN 'finished'
                    THEN DATEADD
                    (
                        MINUTE,
                        rawPlan.ElapsedMinutes / 6,
                        rawPlan.ShiftStart
                    )
                ELSE DATEADD(MINUTE, 5 + (rawPlan.LineId % 10), rawPlan.ShiftStart)
            END AS EffectiveStart
        FROM RawPlan rawPlan
    )
    INSERT INTO #BatchPlan
        (Uid, CustomerId, FactoryId, LineId, SkuId, RouteId, ShiftId,
         Status, Stage, PlannedQty, ProducedQty, RejectedQty,
         PlannedStart, PlannedEnd, ActualStart, ActualEnd, Priority, Notes)
    SELECT
        timedPlan.Uid,
        timedPlan.CustomerId,
        timedPlan.FactoryId,
        timedPlan.LineId,
        timedPlan.SkuId,
        timedPlan.RouteId,
        timedPlan.ShiftId,
        timedPlan.Status,
        timedPlan.Stage,
        timedPlan.PlannedQty,
        CASE
            WHEN timedPlan.Status = 'production'
                THEN CONVERT(int, ROUND(timedPlan.PlannedQty * 0.55, 0))
            WHEN timedPlan.Status = 'finished'
                THEN timedPlan.PlannedQty - CONVERT
                (
                    int,
                    ROUND(timedPlan.PlannedQty * 0.012, 0)
                )
            ELSE 0
        END,
        CASE
            WHEN timedPlan.Status = 'production'
                THEN CONVERT(int, ROUND(timedPlan.PlannedQty * 0.004, 0))
            WHEN timedPlan.Status = 'finished'
                THEN CONVERT(int, ROUND(timedPlan.PlannedQty * 0.012, 0))
            ELSE 0
        END,
        timedPlan.EffectiveStart,
        CASE
            WHEN timedPlan.Status = 'planned'
                THEN DATEADD(HOUR, 6, timedPlan.EffectiveStart)
            ELSE DATEADD
            (
                MINUTE,
                timedPlan.ElapsedMinutes + 120,
                timedPlan.ShiftStart
            )
        END,
        CASE
            WHEN timedPlan.Status IN ('in_progress', 'production', 'finished')
                THEN timedPlan.EffectiveStart
            ELSE NULL
        END,
        CASE
            WHEN timedPlan.Status = 'finished'
                THEN DATEADD
                (
                    MINUTE,
                    (timedPlan.ElapsedMinutes * 3) / 4,
                    timedPlan.ShiftStart
                )
            ELSE NULL
        END,
        timedPlan.Priority,
        CONCAT
        (
            @Marker,
            '|BATCH|',
            UPPER(timedPlan.Status),
            '|LINE:',
            timedPlan.LineId
        )
    FROM TimedPlan timedPlan;

    IF EXISTS
    (
        SELECT 1
        FROM Production.Batches batch
        JOIN #BatchPlan plan ON plan.Uid = batch.Uid
        WHERE batch.Notes IS NULL
           OR LEFT(batch.Notes, LEN(@Marker)) <> @Marker
    )
        THROW 51008, 'A generated batch UID is already used by a non-seed batch.', 1;

    UPDATE batch
       SET batch.CustomerId = plan.CustomerId,
           batch.SkuId = plan.SkuId,
           batch.LineId = plan.LineId,
           batch.RouteId = plan.RouteId,
           batch.Status = plan.Status,
           batch.Stage = plan.Stage,
           batch.PlannedQty = plan.PlannedQty,
           batch.ProducedQty = plan.ProducedQty,
           batch.RejectedQty = plan.RejectedQty,
           batch.PlannedStart = plan.PlannedStart,
           batch.PlannedEnd = plan.PlannedEnd,
           batch.ActualStart = plan.ActualStart,
           batch.ActualEnd = plan.ActualEnd,
           batch.ShiftId = plan.ShiftId,
           batch.Priority = plan.Priority,
           batch.Notes = plan.Notes,
           batch.IsDeleted = 0,
           batch.DeletedAt = NULL,
           batch.UpdatedAt = @Now
    FROM Production.Batches batch
    JOIN #BatchPlan plan ON plan.Uid = batch.Uid;
    SET @BatchesUpdated = @@ROWCOUNT;

    INSERT INTO Production.Batches
        (Uid, CustomerId, SkuId, LineId, RouteId, Status, Stage,
         PlannedQty, ProducedQty, RejectedQty, PlannedStart, PlannedEnd,
         ActualStart, ActualEnd, ShiftId, Priority, Notes)
    SELECT
        plan.Uid,
        plan.CustomerId,
        plan.SkuId,
        plan.LineId,
        plan.RouteId,
        plan.Status,
        plan.Stage,
        plan.PlannedQty,
        plan.ProducedQty,
        plan.RejectedQty,
        plan.PlannedStart,
        plan.PlannedEnd,
        plan.ActualStart,
        plan.ActualEnd,
        plan.ShiftId,
        plan.Priority,
        plan.Notes
    FROM #BatchPlan plan
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Production.Batches existing
        WHERE existing.Uid = plan.Uid
          AND existing.IsDeleted = 0
    );
    SET @BatchesInserted = @@ROWCOUNT;

    SELECT
        batch.Id AS BatchId,
        plan.*
    INTO #SeedBatches
    FROM #BatchPlan plan
    JOIN Production.Batches batch
      ON batch.Uid = plan.Uid
     AND batch.IsDeleted = 0;

    --------------------------------------------------------------------------
    -- 4) Create the BOM snapshot for every generated batch
    --------------------------------------------------------------------------

    CREATE TABLE #BatchMaterialPlan
    (
        BatchId int NOT NULL,
        MaterialId int NOT NULL,
        MaterialUid varchar(30) NOT NULL,
        SiloId int NULL,
        ScaleId int NULL,
        PlannedQty decimal(12,4) NOT NULL,
        DispensedQty decimal(12,4) NOT NULL,
        DeviationPct decimal(6,3) NULL,
        DispenseStartedAt datetime2(7) NULL,
        DispenseCompletedAt datetime2(7) NULL,
        Status varchar(20) NOT NULL,
        PRIMARY KEY (BatchId, MaterialId)
    );

    ;WITH BomRows AS
    (
        SELECT
            seedBatch.BatchId,
            seedBatch.FactoryId,
            seedBatch.Status AS BatchStatus,
            seedBatch.ActualStart,
            bom.MaterialId,
            bom.QtyPerBatch,
            sku.StandardBatchSize,
            ROW_NUMBER() OVER
            (
                PARTITION BY seedBatch.BatchId
                ORDER BY COALESCE(bom.SequenceNo, 32767), bom.MaterialId
            ) AS MaterialOrder
        FROM #SeedBatches seedBatch
        JOIN MasterData.Skus sku
          ON sku.Id = seedBatch.SkuId
         AND sku.IsDeleted = 0
        JOIN MasterData.BillOfMaterials bom
          ON bom.SkuId = seedBatch.SkuId
         AND bom.IsDeleted = 0
    ),
    Calculated AS
    (
        SELECT
            bomRow.*,
            seedBatch.PlannedQty AS BatchPlannedQty,
            CONVERT
            (
                decimal(12,4),
                ROUND
                (
                    bomRow.QtyPerBatch
                    * CONVERT(decimal(18,6), seedBatch.PlannedQty)
                    / NULLIF(bomRow.StandardBatchSize, 0),
                    4
                )
            ) AS MaterialPlannedQty
        FROM BomRows bomRow
        JOIN #SeedBatches seedBatch
          ON seedBatch.BatchId = bomRow.BatchId
    ),
    Quantities AS
    (
        SELECT
            calculated.*,
            CONVERT
            (
                decimal(12,4),
                CASE
                    WHEN calculated.BatchStatus IN ('production', 'finished')
                        THEN calculated.MaterialPlannedQty
                    WHEN calculated.BatchStatus = 'in_progress'
                         AND calculated.MaterialOrder = 1
                        THEN calculated.MaterialPlannedQty
                    WHEN calculated.BatchStatus = 'in_progress'
                         AND calculated.MaterialOrder = 2
                        THEN ROUND(calculated.MaterialPlannedQty * 0.45, 4)
                    ELSE 0
                END
            ) AS MaterialDispensedQty
        FROM Calculated calculated
    )
    INSERT INTO #BatchMaterialPlan
        (BatchId, MaterialId, MaterialUid, SiloId, ScaleId,
         PlannedQty, DispensedQty, DeviationPct,
         DispenseStartedAt, DispenseCompletedAt, Status)
    SELECT
        quantity.BatchId,
        quantity.MaterialId,
        CONCAT
        (
            'RM-',
            RIGHT(@TodayKey, 6),
            '-',
            RIGHT('00000' + CONVERT(varchar(10), quantity.BatchId), 5),
            '-',
            RIGHT('000' + CONVERT(varchar(10), quantity.MaterialId), 3)
        ),
        silo.Id,
        CASE
            WHEN quantity.BatchStatus IN ('in_progress', 'production', 'finished')
                 AND quantity.MaterialDispensedQty > 0
                THEN scale.Id
            ELSE NULL
        END,
        quantity.MaterialPlannedQty,
        quantity.MaterialDispensedQty,
        CASE
            WHEN quantity.MaterialDispensedQty <= 0
                 OR quantity.MaterialPlannedQty <= 0
                THEN NULL
            ELSE CONVERT
            (
                decimal(6,3),
                ROUND
                (
                    (quantity.MaterialDispensedQty - quantity.MaterialPlannedQty)
                    / quantity.MaterialPlannedQty * 100,
                    3
                )
            )
        END,
        CASE
            WHEN quantity.MaterialDispensedQty > 0
                THEN DATEADD
                (
                    MINUTE,
                    5 + quantity.MaterialOrder,
                    quantity.ActualStart
                )
            ELSE NULL
        END,
        CASE
            WHEN quantity.MaterialDispensedQty = quantity.MaterialPlannedQty
                THEN DATEADD
                (
                    MINUTE,
                    10 + quantity.MaterialOrder,
                    quantity.ActualStart
                )
            ELSE NULL
        END,
        CASE
            WHEN quantity.MaterialDispensedQty = quantity.MaterialPlannedQty
                 AND quantity.MaterialPlannedQty > 0
                THEN 'complete'
            ELSE 'pending'
        END
    FROM Quantities quantity
    OUTER APPLY
    (
        SELECT TOP (1) candidateSilo.Id
        FROM Scada.Silos candidateSilo
        WHERE candidateSilo.FactoryId = quantity.FactoryId
          AND candidateSilo.CurrentMaterialId = quantity.MaterialId
          AND candidateSilo.IsDeleted = 0
        ORDER BY candidateSilo.Id
    ) silo
    OUTER APPLY
    (
        SELECT TOP (1) candidateScale.Id
        FROM Scada.Scales candidateScale
        WHERE candidateScale.FactoryId = quantity.FactoryId
          AND candidateScale.IsDeleted = 0
        ORDER BY candidateScale.Id
    ) scale;

    UPDATE batchMaterial
       SET batchMaterial.MaterialUid = plan.MaterialUid,
           batchMaterial.SiloId = plan.SiloId,
           batchMaterial.ScaleId = plan.ScaleId,
           batchMaterial.PlannedQty = plan.PlannedQty,
           batchMaterial.DispensedQty = plan.DispensedQty,
           batchMaterial.DeviationPct = plan.DeviationPct,
           batchMaterial.DispenseStartedAt = plan.DispenseStartedAt,
           batchMaterial.DispenseCompletedAt = plan.DispenseCompletedAt,
           batchMaterial.Status = plan.Status,
           batchMaterial.IsDeleted = 0,
           batchMaterial.DeletedAt = NULL,
           batchMaterial.UpdatedAt = @Now
    FROM Production.BatchMaterials batchMaterial
    JOIN #BatchMaterialPlan plan
      ON plan.BatchId = batchMaterial.BatchId
     AND plan.MaterialId = batchMaterial.MaterialId;
    SET @BatchMaterialsUpdated = @@ROWCOUNT;

    INSERT INTO Production.BatchMaterials
        (BatchId, MaterialId, MaterialUid, SiloId, ScaleId,
         PlannedQty, DispensedQty, DeviationPct, DispenseStartedAt,
         DispenseCompletedAt, DispensedById, Status)
    SELECT
        plan.BatchId,
        plan.MaterialId,
        plan.MaterialUid,
        plan.SiloId,
        plan.ScaleId,
        plan.PlannedQty,
        plan.DispensedQty,
        plan.DeviationPct,
        plan.DispenseStartedAt,
        plan.DispenseCompletedAt,
        NULL,
        plan.Status
    FROM #BatchMaterialPlan plan
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Production.BatchMaterials existing
        WHERE existing.BatchId = plan.BatchId
          AND existing.MaterialId = plan.MaterialId
          AND existing.IsDeleted = 0
    );
    SET @BatchMaterialsInserted = @@ROWCOUNT;

    --------------------------------------------------------------------------
    -- 5) Add stage timelines and production schedules
    --------------------------------------------------------------------------

    CREATE TABLE #StageLogPlan
    (
        BatchId int NOT NULL,
        StepOrder int NOT NULL,
        FromStage varchar(20) NULL,
        ToStage varchar(20) NOT NULL,
        TransitionedAt datetime2(7) NOT NULL,
        MachineId int NULL,
        Notes nvarchar(max) NOT NULL,
        PRIMARY KEY (BatchId, StepOrder)
    );

    INSERT INTO #StageLogPlan
        (BatchId, StepOrder, FromStage, ToStage,
         TransitionedAt, MachineId, Notes)
    SELECT
        seedBatch.BatchId,
        stage.StepOrder,
        stage.FromStage,
        stage.ToStage,
        CASE stage.StepOrder
            WHEN 1 THEN CASE
                WHEN seedBatch.Status = 'planned' THEN @Now
                ELSE DATEADD(MINUTE, -5, seedBatch.PlannedStart)
            END
            WHEN 2 THEN DATEADD(MINUTE, 5, seedBatch.ActualStart)
            WHEN 3 THEN DATEADD(MINUTE, 15, seedBatch.ActualStart)
            ELSE seedBatch.ActualEnd
        END,
        CASE WHEN stage.StepOrder = 1 THEN NULL ELSE machine.Id END,
        CONCAT
        (
            @Marker,
            '|STAGE|',
            seedBatch.BatchId,
            '|',
            stage.StepOrder,
            '|',
            stage.ToStage
        )
    FROM #SeedBatches seedBatch
    CROSS APPLY
    (
        SELECT *
        FROM
        (
            VALUES
                (1, CONVERT(varchar(20), NULL), 'warehouse'),
                (2, 'warehouse', 'dispense'),
                (3, 'dispense', 'production'),
                (4, 'production', 'finished')
        ) value(StepOrder, FromStage, ToStage)
        WHERE value.StepOrder = 1
           OR
           (
               value.StepOrder = 2
               AND seedBatch.Status IN ('in_progress', 'production', 'finished')
           )
           OR
           (
               value.StepOrder = 3
               AND seedBatch.Status IN ('production', 'finished')
           )
           OR
           (
               value.StepOrder = 4
               AND seedBatch.Status = 'finished'
           )
    ) stage
    OUTER APPLY
    (
        SELECT TOP (1) candidateMachine.Id
        FROM Scada.Machines candidateMachine
        WHERE candidateMachine.LineId = seedBatch.LineId
          AND candidateMachine.IsActive = 1
          AND candidateMachine.IsDeleted = 0
        ORDER BY candidateMachine.Id
    ) machine;

    INSERT INTO Production.BatchStageLogs
        (BatchId, FromStage, ToStage, TransitionedAt,
         TransitionedById, MachineId, Notes)
    SELECT
        plan.BatchId,
        plan.FromStage,
        plan.ToStage,
        plan.TransitionedAt,
        NULL,
        plan.MachineId,
        plan.Notes
    FROM #StageLogPlan plan
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Production.BatchStageLogs existing
        WHERE existing.Notes = plan.Notes
    );
    SET @StageLogsInserted = @@ROWCOUNT;

    INSERT INTO Planning.ProductionSchedules
        (CustomerId, BatchId, LineId, ScheduledStart, ScheduledEnd,
         IsStoppage, StoppageType, Notes)
    SELECT
        seedBatch.CustomerId,
        seedBatch.BatchId,
        seedBatch.LineId,
        seedBatch.PlannedStart,
        COALESCE(seedBatch.PlannedEnd, DATEADD(HOUR, 6, seedBatch.PlannedStart)),
        0,
        NULL,
        CONCAT(@Marker, '|SCHEDULE|', seedBatch.BatchId)
    FROM #SeedBatches seedBatch
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Planning.ProductionSchedules existing
        WHERE existing.BatchId = seedBatch.BatchId
          AND existing.IsDeleted = 0
    );
    SET @SchedulesInserted = @@ROWCOUNT;

    --------------------------------------------------------------------------
    -- 6) Refresh current SCADA snapshots without adding new definitions
    --------------------------------------------------------------------------

    CREATE TABLE #MachineRuntimePlan
    (
        MachineId int NOT NULL PRIMARY KEY,
        FactoryId int NOT NULL,
        LineId int NOT NULL,
        DeviceId int NULL,
        ShiftId int NOT NULL,
        ShiftStart datetime2(7) NOT NULL,
        ShiftEnd datetime2(7) NOT NULL,
        State varchar(20) NOT NULL,
        SpeedRpm int NULL,
        TempCelsius decimal(6,2) NULL,
        VibrationMms decimal(6,3) NULL,
        EnergyKwh decimal(10,3) NULL,
        ActiveBatchId int NULL,
        StateStartedAt datetime2(7) NOT NULL,
        TelemetryAt datetime2(7) NOT NULL,
        LastPayload nvarchar(max) NULL
    );

    INSERT INTO #MachineRuntimePlan
        (MachineId, FactoryId, LineId, DeviceId, ShiftId, ShiftStart,
         ShiftEnd, State, SpeedRpm, TempCelsius, VibrationMms, EnergyKwh,
         ActiveBatchId, StateStartedAt, TelemetryAt, LastPayload)
    SELECT
        machine.Id,
        line.FactoryId,
        machine.LineId,
        machine.DeviceId,
        currentShift.ShiftId,
        currentShift.ShiftStart,
        currentShift.ShiftEnd,
        runtimeState.State,
        CASE runtimeState.State
            WHEN 'running'
                THEN CONVERT
                (
                    int,
                    ROUND
                    (
                        COALESCE(machine.RatedSpeedRpm, 1000)
                        * (0.72 + ((machine.Id % 18) / 100.0)),
                        0
                    )
                )
            WHEN 'changeover'
                THEN CONVERT(int, COALESCE(machine.RatedSpeedRpm, 1000) * 0.18)
            ELSE 0
        END,
        CONVERT
        (
            decimal(6,2),
            CASE runtimeState.State
                WHEN 'fault' THEN 78.00 + (machine.Id % 5)
                ELSE 39.00 + (machine.Id % 15)
            END
        ),
        CONVERT
        (
            decimal(6,3),
            CASE runtimeState.State
                WHEN 'fault' THEN 7.200 + ((machine.Id % 4) / 10.0)
                ELSE 1.100 + ((machine.Id % 25) / 10.0)
            END
        ),
        CONVERT(decimal(10,3), 90.000 + (machine.Id * 3.750)),
        CASE
            WHEN runtimeState.State = 'idle' THEN NULL
            ELSE productionBatch.BatchId
        END,
        DATEADD
        (
            MINUTE,
            CASE
                WHEN DATEDIFF(MINUTE, currentShift.ShiftStart, @Now) < 10 THEN 1
                ELSE (DATEDIFF(MINUTE, currentShift.ShiftStart, @Now) * 2) / 3
            END,
            currentShift.ShiftStart
        ),
        DATEADD(SECOND, -(machine.Id % 90), @Now),
        CONCAT
        (
            '{"source":"',
            @HistorySource,
            '","state":"',
            runtimeState.State,
            '","machineId":',
            machine.Id,
            '}'
        )
    FROM Scada.Machines machine
    JOIN Org.ProductionLines line
      ON line.Id = machine.LineId
     AND line.IsActive = 1
     AND line.IsDeleted = 0
    JOIN #CurrentShifts currentShift
      ON currentShift.FactoryId = line.FactoryId
    CROSS APPLY
    (
        SELECT
            CASE machine.Id % 8
                WHEN 0 THEN 'fault'
                WHEN 1 THEN 'changeover'
                WHEN 2 THEN 'idle'
                ELSE 'running'
            END AS State
    ) runtimeState
    OUTER APPLY
    (
        SELECT TOP (1) seedBatch.BatchId
        FROM #SeedBatches seedBatch
        WHERE seedBatch.LineId = machine.LineId
          AND seedBatch.Status = 'production'
        ORDER BY seedBatch.BatchId DESC
    ) productionBatch
    WHERE machine.IsActive = 1
      AND machine.IsDeleted = 0;

    UPDATE machineState
       SET machineState.State = plan.State,
           machineState.SpeedRpm = plan.SpeedRpm,
           machineState.TempCelsius = plan.TempCelsius,
           machineState.VibrationMms = plan.VibrationMms,
           machineState.EnergyKwh = plan.EnergyKwh,
           machineState.ActiveBatchId = plan.ActiveBatchId,
           machineState.StateStartedAt = plan.StateStartedAt,
           machineState.TelemetryAt = plan.TelemetryAt,
           machineState.LastPayload = plan.LastPayload,
           machineState.UpdatedAt = @Now
    FROM Scada.MachineStates machineState
    JOIN #MachineRuntimePlan plan
      ON plan.MachineId = machineState.MachineId;
    SET @MachineStatesUpdated = @@ROWCOUNT;

    INSERT INTO Scada.MachineStates
        (MachineId, State, SpeedRpm, TempCelsius, VibrationMms, EnergyKwh,
         ActiveBatchId, StateStartedAt, TelemetryAt, LastPayload)
    SELECT
        plan.MachineId,
        plan.State,
        plan.SpeedRpm,
        plan.TempCelsius,
        plan.VibrationMms,
        plan.EnergyKwh,
        plan.ActiveBatchId,
        plan.StateStartedAt,
        plan.TelemetryAt,
        plan.LastPayload
    FROM #MachineRuntimePlan plan
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Scada.MachineStates existing
        WHERE existing.MachineId = plan.MachineId
    );
    SET @MachineStatesInserted = @@ROWCOUNT;

    /*
        Remove only this script's current-shift history before rebuilding it.
        No user, PLC, or older seed history is removed.
    */
    DELETE history
    FROM Scada.MachineStateHistories history
    JOIN #MachineRuntimePlan plan
      ON plan.MachineId = history.MachineId
    WHERE history.TriggerSource = @HistorySource
      AND history.TransitionedAt >= plan.ShiftStart
      AND history.TransitionedAt < plan.ShiftEnd;

    INSERT INTO Scada.MachineStateHistories
        (MachineId, FromState, ToState, TransitionedAt,
         TriggerSource, TriggeredByUserId, Notes)
    SELECT
        plan.MachineId,
        transition.FromState,
        transition.ToState,
        transition.TransitionedAt,
        @HistorySource,
        NULL,
        CONCAT
        (
            @Marker,
            '|MACHINE_HISTORY|',
            plan.MachineId,
            '|',
            transition.StepOrder
        )
    FROM #MachineRuntimePlan plan
    CROSS APPLY
    (
        SELECT *
        FROM
        (
            VALUES
                (
                    1,
                    CONVERT(varchar(20), 'offline'),
                    CONVERT(varchar(20), 'idle'),
                    plan.ShiftStart
                ),
                (
                    2,
                    CONVERT(varchar(20), 'idle'),
                    CONVERT(varchar(20), 'running'),
                    DATEADD
                    (
                        MINUTE,
                        CASE
                            WHEN DATEDIFF(MINUTE, plan.ShiftStart, @Now) < 9 THEN 3
                            ELSE DATEDIFF(MINUTE, plan.ShiftStart, @Now) / 3
                        END,
                        plan.ShiftStart
                    )
                ),
                (
                    3,
                    CONVERT(varchar(20), 'running'),
                    plan.State,
                    plan.StateStartedAt
                )
        ) value(StepOrder, FromState, ToState, TransitionedAt)
        WHERE value.StepOrder < 3
           OR plan.State <> 'running'
    ) transition
    WHERE transition.TransitionedAt <= @Now;
    SET @MachineHistoryInserted = @@ROWCOUNT;

    UPDATE device
       SET device.ConnectionStatus = 'online',
           device.LastSeenAt = plan.TelemetryAt,
           device.UpdatedAt = @Now
    FROM IoT.Devices device
    JOIN #MachineRuntimePlan plan
      ON plan.DeviceId = device.Id
    WHERE device.IsDeleted = 0;

    --------------------------------------------------------------------------
    -- 7) Add OEE aggregates for all shifts during the last 31 days
    --------------------------------------------------------------------------

    CREATE TABLE #CalendarDates
    (
        CalendarDate date NOT NULL PRIMARY KEY
    );

    ;WITH DayNumbers AS
    (
        SELECT 0 AS DayOffset
        UNION ALL
        SELECT DayOffset + 1
        FROM DayNumbers
        WHERE DayOffset < 30
    )
    INSERT INTO #CalendarDates (CalendarDate)
    SELECT DATEADD(DAY, -DayOffset, @Today)
    FROM DayNumbers
    OPTION (MAXRECURSION 31);

    CREATE TABLE #OeePlan
    (
        MachineId int NOT NULL,
        ShiftId int NOT NULL,
        ShiftDate datetime2(7) NOT NULL,
        AvailabilityPct decimal(5,2) NOT NULL,
        PerformancePct decimal(5,2) NOT NULL,
        QualityPct decimal(5,2) NOT NULL,
        OeePct decimal(5,2) NOT NULL,
        AverageSpeedRpm decimal(10,2) NOT NULL,
        TotalOutput int NOT NULL,
        RejectedUnits int NOT NULL,
        DowntimeMin int NOT NULL,
        EnergyKwh decimal(10,3) NOT NULL,
        ComputedAt datetime2(7) NOT NULL,
        PRIMARY KEY (MachineId, ShiftId, ShiftDate)
    );

    ;WITH ShiftPeriods AS
    (
        SELECT
            machine.Id AS MachineId,
            machine.RatedSpeedRpm,
            shift.Id AS ShiftId,
            calendar.CalendarDate,
            DATEADD
            (
                SECOND,
                DATEDIFF(SECOND, CONVERT(time, '00:00:00'), shift.StartTime),
                CONVERT(datetime2, calendar.CalendarDate)
            ) AS ShiftStart,
            CASE
                WHEN shift.EndTime <= shift.StartTime
                    THEN DATEADD
                    (
                        DAY,
                        1,
                        DATEADD
                        (
                            SECOND,
                            DATEDIFF(SECOND, CONVERT(time, '00:00:00'), shift.EndTime),
                            CONVERT(datetime2, calendar.CalendarDate)
                        )
                    )
                ELSE DATEADD
                (
                    SECOND,
                    DATEDIFF(SECOND, CONVERT(time, '00:00:00'), shift.EndTime),
                    CONVERT(datetime2, calendar.CalendarDate)
                )
            END AS ShiftEnd
        FROM Scada.Machines machine
        JOIN Org.ProductionLines line
          ON line.Id = machine.LineId
         AND line.IsActive = 1
         AND line.IsDeleted = 0
        JOIN Planning.Shifts shift
          ON shift.FactoryId = line.FactoryId
         AND shift.IsActive = 1
         AND shift.IsDeleted = 0
        CROSS JOIN #CalendarDates calendar
        WHERE machine.IsActive = 1
          AND machine.IsDeleted = 0
    ),
    Metrics AS
    (
        SELECT
            period.*,
            CONVERT
            (
                decimal(5,2),
                78 + ((period.MachineId * 7 + period.ShiftId * 3
                       + DATEDIFF(DAY, period.CalendarDate, @Today)) % 21)
            ) AS AvailabilityPct,
            CONVERT
            (
                decimal(5,2),
                76 + ((period.MachineId * 5 + period.ShiftId * 2
                       + DATEDIFF(DAY, period.CalendarDate, @Today)) % 22)
            ) AS PerformancePct,
            CONVERT
            (
                decimal(5,2),
                94 + ((period.MachineId + period.ShiftId
                       + DATEDIFF(DAY, period.CalendarDate, @Today)) % 6)
            ) AS QualityPct
        FROM ShiftPeriods period
        WHERE period.ShiftStart <= @Now
    )
    INSERT INTO #OeePlan
        (MachineId, ShiftId, ShiftDate, AvailabilityPct, PerformancePct,
         QualityPct, OeePct, AverageSpeedRpm, TotalOutput, RejectedUnits,
         DowntimeMin, EnergyKwh, ComputedAt)
    SELECT
        metric.MachineId,
        metric.ShiftId,
        CONVERT(datetime2, metric.CalendarDate),
        metric.AvailabilityPct,
        metric.PerformancePct,
        metric.QualityPct,
        CONVERT
        (
            decimal(5,2),
            ROUND
            (
                metric.AvailabilityPct
                * metric.PerformancePct
                * metric.QualityPct / 10000,
                2
            )
        ),
        CONVERT
        (
            decimal(10,2),
            ROUND
            (
                COALESCE(metric.RatedSpeedRpm, 1000)
                * metric.PerformancePct / 100,
                2
            )
        ),
        5000
            + (metric.MachineId * 137)
            + (metric.ShiftId * 53)
            + (DATEDIFF(DAY, metric.CalendarDate, @Today) * 17),
        CONVERT
        (
            int,
            ROUND
            (
                (
                    5000
                    + (metric.MachineId * 137)
                    + (metric.ShiftId * 53)
                )
                * (100 - metric.QualityPct) / 100,
                0
            )
        ),
        CONVERT(int, ROUND(480 * (100 - metric.AvailabilityPct) / 100, 0)),
        CONVERT
        (
            decimal(10,3),
            70 + (metric.MachineId * 2.75) + (metric.ShiftId * 1.25)
        ),
        CASE WHEN metric.ShiftEnd > @Now THEN @Now ELSE metric.ShiftEnd END
    FROM Metrics metric;

    UPDATE snapshot
       SET snapshot.AverageSpeedRpm = plan.AverageSpeedRpm,
           snapshot.UpdatedAt = @Now
    FROM Analytics.OeeSnapshots snapshot
    JOIN #OeePlan plan
      ON plan.MachineId = snapshot.MachineId
     AND plan.ShiftId = snapshot.ShiftId
     AND plan.ShiftDate = snapshot.ShiftDate
    WHERE snapshot.AverageSpeedRpm IS NULL;
    SET @OeeSpeedBackfilled = @@ROWCOUNT;

    INSERT INTO Analytics.OeeSnapshots
        (MachineId, ShiftId, ShiftDate, AvailabilityPct, PerformancePct,
         QualityPct, OeePct, AverageSpeedRpm, TotalOutput, RejectedUnits,
         DowntimeMin, EnergyKwh, ComputedAt)
    SELECT
        plan.MachineId,
        plan.ShiftId,
        plan.ShiftDate,
        plan.AvailabilityPct,
        plan.PerformancePct,
        plan.QualityPct,
        plan.OeePct,
        plan.AverageSpeedRpm,
        plan.TotalOutput,
        plan.RejectedUnits,
        plan.DowntimeMin,
        plan.EnergyKwh,
        plan.ComputedAt
    FROM #OeePlan plan
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Analytics.OeeSnapshots existing
        WHERE existing.MachineId = plan.MachineId
          AND existing.ShiftId = plan.ShiftId
          AND existing.ShiftDate = plan.ShiftDate
    );
    SET @OeeInserted = @@ROWCOUNT;

    --------------------------------------------------------------------------
    -- 8) Add diverse alarms by reusing existing machines/devices/rules/users
    --------------------------------------------------------------------------

    CREATE TABLE #AlarmSeed
    (
        SeedKey nvarchar(300) NOT NULL PRIMARY KEY,
        RuleId int NULL,
        CustomerId int NOT NULL,
        DeviceId int NULL,
        MachineId int NOT NULL,
        Title nvarchar(200) NOT NULL,
        Severity varchar(15) NOT NULL,
        TagName varchar(100) NOT NULL,
        TriggerValue decimal(14,4) NOT NULL,
        TriggeredAt datetime2(7) NOT NULL,
        ResolvedAt datetime2(7) NULL,
        AcknowledgedAt datetime2(7) NULL,
        AcknowledgedById int NULL,
        NotificationSent bit NOT NULL,
        ProbableCause nvarchar(max) NULL,
        Notes nvarchar(max) NOT NULL
    );

    INSERT INTO #AlarmSeed
        (SeedKey, RuleId, CustomerId, DeviceId, MachineId, Title, Severity,
         TagName, TriggerValue, TriggeredAt, ResolvedAt, AcknowledgedAt,
         AcknowledgedById, NotificationSent, ProbableCause, Notes)
    SELECT
        CONCAT
        (
            @Marker,
            '|ALARM|',
            alarmType.AlarmCode,
            '|MACHINE:',
            runtime.MachineId
        ),
        alarmRule.Id,
        customer.Id,
        runtime.DeviceId,
        runtime.MachineId,
        CONCAT(alarmType.TitlePrefix, machine.Name),
        alarmType.Severity,
        alarmType.TagName,
        alarmType.TriggerValue + (runtime.MachineId % 7),
        alarmTime.TriggeredAt,
        alarmTime.ResolvedAt,
        alarmTime.AcknowledgedAt,
        CASE
            WHEN alarmTime.AcknowledgedAt IS NULL THEN NULL
            ELSE customerUser.Id
        END,
        1,
        alarmType.ProbableCause,
        CONCAT
        (
            @Marker,
            '|ALARM|',
            alarmType.AlarmCode,
            '|MACHINE:',
            runtime.MachineId
        )
    FROM #MachineRuntimePlan runtime
    JOIN Scada.Machines machine
      ON machine.Id = runtime.MachineId
    JOIN Org.ProductionLines line
      ON line.Id = runtime.LineId
    JOIN Org.Factories factory
      ON factory.Id = line.FactoryId
    JOIN Org.Customers customer
      ON customer.Id = factory.CustomerId
    CROSS APPLY
    (
        SELECT *
        FROM
        (
            VALUES
                (
                    'ACTIVE_UNACK',
                    N'High temperature - ',
                    CASE runtime.MachineId % 4
                        WHEN 0 THEN 'critical'
                        WHEN 1 THEN 'high'
                        WHEN 2 THEN 'medium'
                        ELSE 'low'
                    END,
                    'temperature',
                    CONVERT(decimal(14,4), 74.0000),
                    N'Cooling or ventilation performance requires inspection.'
                ),
                (
                    'ACTIVE_ACK',
                    N'Vibration warning - ',
                    CASE runtime.MachineId % 3
                        WHEN 0 THEN 'high'
                        WHEN 1 THEN 'medium'
                        ELSE 'low'
                    END,
                    'vibration',
                    CONVERT(decimal(14,4), 5.8000),
                    N'Possible bearing wear or mechanical imbalance.'
                ),
                (
                    'RESOLVED_CURRENT',
                    N'Speed deviation resolved - ',
                    'medium',
                    'speed',
                    CONVERT(decimal(14,4), 18.0000),
                    N'Temporary speed deviation during ramp-up.'
                ),
                (
                    'RESOLVED_PREVIOUS',
                    N'Power spike resolved - ',
                    'high',
                    'power',
                    CONVERT(decimal(14,4), 125.0000),
                    N'Short transient load increase.'
                )
        ) value
        (
            AlarmCode,
            TitlePrefix,
            Severity,
            TagName,
            TriggerValue,
            ProbableCause
        )
    ) alarmType
    CROSS APPLY
    (
        SELECT
            CASE alarmType.AlarmCode
                WHEN 'ACTIVE_UNACK'
                    THEN DATEADD(MINUTE, -(5 + (runtime.MachineId % 8)), @Now)
                WHEN 'ACTIVE_ACK'
                    THEN DATEADD
                    (
                        MINUTE,
                        CASE
                            WHEN DATEDIFF(MINUTE, runtime.ShiftStart, @Now) < 30
                                THEN 5
                            ELSE DATEDIFF(MINUTE, runtime.ShiftStart, @Now) / 3
                        END,
                        runtime.ShiftStart
                    )
                WHEN 'RESOLVED_CURRENT'
                    THEN DATEADD
                    (
                        MINUTE,
                        CASE
                            WHEN DATEDIFF(MINUTE, runtime.ShiftStart, @Now) < 40
                                THEN 8
                            ELSE DATEDIFF(MINUTE, runtime.ShiftStart, @Now) / 2
                        END,
                        runtime.ShiftStart
                    )
                ELSE DATEADD
                (
                    HOUR,
                    -(18 + (runtime.MachineId % 5)),
                    @Now
                )
            END AS TriggeredAt,
            CASE alarmType.AlarmCode
                WHEN 'RESOLVED_CURRENT'
                    THEN DATEADD
                    (
                        MINUTE,
                        12,
                        DATEADD
                        (
                            MINUTE,
                            CASE
                                WHEN DATEDIFF(MINUTE, runtime.ShiftStart, @Now) < 40
                                    THEN 8
                                ELSE DATEDIFF(MINUTE, runtime.ShiftStart, @Now) / 2
                            END,
                            runtime.ShiftStart
                        )
                    )
                WHEN 'RESOLVED_PREVIOUS'
                    THEN DATEADD
                    (
                        MINUTE,
                        20,
                        DATEADD
                        (
                            HOUR,
                            -(18 + (runtime.MachineId % 5)),
                            @Now
                        )
                    )
                ELSE NULL
            END AS ResolvedAt,
            CASE alarmType.AlarmCode
                WHEN 'ACTIVE_ACK'
                    THEN DATEADD
                    (
                        MINUTE,
                        5,
                        DATEADD
                        (
                            MINUTE,
                            CASE
                                WHEN DATEDIFF(MINUTE, runtime.ShiftStart, @Now) < 30
                                    THEN 5
                                ELSE DATEDIFF(MINUTE, runtime.ShiftStart, @Now) / 3
                            END,
                            runtime.ShiftStart
                        )
                    )
                WHEN 'RESOLVED_CURRENT'
                    THEN DATEADD
                    (
                        MINUTE,
                        3,
                        DATEADD
                        (
                            MINUTE,
                            CASE
                                WHEN DATEDIFF(MINUTE, runtime.ShiftStart, @Now) < 40
                                    THEN 8
                                ELSE DATEDIFF(MINUTE, runtime.ShiftStart, @Now) / 2
                            END,
                            runtime.ShiftStart
                        )
                    )
                WHEN 'RESOLVED_PREVIOUS'
                    THEN DATEADD
                    (
                        MINUTE,
                        4,
                        DATEADD
                        (
                            HOUR,
                            -(18 + (runtime.MachineId % 5)),
                            @Now
                        )
                    )
                ELSE NULL
            END AS AcknowledgedAt
    ) alarmTime
    OUTER APPLY
    (
        SELECT TOP (1) existingRule.Id
        FROM Alarm.AlarmRules existingRule
        WHERE existingRule.CustomerId = customer.Id
          AND existingRule.IsActive = 1
          AND existingRule.IsDeleted = 0
        ORDER BY
            CASE
                WHEN existingRule.TagName = alarmType.TagName THEN 0
                ELSE 1
            END,
            existingRule.Id
    ) alarmRule
    OUTER APPLY
    (
        SELECT TOP (1) applicationUser.Id
        FROM Security.Users applicationUser
        WHERE applicationUser.CustomerId = customer.Id
          AND applicationUser.IsActive = 1
        ORDER BY applicationUser.Id
    ) customerUser;

    UPDATE alarmEvent
       SET alarmEvent.RuleId = seed.RuleId,
           alarmEvent.CustomerId = seed.CustomerId,
           alarmEvent.DeviceId = seed.DeviceId,
           alarmEvent.MachineId = seed.MachineId,
           alarmEvent.SiloId = NULL,
           alarmEvent.Title = seed.Title,
           alarmEvent.Severity = seed.Severity,
           alarmEvent.TagName = seed.TagName,
           alarmEvent.TriggerValue = seed.TriggerValue,
           alarmEvent.TriggeredAt = seed.TriggeredAt,
           alarmEvent.ResolvedAt = seed.ResolvedAt,
           alarmEvent.AcknowledgedAt = seed.AcknowledgedAt,
           alarmEvent.AcknowledgedById = seed.AcknowledgedById,
           alarmEvent.NotificationSent = seed.NotificationSent,
           alarmEvent.ProbableCause = seed.ProbableCause,
           alarmEvent.Notes = seed.Notes,
           alarmEvent.UpdatedAt = @Now
    FROM Alarm.AlarmEvents alarmEvent
    JOIN #AlarmSeed seed
      ON LEFT(alarmEvent.Notes, LEN(seed.SeedKey)) = seed.SeedKey;
    SET @AlarmEventsUpdated = @@ROWCOUNT;

    INSERT INTO Alarm.AlarmEvents
        (RuleId, CustomerId, DeviceId, MachineId, SiloId, Title, Severity,
         TagName, TriggerValue, TriggeredAt, ResolvedAt, AcknowledgedAt,
         AcknowledgedById, NotificationSent, ProbableCause, Notes)
    SELECT
        seed.RuleId,
        seed.CustomerId,
        seed.DeviceId,
        seed.MachineId,
        NULL,
        seed.Title,
        seed.Severity,
        seed.TagName,
        seed.TriggerValue,
        seed.TriggeredAt,
        seed.ResolvedAt,
        seed.AcknowledgedAt,
        seed.AcknowledgedById,
        seed.NotificationSent,
        seed.ProbableCause,
        seed.Notes
    FROM #AlarmSeed seed
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Alarm.AlarmEvents existing
        WHERE LEFT(existing.Notes, LEN(seed.SeedKey)) = seed.SeedKey
    );
    SET @AlarmEventsInserted = @@ROWCOUNT;

    --------------------------------------------------------------------------
    -- 9) Add raw/tag telemetry so alarm detail waveforms contain real rows
    --------------------------------------------------------------------------

    INSERT INTO IoT.TelemetryRaw
        (DeviceId, RecordedAt, ReceivedAt, RawPayload, Protocol, Quality)
    SELECT
        plan.DeviceId,
        plan.TelemetryAt,
        @Now,
        CONCAT
        (
            '{"source":"',
            @HistorySource,
            '","machineId":',
            plan.MachineId,
            ',"state":"',
            plan.State,
            '","speedRpm":',
            COALESCE(CONVERT(varchar(20), plan.SpeedRpm), 'null'),
            ',"temperature":',
            COALESCE(CONVERT(varchar(30), plan.TempCelsius), 'null'),
            ',"vibration":',
            COALESCE(CONVERT(varchar(30), plan.VibrationMms), 'null'),
            '}'
        ),
        'seed',
        192
    FROM #MachineRuntimePlan plan
    WHERE plan.DeviceId IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM IoT.TelemetryRaw existing
          WHERE existing.DeviceId = plan.DeviceId
            AND existing.RecordedAt = plan.TelemetryAt
      );
    SET @TelemetryRawInserted = @@ROWCOUNT;

    ;WITH AlarmTelemetry AS
    (
        SELECT DISTINCT
            alarm.DeviceId,
            alarm.MachineId,
            alarm.TriggeredAt,
            tag.TagName,
            tag.Unit,
            tag.BaseValue,
            point.PointOrder,
            point.MinuteOffset
        FROM #AlarmSeed alarm
        CROSS APPLY
        (
            SELECT *
            FROM
            (
                VALUES
                    (
                        alarm.TagName,
                        CASE alarm.TagName
                            WHEN 'temperature' THEN 'C'
                            WHEN 'vibration' THEN 'mm/s'
                            WHEN 'speed' THEN 'rpm'
                            WHEN 'power' THEN 'kW'
                            ELSE NULL
                        END,
                        alarm.TriggerValue
                    ),
                    (
                        CASE alarm.TagName
                            WHEN 'temperature' THEN 'vibration'
                            WHEN 'vibration' THEN 'temperature'
                            WHEN 'speed' THEN 'power'
                            WHEN 'power' THEN 'current'
                            ELSE 'current'
                        END,
                        CASE alarm.TagName
                            WHEN 'temperature' THEN 'mm/s'
                            WHEN 'vibration' THEN 'C'
                            WHEN 'speed' THEN 'kW'
                            WHEN 'power' THEN 'A'
                            ELSE 'A'
                        END,
                        CASE alarm.TagName
                            WHEN 'temperature' THEN CONVERT(decimal(14,4), 3.2000)
                            WHEN 'vibration' THEN CONVERT(decimal(14,4), 48.0000)
                            WHEN 'speed' THEN CONVERT(decimal(14,4), 95.0000)
                            WHEN 'power' THEN CONVERT(decimal(14,4), 18.0000)
                            ELSE CONVERT(decimal(14,4), 12.0000)
                        END
                    )
            ) value(TagName, Unit, BaseValue)
        ) tag
        CROSS APPLY
        (
            VALUES
                (1, -15),
                (2, -10),
                (3, -5),
                (4, 0),
                (5, 1),
                (6, 2),
                (7, 3)
        ) point(PointOrder, MinuteOffset)
        WHERE alarm.DeviceId IS NOT NULL
    ),
    RankedTelemetry AS
    (
        SELECT
            telemetry.*,
            DATEADD
            (
                MINUTE,
                telemetry.MinuteOffset,
                telemetry.TriggeredAt
            ) AS RecordedAt,
            ROW_NUMBER() OVER
            (
                PARTITION BY
                    telemetry.DeviceId,
                    telemetry.TagName,
                    DATEADD
                    (
                        MINUTE,
                        telemetry.MinuteOffset,
                        telemetry.TriggeredAt
                    )
                ORDER BY telemetry.BaseValue DESC, telemetry.PointOrder
            ) AS DuplicateOrder
        FROM AlarmTelemetry telemetry
    )
    INSERT INTO IoT.TelemetryTags
        (DeviceId, TagName, RecordedAt, ValueNum, ValueStr, Unit)
    SELECT
        telemetry.DeviceId,
        telemetry.TagName,
        telemetry.RecordedAt,
        CONVERT
        (
            decimal(18,6),
            telemetry.BaseValue
            + CASE telemetry.PointOrder
                WHEN 1 THEN -2.2000
                WHEN 2 THEN -1.4000
                WHEN 3 THEN -0.6000
                WHEN 4 THEN 0.0000
                WHEN 5 THEN -0.3000
                WHEN 6 THEN -0.9000
                ELSE -1.6000
              END
            + ((telemetry.MachineId % 5) / 10.0)
        ),
        NULL,
        telemetry.Unit
    FROM RankedTelemetry telemetry
    WHERE telemetry.DuplicateOrder = 1
      AND NOT EXISTS
    (
        SELECT 1
        FROM IoT.TelemetryTags existing
        WHERE existing.DeviceId = telemetry.DeviceId
          AND existing.TagName = telemetry.TagName
          AND existing.RecordedAt = telemetry.RecordedAt
    );
    SET @TelemetryTagsInserted = @@ROWCOUNT;

    --------------------------------------------------------------------------
    -- 10) Refresh existing silo/scale snapshots for Zones 1 and 2
    --------------------------------------------------------------------------

    UPDATE silo
       SET silo.CurrentQtyKg = CONVERT
           (
               decimal(10,3),
               silo.CapacityKg
               * CASE silo.Id % 5
                   WHEN 0 THEN 1.000
                   WHEN 1 THEN 0.720
                   WHEN 2 THEN 0.180
                   WHEN 3 THEN 0.080
                   ELSE 0.540
                 END
           ),
           silo.CurrentLotUid = COALESCE
           (
               silo.CurrentLotUid,
               CONCAT
               (
                   'LOT-',
                   @TodayKey,
                   '-',
                   RIGHT('0000' + CONVERT(varchar(10), silo.Id), 4)
               )
           ),
           silo.TelemetryAt = DATEADD(SECOND, -(silo.Id % 45), @Now),
           silo.UpdatedAt = @Now
    FROM Scada.Silos silo
    WHERE silo.IsDeleted = 0;
    SET @SilosUpdated = @@ROWCOUNT;

    ;WITH RankedScales AS
    (
        SELECT
            scale.Id,
            scale.FactoryId,
            ROW_NUMBER() OVER
            (
                PARTITION BY scale.FactoryId
                ORDER BY scale.Id
            ) AS ScaleOrder
        FROM Scada.Scales scale
        WHERE scale.IsDeleted = 0
    ),
    ScaleAssignments AS
    (
        SELECT
            rankedScale.Id AS ScaleId,
            rankedScale.ScaleOrder,
            activeBatch.BatchId,
            activeMaterial.MaterialId,
            activeMaterial.DispensedQty
        FROM RankedScales rankedScale
        OUTER APPLY
        (
            SELECT TOP (1)
                seedBatch.BatchId
            FROM #SeedBatches seedBatch
            WHERE seedBatch.FactoryId = rankedScale.FactoryId
              AND seedBatch.Status = 'in_progress'
              AND seedBatch.Stage = 'dispense'
            ORDER BY seedBatch.LineId, seedBatch.BatchId
        ) activeBatch
        OUTER APPLY
        (
            SELECT TOP (1)
                materialPlan.MaterialId,
                materialPlan.DispensedQty
            FROM #BatchMaterialPlan materialPlan
            WHERE materialPlan.BatchId = activeBatch.BatchId
              AND materialPlan.Status = 'pending'
            ORDER BY
                CASE WHEN materialPlan.DispensedQty = 0 THEN 0 ELSE 1 END,
                materialPlan.MaterialId
        ) activeMaterial
    )
    UPDATE scale
       SET scale.ScaleState = CASE
               WHEN assignment.ScaleOrder = 1
                    AND assignment.BatchId IS NOT NULL
                    AND assignment.MaterialId IS NOT NULL
                   THEN 'assigned'
               ELSE 'ready'
           END,
           scale.ActiveBatchId = CASE
               WHEN assignment.ScaleOrder = 1 THEN assignment.BatchId
               ELSE NULL
           END,
           scale.ActiveMaterialId = CASE
               WHEN assignment.ScaleOrder = 1 THEN assignment.MaterialId
               ELSE NULL
           END,
           scale.WeightKg = CASE
               WHEN assignment.ScaleOrder = 1
                   THEN COALESCE(assignment.DispensedQty, 0)
               ELSE 0
           END,
           scale.TelemetryAt = DATEADD(SECOND, -(scale.Id % 45), @Now),
           scale.UpdatedAt = @Now
    FROM Scada.Scales scale
    JOIN ScaleAssignments assignment
      ON assignment.ScaleId = scale.Id;
    SET @ScalesUpdated = @@ROWCOUNT;

    COMMIT TRANSACTION;

    --------------------------------------------------------------------------
    -- 11) Result summary
    --------------------------------------------------------------------------

    SELECT
        'Seed completed' AS Status,
        @Now AS SeededAtUtc,
        (SELECT COUNT(*) FROM #LineSeedBase) AS EligibleLines,
        @BatchesInserted AS BatchesInserted,
        @BatchesUpdated AS BatchesUpdated,
        @BatchMaterialsInserted AS BatchMaterialsInserted,
        @BatchMaterialsUpdated AS BatchMaterialsUpdated,
        @StageLogsInserted AS StageLogsInserted,
        @SchedulesInserted AS ProductionSchedulesInserted,
        @MachineStatesInserted AS MachineStatesInserted,
        @MachineStatesUpdated AS MachineStatesUpdated,
        @MachineHistoryInserted AS CurrentShiftMachineHistoryInserted,
        @OeeInserted AS OeeSnapshotsInserted,
        @OeeSpeedBackfilled AS ExistingOeeSpeedBackfilled,
        @AlarmEventsInserted AS AlarmEventsInserted,
        @AlarmEventsUpdated AS AlarmEventsUpdated,
        @TelemetryRawInserted AS TelemetryRawInserted,
        @TelemetryTagsInserted AS TelemetryTagsInserted,
        @SilosUpdated AS SilosUpdated,
        @ScalesUpdated AS ScalesUpdated;

    SELECT
        batch.Status,
        batch.Stage,
        COUNT(*) AS BatchCount
    FROM Production.Batches batch
    WHERE LEFT(batch.Notes, LEN(@Marker + N'|BATCH|')) =
          @Marker + N'|BATCH|'
      AND batch.IsDeleted = 0
    GROUP BY batch.Status, batch.Stage
    ORDER BY batch.Status;

    SELECT
        machineState.State,
        COUNT(*) AS MachineCount
    FROM Scada.MachineStates machineState
    JOIN #MachineRuntimePlan plan
      ON plan.MachineId = machineState.MachineId
    GROUP BY machineState.State
    ORDER BY machineState.State;

    SELECT
        CASE
            WHEN alarm.ResolvedAt IS NOT NULL THEN 'resolved'
            WHEN alarm.AcknowledgedAt IS NOT NULL THEN 'acknowledged_active'
            ELSE 'unacknowledged_active'
        END AS AlarmState,
        alarm.Severity,
        COUNT(*) AS AlarmCount
    FROM Alarm.AlarmEvents alarm
    WHERE LEFT(alarm.Notes, LEN(@Marker + N'|ALARM|')) =
          @Marker + N'|ALARM|'
    GROUP BY
        CASE
            WHEN alarm.ResolvedAt IS NOT NULL THEN 'resolved'
            WHEN alarm.AcknowledgedAt IS NOT NULL THEN 'acknowledged_active'
            ELSE 'unacknowledged_active'
        END,
        alarm.Severity
    ORDER BY AlarmState, alarm.Severity;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
