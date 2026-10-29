SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
    GeniePark demo seed.
    Run this after EF creates the new database schema.
    Target shape:
      - 2 customers
      - 2 factories per customer
      - 3 lines per factory
      - 3 machines per line
      - rich SKU/BOM/routes/batches/SCADA/IoT/alarms/maintenance/analytics/config data

    Note:
      Open the target database first in SSMS, then run this script.
*/

BEGIN TRANSACTION;

DECLARE @now datetime2 = SYSUTCDATETIME();
DECLARE @today datetime2 = CONVERT(date, @now);
DECLARE @ym char(4) = FORMAT(@now, 'yyMM');

IF OBJECT_ID('Planning.ShiftFactories', 'U') IS NULL
    THROW 51000, 'Planning.ShiftFactories was not found. Run EF database update after the ShiftFactory change first.', 1;

-------------------------------------------------------------------------------
-- Organization
-------------------------------------------------------------------------------

INSERT INTO Org.Customers
    (Code, Name, Tier, SubscriptionStarts, SubscriptionEnds, CountryCode, ContactEmail, Industry, ErpSystem, ErpEndpointUrl, IsActive)
SELECT v.Code, v.Name, v.Tier, DATEADD(month, -2, @today), DATEADD(year, 1, @today), v.CountryCode, v.ContactEmail, v.Industry, v.ErpSystem, v.ErpEndpointUrl, 1
FROM (VALUES
    ('EGYPHARMA', 'Egypt Pharma Manufacturing', 'enterprise', 'EGY', 'ops@egypharma.local', 'Pharmaceutical Solid Dosage', 'SAP S/4HANA', 'https://erp.egypharma.local/api'),
    ('NILEFOOD',  'Nile Food Processing',       'professional', 'EGY', 'ops@nilefood.local',  'Food and Packaging',              'Oracle ERP', 'https://erp.nilefood.local/api')
) v(Code, Name, Tier, CountryCode, ContactEmail, Industry, ErpSystem, ErpEndpointUrl)
WHERE NOT EXISTS (SELECT 1 FROM Org.Customers c WHERE c.Code = v.Code AND c.IsDeleted = 0);

INSERT INTO Org.Factories
    (CustomerId, Code, Name, City, CountryCode, TimeZone, IsActive)
SELECT c.Id, v.FactoryCode, v.FactoryName, v.City, 'EGY', 'Africa/Cairo', 1
FROM Org.Customers c
CROSS APPLY (VALUES
    (CONCAT(c.Code, '-F01'), CONCAT(c.Name, ' - Cairo Plant'), 'Cairo'),
    (CONCAT(c.Code, '-F02'), CONCAT(c.Name, ' - Alexandria Plant'), 'Alexandria')
) v(FactoryCode, FactoryName, City)
WHERE c.Code IN ('EGYPHARMA', 'NILEFOOD')
  AND NOT EXISTS (SELECT 1 FROM Org.Factories f WHERE f.CustomerId = c.Id AND f.Code = v.FactoryCode AND f.IsDeleted = 0);

INSERT INTO Org.ProductionLines
    (FactoryId, Code, Name, ProductType, MaxSpeedUpm, IsActive)
SELECT f.Id, v.LineCode, v.LineName, v.ProductType, v.MaxSpeedUpm, 1
FROM Org.Factories f
CROSS APPLY (VALUES
    ('L01', 'Solid Dosage Line', 'Tablets', 1800),
    ('L02', 'Capsule Filling Line', 'Capsules', 1400),
    ('L03', 'Packaging Line', 'Packaging', 2200)
) v(LineCode, LineName, ProductType, MaxSpeedUpm)
WHERE NOT EXISTS (SELECT 1 FROM Org.ProductionLines l WHERE l.FactoryId = f.Id AND l.Code = v.LineCode AND l.IsDeleted = 0);

-------------------------------------------------------------------------------
-- Shifts and shared shift links
-------------------------------------------------------------------------------

INSERT INTO Planning.Shifts
    (FactoryId, Name, StartTime, EndTime, IsActive)
SELECT f.Id, v.Name, CAST(v.StartTime AS time), CAST(v.EndTime AS time), 1
FROM Org.Factories f
CROSS APPLY (VALUES
    ('Morning Shift', '08:00:00', '16:00:00'),
    ('Evening Shift', '16:00:00', '00:00:00'),
    ('Night Shift',   '00:00:00', '08:00:00')
) v(Name, StartTime, EndTime)
WHERE NOT EXISTS (SELECT 1 FROM Planning.Shifts s WHERE s.FactoryId = f.Id AND s.Name = v.Name AND s.IsDeleted = 0);

INSERT INTO Planning.ShiftFactories (ShiftId, FactoryId)
SELECT s.Id, f2.Id
FROM Planning.Shifts s
JOIN Org.Factories f1 ON f1.Id = s.FactoryId
JOIN Org.Factories f2 ON f2.CustomerId = f1.CustomerId
WHERE s.IsDeleted = 0
  AND f2.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Planning.ShiftFactories sf WHERE sf.ShiftId = s.Id AND sf.FactoryId = f2.Id);

-------------------------------------------------------------------------------
-- Master data: materials, SKUs, parameters, BOM
-------------------------------------------------------------------------------

INSERT INTO MasterData.Materials
    (CustomerId, Code, Name, MaterialType, Uom, ErpCode, SupplierCode, ReorderPointKg, MinStockKg, IsActive)
SELECT c.Id, v.Code, v.Name, v.MaterialType, v.Uom, v.ErpCode, v.SupplierCode, v.ReorderPointKg, v.MinStockKg, 1
FROM Org.Customers c
CROSS APPLY (VALUES
    ('RM-PARA',   'Paracetamol Active Ingredient', 'RM',  'kg', 'SAP-RM-001', 'SUP-API-01', 500.000, 300.000),
    ('RM-MCC',    'Microcrystalline Cellulose',    'RM',  'kg', 'SAP-RM-002', 'SUP-EXC-01', 800.000, 450.000),
    ('RM-STARCH', 'Corn Starch',                    'RM',  'kg', 'SAP-RM-003', 'SUP-EXC-02', 700.000, 400.000),
    ('PKG-BLSTR', 'PVC Blister Roll',               'PKG', 'roll', 'SAP-PK-001', 'SUP-PKG-01', 40.000, 20.000),
    ('RM-AMOX',   'Amoxicillin Trihydrate',         'RM',  'kg', 'SAP-RM-004', 'SUP-API-02', 250.000, 120.000),
    ('RM-LACT',   'Lactose Monohydrate',            'RM',  'kg', 'SAP-RM-005', 'SUP-EXC-03', 600.000, 300.000)
) v(Code, Name, MaterialType, Uom, ErpCode, SupplierCode, ReorderPointKg, MinStockKg)
WHERE NOT EXISTS (SELECT 1 FROM MasterData.Materials m WHERE m.CustomerId = c.Id AND m.Code = v.Code AND m.IsDeleted = 0);

INSERT INTO MasterData.Skus
    (CustomerId, Code, Name, Category, Uom, StandardBatchSize, ShelfLifeMonths, RatedSpeedRpm, UnitWeightG, ErpSource, ErpMaterialCode, LastErpSyncAt, IsActive)
SELECT c.Id, v.Code, v.Name, v.Category, v.Uom, v.StandardBatchSize, v.ShelfLifeMonths, v.RatedSpeedRpm, v.UnitWeightG, v.ErpSource, v.ErpMaterialCode, DATEADD(hour, -6, @now), 1
FROM Org.Customers c
CROSS APPLY (VALUES
    ('SKU-PARA-500', 'Paracetamol 500mg Tablets',    'Analgesic',  'tablet', 100000, 24, 1500, 0.6500, 1, 'SAP-FG-0001'),
    ('SKU-AMOX-250', 'Amoxicillin 250mg Capsules',   'Antibiotic', 'capsule', 80000, 18, 1200, 0.4100, 1, 'SAP-FG-0002'),
    ('SKU-IBU-400',  'Ibuprofen 400mg Tablets',      'NSAID',      'tablet', 120000, 30, 1600, 0.5800, 0, 'MES-FG-0003')
) v(Code, Name, Category, Uom, StandardBatchSize, ShelfLifeMonths, RatedSpeedRpm, UnitWeightG, ErpSource, ErpMaterialCode)
WHERE NOT EXISTS (SELECT 1 FROM MasterData.Skus s WHERE s.CustomerId = c.Id AND s.Code = v.Code AND s.IsDeleted = 0);

INSERT INTO MasterData.SkuProcessParameters
    (SkuId, Name, Value, Unit, DataType)
