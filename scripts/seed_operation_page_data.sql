/*
    GeniePark - Operation page demo data

    Uses existing master data only. It does not create customers, factories,
    lines, machines, shifts, or SKUs.

    Populates the sources read by OperationController:
      - RealTime.Production
      - Analytics.OeeSnapshots
      - Scada.MachineStateHistories
      - Alarm.AlarmEvents
      - Production.Batches
      - Maintenance.WorkOrders / Maintenance.BreakdownLogs

    Re-running is safe for the deterministic demo rows. A fresh Production
    reading is appended once per execution minute, matching append-only logic.
*/

USE [GeniePark];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @FactoryId int = 1;
DECLARE @LineId int = 1;
DECLARE @DaysBack int = 31;
DECLARE @Now datetime2(0) = SYSUTCDATETIME();
DECLARE @Anchor datetime2(0) = DATEADD
(
    SECOND,
    -5,
    DATEADD(MINUTE, DATEDIFF(MINUTE, 0, SYSUTCDATETIME()), 0)
);
DECLARE @DataFrom datetime2(0) = DATEADD(DAY, -@DaysBack, @Now);
DECLARE @CustomerId int;
DECLARE @SkuId int;

IF DB_NAME() <> N'GeniePark'
    THROW 52000, 'Run this script only against the GeniePark database.', 1;

IF OBJECT_ID(N'RealTime.Production', N'U') IS NULL
    THROW 52001, 'RealTime.Production was not found. Apply the latest migrations first.', 1;

IF COL_LENGTH('RealTime.Production', 'CustomerId') IS NULL
    THROW 52007, 'RealTime.Production.CustomerId was not found. Apply AddCustomerIdToProduction first.', 1;

IF COL_LENGTH('Analytics.OeeSnapshots', 'AverageSpeedRpm') IS NULL
    THROW 52002, 'Analytics.OeeSnapshots.AverageSpeedRpm was not found.', 1;

SELECT @CustomerId = factory.CustomerId
FROM Org.Factories factory
JOIN Org.ProductionLines line ON line.FactoryId = factory.Id
WHERE factory.Id = @FactoryId
  AND line.Id = @LineId
  AND factory.IsActive = 1
  AND factory.IsDeleted = 0
  AND line.IsActive = 1
  AND line.IsDeleted = 0;

IF @CustomerId IS NULL
    THROW 52003, 'The selected active factory/line pair was not found.', 1;

SELECT TOP (1) @SkuId = sku.Id
FROM MasterData.Skus sku
WHERE sku.CustomerId = @CustomerId
  AND sku.IsActive = 1
  AND sku.IsDeleted = 0
ORDER BY sku.Id;

IF @SkuId IS NULL
    THROW 52004, 'No active SKU exists for the selected customer.', 1;

DECLARE @Machines TABLE
(
    MachineId int NOT NULL PRIMARY KEY,
    MachineCode nvarchar(20) NOT NULL,
    MachineName nvarchar(100) NOT NULL,
    RatedSpeedRpm int NULL,
    SequenceNo int NOT NULL
);

INSERT INTO @Machines (MachineId, MachineCode, MachineName, RatedSpeedRpm, SequenceNo)
SELECT
    machine.Id,
    machine.Code,
    machine.Name,
    machine.RatedSpeedRpm,
    CONVERT(int, ROW_NUMBER() OVER (ORDER BY machine.Id))
FROM Scada.Machines machine
WHERE machine.LineId = @LineId
  AND machine.IsActive = 1
  AND machine.IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM @Machines)
    THROW 52005, 'No active machines exist on the selected line.', 1;

/* Build real shift windows for the last month, including overnight shifts. */
DECLARE @Dates TABLE (ShiftDate date NOT NULL PRIMARY KEY);
DECLARE @DateCursor date = DATEADD(DAY, -@DaysBack - 1, CONVERT(date, @Now));
DECLARE @LastDate date = DATEADD(DAY, 1, CONVERT(date, @Now));

WHILE @DateCursor <= @LastDate
BEGIN
    INSERT INTO @Dates (ShiftDate) VALUES (@DateCursor);
    SET @DateCursor = DATEADD(DAY, 1, @DateCursor);
END;

