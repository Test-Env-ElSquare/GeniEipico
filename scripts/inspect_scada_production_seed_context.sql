USE [GeniePark];
SET NOCOUNT ON;

DECLARE @UtcNow DATETIME2 = SYSUTCDATETIME();

SELECT
    DB_NAME() AS DatabaseName,
    @UtcNow AS UtcNow,
    CONVERT(date, @UtcNow) AS UtcDate;

-------------------------------------------------------------------------------
-- 1. Data volume in the tables used by the SCADA production zone.
-------------------------------------------------------------------------------

SELECT 'Org.Customers' AS TableName, COUNT_BIG(*) AS RowCount FROM Org.Customers
UNION ALL SELECT 'Org.Factories', COUNT_BIG(*) FROM Org.Factories
UNION ALL SELECT 'Org.ProductionLines', COUNT_BIG(*) FROM Org.ProductionLines
UNION ALL SELECT 'Scada.Machines', COUNT_BIG(*) FROM Scada.Machines
UNION ALL SELECT 'Scada.MachineStates', COUNT_BIG(*) FROM Scada.MachineStates
UNION ALL SELECT 'Scada.MachineStateHistories', COUNT_BIG(*) FROM Scada.MachineStateHistories
UNION ALL SELECT 'Planning.Shifts', COUNT_BIG(*) FROM Planning.Shifts
UNION ALL SELECT 'MasterData.Skus', COUNT_BIG(*) FROM MasterData.Skus
UNION ALL SELECT 'Production.Batches', COUNT_BIG(*) FROM Production.Batches
UNION ALL SELECT 'Production.BatchStageLogs', COUNT_BIG(*) FROM Production.BatchStageLogs
UNION ALL SELECT 'Analytics.OeeSnapshots', COUNT_BIG(*) FROM Analytics.OeeSnapshots
UNION ALL SELECT 'Asset.Assets', COUNT_BIG(*) FROM Asset.Assets
UNION ALL SELECT 'IoT.Devices', COUNT_BIG(*) FROM IoT.Devices
UNION ALL SELECT 'IoT.TelemetryTags', COUNT_BIG(*) FROM IoT.TelemetryTags
ORDER BY TableName;

-------------------------------------------------------------------------------
-- 2. Customer -> Factory -> Line -> Machine hierarchy and machine definitions.
-------------------------------------------------------------------------------

SELECT
    c.Id AS CustomerId,
    c.Code AS CustomerCode,
    c.Name AS CustomerName,
    f.Id AS FactoryId,
    f.Code AS FactoryCode,
    f.Name AS FactoryName,
    l.Id AS LineId,
    l.Code AS LineCode,
    l.Name AS LineName,
    m.Id AS MachineId,
    m.Code AS MachineCode,
    m.Name AS MachineName,
    m.MachineType,
    m.RatedSpeedRpm,
    m.DeviceId,
    a.Id AS AssetId,
    a.Code AS AssetCode,
    m.IsActive AS MachineIsActive
FROM Org.Customers c
JOIN Org.Factories f
  ON f.CustomerId = c.Id
 AND f.IsDeleted = 0
JOIN Org.ProductionLines l
  ON l.FactoryId = f.Id
 AND l.IsDeleted = 0
JOIN Scada.Machines m
  ON m.LineId = l.Id
 AND m.IsDeleted = 0
LEFT JOIN Asset.Assets a
  ON a.MachineId = m.Id
 AND a.IsDeleted = 0
WHERE c.IsDeleted = 0
ORDER BY c.Id, f.Id, l.Id, m.Id;

-------------------------------------------------------------------------------
-- 3. Shift definitions. These determine Duration = 0 and Duration = 1.
-------------------------------------------------------------------------------

SELECT
    s.Id AS ShiftId,
    s.FactoryId,
    f.Code AS FactoryCode,
    f.Name AS FactoryName,
    s.Name AS ShiftName,
    s.StartTime,
    s.EndTime,
    s.IsActive,
    s.IsDeleted
FROM Planning.Shifts s
JOIN Org.Factories f ON f.Id = s.FactoryId
ORDER BY s.FactoryId, s.StartTime;

-------------------------------------------------------------------------------
-- 4. Current machine snapshot. OEE is intentionally not stored here.
-------------------------------------------------------------------------------

SELECT
    m.LineId,
    ms.MachineId,
    m.Code AS MachineCode,
    m.Name AS MachineName,
    ms.State,
    ms.SpeedRpm,
    ms.TempCelsius,
    ms.VibrationMms,
    ms.EnergyKwh,
    ms.ActiveBatchId,
    ms.StateStartedAt,
    ms.TelemetryAt,
    ms.CreatedAt,
    ms.UpdatedAt