SELECT s.Id, v.Name, v.Value, v.Unit, 'number'
FROM MasterData.Skus s
CROSS APPLY (VALUES
    ('Mix Time', '15', 'min'),
    ('Press Force', '12', 'kN'),
    ('Drying Temp', '60', 'C'),
    ('Coating Thickness', '0.10', 'mm')
) v(Name, Value, Unit)
WHERE NOT EXISTS (SELECT 1 FROM MasterData.SkuProcessParameters p WHERE p.SkuId = s.Id AND p.Name = v.Name AND p.IsDeleted = 0);

INSERT INTO MasterData.BillOfMaterials
    (SkuId, MaterialId, QtyPerBatch, TolerancePct, SequenceNo, IsCritical)
SELECT s.Id, m.Id, v.QtyPerBatch, v.TolerancePct, v.SequenceNo, v.IsCritical
FROM MasterData.Skus s
JOIN Org.Customers c ON c.Id = s.CustomerId
CROSS APPLY (VALUES
    ('RM-PARA', 75.5000, 2.00, 1, 1),
    ('RM-MCC', 120.0000, 2.50, 2, 0),
    ('RM-STARCH', 30.0000, 3.00, 3, 0),
    ('PKG-BLSTR', 20.0000, 1.50, 4, 0)
) v(MaterialCode, QtyPerBatch, TolerancePct, SequenceNo, IsCritical)
JOIN MasterData.Materials m ON m.CustomerId = c.Id AND m.Code = v.MaterialCode AND m.IsDeleted = 0
WHERE s.Code = 'SKU-PARA-500'
  AND NOT EXISTS (SELECT 1 FROM MasterData.BillOfMaterials b WHERE b.SkuId = s.Id AND b.MaterialId = m.Id AND b.IsDeleted = 0);

INSERT INTO MasterData.BillOfMaterials
    (SkuId, MaterialId, QtyPerBatch, TolerancePct, SequenceNo, IsCritical)
SELECT s.Id, m.Id, v.QtyPerBatch, v.TolerancePct, v.SequenceNo, v.IsCritical
FROM MasterData.Skus s
JOIN Org.Customers c ON c.Id = s.CustomerId
CROSS APPLY (VALUES
    ('RM-AMOX', 55.0000, 2.00, 1, 1),
    ('RM-LACT', 90.0000, 2.50, 2, 0),
    ('RM-STARCH', 18.0000, 3.00, 3, 0),
    ('PKG-BLSTR', 16.0000, 1.50, 4, 0)
) v(MaterialCode, QtyPerBatch, TolerancePct, SequenceNo, IsCritical)
JOIN MasterData.Materials m ON m.CustomerId = c.Id AND m.Code = v.MaterialCode AND m.IsDeleted = 0
WHERE s.Code = 'SKU-AMOX-250'
  AND NOT EXISTS (SELECT 1 FROM MasterData.BillOfMaterials b WHERE b.SkuId = s.Id AND b.MaterialId = m.Id AND b.IsDeleted = 0);

INSERT INTO MasterData.BillOfMaterials
    (SkuId, MaterialId, QtyPerBatch, TolerancePct, SequenceNo, IsCritical)
SELECT s.Id, m.Id, v.QtyPerBatch, v.TolerancePct, v.SequenceNo, v.IsCritical
FROM MasterData.Skus s
JOIN Org.Customers c ON c.Id = s.CustomerId
CROSS APPLY (VALUES
    ('RM-PARA', 45.0000, 2.00, 1, 1),
    ('RM-MCC', 95.0000, 2.50, 2, 0),
    ('RM-LACT', 40.0000, 3.00, 3, 0),
    ('PKG-BLSTR', 22.0000, 1.50, 4, 0)
) v(MaterialCode, QtyPerBatch, TolerancePct, SequenceNo, IsCritical)
JOIN MasterData.Materials m ON m.CustomerId = c.Id AND m.Code = v.MaterialCode AND m.IsDeleted = 0
WHERE s.Code = 'SKU-IBU-400'
  AND NOT EXISTS (SELECT 1 FROM MasterData.BillOfMaterials b WHERE b.SkuId = s.Id AND b.MaterialId = m.Id AND b.IsDeleted = 0);

-------------------------------------------------------------------------------
-- Planning: routes, route steps, MRP
-------------------------------------------------------------------------------

INSERT INTO Planning.Routes
    (CustomerId, Code, Name, SkuId, CycleTimeHours, Status, Version)
SELECT s.CustomerId, CONCAT('RT-', s.Code), CONCAT(s.Name, ' Standard Route'), s.Id, 8.50, 'active', 1
FROM MasterData.Skus s
WHERE NOT EXISTS (SELECT 1 FROM Planning.Routes r WHERE r.CustomerId = s.CustomerId AND r.Code = CONCAT('RT-', s.Code) AND r.IsDeleted = 0);

INSERT INTO Planning.RouteSteps
    (RouteId, StepNo, StepName, MachineType, EstDurationMin)
SELECT r.Id, v.StepNo, v.StepName, v.MachineType, v.EstDurationMin
FROM Planning.Routes r
CROSS APPLY (VALUES
    (1, 'Warehouse Release', 'Warehouse', 30),
    (2, 'Dispense Materials', 'Scale', 90),
    (3, 'Manufacturing', 'Tablet Press', 240),
    (4, 'Packaging', 'Packaging', 120)
) v(StepNo, StepName, MachineType, EstDurationMin)
WHERE NOT EXISTS (SELECT 1 FROM Planning.RouteSteps rs WHERE rs.RouteId = r.Id AND rs.StepNo = v.StepNo AND rs.IsDeleted = 0);

INSERT INTO Planning.RouteStepParameters
    (RouteStepId, Name, Value, Unit, DataType)
SELECT rs.Id, v.Name, v.Value, v.Unit, v.DataType
FROM Planning.RouteSteps rs
CROSS APPLY (VALUES
    ('targetOee', '82', '%', 'number'),
    ('operatorCheck', 'required', NULL, 'string')
) v(Name, Value, Unit, DataType)
WHERE NOT EXISTS (SELECT 1 FROM Planning.RouteStepParameters p WHERE p.RouteStepId = rs.Id AND p.Name = v.Name AND p.IsDeleted = 0);

INSERT INTO Planning.MrpRequirements
    (CustomerId, MaterialId, PlanDate, RequiredQty, AvailableQty, GapQty, MrpStatus, PoReference, PoExpectedDate)
SELECT m.CustomerId, m.Id, DATEADD(day, v.DayOffset, @today), v.RequiredQty, v.AvailableQty,
       CASE WHEN v.RequiredQty > v.AvailableQty THEN v.RequiredQty - v.AvailableQty ELSE 0 END,
       CASE WHEN v.RequiredQty > v.AvailableQty THEN 'shortage' ELSE 'ok' END,
       CASE WHEN v.RequiredQty > v.AvailableQty THEN CONCAT('PO-', m.Code, '-', v.DayOffset) ELSE NULL END,
       CASE WHEN v.RequiredQty > v.AvailableQty THEN DATEADD(day, v.DayOffset + 3, @today) ELSE NULL END
FROM MasterData.Materials m
CROSS APPLY (VALUES
    (1, 300.000, 450.000),
    (7, 950.000, 650.000)
) v(DayOffset, RequiredQty, AvailableQty)
WHERE NOT EXISTS (SELECT 1 FROM Planning.MrpRequirements mr WHERE mr.CustomerId = m.CustomerId AND mr.MaterialId = m.Id AND mr.PlanDate = DATEADD(day, v.DayOffset, @today));

-------------------------------------------------------------------------------
-- IoT devices
-------------------------------------------------------------------------------

INSERT INTO IoT.DeviceTypes
    (Code, Label, Protocol, DefaultPollIntervalMs, IsActive)
SELECT v.Code, v.Label, v.Protocol, v.DefaultPollIntervalMs, 1
FROM (VALUES
    ('PLC',   'Production PLC', 'MQTT', 1000),
    ('SCALE', 'Industrial Scale', 'OPC-UA', 500),
    ('SILO',  'Silo Level Sensor', 'MQTT', 2000),
    ('ENV',   'Environment Sensor', 'MQTT', 5000)
) v(Code, Label, Protocol, DefaultPollIntervalMs)
WHERE NOT EXISTS (SELECT 1 FROM IoT.DeviceTypes dt WHERE dt.Code = v.Code AND dt.IsDeleted = 0);

INSERT INTO IoT.DeviceTypeTagDefinitions
    (DeviceTypeId, TagName, DataType, Unit, IsRequired, MinValue, MaxValue)