DECLARE @ShiftWindows TABLE
(
    ShiftId int NOT NULL,
    ShiftName nvarchar(200) NOT NULL,
    ShiftDate date NOT NULL,
    WindowStart datetime2(0) NOT NULL,
    WindowEnd datetime2(0) NOT NULL,
    PRIMARY KEY (ShiftId, ShiftDate)
);

;WITH CandidateWindows AS
(
    SELECT
        shift.Id AS ShiftId,
        shift.Name AS ShiftName,
        dates.ShiftDate,
        calculated.WindowStart,
        calculated.WindowEnd,
        CASE WHEN shift.FactoryId = @FactoryId THEN 0 ELSE 1 END AS LinkPriority
    FROM Planning.Shifts shift
    CROSS JOIN @Dates dates
    CROSS APPLY
    (
        SELECT
            DATEADD
            (
                SECOND,
                DATEDIFF(SECOND, CONVERT(time, '00:00:00'), shift.StartTime),
                CONVERT(datetime2(0), dates.ShiftDate)
            ) AS WindowStart,
            DATEADD
            (
                DAY,
                CASE WHEN shift.StartTime >= shift.EndTime THEN 1 ELSE 0 END,
                DATEADD
                (
                    SECOND,
                    DATEDIFF(SECOND, CONVERT(time, '00:00:00'), shift.EndTime),
                    CONVERT(datetime2(0), dates.ShiftDate)
                )
            ) AS WindowEnd
    ) calculated
    WHERE shift.IsActive = 1
      AND shift.IsDeleted = 0
      AND
      (
          shift.FactoryId = @FactoryId
          OR EXISTS
          (
              SELECT 1
              FROM Planning.ShiftFactories link
              WHERE link.ShiftId = shift.Id
                AND link.FactoryId = @FactoryId
          )
      )
      AND calculated.WindowEnd > @DataFrom
      AND calculated.WindowStart < @Now
),
RankedWindows AS
(
    SELECT *,
        ROW_NUMBER() OVER
        (
            PARTITION BY ShiftDate, WindowStart, WindowEnd
            ORDER BY LinkPriority, ShiftId
        ) AS RowNumber
    FROM CandidateWindows
)
INSERT INTO @ShiftWindows (ShiftId, ShiftName, ShiftDate, WindowStart, WindowEnd)
SELECT ShiftId, ShiftName, ShiftDate, WindowStart, WindowEnd
FROM RankedWindows
WHERE RowNumber = 1;

DECLARE @CurrentShiftId int;
DECLARE @CurrentShiftName nvarchar(200);
DECLARE @CurrentShiftDate date;
DECLARE @CurrentShiftStart datetime2(0);
DECLARE @CurrentShiftEnd datetime2(0);

SELECT TOP (1)
    @CurrentShiftId = ShiftId,
    @CurrentShiftName = ShiftName,
    @CurrentShiftDate = ShiftDate,
    @CurrentShiftStart = WindowStart,
    @CurrentShiftEnd = WindowEnd
FROM @ShiftWindows
WHERE @Now >= WindowStart
  AND @Now < WindowEnd
ORDER BY WindowStart DESC, ShiftId;

IF @CurrentShiftId IS NULL
    THROW 52006, 'The current shift could not be resolved for the selected factory.', 1;

/* Keep the live reading inside the current shift even at a shift boundary. */
IF @Anchor <= @CurrentShiftStart
    SET @Anchor = DATEADD(SECOND, 1, @CurrentShiftStart);

IF @Anchor >= @Now
    SET @Anchor = DATEADD(SECOND, -1, @Now);

DECLARE @MarkerPrefix nvarchar(80) = N'OPERATION_PAGE_DEMO|';
DECLARE @BatchUid varchar(30) = LEFT
(
    CONCAT
    (
        'OP-DEMO-L', @LineId, '-',
        CONVERT(char(8), @CurrentShiftDate, 112),
        RIGHT('0' + CONVERT(varchar(2), DATEPART(HOUR, @CurrentShiftStart)), 2)
    ),
    30
);