FROM Scada.MachineStates ms
JOIN Scada.Machines m ON m.Id = ms.MachineId
ORDER BY m.LineId, ms.MachineId;

-------------------------------------------------------------------------------
-- 5. Historical state transitions used to build the state timeline.
-------------------------------------------------------------------------------

SELECT TOP (1000)
    h.Id,
    m.LineId,
    h.MachineId,
    m.Code AS MachineCode,
    h.FromState,
    h.ToState,
    h.TransitionedAt,
    h.TriggerSource,
    h.Notes
FROM Scada.MachineStateHistories h
JOIN Scada.Machines m ON m.Id = h.MachineId
ORDER BY h.TransitionedAt DESC, h.Id DESC;

-------------------------------------------------------------------------------
-- 6. OEE snapshots used by Availability, Quality, OEE and historical Speed.
-------------------------------------------------------------------------------

SELECT TOP (1000)
    o.*,
    m.LineId,
    m.Code AS MachineCode,
    m.Name AS MachineName,
    s.FactoryId,
    s.Name AS ShiftName
FROM Analytics.OeeSnapshots o
JOIN Scada.Machines m ON m.Id = o.MachineId
JOIN Planning.Shifts s ON s.Id = o.ShiftId
ORDER BY o.ComputedAt DESC, o.Id DESC;

-------------------------------------------------------------------------------
-- 7. Batches that can appear as key events for each production line.
-------------------------------------------------------------------------------

SELECT TOP (1000)
    b.Id AS BatchId,
    b.Uid AS BatchUid,
    b.CustomerId,
    f.Id AS FactoryId,
    f.Code AS FactoryCode,
    b.LineId,
    l.Code AS LineCode,
    l.Name AS LineName,
    b.SkuId,
    sku.Code AS SkuCode,
    sku.Name AS ProductName,
    b.ShiftId,
    b.Status,
    b.Stage,
    b.PlannedStart,
    b.ActualStart,
    b.PlannedEnd,
    b.ActualEnd,
    b.ProducedQty,
    b.RejectedQty,
    b.IsDeleted
FROM Production.Batches b
JOIN Org.ProductionLines l ON l.Id = b.LineId
JOIN Org.Factories f ON f.Id = l.FactoryId
JOIN MasterData.Skus sku ON sku.Id = b.SkuId
ORDER BY COALESCE(b.ActualStart, b.PlannedStart) DESC, b.Id DESC;

-------------------------------------------------------------------------------
-- 8. Batch stage events and optional machine linkage.
-------------------------------------------------------------------------------

SELECT TOP (1000)
    log.Id,
    log.BatchId,
    b.Uid AS BatchUid,
    b.LineId,
    log.MachineId,
    m.Code AS MachineCode,
    log.FromStage,
    log.ToStage,
    log.TransitionedAt,
    log.Notes
FROM Production.BatchStageLogs log
JOIN Production.Batches b ON b.Id = log.BatchId
LEFT JOIN Scada.Machines m ON m.Id = log.MachineId
ORDER BY log.TransitionedAt DESC, log.Id DESC;

-------------------------------------------------------------------------------
-- 9. Existing rows dated today. This shows exactly what is missing before seed.
-------------------------------------------------------------------------------

SELECT
    'MachineStates telemetry today' AS DataSet,
    COUNT_BIG(*) AS RowCount
FROM Scada.MachineStates
WHERE TelemetryAt >= CONVERT(date, @UtcNow)
  AND TelemetryAt < DATEADD(day, 1, CONVERT(date, @UtcNow))
UNION ALL
SELECT
    'Machine state transitions today',
    COUNT_BIG(*)
FROM Scada.MachineStateHistories
WHERE TransitionedAt >= CONVERT(date, @UtcNow)
  AND TransitionedAt < DATEADD(day, 1, CONVERT(date, @UtcNow))
UNION ALL
SELECT
    'OEE snapshots computed today',
    COUNT_BIG(*)
FROM Analytics.OeeSnapshots
WHERE ComputedAt >= CONVERT(date, @UtcNow)
  AND ComputedAt < DATEADD(day, 1, CONVERT(date, @UtcNow))
UNION ALL
SELECT
    'Batches actually started today',
    COUNT_BIG(*)
FROM Production.Batches
WHERE IsDeleted = 0
  AND ActualStart >= CONVERT(date, @UtcNow)
  AND ActualStart < DATEADD(day, 1, CONVERT(date, @UtcNow));