SELECT dt.Id, v.TagName, v.DataType, v.Unit, v.IsRequired, v.MinValue, v.MaxValue
FROM IoT.DeviceTypes dt
CROSS APPLY (VALUES
    ('speedRpm', 'number', 'rpm', 1, 0.0000, 3000.0000),
    ('temperature', 'number', 'C', 1, 0.0000, 120.0000),
    ('vibration', 'number', 'mm/s', 0, 0.0000, 20.0000),
    ('energyKwh', 'number', 'kWh', 0, 0.0000, 999999.0000)
) v(TagName, DataType, Unit, IsRequired, MinValue, MaxValue)
WHERE dt.Code = 'PLC'
  AND NOT EXISTS (SELECT 1 FROM IoT.DeviceTypeTagDefinitions d WHERE d.DeviceTypeId = dt.Id AND d.TagName = v.TagName AND d.IsDeleted = 0);

INSERT INTO IoT.DeviceTypeTagDefinitions
    (DeviceTypeId, TagName, DataType, Unit, IsRequired, MinValue, MaxValue)
SELECT dt.Id, v.TagName, v.DataType, v.Unit, v.IsRequired, v.MinValue, v.MaxValue
FROM IoT.DeviceTypes dt
CROSS APPLY (VALUES
    ('weightKg', 'number', 'kg', 1, 0.0000, 500.0000),
    ('state', 'string', NULL, 1, NULL, NULL)
) v(TagName, DataType, Unit, IsRequired, MinValue, MaxValue)
WHERE dt.Code = 'SCALE'
  AND NOT EXISTS (SELECT 1 FROM IoT.DeviceTypeTagDefinitions d WHERE d.DeviceTypeId = dt.Id AND d.TagName = v.TagName AND d.IsDeleted = 0);

INSERT INTO IoT.DeviceTypeTagDefinitions
    (DeviceTypeId, TagName, DataType, Unit, IsRequired, MinValue, MaxValue)
SELECT dt.Id, v.TagName, v.DataType, v.Unit, v.IsRequired, v.MinValue, v.MaxValue
FROM IoT.DeviceTypes dt
CROSS APPLY (VALUES
    ('levelPct', 'number', '%', 1, 0.0000, 100.0000),
    ('qtyKg', 'number', 'kg', 1, 0.0000, 20000.0000)
) v(TagName, DataType, Unit, IsRequired, MinValue, MaxValue)
WHERE dt.Code = 'SILO'
  AND NOT EXISTS (SELECT 1 FROM IoT.DeviceTypeTagDefinitions d WHERE d.DeviceTypeId = dt.Id AND d.TagName = v.TagName AND d.IsDeleted = 0);

INSERT INTO IoT.Devices
    (CustomerId, FactoryId, LineId, DeviceTypeId, SerialNumber, MqttClientId, MqttTopicPrefix, ApiKeyHash, FirmwareVersion, HardwareModel, PollIntervalMs, LastSeenAt, ConnectionStatus, ProvisionedAt, IsActive)
SELECT c.Id, f.Id, l.Id, dt.Id,
       CONCAT('DEV-', f.Code, '-', l.Code, '-', v.MachineCode),
       CONCAT('mqtt-', LOWER(f.Code), '-', LOWER(l.Code), '-', LOWER(v.MachineCode)),
       CONCAT('geniepark/', LOWER(c.Code), '/', LOWER(f.Code), '/', LOWER(l.Code), '/', LOWER(v.MachineCode)),
       'seed-hash', '1.4.2', 'Siemens S7-1500', 1000, DATEADD(minute, -5, @now), 'online', DATEADD(day, -30, @now), 1
FROM Org.Customers c
JOIN Org.Factories f ON f.CustomerId = c.Id AND f.IsDeleted = 0
JOIN Org.ProductionLines l ON l.FactoryId = f.Id AND l.IsDeleted = 0
JOIN IoT.DeviceTypes dt ON dt.Code = 'PLC' AND dt.IsDeleted = 0
CROSS APPLY (VALUES
    ('MIX01'), ('TAB01'), ('PKG01')
) v(MachineCode)
WHERE NOT EXISTS (SELECT 1 FROM IoT.Devices d WHERE d.SerialNumber = CONCAT('DEV-', f.Code, '-', l.Code, '-', v.MachineCode) AND d.IsDeleted = 0);

INSERT INTO IoT.Devices
    (CustomerId, FactoryId, LineId, DeviceTypeId, SerialNumber, MqttClientId, MqttTopicPrefix, ApiKeyHash, FirmwareVersion, HardwareModel, PollIntervalMs, LastSeenAt, ConnectionStatus, ProvisionedAt, IsActive)
SELECT c.Id, f.Id, NULL, dt.Id,
       CONCAT('DEV-', f.Code, '-', v.Code),
       CONCAT('mqtt-', LOWER(f.Code), '-', LOWER(v.Code)),
       CONCAT('geniepark/', LOWER(c.Code), '/', LOWER(f.Code), '/', LOWER(v.Code)),
       'seed-hash', '2.1.0', v.Model, v.PollMs, DATEADD(minute, -3, @now), 'online', DATEADD(day, -25, @now), 1
FROM Org.Customers c
JOIN Org.Factories f ON f.CustomerId = c.Id AND f.IsDeleted = 0
CROSS APPLY (VALUES
    ('SILO01', 'SILO', 'VEGA Level Sensor', 2000),
    ('SILO02', 'SILO', 'VEGA Level Sensor', 2000),
    ('SILO03', 'SILO', 'VEGA Level Sensor', 2000),
    ('SCALE01', 'SCALE', 'Mettler Toledo IND', 500),
    ('SCALE02', 'SCALE', 'Mettler Toledo IND', 500)
) v(Code, Kind, Model, PollMs)
JOIN IoT.DeviceTypes dt ON dt.Code = CASE WHEN v.Kind = 'SILO' THEN 'SILO' ELSE 'SCALE' END AND dt.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM IoT.Devices d WHERE d.SerialNumber = CONCAT('DEV-', f.Code, '-', v.Code) AND d.IsDeleted = 0);

-------------------------------------------------------------------------------
-- SCADA machines, silos, scales
-------------------------------------------------------------------------------

INSERT INTO Scada.Machines
    (LineId, DeviceId, Code, Name, MachineType, Manufacturer, ModelNumber, RatedSpeedRpm, RatedPowerKw, InstallDate, IsActive)
SELECT l.Id, d.Id, v.MachineCode, v.MachineName, v.MachineType, v.Manufacturer, v.ModelNumber, v.RatedSpeedRpm, v.RatedPowerKw, DATEADD(year, -2, @today), 1
FROM Org.ProductionLines l
JOIN Org.Factories f ON f.Id = l.FactoryId
CROSS APPLY (VALUES
    ('MIX01', 'High Shear Mixer', 'Mixer', 'GEA', 'HSM-1200', 900, 18.50),
    ('TAB01', 'Tablet Press', 'Tablet Press', 'Korsch', 'XL-400', 1500, 32.00),
    ('PKG01', 'Blister Packaging Machine', 'Packaging', 'Uhlmann', 'B1240', 1800, 24.25)
) v(MachineCode, MachineName, MachineType, Manufacturer, ModelNumber, RatedSpeedRpm, RatedPowerKw)
JOIN IoT.Devices d ON d.SerialNumber = CONCAT('DEV-', f.Code, '-', l.Code, '-', v.MachineCode) AND d.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM Scada.Machines m WHERE m.LineId = l.Id AND m.Code = v.MachineCode AND m.IsDeleted = 0);

INSERT INTO Scada.Silos
    (FactoryId, DeviceId, Code, Name, SiloType, Zone, CapacityKg, LowLevelPct, CriticalLevelPct, CurrentQtyKg, CurrentMaterialId, CurrentLotUid, TelemetryAt)
SELECT f.Id, d.Id, v.Code, v.Name, 'raw', v.Zone, 10000.00, 20.00, 10.00, v.CurrentQtyKg, m.Id, CONCAT('LOT-', f.Code, '-', v.Code), DATEADD(minute, -4, @now)
FROM Org.Factories f
CROSS APPLY (VALUES
    ('SILO01', 'API Silo', 'Warehouse A', 'RM-PARA', 7200.000),
    ('SILO02', 'Excipient Silo', 'Warehouse A', 'RM-MCC', 5400.000),
    ('SILO03', 'Starch Silo', 'Warehouse B', 'RM-STARCH', 3100.000)
) v(Code, Name, Zone, MaterialCode, CurrentQtyKg)
JOIN Org.Customers c ON c.Id = f.CustomerId
JOIN MasterData.Materials m ON m.CustomerId = c.Id AND m.Code = v.MaterialCode AND m.IsDeleted = 0
JOIN IoT.Devices d ON d.SerialNumber = CONCAT('DEV-', f.Code, '-', v.Code) AND d.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM Scada.Silos s WHERE s.FactoryId = f.Id AND s.Code = v.Code AND s.IsDeleted = 0);