BEGIN TRY
    BEGIN TRANSACTION;

    /* Last/current batch displayed beside every machine on the line. */
    IF NOT EXISTS
    (
        SELECT 1
        FROM Production.Batches
        WHERE Uid = @BatchUid AND IsDeleted = 0
    )
    BEGIN
        INSERT INTO Production.Batches
        (
            Uid, CustomerId, SkuId, LineId, Status, Stage,
            PlannedQty, ProducedQty, RejectedQty,
            PlannedStart, PlannedEnd, ActualStart, ActualEnd,
            ShiftId, Priority, Notes, IsDeleted, CreatedAt
        )
        VALUES
        (
            @BatchUid, @CustomerId, @SkuId, @LineId, 'production', 'production',
            25000, 8750, 42,
            @CurrentShiftStart, @CurrentShiftEnd, @CurrentShiftStart, NULL,
            @CurrentShiftId, 2, @MarkerPrefix + N'BATCH', 0, @Now
        );
    END;

    /* One hourly production signal through each historical shift window. */
    DECLARE @HourNumbers TABLE (HourNumber int NOT NULL PRIMARY KEY);
    DECLARE @Hour int = 0;
    WHILE @Hour < 24
    BEGIN
        INSERT INTO @HourNumbers (HourNumber) VALUES (@Hour);
        SET @Hour += 1;
    END;

    DECLARE @SignalTimes TABLE
    (
        SignalNo int IDENTITY(1, 1) NOT NULL,
        SignalAt datetime2(0) NOT NULL UNIQUE
    );

    INSERT INTO @SignalTimes (SignalAt)
    SELECT DISTINCT eventTimes.SignalAt
    FROM @ShiftWindows window
    CROSS JOIN @HourNumbers hours
    CROSS APPLY
    (
        SELECT DATEADD(MINUTE, 5 + hours.HourNumber * 60, window.WindowStart) AS SignalAt
    ) eventTimes
    WHERE eventTimes.SignalAt < window.WindowEnd
      AND eventTimes.SignalAt >= @DataFrom
      AND eventTimes.SignalAt < @Now;

    /* Keeps hierarchy/summary/machines live for the two-minute live cutoff. */
    IF NOT EXISTS (SELECT 1 FROM @SignalTimes WHERE SignalAt = @Anchor)
        INSERT INTO @SignalTimes (SignalAt) VALUES (@Anchor);

    INSERT INTO RealTime.Production
    (
        MachineId, [State], Speed, [Count], CountDiff,
        CustomerId, LineId, FactoryId, Temp, Qreject, StateReason,
        SKU, Fault, [TimeStamp]
    )
    SELECT
        machine.MachineCode,
        1,
        COALESCE(machine.RatedSpeedRpm, 380 + machine.SequenceNo * 35)
            - (signal.SignalNo % 4) * 8,
        100000 + machine.SequenceNo * 100000
            + DATEDIFF(MINUTE, @DataFrom, signal.SignalAt) * 10,
        CONVERT(decimal(18, 4), 260 + machine.SequenceNo * 25 + (signal.SignalNo % 5) * 12),
        @CustomerId,
        @LineId,
        @FactoryId,
        CONVERT(decimal(10, 3), 31.5 + machine.SequenceNo * 0.8 + (signal.SignalNo % 4) * 0.2),
        CASE WHEN signal.SignalNo % 7 = 0 THEN 3
             WHEN signal.SignalNo % 4 = 0 THEN 1
             ELSE 0 END,
        -1,
        @SkuId,
        0,
        signal.SignalAt
    FROM @Machines machine
    CROSS JOIN @SignalTimes signal
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM RealTime.Production existing
        WHERE existing.CustomerId = @CustomerId
          AND existing.FactoryId = @FactoryId
          AND existing.LineId = @LineId
          AND existing.MachineId = machine.MachineCode
          AND existing.[TimeStamp] = signal.SignalAt
    );

    /* OEE source used by operation/summary. */
    INSERT INTO Analytics.OeeSnapshots
    (
        MachineId, ShiftId, ShiftDate,
        AvailabilityPct, PerformancePct, QualityPct, OeePct,
        AverageSpeedRpm, TotalOutput, RejectedUnits, DowntimeMin,
        EnergyKwh, ComputedAt, CreatedAt
    )
    SELECT
        machine.MachineId,
        window.ShiftId,
        CONVERT(datetime2(0), window.ShiftDate),
        metrics.AvailabilityPct,
        metrics.PerformancePct,
        metrics.QualityPct,
        ROUND
        (
            metrics.AvailabilityPct * metrics.PerformancePct * metrics.QualityPct / 10000.0,
            2
        ),
        CONVERT(decimal(10, 2), COALESCE(machine.RatedSpeedRpm, 400 + machine.SequenceNo * 30) * 0.82),
        4200 + machine.SequenceNo * 350 + DATEPART(DAY, window.ShiftDate) * 10,
        15 + machine.SequenceNo * 3 + DATEPART(DAY, window.ShiftDate) % 5,
        18 + machine.SequenceNo * 4,
        CONVERT(decimal(10, 3), 75 + machine.SequenceNo * 8 + DATEPART(DAY, window.ShiftDate) * 0.5),
        computed.ComputedAt,
        @Now
    FROM @ShiftWindows window
    CROSS JOIN @Machines machine
    CROSS APPLY
    (
        SELECT
            CONVERT(decimal(5, 2), 91 + (machine.SequenceNo + DATEPART(DAY, window.ShiftDate)) % 6) AS AvailabilityPct,
            CONVERT(decimal(5, 2), 88 + (machine.SequenceNo * 2 + DATEPART(DAY, window.ShiftDate)) % 9) AS PerformancePct,
            CONVERT(decimal(5, 2), 96 + (machine.SequenceNo + DATEPART(DAY, window.ShiftDate)) % 4) AS QualityPct
    ) metrics
    CROSS APPLY
    (
        SELECT DATEADD
        (
            SECOND,
            DATEDIFF
            (
                SECOND,
                window.WindowStart,
                CASE WHEN window.WindowEnd < @Now THEN window.WindowEnd ELSE @Now END
            ) / 2,
            window.WindowStart
        ) AS ComputedAt
    ) computed
    WHERE computed.ComputedAt >= @DataFrom
      AND computed.ComputedAt < @Now
      AND NOT EXISTS
      (
          SELECT 1
          FROM Analytics.OeeSnapshots existing
          WHERE existing.MachineId = machine.MachineId
            AND existing.ShiftId = window.ShiftId
            AND existing.ShiftDate = CONVERT(datetime2(0), window.ShiftDate)
      );

    /* State transitions drive utilization, timeline, stoppages and stop counts. */
    DECLARE @HistoryEvents TABLE
    (
        MachineId int NOT NULL,
        FromState varchar(20) NULL,
        ToState varchar(20) NOT NULL,
        TransitionedAt datetime2(0) NOT NULL,
        Notes nvarchar(200) NOT NULL
    );

    INSERT INTO @HistoryEvents (MachineId, FromState, ToState, TransitionedAt, Notes)
    SELECT machine.MachineId, NULL, 'running', window.WindowStart,
           CONCAT(@MarkerPrefix, window.ShiftId, '|', CONVERT(char(8), window.ShiftDate, 112), '|START')
    FROM @ShiftWindows window
    CROSS JOIN @Machines machine
    WHERE window.WindowStart < @Now
    UNION ALL
    SELECT machine.MachineId, 'running', 'idle', eventTimes.StopAt,
           CONCAT(@MarkerPrefix, window.ShiftId, '|', CONVERT(char(8), window.ShiftDate, 112), '|IDLE')
    FROM @ShiftWindows window
    CROSS JOIN @Machines machine
    CROSS APPLY
    (
        SELECT DATEADD(SECOND, DATEDIFF(SECOND, window.WindowStart, window.WindowEnd) * 20 / 100, window.WindowStart) AS StopAt
    ) eventTimes
    WHERE eventTimes.StopAt < @Now
    UNION ALL
    SELECT machine.MachineId, 'idle', 'running', eventTimes.ResumeAt,
           CONCAT(@MarkerPrefix, window.ShiftId, '|', CONVERT(char(8), window.ShiftDate, 112), '|RESUME1')
    FROM @ShiftWindows window
    CROSS JOIN @Machines machine
    CROSS APPLY
    (
        SELECT DATEADD(SECOND, DATEDIFF(SECOND, window.WindowStart, window.WindowEnd) * 32 / 100, window.WindowStart) AS ResumeAt
    ) eventTimes
    WHERE eventTimes.ResumeAt < @Now
    UNION ALL
    SELECT machine.MachineId, 'running', 'fault', eventTimes.FaultAt,
           CONCAT(@MarkerPrefix, window.ShiftId, '|', CONVERT(char(8), window.ShiftDate, 112), '|FAULT')
    FROM @ShiftWindows window
    CROSS JOIN @Machines machine
    CROSS APPLY
    (
        SELECT DATEADD(SECOND, DATEDIFF(SECOND, window.WindowStart, window.WindowEnd) * 58 / 100, window.WindowStart) AS FaultAt
    ) eventTimes
    WHERE eventTimes.FaultAt < @Now
    UNION ALL
    SELECT machine.MachineId, 'fault', 'running', eventTimes.ResumeAt,
           CONCAT(@MarkerPrefix, window.ShiftId, '|', CONVERT(char(8), window.ShiftDate, 112), '|RESUME2')
    FROM @ShiftWindows window
    CROSS JOIN @Machines machine
    CROSS APPLY
    (
        SELECT DATEADD(SECOND, DATEDIFF(SECOND, window.WindowStart, window.WindowEnd) * 68 / 100, window.WindowStart) AS ResumeAt
    ) eventTimes
    WHERE eventTimes.ResumeAt < @Now;

    /*
       Quick current-shift events make the live page useful even when the
       normal percentage-based demo events have not happened yet.
    */
    INSERT INTO @HistoryEvents (MachineId, FromState, ToState, TransitionedAt, Notes)
    SELECT machine.MachineId, 'running', 'idle', DATEADD(MINUTE, 5, @CurrentShiftStart),
           CONCAT(@MarkerPrefix, @CurrentShiftId, '|', CONVERT(char(8), @CurrentShiftDate, 112), '|CURRENT_IDLE')
    FROM @Machines machine
    WHERE DATEADD(MINUTE, 5, @CurrentShiftStart) < @Now
    UNION ALL
    SELECT machine.MachineId, 'idle', 'running', DATEADD(MINUTE, 12, @CurrentShiftStart),
           CONCAT(@MarkerPrefix, @CurrentShiftId, '|', CONVERT(char(8), @CurrentShiftDate, 112), '|CURRENT_RESUME1')
    FROM @Machines machine
    WHERE DATEADD(MINUTE, 12, @CurrentShiftStart) < @Now
    UNION ALL
    SELECT machine.MachineId, 'running', 'fault', DATEADD(MINUTE, 20, @CurrentShiftStart),
           CONCAT(@MarkerPrefix, @CurrentShiftId, '|', CONVERT(char(8), @CurrentShiftDate, 112), '|CURRENT_FAULT')
    FROM @Machines machine
    WHERE DATEADD(MINUTE, 20, @CurrentShiftStart) < @Now
    UNION ALL
    SELECT machine.MachineId, 'fault', 'running', DATEADD(MINUTE, 27, @CurrentShiftStart),
           CONCAT(@MarkerPrefix, @CurrentShiftId, '|', CONVERT(char(8), @CurrentShiftDate, 112), '|CURRENT_RESUME2')
    FROM @Machines machine
    WHERE DATEADD(MINUTE, 27, @CurrentShiftStart) < @Now;

    INSERT INTO Scada.MachineStateHistories
    (
        MachineId, FromState, ToState, TransitionedAt,
        TriggerSource, Notes, CreatedAt
    )
    SELECT
        event.MachineId,
        MAX(event.FromState),
        event.ToState,
        event.TransitionedAt,
        'operation_demo',
        MIN(event.Notes),
        event.TransitionedAt
    FROM @HistoryEvents event
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Scada.MachineStateHistories existing
        WHERE existing.MachineId = event.MachineId
          AND existing.TransitionedAt = event.TransitionedAt
          AND existing.ToState = event.ToState
          AND existing.TriggerSource = 'operation_demo'
    )
    GROUP BY event.MachineId, event.ToState, event.TransitionedAt;

    /* Direct system alarms used only for hourly alarm counts on Operation. */
    INSERT INTO [Alarm].AlarmEvents
    (
        CustomerId, MachineId, Title, Severity, TagName,
        TriggerValue, TriggeredAt, ResolvedAt,
        NotificationSent, ProbableCause, Notes, CreatedAt
    )
    SELECT
        @CustomerId,
        machine.MachineId,
        CONCAT(machine.MachineName, ' demo process alarm'),
        CASE machine.SequenceNo % 3
            WHEN 0 THEN 'critical'
            WHEN 1 THEN 'high'
            ELSE 'medium'
        END,
        CASE machine.SequenceNo % 2 WHEN 0 THEN 'Temp' ELSE 'Fault' END,
        CONVERT(decimal(14, 4), 70 + machine.SequenceNo * 4),
        alarmTimes.AlarmAt,
        CASE
            WHEN machine.SequenceNo % 2 = 0
             AND DATEADD(MINUTE, 15, alarmTimes.AlarmAt) < @Now
                THEN DATEADD(MINUTE, 15, alarmTimes.AlarmAt)
            ELSE NULL
        END,
        0,
        CASE machine.SequenceNo % 3
            WHEN 0 THEN N'Motor overload'
            WHEN 1 THEN N'High process temperature'
            ELSE N'Temporary sensor threshold'
        END,
        CONCAT(@MarkerPrefix, window.ShiftId, '|', CONVERT(char(8), window.ShiftDate, 112), '|M', machine.MachineId),
        alarmTimes.AlarmAt
    FROM @ShiftWindows window
    CROSS JOIN @Machines machine
    CROSS APPLY
    (
        SELECT DATEADD(SECOND, DATEDIFF(SECOND, window.WindowStart, window.WindowEnd) * 45 / 100, window.WindowStart) AS AlarmAt
    ) alarmTimes
    WHERE alarmTimes.AlarmAt >= @DataFrom
      AND alarmTimes.AlarmAt < @Now
      AND NOT EXISTS
      (
          SELECT 1
          FROM [Alarm].AlarmEvents existing
          WHERE existing.CustomerId = @CustomerId
            AND existing.MachineId = machine.MachineId
            AND existing.Notes = CONCAT(@MarkerPrefix, window.ShiftId, '|', CONVERT(char(8), window.ShiftDate, 112), '|M', machine.MachineId)
      );

    /* A current-shift alarm for the hourly-production chart. */
    INSERT INTO [Alarm].AlarmEvents
    (
        CustomerId, MachineId, Title, Severity, TagName,
        TriggerValue, TriggeredAt, ResolvedAt,
        NotificationSent, ProbableCause, Notes, CreatedAt
    )
    SELECT
        @CustomerId,
        machine.MachineId,
        CONCAT(machine.MachineName, ' current-shift demo alarm'),
        CASE machine.SequenceNo % 3
            WHEN 0 THEN 'critical'
            WHEN 1 THEN 'high'
            ELSE 'medium'
        END,
        CASE machine.SequenceNo % 2 WHEN 0 THEN 'Temp' ELSE 'Fault' END,
        CONVERT(decimal(14, 4), 72 + machine.SequenceNo * 3),
        DATEADD(MINUTE, 18 + machine.SequenceNo, @CurrentShiftStart),
        CASE WHEN machine.SequenceNo % 2 = 0
            THEN DATEADD(MINUTE, 26 + machine.SequenceNo, @CurrentShiftStart)
            ELSE NULL END,
        0,
        N'Current-shift operation demo event',
        CONCAT(@MarkerPrefix, @CurrentShiftId, '|', CONVERT(char(8), @CurrentShiftDate, 112), '|CURRENT|M', machine.MachineId),
        DATEADD(MINUTE, 18 + machine.SequenceNo, @CurrentShiftStart)
    FROM @Machines machine
    WHERE DATEADD(MINUTE, 18 + machine.SequenceNo, @CurrentShiftStart) < @Now
      AND NOT EXISTS
      (
          SELECT 1
          FROM [Alarm].AlarmEvents existing
          WHERE existing.CustomerId = @CustomerId
            AND existing.MachineId = machine.MachineId
            AND existing.Notes = CONCAT(@MarkerPrefix, @CurrentShiftId, '|', CONVERT(char(8), @CurrentShiftDate, 112), '|CURRENT|M', machine.MachineId)
      );

    /* One linked work order and matching breakdown for the first machine. */
    DECLARE @FirstMachineId int = (SELECT TOP (1) MachineId FROM @Machines ORDER BY SequenceNo);
    DECLARE @CurrentStopAt datetime2(0) = DATEADD(MINUTE, 5, @CurrentShiftStart);
    DECLARE @CurrentResumeAt datetime2(0) = DATEADD(MINUTE, 12, @CurrentShiftStart);
    DECLARE @WorkOrderNumber varchar(20) = CONCAT
    (
        'OW', CONVERT(char(8), @CurrentShiftDate, 112),
        RIGHT('0' + CONVERT(varchar(2), DATEPART(HOUR, @CurrentShiftStart)), 2),
        RIGHT('000000' + CONVERT(varchar(6), @FirstMachineId), 6)
    );
    DECLARE @LinkedAlarmId int;
    DECLARE @WorkOrderId int;

    SELECT TOP (1) @LinkedAlarmId = alarm.Id
    FROM [Alarm].AlarmEvents alarm
    WHERE alarm.CustomerId = @CustomerId
      AND alarm.MachineId = @FirstMachineId
      AND alarm.TriggeredAt >= @CurrentShiftStart
      AND alarm.TriggeredAt < @CurrentShiftEnd
      AND alarm.Notes LIKE @MarkerPrefix + N'%'
    ORDER BY alarm.TriggeredAt DESC;

    IF @CurrentStopAt < @Now
       AND NOT EXISTS
       (
           SELECT 1
           FROM Maintenance.WorkOrders
           WHERE Number = @WorkOrderNumber AND IsDeleted = 0
       )
    BEGIN
        INSERT INTO Maintenance.WorkOrders
        (
            Number, CustomerId, FactoryId, WorkOrderType, Priority,
            MachineId, AlarmEventId, Description,
            PlannedStart, PlannedDurationHours, ActualStart, ActualEnd,
            Status, FailureMode, IsDeleted, CreatedAt
        )
        VALUES
        (
            @WorkOrderNumber, @CustomerId, @FactoryId, 'corrective', 'medium',
            @FirstMachineId, @LinkedAlarmId, N'Operation page demo stoppage work order',
            @CurrentStopAt, 1.00, @CurrentStopAt,
            CASE WHEN @CurrentResumeAt < @Now THEN @CurrentResumeAt ELSE NULL END,
            CASE WHEN @CurrentResumeAt < @Now THEN 'resolved' ELSE 'open' END,
            N'Conveyor jam', 0, @Now
        );
    END;

    SELECT @WorkOrderId = Id
    FROM Maintenance.WorkOrders
    WHERE Number = @WorkOrderNumber AND IsDeleted = 0;

    IF @CurrentStopAt < @Now
    BEGIN
        INSERT INTO Maintenance.BreakdownLogs
        (
            Reference, MachineId, AlarmEventId, WorkOrderId,
            BreakdownAt, ResolvedAt, MttrHours,
            RootCause, FailureMode, Status, CreatedAt
        )
        SELECT
            CONCAT
            (
                'OB', CONVERT(char(8), @CurrentShiftDate, 112),
                RIGHT('0' + CONVERT(varchar(2), DATEPART(HOUR, @CurrentShiftStart)), 2),
                RIGHT('000000' + CONVERT(varchar(6), machine.MachineId), 6)
            ),
            machine.MachineId,
            CASE WHEN machine.MachineId = @FirstMachineId THEN @LinkedAlarmId ELSE NULL END,
            CASE WHEN machine.MachineId = @FirstMachineId THEN @WorkOrderId ELSE NULL END,
            @CurrentStopAt,
            CASE WHEN @CurrentResumeAt < @Now THEN @CurrentResumeAt ELSE NULL END,
            CASE WHEN @CurrentResumeAt < @Now
                THEN CONVERT(decimal(6, 2), DATEDIFF(MINUTE, @CurrentStopAt, @CurrentResumeAt) / 60.0)
                ELSE NULL END,
            CASE machine.SequenceNo % 3
                WHEN 0 THEN N'Motor overload'
                WHEN 1 THEN N'Conveyor jam'
                ELSE N'Material feed interruption'
            END,
            N'Unplanned stop',
            CASE WHEN @CurrentResumeAt < @Now THEN 'resolved' ELSE 'open' END,
            @Now
        FROM @Machines machine
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM Maintenance.BreakdownLogs existing
            WHERE existing.Reference = CONCAT
            (
                'OB', CONVERT(char(8), @CurrentShiftDate, 112),
                RIGHT('0' + CONVERT(varchar(2), DATEPART(HOUR, @CurrentShiftStart)), 2),
                RIGHT('000000' + CONVERT(varchar(6), machine.MachineId), 6)
            )
        );
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

