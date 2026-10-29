/*
    Injects linked test alarms for the Active and History endpoints.

    Change @FactoryId before running when testing another factory.

    Expected API visibility:
      ACTIVE_ONLY         -> Active
      ACK_BOTH_LISTS      -> Active and History
      RESOLVED_HISTORY    -> History
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @FactoryId int = 1;
DECLARE @CustomerId int;
DECLARE @LineId int;
DECLARE @MachineId int;
DECLARE @DeviceId int;
DECLARE @RuleId int;
DECLARE @AcknowledgedById int;
DECLARE @Severity nvarchar(15);
DECLARE @TagName nvarchar(100);
DECLARE @TriggerValue decimal(14, 4);
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @RunId varchar(30) =
    REPLACE(REPLACE(REPLACE(CONVERT(varchar(27), @Now, 126), '-', ''), ':', ''), '.', '');

SELECT
    @CustomerId = factory.CustomerId,
    @LineId = productionLine.Id,
    @MachineId = machine.Id,
    @DeviceId = machine.DeviceId
FROM Org.Factories factory
CROSS APPLY
(
    SELECT TOP (1)
        candidateLine.Id
    FROM Org.ProductionLines candidateLine
    WHERE candidateLine.FactoryId = factory.Id
      AND candidateLine.IsActive = 1
      AND candidateLine.IsDeleted = 0
    ORDER BY candidateLine.Id
) productionLine
CROSS APPLY
(
    SELECT TOP (1)
        candidateMachine.Id,
        candidateMachine.DeviceId
    FROM Scada.Machines candidateMachine
    WHERE candidateMachine.LineId = productionLine.Id
      AND candidateMachine.DeviceId IS NOT NULL
      AND candidateMachine.IsActive = 1
      AND candidateMachine.IsDeleted = 0
    ORDER BY candidateMachine.Id
) machine
WHERE factory.Id = @FactoryId
  AND factory.IsActive = 1
  AND factory.IsDeleted = 0;

IF @CustomerId IS NULL OR @MachineId IS NULL OR @DeviceId IS NULL
BEGIN
    THROW 51000, 'No active machine with a device was found in the selected factory.', 1;
END;

SELECT TOP (1)
    @RuleId = alarmRule.Id,
    @Severity = alarmRule.Severity,
    @TagName = alarmRule.TagName,
    @TriggerValue =
        CASE
            WHEN alarmRule.MaximumValue IS NOT NULL
                THEN alarmRule.MaximumValue + 10
            WHEN alarmRule.MinimumValue IS NOT NULL
                THEN alarmRule.MinimumValue - 10
            ELSE 100
        END
FROM Alarm.AlarmRules alarmRule
WHERE alarmRule.CustomerId = @CustomerId
  AND alarmRule.ScopeType = 'machine'
  AND alarmRule.IsActive = 1
  AND alarmRule.IsDeleted = 0
  AND
  (
      alarmRule.ScopeEntityId = @MachineId
      OR EXISTS
      (
          SELECT 1
          FROM Alarm.AlarmRuleScopes alarmScope
          WHERE alarmScope.AlarmRuleId = alarmRule.Id
            AND alarmScope.ScopeType = 'machine'
            AND alarmScope.EntityId = @MachineId
            AND alarmScope.IsDeleted = 0
      )
  )
ORDER BY alarmRule.Id;

IF @RuleId IS NULL
BEGIN
    THROW 51001, 'No active alarm rule is linked to a machine in the selected factory.', 1;
END;

SELECT TOP (1)
    @AcknowledgedById = appUser.Id
FROM Security.Users appUser
WHERE appUser.CustomerId = @CustomerId
  AND appUser.IsActive = 1
ORDER BY appUser.Id;

IF @AcknowledgedById IS NULL
BEGIN
    THROW 51002, 'No active user was found for the selected factory customer.', 1;
END;

BEGIN TRANSACTION;

BEGIN TRY
    INSERT INTO Alarm.AlarmEvents
    (
        RuleId,
        CustomerId,
        DeviceId,
        MachineId,
        SiloId,
        Title,
        Severity,
        TagName,
        TriggerValue,
        TriggeredAt,
        ResolvedAt,
        AcknowledgedAt,
        AcknowledgedById,
        NotificationSent,
        Notes
    )
    VALUES
    (
        @RuleId,
        @CustomerId,
        @DeviceId,
        @MachineId,
        NULL,
        CONCAT('TEST ACTIVE_ONLY ', @RunId),
        @Severity,
        @TagName,
        @TriggerValue,
        DATEADD(minute, -3, @Now),
        NULL,
        NULL,
        NULL,
        0,
        'Linked test alarm for the Active endpoint.'
    );

    INSERT INTO Alarm.AlarmEvents
    (
        RuleId,
        CustomerId,
        DeviceId,
        MachineId,
        SiloId,
        Title,
        Severity,
        TagName,
        TriggerValue,
        TriggeredAt,
        ResolvedAt,
        AcknowledgedAt,
        AcknowledgedById,
        NotificationSent,
        Notes
    )
    VALUES
    (
        @RuleId,
        @CustomerId,
        @DeviceId,
        @MachineId,
        NULL,
        CONCAT('TEST ACK_BOTH_LISTS ', @RunId),
        @Severity,
        @TagName,
        @TriggerValue,
        DATEADD(minute, -2, @Now),
        NULL,
        DATEADD(minute, -1, @Now),
        @AcknowledgedById,
        0,
        'Acknowledged and unresolved: expected in Active and History.'
    );

    INSERT INTO Alarm.AlarmEvents
    (
        RuleId,
        CustomerId,
        DeviceId,
        MachineId,
        SiloId,
        Title,
        Severity,
        TagName,
        TriggerValue,
        TriggeredAt,
        ResolvedAt,
        AcknowledgedAt,
        AcknowledgedById,
        NotificationSent,
        Notes
    )
    VALUES
    (
        @RuleId,
        @CustomerId,
        @DeviceId,
        @MachineId,
        NULL,
        CONCAT('TEST RESOLVED_HISTORY ', @RunId),
        @Severity,
        @TagName,
        @TriggerValue,
        DATEADD(minute, -10, @Now),
        DATEADD(minute, -5, @Now),
        DATEADD(minute, -8, @Now),
        @AcknowledgedById,
        0,
        'Resolved linked test alarm for the History endpoint.'
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    alarmEvent.Id,
    alarmEvent.Title,
    alarmEvent.CustomerId,
    @FactoryId AS FactoryId,
    @LineId AS LineId,
    alarmEvent.MachineId,
    alarmEvent.DeviceId,
    alarmEvent.RuleId,
    alarmEvent.Severity,
    alarmEvent.TagName,
    alarmEvent.TriggerValue,
    alarmEvent.TriggeredAt,
    alarmEvent.AcknowledgedAt,
    alarmEvent.ResolvedAt,
    CASE
        WHEN alarmEvent.ResolvedAt IS NOT NULL THEN 'history'
        WHEN alarmEvent.AcknowledgedAt IS NOT NULL THEN 'active_and_history'
        ELSE 'active'
    END AS ExpectedList
FROM Alarm.AlarmEvents alarmEvent
WHERE alarmEvent.Title IN
(
    CONCAT('TEST ACTIVE_ONLY ', @RunId),
    CONCAT('TEST ACK_BOTH_LISTS ', @RunId),
    CONCAT('TEST RESOLVED_HISTORY ', @RunId)
)
ORDER BY alarmEvent.Id;