INSERT INTO Scada.Scales
    (FactoryId, DeviceId, Code, Name, Zone, CapacityKg, ResolutionG, Protocol, TareKg, WeightKg, ScaleState, CalibratedAt, CalibrationCert, TelemetryAt)
SELECT f.Id, d.Id, v.Code, v.Name, v.Zone, v.CapacityKg, 0.100, 'OPC-UA', 0.000, v.WeightKg, 'ready', DATEADD(month, -1, @today), CONCAT('CAL-', f.Code, '-', v.Code), DATEADD(minute, -3, @now)
FROM Org.Factories f
CROSS APPLY (VALUES
    ('SCALE01', 'Dispense Scale 1', 'Dispense', 250.000, 0.0000),
    ('SCALE02', 'Dispense Scale 2', 'Dispense', 100.000, 12.3500)
) v(Code, Name, Zone, CapacityKg, WeightKg)
JOIN IoT.Devices d ON d.SerialNumber = CONCAT('DEV-', f.Code, '-', v.Code) AND d.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM Scada.Scales s WHERE s.FactoryId = f.Id AND s.Code = v.Code AND s.IsDeleted = 0);

-------------------------------------------------------------------------------
-- Production batches: 2 batches per status per customer
-------------------------------------------------------------------------------

DECLARE @BatchSeed table
(
    CustomerCode varchar(30),
    Status varchar(20),
    Seq int,
    SkuCode varchar(40),
    FactoryOrdinal int,
    LineCode varchar(20),
    PlannedQty int,
    Stage varchar(20),
    Priority smallint
);

INSERT INTO @BatchSeed (CustomerCode, Status, Seq, SkuCode, FactoryOrdinal, LineCode, PlannedQty, Stage, Priority)
SELECT c.Code, s.Status, s.Seq,
       CASE WHEN s.Seq = 1 THEN 'SKU-PARA-500' ELSE 'SKU-AMOX-250' END,
       CASE WHEN s.Seq = 1 THEN 1 ELSE 2 END,
       CASE WHEN s.Seq = 1 THEN 'L01' ELSE 'L02' END,
       CASE WHEN s.Status = 'planned' THEN 100000 WHEN s.Status = 'in_progress' THEN 90000 WHEN s.Status = 'production' THEN 120000 WHEN s.Status = 'finished' THEN 80000 ELSE 75000 END,
       CASE WHEN s.Status = 'planned' THEN 'warehouse' WHEN s.Status = 'in_progress' THEN 'dispense' WHEN s.Status = 'production' THEN 'production' WHEN s.Status = 'finished' THEN 'finished' ELSE 'warehouse' END,
       CASE WHEN s.Status = 'production' THEN 8 WHEN s.Status = 'in_progress' THEN 7 ELSE 5 END
FROM Org.Customers c
CROSS JOIN (VALUES
    ('planned', 1), ('planned', 2),
    ('in_progress', 1), ('in_progress', 2),
    ('production', 1), ('production', 2),
    ('finished', 1), ('finished', 2),
    ('cancelled', 1), ('cancelled', 2)
) s(Status, Seq)
WHERE c.Code IN ('EGYPHARMA', 'NILEFOOD');

WITH FactoryPick AS
(
    SELECT f.*, ROW_NUMBER() OVER (PARTITION BY f.CustomerId ORDER BY f.Code) AS FactoryOrdinal
    FROM Org.Factories f
    WHERE f.IsDeleted = 0
)
INSERT INTO Production.Batches
    (Uid, CustomerId, SkuId, LineId, RouteId, Status, Stage, PlannedQty, ProducedQty, RejectedQty, PlannedStart, PlannedEnd, ActualStart, ActualEnd, ShiftId, Priority, Notes)
SELECT CONCAT('BT-', @ym, '-', RIGHT('00000' + CAST(100 + ROW_NUMBER() OVER (ORDER BY c.Code, bs.Status, bs.Seq) AS varchar(5)), 5), '-', COALESCE(c.CountryCode, 'EGY')),
       c.Id, sku.Id, l.Id, r.Id, bs.Status, bs.Stage, bs.PlannedQty,
       CASE WHEN bs.Status = 'finished' THEN bs.PlannedQty - 250 WHEN bs.Status = 'production' THEN bs.PlannedQty / 2 ELSE 0 END,
       CASE WHEN bs.Status = 'finished' THEN 250 WHEN bs.Status = 'production' THEN 45 ELSE 0 END,
       DATEADD(day, CASE bs.Status WHEN 'planned' THEN 2 WHEN 'in_progress' THEN 0 WHEN 'production' THEN -1 WHEN 'finished' THEN -5 ELSE -3 END, DATEADD(hour, 9 + bs.Seq, @today)),
       DATEADD(day, CASE bs.Status WHEN 'planned' THEN 2 WHEN 'in_progress' THEN 0 WHEN 'production' THEN -1 WHEN 'finished' THEN -5 ELSE -3 END, DATEADD(hour, 15 + bs.Seq, @today)),
       CASE WHEN bs.Status IN ('in_progress', 'production', 'finished') THEN DATEADD(day, CASE bs.Status WHEN 'in_progress' THEN 0 WHEN 'production' THEN -1 ELSE -5 END, DATEADD(hour, 9 + bs.Seq, @today)) ELSE NULL END,
       CASE WHEN bs.Status = 'finished' THEN DATEADD(day, -5, DATEADD(hour, 15 + bs.Seq, @today)) ELSE NULL END,
       sh.Id, bs.Priority,
       CONCAT('Demo ', bs.Status, ' batch for ', c.Code)
FROM @BatchSeed bs
JOIN Org.Customers c ON c.Code = bs.CustomerCode
JOIN FactoryPick f ON f.CustomerId = c.Id AND f.FactoryOrdinal = bs.FactoryOrdinal
JOIN Org.ProductionLines l ON l.FactoryId = f.Id AND l.Code = bs.LineCode AND l.IsDeleted = 0
JOIN MasterData.Skus sku ON sku.CustomerId = c.Id AND sku.Code = bs.SkuCode AND sku.IsDeleted = 0
JOIN Planning.Routes r ON r.CustomerId = c.Id AND r.SkuId = sku.Id AND r.Status = 'active' AND r.IsDeleted = 0
JOIN Planning.Shifts sh ON sh.FactoryId = f.Id AND sh.Name = 'Morning Shift' AND sh.IsDeleted = 0
WHERE NOT EXISTS
(
    SELECT 1
    FROM Production.Batches b
    WHERE b.CustomerId = c.Id
      AND b.Status = bs.Status
      AND b.Notes = CONCAT('Demo ', bs.Status, ' batch for ', c.Code)
      AND b.LineId = l.Id
      AND b.IsDeleted = 0
);

INSERT INTO Production.BatchMaterials
    (BatchId, MaterialId, MaterialUid, SiloId, ScaleId, PlannedQty, DispensedQty, DeviationPct, DispenseStartedAt, DispenseCompletedAt, DispensedById, Status)
SELECT b.Id, bom.MaterialId,
       CONCAT('RM-', @ym, '-', RIGHT('00000' + CAST(ROW_NUMBER() OVER (ORDER BY b.Id, bom.SequenceNo) AS varchar(5)), 5)),
       silo.Id,
       sc.Id,
       ROUND(bom.QtyPerBatch * (CONVERT(decimal(18, 4), b.PlannedQty) / NULLIF(sku.StandardBatchSize, 0)), 4),
       CASE WHEN b.Status IN ('production', 'finished') THEN ROUND(bom.QtyPerBatch * (CONVERT(decimal(18, 4), b.PlannedQty) / NULLIF(sku.StandardBatchSize, 0)) * 1.004, 4)
            WHEN b.Status = 'in_progress' AND bom.SequenceNo <= 2 THEN ROUND(bom.QtyPerBatch * (CONVERT(decimal(18, 4), b.PlannedQty) / NULLIF(sku.StandardBatchSize, 0)) * 0.997, 4)
            ELSE 0 END,
       CASE WHEN b.Status IN ('production', 'finished') THEN 0.400
            WHEN b.Status = 'in_progress' AND bom.SequenceNo <= 2 THEN -0.300
            ELSE NULL END,
       CASE WHEN b.Status IN ('in_progress', 'production', 'finished') THEN DATEADD(minute, 30 + COALESCE(bom.SequenceNo, 1), b.ActualStart) ELSE NULL END,
       CASE WHEN b.Status IN ('production', 'finished') OR (b.Status = 'in_progress' AND bom.SequenceNo <= 2) THEN DATEADD(minute, 45 + COALESCE(bom.SequenceNo, 1), b.ActualStart) ELSE NULL END,
       NULL,
       CASE WHEN b.Status IN ('production', 'finished') THEN 'completed'
            WHEN b.Status = 'in_progress' AND bom.SequenceNo <= 2 THEN 'completed'
            ELSE 'pending' END
