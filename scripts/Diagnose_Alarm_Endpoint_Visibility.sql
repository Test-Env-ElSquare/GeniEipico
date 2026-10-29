/*
    Diagnoses alarm visibility using the same source priority as the API:
      Machine -> Silo -> Device -> System

    Change the filter values to match the API request and authenticated token.
*/

USE GeniePark;
SET NOCOUNT ON;

DECLARE @TokenCustomerId int = 1;
DECLARE @RequestedFactoryId int = 1;
DECLARE @RequestedLineId int = NULL;
DECLARE @From datetime2 = DATEADD(month, -1, SYSUTCDATETIME());
DECLARE @To datetime2 = SYSUTCDATETIME();

SELECT
    @@SERVERNAME AS SqlServer,
    DB_NAME() AS DatabaseName,
    SYSUTCDATETIME() AS DatabaseUtcNow,
    @TokenCustomerId AS TokenCustomerId,
    @RequestedFactoryId AS RequestedFactoryId,
    @RequestedLineId AS RequestedLineId,
    @From AS FilterFrom,
    @To AS FilterTo;

SELECT
    COUNT(*) AS TotalAlarmEvents,
    SUM(CASE WHEN Title LIKE 'TEST %' THEN 1 ELSE 0 END) AS InjectedTestEvents,
    MAX(Id) AS MaximumAlarmEventId
FROM Alarm.AlarmEvents;

;WITH AlarmSources AS
(
    SELECT
        alarmEvent.Id,
        alarmEvent.RuleId,
        alarmEvent.CustomerId,
        alarmEvent.DeviceId,
        alarmEvent.MachineId,
        alarmEvent.SiloId,
        alarmEvent.Title,
        alarmEvent.Severity,
        alarmEvent.TagName,
        alarmEvent.TriggerValue,
        alarmEvent.TriggeredAt,
        alarmEvent.AcknowledgedAt,
        alarmEvent.ResolvedAt,
        CASE
            WHEN alarmEvent.MachineId IS NOT NULL THEN 'machine'
            WHEN alarmEvent.SiloId IS NOT NULL THEN 'silo'
            WHEN alarmEvent.DeviceId IS NOT NULL THEN 'device'
            ELSE 'system'
        END AS SourceType,
        CASE
            WHEN alarmEvent.MachineId IS NOT NULL THEN machineLine.FactoryId
            WHEN alarmEvent.SiloId IS NOT NULL THEN silo.FactoryId
            WHEN alarmEvent.DeviceId IS NOT NULL THEN device.FactoryId
            ELSE 0
        END AS ResolvedFactoryId,
        CASE
            WHEN alarmEvent.MachineId IS NOT NULL THEN machine.LineId
            WHEN alarmEvent.DeviceId IS NOT NULL THEN device.LineId
            ELSE NULL
        END AS ResolvedLineId
    FROM Alarm.AlarmEvents alarmEvent
    LEFT JOIN Scada.Machines machine
        ON machine.Id = alarmEvent.MachineId
    LEFT JOIN Org.ProductionLines machineLine
        ON machineLine.Id = machine.LineId
    LEFT JOIN Scada.Silos silo
        ON silo.Id = alarmEvent.SiloId
    LEFT JOIN IoT.Devices device
        ON device.Id = alarmEvent.DeviceId
)
SELECT
    alarmSource.*,
    CASE
        WHEN alarmSource.ResolvedAt IS NULL THEN 1
        ELSE 0
    END AS MatchesActiveStatus,
    CASE
        WHEN alarmSource.AcknowledgedAt IS NOT NULL
          OR alarmSource.ResolvedAt IS NOT NULL THEN 1
        ELSE 0
    END AS MatchesHistoryStatus,
    CASE
        WHEN alarmSource.CustomerId = @TokenCustomerId THEN 1
        ELSE 0
    END AS MatchesTokenCustomer,
    CASE
        WHEN alarmSource.ResolvedFactoryId = @RequestedFactoryId THEN 1
        ELSE 0
    END AS MatchesFactory,
    CASE
        WHEN @RequestedLineId IS NULL
          OR alarmSource.ResolvedLineId = @RequestedLineId THEN 1
        ELSE 0
    END AS MatchesLine,
    CASE
        WHEN alarmSource.TriggeredAt >= @From
         AND alarmSource.TriggeredAt <= @To THEN 1
        ELSE 0
    END AS MatchesTime,
    CASE
        WHEN alarmSource.CustomerId = @TokenCustomerId
         AND alarmSource.ResolvedFactoryId = @RequestedFactoryId
         AND (@RequestedLineId IS NULL OR alarmSource.ResolvedLineId = @RequestedLineId)
         AND alarmSource.TriggeredAt >= @From
         AND alarmSource.TriggeredAt <= @To
         AND alarmSource.ResolvedAt IS NULL THEN 1
        ELSE 0
    END AS ExpectedInActive,
    CASE
        WHEN alarmSource.CustomerId = @TokenCustomerId
         AND alarmSource.ResolvedFactoryId = @RequestedFactoryId
         AND (@RequestedLineId IS NULL OR alarmSource.ResolvedLineId = @RequestedLineId)
         AND alarmSource.TriggeredAt >= @From
         AND alarmSource.TriggeredAt <= @To
         AND
         (
             alarmSource.AcknowledgedAt IS NOT NULL
             OR alarmSource.ResolvedAt IS NOT NULL
         ) THEN 1
        ELSE 0
    END AS ExpectedInHistory
FROM AlarmSources alarmSource
ORDER BY alarmSource.Id DESC;

SELECT TOP (100)
    alarmRule.Id,
    alarmRule.CustomerId,
    alarmRule.Name,
    alarmRule.ScopeEntityId AS MachineId,
    alarmRule.TagName,
    alarmRule.MinimumValue,
    alarmRule.MaximumValue,
    alarmRule.Severity,
    alarmRule.IsActive,
    alarmRule.CreateWorkOrder,
    alarmScope.EntityId AS ScopeMachineId,
    alarmAction.ActionType,
    alarmAction.IsEnabled AS ActionEnabled,
    alarmAction.AssignedToId
FROM Alarm.AlarmRules alarmRule
LEFT JOIN Alarm.AlarmRuleScopes alarmScope
    ON alarmScope.AlarmRuleId = alarmRule.Id
   AND alarmScope.IsDeleted = 0
LEFT JOIN Alarm.AlarmRuleActions alarmAction
    ON alarmAction.AlarmRuleId = alarmRule.Id
   AND alarmAction.IsDeleted = 0
WHERE alarmRule.IsDeleted = 0
ORDER BY alarmRule.Id;
