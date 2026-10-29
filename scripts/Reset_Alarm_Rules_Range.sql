/*
    Replaces every existing alarm rule with machine range rules.

    Existing AlarmEvents are preserved. Their RuleId is cleared before the old
    rules are deleted so alarm history and linked work orders remain intact.

    Run the EF migrations before this script so MinimumValue/MaximumValue exist
    and the legacy condition columns have been removed.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

BEGIN TRY
    UPDATE Alarm.AlarmEvents
    SET RuleId = NULL
    WHERE RuleId IS NOT NULL;

    DELETE FROM Alarm.AlarmRuleEmailTargets;
    DELETE FROM Alarm.AlarmRuleSmsTargets;
    DELETE FROM Alarm.AlarmRuleActions;
    DELETE FROM Alarm.AlarmRuleScopes;
    DELETE FROM Alarm.AlarmRules;

    INSERT INTO Alarm.AlarmRules
    (
        CustomerId,
        Name,
        RuleType,
        ScopeType,
        ScopeEntityId,
        TagName,
        MinimumValue,
        MaximumValue,
        Severity,
        CreateWorkOrder,
        WorkOrderTeam,
        IsActive,
        CreatedById
    )
    SELECT
        factory.CustomerId,
        CONCAT(machine.Code, ' temperature range'),
        'user',
        'machine',
        machine.Id,
        'temperature',
        0.0000,
        70.0000,
        'high',
        CASE WHEN assignee.Id IS NULL THEN 0 ELSE 1 END,
        'Maintenance',
        1,
        assignee.Id
    FROM Scada.Machines machine
    JOIN Org.ProductionLines line
        ON line.Id = machine.LineId
    JOIN Org.Factories factory
        ON factory.Id = line.FactoryId
    OUTER APPLY
    (
        SELECT TOP (1) appUser.Id
        FROM Security.Users appUser
        WHERE appUser.CustomerId = factory.CustomerId
          AND appUser.IsActive = 1
        ORDER BY appUser.Id
    ) assignee
    WHERE machine.IsActive = 1
      AND machine.IsDeleted = 0
      AND line.IsActive = 1
      AND line.IsDeleted = 0
      AND factory.IsActive = 1
      AND factory.IsDeleted = 0;

    INSERT INTO Alarm.AlarmRules
    (
        CustomerId,
        Name,
        RuleType,
        ScopeType,
        ScopeEntityId,
        TagName,
        MinimumValue,
        MaximumValue,
        Severity,
        CreateWorkOrder,
        WorkOrderTeam,
        IsActive,
        CreatedById
    )
    SELECT
        factory.CustomerId,
        CONCAT(machine.Code, ' vibration range'),
        'user',
        'machine',
        machine.Id,
        'vibration',
        0.0000,
        8.0000,
        'critical',
        CASE WHEN assignee.Id IS NULL THEN 0 ELSE 1 END,
        'Maintenance',
        1,
        assignee.Id
    FROM Scada.Machines machine
    JOIN Org.ProductionLines line
        ON line.Id = machine.LineId
    JOIN Org.Factories factory
        ON factory.Id = line.FactoryId
    OUTER APPLY
    (
        SELECT TOP (1) appUser.Id
        FROM Security.Users appUser
        WHERE appUser.CustomerId = factory.CustomerId
          AND appUser.IsActive = 1
        ORDER BY appUser.Id
    ) assignee
    WHERE machine.IsActive = 1
      AND machine.IsDeleted = 0
      AND line.IsActive = 1
      AND line.IsDeleted = 0
      AND factory.IsActive = 1
      AND factory.IsDeleted = 0;

    INSERT INTO Alarm.AlarmRuleScopes
    (
        AlarmRuleId,
        ScopeType,
        EntityId
    )
    SELECT
        alarmRule.Id,
        'machine',
        alarmRule.ScopeEntityId
    FROM Alarm.AlarmRules alarmRule
    WHERE alarmRule.ScopeType = 'machine'
      AND alarmRule.ScopeEntityId IS NOT NULL
      AND alarmRule.IsDeleted = 0;

    INSERT INTO Alarm.AlarmRuleActions
    (
        AlarmRuleId,
        SequenceNo,
        ActionType,
        IsEnabled,
        WorkOrderPriority,
        AssignedToId
    )
    SELECT
        alarmRule.Id,
        1,
        'create_work_order',
        1,
        CASE alarmRule.Severity
            WHEN 'critical' THEN 'critical'
            WHEN 'high' THEN 'high'
            WHEN 'medium' THEN 'medium'
            ELSE 'low'
        END,
        alarmRule.CreatedById
    FROM Alarm.AlarmRules alarmRule
    WHERE alarmRule.CreateWorkOrder = 1
      AND alarmRule.CreatedById IS NOT NULL
      AND alarmRule.IsDeleted = 0;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    alarmRule.Id,
    alarmRule.CustomerId,
    alarmRule.Name,
    alarmRule.ScopeEntityId AS MachineId,
    alarmRule.TagName AS Parameter,
    alarmRule.MinimumValue,
    alarmRule.MaximumValue,
    alarmRule.Severity,
    alarmRule.IsActive,
    alarmRule.CreateWorkOrder,
    alarmAction.AssignedToId
FROM Alarm.AlarmRules alarmRule
LEFT JOIN Alarm.AlarmRuleActions alarmAction
    ON alarmAction.AlarmRuleId = alarmRule.Id
   AND alarmAction.ActionType = 'create_work_order'
   AND alarmAction.IsDeleted = 0
WHERE alarmRule.IsDeleted = 0
ORDER BY alarmRule.CustomerId, alarmRule.ScopeEntityId, alarmRule.TagName;