FROM Production.Batches b
JOIN MasterData.Skus sku ON sku.Id = b.SkuId
JOIN MasterData.BillOfMaterials bom ON bom.SkuId = b.SkuId AND bom.IsDeleted = 0
LEFT JOIN Scada.Silos silo ON silo.FactoryId = (SELECT l.FactoryId FROM Org.ProductionLines l WHERE l.Id = b.LineId) AND silo.CurrentMaterialId = bom.MaterialId AND silo.IsDeleted = 0
OUTER APPLY
(
    SELECT TOP 1 s.Id
    FROM Scada.Scales s
    JOIN Org.ProductionLines l ON l.Id = b.LineId
    WHERE s.FactoryId = l.FactoryId AND s.IsDeleted = 0
    ORDER BY s.Code
) sc
WHERE b.Notes LIKE 'Demo % batch for %'
  AND NOT EXISTS (SELECT 1 FROM Production.BatchMaterials bm WHERE bm.BatchId = b.Id AND bm.MaterialId = bom.MaterialId AND bm.IsDeleted = 0);

INSERT INTO Production.BatchStageLogs
    (BatchId, FromStage, ToStage, TransitionedAt, TransitionedById, MachineId, Notes)
SELECT b.Id, NULL, 'warehouse', DATEADD(minute, -20, b.PlannedStart), NULL, NULL, 'Batch created from seed script'
FROM Production.Batches b
WHERE b.Notes LIKE 'Demo % batch for %'
  AND NOT EXISTS (SELECT 1 FROM Production.BatchStageLogs sl WHERE sl.BatchId = b.Id AND sl.ToStage = 'warehouse');

INSERT INTO Production.BatchStageLogs
    (BatchId, FromStage, ToStage, TransitionedAt, TransitionedById, MachineId, Notes)
SELECT b.Id, 'warehouse', b.Stage, DATEADD(minute, 20, b.ActualStart), NULL, m.Id, CONCAT('Moved to ', b.Stage)
FROM Production.Batches b
OUTER APPLY
(
    SELECT TOP 1 m.Id
    FROM Scada.Machines m
    WHERE m.LineId = b.LineId AND m.IsDeleted = 0
    ORDER BY m.Code
) m
WHERE b.Status IN ('in_progress', 'production', 'finished')
  AND b.Notes LIKE 'Demo % batch for %'
  AND NOT EXISTS (SELECT 1 FROM Production.BatchStageLogs sl WHERE sl.BatchId = b.Id AND sl.ToStage = b.Stage AND sl.FromStage = 'warehouse');

INSERT INTO Production.BatchUidSequences
    (CustomerId, Prefix, YearMonth, LastSeq)
SELECT c.Id, 'BT', @ym, 200
FROM Org.Customers c
WHERE NOT EXISTS (SELECT 1 FROM Production.BatchUidSequences s WHERE s.CustomerId = c.Id AND s.Prefix = 'BT' AND s.YearMonth = @ym);

INSERT INTO Planning.ProductionSchedules
    (CustomerId, BatchId, LineId, ScheduledStart, ScheduledEnd, IsStoppage, StoppageType, Notes)
SELECT b.CustomerId, b.Id, b.LineId, b.PlannedStart, COALESCE(b.PlannedEnd, DATEADD(hour, 6, b.PlannedStart)), 0, NULL, 'Seed production schedule'
FROM Production.Batches b
WHERE b.Notes LIKE 'Demo % batch for %'
  AND NOT EXISTS (SELECT 1 FROM Planning.ProductionSchedules ps WHERE ps.BatchId = b.Id AND ps.IsDeleted = 0);

-------------------------------------------------------------------------------
-- Current SCADA state and telemetry
-------------------------------------------------------------------------------

INSERT INTO Scada.MachineStates
    (MachineId, State, SpeedRpm, TempCelsius, VibrationMms, EnergyKwh, ActiveBatchId, StateStartedAt, TelemetryAt, LastPayload)
SELECT m.Id,
       CASE WHEN activeBatch.Id IS NULL THEN 'idle' ELSE 'running' END,
       CASE WHEN activeBatch.Id IS NULL THEN 0 ELSE COALESCE(m.RatedSpeedRpm, 1000) - 120 END,
       43.25, 2.145, 185.750,
       activeBatch.Id,
       DATEADD(hour, -2, @now),
       DATEADD(minute, -2, @now),
       CONCAT('{"speedRpm":', CASE WHEN activeBatch.Id IS NULL THEN 0 ELSE COALESCE(m.RatedSpeedRpm, 1000) - 120 END, ',"state":"', CASE WHEN activeBatch.Id IS NULL THEN 'idle' ELSE 'running' END, '"}')
FROM Scada.Machines m
OUTER APPLY
(
    SELECT TOP 1 b.Id
    FROM Production.Batches b
    WHERE b.LineId = m.LineId AND b.Status IN ('in_progress', 'production') AND b.IsDeleted = 0
    ORDER BY b.PlannedStart DESC
) activeBatch
WHERE NOT EXISTS (SELECT 1 FROM Scada.MachineStates ms WHERE ms.MachineId = m.Id);

INSERT INTO Scada.MachineStateHistories
    (MachineId, FromState, ToState, TransitionedAt, TriggerSource, TriggeredByUserId, Notes)
SELECT m.Id, v.FromState, v.ToState, DATEADD(hour, -v.HoursAgo, @now), 'seed', NULL, v.Notes
FROM Scada.Machines m
CROSS APPLY (VALUES
    ('offline', 'idle', 8, 'Machine became available'),
    ('idle', 'running', 2, 'Batch assigned to machine')
) v(FromState, ToState, HoursAgo, Notes)
WHERE NOT EXISTS (SELECT 1 FROM Scada.MachineStateHistories h WHERE h.MachineId = m.Id AND h.ToState = v.ToState AND h.TransitionedAt = DATEADD(hour, -v.HoursAgo, @now));

INSERT INTO Config.MachineConfigs
    (MachineId, RatedSpeedRpm, MinSpeedRpm, MaxTempCelsius, MaxVibrationMms, RatedPowerKw)
SELECT m.Id, m.RatedSpeedRpm, 300, 75.00, 6.000, m.RatedPowerKw
FROM Scada.Machines m
WHERE NOT EXISTS (SELECT 1 FROM Config.MachineConfigs mc WHERE mc.MachineId = m.Id);

INSERT INTO IoT.TelemetryRaw
    (DeviceId, RecordedAt, ReceivedAt, RawPayload, Protocol, Quality)
SELECT d.Id,
       DATEADD(minute, -v.MinutesAgo - CAST(ROW_NUMBER() OVER (ORDER BY d.Id) AS int), @now),
       DATEADD(minute, -v.MinutesAgo - CAST(ROW_NUMBER() OVER (ORDER BY d.Id) AS int), @now),
       CONCAT('{"serial":"', d.SerialNumber, '","speedRpm":', 900 + (d.Id % 500), ',"temperature":', 38 + (d.Id % 20), ',"quality":192}'),
       'MQTT',
       192
FROM IoT.Devices d
CROSS APPLY (VALUES (1), (2), (3)) v(MinutesAgo)
WHERE d.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM IoT.TelemetryRaw tr WHERE tr.DeviceId = d.Id AND tr.RecordedAt = DATEADD(minute, -v.MinutesAgo - CAST(d.Id AS int), @now));

INSERT INTO IoT.TelemetryTags
    (DeviceId, TagName, RecordedAt, ValueNum, ValueStr, Unit)
SELECT d.Id, v.TagName,
       DATEADD(minute, -v.MinutesAgo - CAST(ROW_NUMBER() OVER (ORDER BY d.Id, v.TagName) AS int), @now),
       v.ValueNum + (d.Id % 10),
       NULL,
       v.Unit
FROM IoT.Devices d
CROSS APPLY (VALUES
    ('speedRpm', 1, 950.000000, 'rpm'),
    ('temperature', 2, 42.000000, 'C'),
    ('energyKwh', 3, 185.000000, 'kWh')
) v(TagName, MinutesAgo, ValueNum, Unit)
WHERE d.IsDeleted = 0
  AND EXISTS (SELECT 1 FROM IoT.DeviceTypes dt WHERE dt.Id = d.DeviceTypeId AND dt.Code = 'PLC')
  AND NOT EXISTS
  (
      SELECT 1
      FROM IoT.TelemetryTags tt
      WHERE tt.DeviceId = d.Id
        AND tt.TagName = v.TagName
        AND tt.RecordedAt = DATEADD(minute, -v.MinutesAgo - CAST(d.Id AS int), @now)
  );