/* Verification summary. */
SELECT
    @FactoryId AS FactoryId,
    @LineId AS LineId,
    @CurrentShiftId AS CurrentShiftId,
    @CurrentShiftName AS CurrentShiftName,
    @CurrentShiftStart AS CurrentShiftStartUtc,
    @CurrentShiftEnd AS CurrentShiftEndUtc,
    (SELECT COUNT(*) FROM @Machines) AS Machines,
    (SELECT COUNT(*) FROM RealTime.Production production
        WHERE production.CustomerId = @CustomerId
          AND production.FactoryId = @FactoryId
          AND production.LineId = @LineId
          AND production.[TimeStamp] >= @DataFrom) AS ProductionRowsInWindow,
    (SELECT COUNT(*) FROM Analytics.OeeSnapshots snapshot
        JOIN @Machines machine ON machine.MachineId = snapshot.MachineId
        WHERE snapshot.ComputedAt >= @DataFrom) AS OeeRowsInWindow,
    (SELECT COUNT(*) FROM Scada.MachineStateHistories history
        JOIN @Machines machine ON machine.MachineId = history.MachineId
        WHERE history.TriggerSource = 'operation_demo'
          AND history.TransitionedAt >= @DataFrom) AS DemoStateTransitions,
    (SELECT COUNT(*) FROM [Alarm].AlarmEvents alarm
        WHERE alarm.CustomerId = @CustomerId
          AND alarm.Notes LIKE @MarkerPrefix + N'%'
          AND alarm.TriggeredAt >= @DataFrom) AS DemoAlarmEvents,
    (SELECT COUNT(*) FROM Production.Batches batch
        WHERE batch.Uid = @BatchUid AND batch.IsDeleted = 0) AS CurrentDemoBatch,
    (SELECT COUNT(*) FROM RealTime.Production production
        WHERE production.CustomerId = @CustomerId
          AND production.FactoryId = @FactoryId
          AND production.LineId = @LineId
          AND production.[TimeStamp] >= @CurrentShiftStart
          AND production.[TimeStamp] < @Now) AS CurrentShiftProductionRows,
    (SELECT COUNT(*) FROM Analytics.OeeSnapshots snapshot
        JOIN @Machines machine ON machine.MachineId = snapshot.MachineId
        WHERE snapshot.ShiftId = @CurrentShiftId
          AND snapshot.ComputedAt >= @CurrentShiftStart
          AND snapshot.ComputedAt < @Now) AS CurrentShiftOeeRows,
    (SELECT COUNT(*) FROM Scada.MachineStateHistories history
        JOIN @Machines machine ON machine.MachineId = history.MachineId
        WHERE history.TriggerSource = 'operation_demo'
          AND history.TransitionedAt >= @CurrentShiftStart
          AND history.TransitionedAt < @Now) AS CurrentShiftStateTransitions,
    (SELECT COUNT(*) FROM [Alarm].AlarmEvents alarm
        WHERE alarm.CustomerId = @CustomerId
          AND alarm.Notes LIKE @MarkerPrefix + N'%'
          AND alarm.TriggeredAt >= @CurrentShiftStart
          AND alarm.TriggeredAt < @Now) AS CurrentShiftAlarmEvents,
    (SELECT COUNT(*) FROM Maintenance.WorkOrders workOrder
        WHERE workOrder.Number = @WorkOrderNumber
          AND workOrder.IsDeleted = 0) AS CurrentDemoWorkOrders,
    (SELECT COUNT(*) FROM Maintenance.BreakdownLogs breakdown
        JOIN @Machines machine ON machine.MachineId = breakdown.MachineId
        WHERE breakdown.Reference LIKE 'OB%'
          AND breakdown.BreakdownAt >= @CurrentShiftStart) AS CurrentDemoBreakdowns;
GO