INSERT INTO IoT.TelemetryTags
    (DeviceId, TagName, RecordedAt, ValueNum, ValueStr, Unit)
SELECT d.Id, 'weightKg', DATEADD(minute, -1 - d.Id, @now), 20.000000 + (d.Id % 15), NULL, 'kg'
FROM IoT.Devices d
JOIN IoT.DeviceTypes dt ON dt.Id = d.DeviceTypeId
WHERE dt.Code = 'SCALE'
  AND NOT EXISTS (SELECT 1 FROM IoT.TelemetryTags tt WHERE tt.DeviceId = d.Id AND tt.TagName = 'weightKg' AND tt.RecordedAt = DATEADD(minute, -1 - d.Id, @now));

INSERT INTO IoT.TelemetryTags
    (DeviceId, TagName, RecordedAt, ValueNum, ValueStr, Unit)
SELECT d.Id, 'levelPct', DATEADD(minute, -1 - d.Id, @now), 45.000000 + (d.Id % 40), NULL, '%'
FROM IoT.Devices d
JOIN IoT.DeviceTypes dt ON dt.Id = d.DeviceTypeId
WHERE dt.Code = 'SILO'
  AND NOT EXISTS (SELECT 1 FROM IoT.TelemetryTags tt WHERE tt.DeviceId = d.Id AND tt.TagName = 'levelPct' AND tt.RecordedAt = DATEADD(minute, -1 - d.Id, @now));

INSERT INTO IoT.TelemetryAggregates
    (DeviceId, TagName, Bucket, Resolution, AvgVal, MinVal, MaxVal, SumVal, SampleCount)
SELECT d.Id, v.TagName, DATEADD(hour, DATEDIFF(hour, 0, @now), 0), '1hr',
       v.AvgVal + (d.Id % 5), v.MinVal, v.MaxVal + (d.Id % 5), v.AvgVal * 60, 60
FROM IoT.Devices d
CROSS APPLY (VALUES
    ('speedRpm', 1080.000000, 850.000000, 1300.000000),
    ('temperature', 47.000000, 36.000000, 58.000000),
    ('energyKwh', 190.000000, 120.000000, 240.000000)
) v(TagName, AvgVal, MinVal, MaxVal)
WHERE d.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM IoT.TelemetryAggregates ta WHERE ta.DeviceId = d.Id AND ta.TagName = v.TagName AND ta.Resolution = '1hr' AND ta.Bucket = DATEADD(hour, DATEDIFF(hour, 0, @now), 0));

-------------------------------------------------------------------------------
-- Assets and analytics
-------------------------------------------------------------------------------

INSERT INTO Asset.Assets
    (CustomerId, FactoryId, Code, Name, AssetType, MachineId, DeviceId, SerialNumber, Manufacturer, InstallDate, WarrantyExpires, HealthScore, Status, UtilizationPct, IsActive)
SELECT c.Id, f.Id, CONCAT('AST-', f.Code, '-', l.Code, '-', m.Code), m.Name, m.MachineType, m.Id, m.DeviceId, CONCAT('ASTSN-', m.Id), m.Manufacturer, DATEADD(year, -2, @today), DATEADD(year, 1, @today), 82 + (m.Id % 15), 'active', 70.00 + (m.Id % 20), 1
FROM Scada.Machines m
JOIN Org.ProductionLines l ON l.Id = m.LineId
JOIN Org.Factories f ON f.Id = l.FactoryId
JOIN Org.Customers c ON c.Id = f.CustomerId
WHERE NOT EXISTS
(
    SELECT 1
    FROM Asset.Assets a
    WHERE a.CustomerId = c.Id
      AND a.Code = CONCAT('AST-', f.Code, '-', l.Code, '-', m.Code)
);

INSERT INTO Analytics.OeeSnapshots
    (MachineId, ShiftId, ShiftDate, AvailabilityPct, PerformancePct, QualityPct, OeePct, AverageSpeedRpm, TotalOutput, RejectedUnits, DowntimeMin, EnergyKwh, ComputedAt)
SELECT m.Id, sh.Id, DATEADD(day, -v.DayOffset, @today),
       88.00 - v.DayOffset, 84.50 - (m.Id % 5), 98.20, 73.00 + (m.Id % 12),
       COALESCE(m.RatedSpeedRpm, 1000) - 120,
       10000 + (m.Id * 20), 50 + (m.Id % 25), 12 + (m.Id % 10), 250.000 + (m.Id % 50), @now
FROM Scada.Machines m
JOIN Org.ProductionLines l ON l.Id = m.LineId
JOIN Planning.Shifts sh ON sh.FactoryId = l.FactoryId AND sh.Name = 'Morning Shift' AND sh.IsDeleted = 0
CROSS APPLY (VALUES (0), (1), (2)) v(DayOffset)
WHERE NOT EXISTS (SELECT 1 FROM Analytics.OeeSnapshots o WHERE o.MachineId = m.Id AND o.ShiftId = sh.Id AND o.ShiftDate = DATEADD(day, -v.DayOffset, @today));

INSERT INTO Analytics.AiInsights
    (CustomerId, InsightType, EntityType, EntityId, Title, Body, ImpactValue, ImpactCurrency, ConfidencePct, ModelVersion, GeneratedAt, ExpiresAt, IsDismissed)
SELECT c.Id, v.InsightType, v.EntityType, v.EntityId, v.Title, v.Body, v.ImpactValue, 'EGP', v.ConfidencePct, 'demo-1.0', DATEADD(hour, -v.HoursAgo, @now), DATEADD(day, 7, @now), 0
FROM Org.Customers c
CROSS APPLY (VALUES
    ('oee', 'factory', NULL, 'OEE improvement opportunity', 'Packaging line stoppages increased during evening shift.', 12500.00, 86.50, 2),
    ('maintenance', 'machine', NULL, 'Vibration trend rising', 'Tablet press vibration is trending upward and should be inspected.', 8000.00, 79.20, 5)
) v(InsightType, EntityType, EntityId, Title, Body, ImpactValue, ConfidencePct, HoursAgo)
WHERE NOT EXISTS (SELECT 1 FROM Analytics.AiInsights ai WHERE ai.CustomerId = c.Id AND ai.Title = v.Title AND ai.IsDeleted = 0);

-------------------------------------------------------------------------------
-- Alarms
-------------------------------------------------------------------------------

INSERT INTO Alarm.AlarmRules
    (CustomerId, Name, RuleType, ScopeType, ScopeEntityId, TagName, MinimumValue, MaximumValue, Severity, CreateWorkOrder, WorkOrderTeam, IsActive, CreatedById)
SELECT c.Id, v.Name, 'system', v.ScopeType, NULL, v.TagName, v.MinimumValue, v.MaximumValue, v.Severity, v.CreateWorkOrder, v.WorkOrderTeam, 1, NULL
FROM Org.Customers c
CROSS APPLY (VALUES
    ('High machine temperature', 'machine', 'temperature', NULL, 70.0000, 'high', 1, 'Maintenance'),
    ('Low silo level', 'silo', 'levelPct', 15.0000, NULL, 'medium', 0, NULL)
) v(Name, ScopeType, TagName, MinimumValue, MaximumValue, Severity, CreateWorkOrder, WorkOrderTeam)
WHERE NOT EXISTS (SELECT 1 FROM Alarm.AlarmRules ar WHERE ar.CustomerId = c.Id AND ar.Name = v.Name AND ar.IsDeleted = 0);

INSERT INTO Alarm.AlarmRuleEmailTargets
    (AlarmRuleId, Email)
SELECT ar.Id, CONCAT('alerts+', c.Code, '@geniepark.local')
FROM Alarm.AlarmRules ar
JOIN Org.Customers c ON c.Id = ar.CustomerId
WHERE NOT EXISTS (SELECT 1 FROM Alarm.AlarmRuleEmailTargets e WHERE e.AlarmRuleId = ar.Id AND e.Email = CONCAT('alerts+', c.Code, '@geniepark.local') AND e.IsDeleted = 0);

INSERT INTO Alarm.AlarmRuleSmsTargets
    (AlarmRuleId, PhoneNumber)
SELECT ar.Id, '+201000000000'
FROM Alarm.AlarmRules ar
WHERE NOT EXISTS (SELECT 1 FROM Alarm.AlarmRuleSmsTargets s WHERE s.AlarmRuleId = ar.Id AND s.PhoneNumber = '+201000000000' AND s.IsDeleted = 0);

INSERT INTO Alarm.AlarmEvents
    (RuleId, CustomerId, DeviceId, MachineId, SiloId, Title, Severity, TagName, TriggerValue, TriggeredAt, ResolvedAt, AcknowledgedAt, AcknowledgedById, NotificationSent, Notes)
SELECT ar.Id, c.Id, d.Id, m.Id, NULL, CONCAT('High temperature on ', m.Name), 'high', 'temperature', 78.2500, DATEADD(hour, -3, @now), NULL, NULL, NULL, 1, 'Seed active alarm'
FROM Org.Customers c
CROSS APPLY (SELECT TOP 1 m.* FROM Scada.Machines m JOIN Org.ProductionLines l ON l.Id = m.LineId JOIN Org.Factories f ON f.Id = l.FactoryId WHERE f.CustomerId = c.Id ORDER BY m.Id) m
LEFT JOIN IoT.Devices d ON d.Id = m.DeviceId
JOIN Alarm.AlarmRules ar ON ar.CustomerId = c.Id AND ar.Name = 'High machine temperature' AND ar.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM Alarm.AlarmEvents ae WHERE ae.CustomerId = c.Id AND ae.Title = CONCAT('High temperature on ', m.Name));

INSERT INTO Alarm.AlarmEvents
    (RuleId, CustomerId, DeviceId, MachineId, SiloId, Title, Severity, TagName, TriggerValue, TriggeredAt, ResolvedAt, AcknowledgedAt, AcknowledgedById, NotificationSent, Notes)
SELECT ar.Id, c.Id, d.Id, NULL, silo.Id, CONCAT('Low level on ', silo.Name), 'medium', 'levelPct', 12.5000, DATEADD(day, -1, @now), DATEADD(hour, -20, @now), DATEADD(hour, -22, @now), NULL, 1, 'Seed resolved alarm'
FROM Org.Customers c
CROSS APPLY (SELECT TOP 1 s.* FROM Scada.Silos s JOIN Org.Factories f ON f.Id = s.FactoryId WHERE f.CustomerId = c.Id ORDER BY s.Id DESC) silo
LEFT JOIN IoT.Devices d ON d.Id = silo.DeviceId
JOIN Alarm.AlarmRules ar ON ar.CustomerId = c.Id AND ar.Name = 'Low silo level' AND ar.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM Alarm.AlarmEvents ae WHERE ae.CustomerId = c.Id AND ae.Title = CONCAT('Low level on ', silo.Name));

-------------------------------------------------------------------------------
-- Maintenance
-------------------------------------------------------------------------------

INSERT INTO Maintenance.SpareParts
    (CustomerId, Code, Name, Category, Uom, QtyOnHand, MinStock, UnitCost, SupplierName, LeadTimeDays, StockStatus)
SELECT c.Id, v.Code, v.Name, v.Category, v.Uom, v.QtyOnHand, v.MinStock, v.UnitCost, v.SupplierName, v.LeadTimeDays, v.StockStatus
FROM Org.Customers c
CROSS APPLY (VALUES
    ('SP-BRG-001', 'Bearing Set 6205', 'Mechanical', 'pcs', 120.000, 25.000, 350.00, 'Motion Parts Co.', 7, 'ok'),
    ('SP-BLT-002', 'Timing Belt 20mm', 'Mechanical', 'pcs', 18.000, 20.000, 480.00, 'Belt Supply Egypt', 10, 'low'),
    ('SP-SNS-003', 'Temperature Sensor PT100', 'Electrical', 'pcs', 45.000, 12.000, 900.00, 'Automation House', 14, 'ok')
) v(Code, Name, Category, Uom, QtyOnHand, MinStock, UnitCost, SupplierName, LeadTimeDays, StockStatus)
WHERE NOT EXISTS (SELECT 1 FROM Maintenance.SpareParts sp WHERE sp.CustomerId = c.Id AND sp.Code = v.Code AND sp.IsDeleted = 0);

INSERT INTO Maintenance.SparePartMachineTypes
    (SparePartId, MachineType)
SELECT sp.Id, v.MachineType
FROM Maintenance.SpareParts sp
CROSS APPLY (VALUES ('Mixer'), ('Tablet Press'), ('Packaging')) v(MachineType)
WHERE NOT EXISTS (SELECT 1 FROM Maintenance.SparePartMachineTypes sm WHERE sm.SparePartId = sp.Id AND sm.MachineType = v.MachineType AND sm.IsDeleted = 0);

INSERT INTO Maintenance.PmSchedules
    (CustomerId, MachineId, AssetId, TaskName, Frequency, IntervalDays, LastDoneAt, NextDueAt, AssignedTeam, EstimatedDurationHours, RequiredPartsNotes, Instructions, IsActive)
SELECT c.Id, m.Id, a.Id, CONCAT('Monthly PM - ', m.Name), 'monthly', 30, DATEADD(day, -20, @today), DATEADD(day, 10, @today), 'Maintenance A', 3.50, 'Bearing set, lubrication kit', 'Inspect, clean, lubricate, and verify safety interlocks.', 1
FROM Org.Customers c
CROSS APPLY (SELECT TOP 4 m.* FROM Scada.Machines m JOIN Org.ProductionLines l ON l.Id = m.LineId JOIN Org.Factories f ON f.Id = l.FactoryId WHERE f.CustomerId = c.Id ORDER BY m.Id) m
LEFT JOIN Asset.Assets a ON a.MachineId = m.Id AND a.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM Maintenance.PmSchedules pm WHERE pm.CustomerId = c.Id AND pm.MachineId = m.Id AND pm.TaskName = CONCAT('Monthly PM - ', m.Name) AND pm.IsDeleted = 0);

INSERT INTO Maintenance.WorkOrders
    (Number, CustomerId, FactoryId, WorkOrderType, Priority, MachineId, ScaleId, AssetId, PmScheduleId, AlarmEventId, Description, AssignedToId, Team, PlannedStart, PlannedDurationHours, ActualStart, ActualEnd, Status, EstimatedCost, ActualCost, FailureMode, ResolutionNotes, CreatedById)
SELECT CONCAT('WO-', c.Code, '-001'), c.Id, f.Id, 'corrective', 'high', m.Id, NULL, a.Id, NULL, ae.Id,
       CONCAT('Investigate high temperature on ', m.Name), NULL, 'Maintenance A', DATEADD(hour, 1, @now), 2.50, NULL, NULL, 'open', 2500.00, NULL, 'Overheating', NULL, NULL
FROM Org.Customers c
CROSS APPLY (SELECT TOP 1 f.* FROM Org.Factories f WHERE f.CustomerId = c.Id AND f.IsDeleted = 0 ORDER BY f.Id) f
CROSS APPLY (SELECT TOP 1 m.* FROM Scada.Machines m JOIN Org.ProductionLines l ON l.Id = m.LineId WHERE l.FactoryId = f.Id ORDER BY m.Id) m
LEFT JOIN Asset.Assets a ON a.MachineId = m.Id AND a.IsDeleted = 0
LEFT JOIN Alarm.AlarmEvents ae ON ae.CustomerId = c.Id AND ae.MachineId = m.Id
WHERE NOT EXISTS (SELECT 1 FROM Maintenance.WorkOrders wo WHERE wo.Number = CONCAT('WO-', c.Code, '-001') AND wo.IsDeleted = 0);

INSERT INTO Maintenance.WorkOrders
    (Number, CustomerId, FactoryId, WorkOrderType, Priority, MachineId, ScaleId, AssetId, PmScheduleId, AlarmEventId, Description, AssignedToId, Team, PlannedStart, PlannedDurationHours, ActualStart, ActualEnd, Status, EstimatedCost, ActualCost, FailureMode, ResolutionNotes, CreatedById)
SELECT CONCAT('WO-', c.Code, '-002'), c.Id, f.Id, 'preventive', 'medium', pm.MachineId, NULL, pm.AssetId, pm.Id, NULL,
       pm.TaskName, NULL, pm.AssignedTeam, pm.NextDueAt, pm.EstimatedDurationHours, DATEADD(day, -1, @now), DATEADD(hour, -20, @now), 'completed', 1800.00, 1650.00, NULL, 'PM completed successfully.', NULL
FROM Org.Customers c
CROSS APPLY (SELECT TOP 1 pm.* FROM Maintenance.PmSchedules pm WHERE pm.CustomerId = c.Id AND pm.IsDeleted = 0 ORDER BY pm.Id) pm
JOIN Org.ProductionLines l ON l.Id = (SELECT m.LineId FROM Scada.Machines m WHERE m.Id = pm.MachineId)
JOIN Org.Factories f ON f.Id = l.FactoryId
WHERE NOT EXISTS (SELECT 1 FROM Maintenance.WorkOrders wo WHERE wo.Number = CONCAT('WO-', c.Code, '-002') AND wo.IsDeleted = 0);

INSERT INTO Maintenance.WoPartsUsed
    (WorkOrderId, SparePartId, QtyUsed, UnitCostAtUse)
SELECT wo.Id, sp.Id, 2.000, sp.UnitCost
FROM Maintenance.WorkOrders wo
JOIN Maintenance.SpareParts sp ON sp.CustomerId = wo.CustomerId AND sp.Code = 'SP-BRG-001' AND sp.IsDeleted = 0
WHERE wo.Status = 'completed'
  AND NOT EXISTS (SELECT 1 FROM Maintenance.WoPartsUsed wop WHERE wop.WorkOrderId = wo.Id AND wop.SparePartId = sp.Id);

INSERT INTO Maintenance.BreakdownLogs
    (Reference, MachineId, AlarmEventId, WorkOrderId, BreakdownAt, ResolvedAt, MttrHours, RootCause, FailureMode, Status)
SELECT CONCAT('BD-', c.Code, '-001'), wo.MachineId, wo.AlarmEventId, wo.Id, DATEADD(hour, -4, @now), NULL, NULL, 'Under investigation', wo.FailureMode, 'open'
FROM Maintenance.WorkOrders wo
JOIN Org.Customers c ON c.Id = wo.CustomerId
WHERE wo.WorkOrderType = 'corrective'
  AND NOT EXISTS (SELECT 1 FROM Maintenance.BreakdownLogs bd WHERE bd.Reference = CONCAT('BD-', c.Code, '-001'));

-------------------------------------------------------------------------------
-- Configuration, ASP.NET Identity, and security audit
-------------------------------------------------------------------------------

INSERT INTO Config.MetricThresholds
    (CustomerId, Metric, Grade, MinValue, MaxValue)
SELECT c.Id, v.Metric, v.Grade, v.MinValue, v.MaxValue
FROM Org.Customers c
CROSS APPLY (VALUES
    ('oee', 'green', 80.000, 100.000),
    ('oee', 'amber', 60.000, 79.999),
    ('oee', 'red', 0.000, 59.999),
    ('temperature', 'warning', 65.000, 75.000),
    ('temperature', 'critical', 75.001, 120.000)
) v(Metric, Grade, MinValue, MaxValue)
WHERE NOT EXISTS (SELECT 1 FROM Config.MetricThresholds mt WHERE mt.CustomerId = c.Id AND mt.Metric = v.Metric AND mt.Grade = v.Grade AND mt.IsDeleted = 0);

INSERT INTO Config.PlatformSettings
    (CustomerId, [Key], [Value], IsSensitive)
SELECT c.Id, v.[Key], v.[Value], v.IsSensitive
FROM Org.Customers c
CROSS APPLY (VALUES
    ('batch.uid.format', 'BT-{YYMM}-{SEQ}-{COUNTRY}', 0),
    ('telemetry.retention.days', '90', 0),
    ('smtp.enabled', 'true', 0),
    ('erp.sync.enabled', 'true', 0)
) v([Key], [Value], IsSensitive)
WHERE NOT EXISTS (SELECT 1 FROM Config.PlatformSettings ps WHERE ps.CustomerId = c.Id AND ps.[Key] = v.[Key]);

-------------------------------------------------------------------------------
-- ASP.NET Core Identity baseline
-- Default login: Admin@admin.com / Admin@123
-------------------------------------------------------------------------------

INSERT INTO Security.Roles
    (Name, NormalizedName, ConcurrencyStamp)
SELECT v.Name, UPPER(v.Name), CONVERT(nvarchar(36), NEWID())
FROM (VALUES
    ('Admin'),
    ('Supervisor'),
    ('Operator'),
    ('Viewer')
) v(Name)
WHERE NOT EXISTS
(
    SELECT 1
    FROM Security.Roles r
    WHERE r.NormalizedName = UPPER(v.Name)
);

DECLARE @adminCustomerId int =
(
    SELECT TOP (1) c.Id
    FROM Org.Customers c
    WHERE c.IsActive = 1
      AND c.IsDeleted = 0
    ORDER BY c.Id
);

IF @adminCustomerId IS NULL
    THROW 51001, 'An active customer is required before the default admin can be created.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM Security.Users u
    WHERE u.NormalizedUserName = 'ADMIN@ADMIN.COM'
       OR u.NormalizedEmail = 'ADMIN@ADMIN.COM'
)
BEGIN
    INSERT INTO Security.Users
    (
        CustomerId,
        FactoryId,
        LineId,
        FullName,
        IsActive,
        LastLoginAt,
        UserName,
        NormalizedUserName,
        Email,
        NormalizedEmail,
        EmailConfirmed,
        PasswordHash,
        SecurityStamp,
        ConcurrencyStamp,
        PhoneNumber,
        PhoneNumberConfirmed,
        TwoFactorEnabled,
        LockoutEnd,
        LockoutEnabled,
        AccessFailedCount
    )
    VALUES
    (
        @adminCustomerId,
        NULL,
        NULL,
        'System Administrator',
        1,
        NULL,
        'Admin@admin.com',
        'ADMIN@ADMIN.COM',
        'Admin@admin.com',
        'ADMIN@ADMIN.COM',
        1,
        'AQAAAAIAAYagAAAAECucBuR1gFGmEp9ncN47bICqKunvBtiz2ZMmXa82FccsibsENbpGRVP7/LgFAXyYQw==',
        REPLACE(CONVERT(nvarchar(36), NEWID()), '-', ''),
        CONVERT(nvarchar(36), NEWID()),
        NULL,
        0,
        0,
        NULL,
        1,
        0
    );
END;

DECLARE @adminUserId int =
(
    SELECT TOP (1) u.Id
    FROM Security.Users u
    WHERE u.NormalizedUserName = 'ADMIN@ADMIN.COM'
    ORDER BY u.Id
);

DECLARE @adminRoleId int =
(
    SELECT TOP (1) r.Id
    FROM Security.Roles r
    WHERE r.NormalizedName = 'ADMIN'
    ORDER BY r.Id
);

IF @adminUserId IS NULL OR @adminRoleId IS NULL
    THROW 51002, 'The default admin user or Admin role could not be resolved.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM Security.UserRoles ur
    WHERE ur.UserId = @adminUserId
      AND ur.RoleId = @adminRoleId
)
BEGIN
    INSERT INTO Security.UserRoles (UserId, RoleId)
    VALUES (@adminUserId, @adminRoleId);
END;

INSERT INTO Security.AuditLogs
    (CustomerId, UserId, Action, EntityType, EntityId, OldValues, NewValues, IpAddress, OccurredAt)
SELECT c.Id, NULL, v.Action, v.EntityType, v.EntityId, NULL, v.NewValues, '127.0.0.1', DATEADD(minute, -v.MinutesAgo, @now)
FROM Org.Customers c
CROSS APPLY (VALUES
    ('seed.created', 'Customer', c.Id, '{"source":"demo-seed"}', 10),
    ('batch.created', 'Batch', NULL, '{"count":"demo"}', 8),
    ('telemetry.ingested', 'Device', NULL, '{"quality":192}', 3)
) v(Action, EntityType, EntityId, NewValues, MinutesAgo)
WHERE NOT EXISTS (SELECT 1 FROM Security.AuditLogs al WHERE al.CustomerId = c.Id AND al.Action = v.Action AND al.OccurredAt = DATEADD(minute, -v.MinutesAgo, @now));

COMMIT TRANSACTION;

SELECT 'Seed completed' AS [Status],
       (SELECT COUNT(*) FROM Org.Customers WHERE IsDeleted = 0) AS Customers,
       (SELECT COUNT(*) FROM Org.Factories WHERE IsDeleted = 0) AS Factories,
       (SELECT COUNT(*) FROM Org.ProductionLines WHERE IsDeleted = 0) AS Lines,
       (SELECT COUNT(*) FROM Scada.Machines WHERE IsDeleted = 0) AS Machines,
       (SELECT COUNT(*) FROM IoT.Devices WHERE IsDeleted = 0) AS Devices,
       (SELECT COUNT(*) FROM Production.Batches WHERE IsDeleted = 0) AS Batches,
       (SELECT COUNT(*) FROM IoT.TelemetryRaw) AS TelemetryRawRows,
       (SELECT COUNT(*) FROM IoT.TelemetryTags) AS TelemetryTagRows,
       (SELECT COUNT(*) FROM Security.Users) AS Users,
       (SELECT COUNT(*) FROM Security.Roles) AS Roles;
