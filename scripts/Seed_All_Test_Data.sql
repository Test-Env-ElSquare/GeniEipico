USE [MESPlatForm];
GO

SET NOCOUNT ON;

DECLARE
    @CustomerId INT,
    @FactoryId INT,
    @Factory2Id INT,
    @LineId INT,
    @Line2Id INT,
    @ShiftMorningId INT,
    @ShiftEveningId INT,
    @ShiftNightId INT,
    @SkuId INT,
    @Sku2Id INT,
    @MaterialId INT,
    @Material2Id INT,
    @BomId INT,
    @Bom2Id INT,
    @DeviceTypeId INT,
    @DeviceId INT,
    @SiloDeviceId INT,
    @ScaleDeviceId INT,
    @MachineId INT,
    @Machine2Id INT,
    @SiloId INT,
    @ScaleId INT,
    @RouteId INT,
    @RouteStepId INT,
    @BatchId INT,
    @Batch2Id INT,
    @AlarmRuleId INT,
    @AlarmEventId INT,
    @SparePartId INT,
    @PmScheduleId INT,
    @WorkOrderId INT,
    @AssetId INT,
    @TelemetryAt DATETIME2,
    @RoleId INT,
    @UserId INT;

SET @TelemetryAt = CONVERT(datetime2, '2026-07-08T08:30:00');

------------------------------------------------------------
-- 1) Organization
------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Org.Customers WHERE Code = 'CUST-EGY-001')
BEGIN
    INSERT INTO Org.Customers
    (Code, Name, Tier, SubscriptionStarts, SubscriptionEnds, CountryCode, ContactEmail, Industry, ErpSystem, ErpEndpointUrl, IsActive)
    VALUES
    ('CUST-EGY-001', 'EIPICO Test Customer', 'premium', '2026-01-01', '2026-12-31', 'EGY', 'it@eipico-test.com', 'Pharmaceutical', 'SAP', 'https://sap-test.local/api', 1);
END
SELECT @CustomerId = Id FROM Org.Customers WHERE Code = 'CUST-EGY-001';

IF NOT EXISTS (SELECT 1 FROM Org.Factories WHERE CustomerId = @CustomerId AND Code = 'FAC-10TH')
BEGIN
    INSERT INTO Org.Factories (CustomerId, Code, Name, City, CountryCode, TimeZone, IsActive)
    VALUES (@CustomerId, 'FAC-10TH', '10th of Ramadan Factory', '10th of Ramadan', 'EGY', 'Africa/Cairo', 1);
END
SELECT @FactoryId = Id FROM Org.Factories WHERE CustomerId = @CustomerId AND Code = 'FAC-10TH';

IF NOT EXISTS (SELECT 1 FROM Org.Factories WHERE CustomerId = @CustomerId AND Code = 'FAC-ALEX')
BEGIN
    INSERT INTO Org.Factories (CustomerId, Code, Name, City, CountryCode, TimeZone, IsActive)
    VALUES (@CustomerId, 'FAC-ALEX', 'Alexandria Packaging Factory', 'Alexandria', 'EGY', 'Africa/Cairo', 1);
END
SELECT @Factory2Id = Id FROM Org.Factories WHERE CustomerId = @CustomerId AND Code = 'FAC-ALEX';

IF NOT EXISTS (SELECT 1 FROM Org.ProductionLines WHERE FactoryId = @FactoryId AND Code = 'L1')
BEGIN
    INSERT INTO Org.ProductionLines (FactoryId, Code, Name, ProductType, MaxSpeedUpm, IsActive)
    VALUES (@FactoryId, 'L1', 'Solid Dosage Line 1', 'Tablet', 120, 1);
END
SELECT @LineId = Id FROM Org.ProductionLines WHERE FactoryId = @FactoryId AND Code = 'L1';

IF NOT EXISTS (SELECT 1 FROM Org.ProductionLines WHERE FactoryId = @FactoryId AND Code = 'L2')
BEGIN
    INSERT INTO Org.ProductionLines (FactoryId, Code, Name, ProductType, MaxSpeedUpm, IsActive)
    VALUES (@FactoryId, 'L2', 'Capsules Line 2', 'Capsule', 90, 1);
END
SELECT @Line2Id = Id FROM Org.ProductionLines WHERE FactoryId = @FactoryId AND Code = 'L2';

------------------------------------------------------------
-- 1b) Minimal Identity user/role for UI testing (no real password flow)
------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Security.Roles WHERE NormalizedName = 'PLATFORM_ADMIN')
BEGIN
    INSERT INTO Security.Roles (Name, NormalizedName, ConcurrencyStamp)
    VALUES ('Platform Admin', 'PLATFORM_ADMIN', CONVERT(nvarchar(36), NEWID()));
END
SELECT @RoleId = Id FROM Security.Roles WHERE NormalizedName = 'PLATFORM_ADMIN';

IF NOT EXISTS (SELECT 1 FROM Security.Users WHERE NormalizedUserName = 'SEED.ADMIN@LOCAL')
BEGIN
    INSERT INTO Security.Users
    (UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount, CustomerId, FactoryId, LineId, FullName, IsActive, LastLoginAt)
    VALUES
    ('seed.admin@local', 'SEED.ADMIN@LOCAL', 'seed.admin@local', 'SEED.ADMIN@LOCAL', 1, NULL, CONVERT(nvarchar(36), NEWID()), CONVERT(nvarchar(36), NEWID()), NULL, 0, 0, NULL, 1, 0, @CustomerId, @FactoryId, @LineId, 'Seed Platform Admin', 1, SYSUTCDATETIME());
END
SELECT @UserId = Id FROM Security.Users WHERE NormalizedUserName = 'SEED.ADMIN@LOCAL';

IF NOT EXISTS (SELECT 1 FROM Security.UserRoles WHERE UserId = @UserId AND RoleId = @RoleId)
BEGIN
    INSERT INTO Security.UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);
END


------------------------------------------------------------
-- 2) Shifts: FAC-10TH uses 3 shifts; FAC-ALEX uses 2 shifts
------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Planning.Shifts WHERE FactoryId = @FactoryId AND Name = 'Morning Shift')
    INSERT INTO Planning.Shifts (FactoryId, Name, StartTime, EndTime, IsActive) VALUES (@FactoryId, 'Morning Shift', '08:00:00', '16:00:00', 1);
IF NOT EXISTS (SELECT 1 FROM Planning.Shifts WHERE FactoryId = @FactoryId AND Name = 'Evening Shift')
    INSERT INTO Planning.Shifts (FactoryId, Name, StartTime, EndTime, IsActive) VALUES (@FactoryId, 'Evening Shift', '16:00:00', '00:00:00', 1);
IF NOT EXISTS (SELECT 1 FROM Planning.Shifts WHERE FactoryId = @FactoryId AND Name = 'Night Shift')
    INSERT INTO Planning.Shifts (FactoryId, Name, StartTime, EndTime, IsActive) VALUES (@FactoryId, 'Night Shift', '00:00:00', '08:00:00', 1);

SELECT @ShiftMorningId = Id FROM Planning.Shifts WHERE FactoryId = @FactoryId AND Name = 'Morning Shift';
SELECT @ShiftEveningId = Id FROM Planning.Shifts WHERE FactoryId = @FactoryId AND Name = 'Evening Shift';
SELECT @ShiftNightId = Id FROM Planning.Shifts WHERE FactoryId = @FactoryId AND Name = 'Night Shift';

IF NOT EXISTS (SELECT 1 FROM Planning.Shifts WHERE FactoryId = @Factory2Id AND Name = 'Day 12h')
    INSERT INTO Planning.Shifts (FactoryId, Name, StartTime, EndTime, IsActive) VALUES (@Factory2Id, 'Day 12h', '08:00:00', '20:00:00', 1);
IF NOT EXISTS (SELECT 1 FROM Planning.Shifts WHERE FactoryId = @Factory2Id AND Name = 'Night 12h')
    INSERT INTO Planning.Shifts (FactoryId, Name, StartTime, EndTime, IsActive) VALUES (@Factory2Id, 'Night 12h', '20:00:00', '08:00:00', 1);

------------------------------------------------------------
-- 3) IoT device type, tag definitions, and devices
------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM IoT.DeviceTypes WHERE Code = 'GENERIC_MACHINE')
BEGIN
    INSERT INTO IoT.DeviceTypes (Code, Label, Protocol, DefaultPollIntervalMs, IsActive)
    VALUES ('GENERIC_MACHINE', 'Generic Machine Telemetry Device', 'REST', 1000, 1);
END
SELECT @DeviceTypeId = Id FROM IoT.DeviceTypes WHERE Code = 'GENERIC_MACHINE';

IF NOT EXISTS (SELECT 1 FROM IoT.DeviceTypeTagDefinitions WHERE DeviceTypeId = @DeviceTypeId AND TagName = 'speed_rpm')
    INSERT INTO IoT.DeviceTypeTagDefinitions (DeviceTypeId, TagName, DataType, Unit, IsRequired, MinValue, MaxValue)
    VALUES (@DeviceTypeId, 'speed_rpm', 'number', 'rpm', 1, 0, 200);
IF NOT EXISTS (SELECT 1 FROM IoT.DeviceTypeTagDefinitions WHERE DeviceTypeId = @DeviceTypeId AND TagName = 'energy_kwh')
    INSERT INTO IoT.DeviceTypeTagDefinitions (DeviceTypeId, TagName, DataType, Unit, IsRequired, MinValue, MaxValue)
    VALUES (@DeviceTypeId, 'energy_kwh', 'number', 'kWh', 0, 0, 100000);
IF NOT EXISTS (SELECT 1 FROM IoT.DeviceTypeTagDefinitions WHERE DeviceTypeId = @DeviceTypeId AND TagName = 'state')
    INSERT INTO IoT.DeviceTypeTagDefinitions (DeviceTypeId, TagName, DataType, Unit, IsRequired, MinValue, MaxValue)
    VALUES (@DeviceTypeId, 'state', 'string', NULL, 1, NULL, NULL);

IF NOT EXISTS (SELECT 1 FROM IoT.Devices WHERE SerialNumber = 'DEV-MIX-01')
BEGIN
    INSERT INTO IoT.Devices
    (CustomerId, FactoryId, LineId, DeviceTypeId, SerialNumber, MqttClientId, MqttTopicPrefix, ApiKeyHash, FirmwareVersion, HardwareModel, PollIntervalMs, LastSeenAt, ConnectionStatus, ProvisionedAt, IsActive)
    VALUES
    (@CustomerId, @FactoryId, @LineId, @DeviceTypeId, 'DEV-MIX-01', 'mqtt-mix-01', 'gns/eipico/mix01', 'seed-hash', '1.0.0', 'GEN-MX100', 1000, SYSUTCDATETIME(), 'online', SYSUTCDATETIME(), 1);
END
SELECT @DeviceId = Id FROM IoT.Devices WHERE SerialNumber = 'DEV-MIX-01';

IF NOT EXISTS (SELECT 1 FROM IoT.Devices WHERE SerialNumber = 'DEV-SILO-01')
BEGIN
    INSERT INTO IoT.Devices (CustomerId, FactoryId, LineId, DeviceTypeId, SerialNumber, MqttClientId, MqttTopicPrefix, ApiKeyHash, FirmwareVersion, HardwareModel, PollIntervalMs, LastSeenAt, ConnectionStatus, ProvisionedAt, IsActive)
    VALUES (@CustomerId, @FactoryId, NULL, @DeviceTypeId, 'DEV-SILO-01', 'mqtt-silo-01', 'gns/eipico/silo01', 'seed-hash', '1.0.0', 'GEN-SILO', 1000, SYSUTCDATETIME(), 'online', SYSUTCDATETIME(), 1);
END
SELECT @SiloDeviceId = Id FROM IoT.Devices WHERE SerialNumber = 'DEV-SILO-01';

IF NOT EXISTS (SELECT 1 FROM IoT.Devices WHERE SerialNumber = 'DEV-SCALE-01')
BEGIN
    INSERT INTO IoT.Devices (CustomerId, FactoryId, LineId, DeviceTypeId, SerialNumber, MqttClientId, MqttTopicPrefix, ApiKeyHash, FirmwareVersion, HardwareModel, PollIntervalMs, LastSeenAt, ConnectionStatus, ProvisionedAt, IsActive)
    VALUES (@CustomerId, @FactoryId, NULL, @DeviceTypeId, 'DEV-SCALE-01', 'mqtt-scale-01', 'gns/eipico/scale01', 'seed-hash', '1.0.0', 'GEN-SCALE', 1000, SYSUTCDATETIME(), 'online', SYSUTCDATETIME(), 1);
END
SELECT @ScaleDeviceId = Id FROM IoT.Devices WHERE SerialNumber = 'DEV-SCALE-01';

------------------------------------------------------------
-- 4) Master data: SKU, materials, BOM, process parameters
------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM MasterData.Skus WHERE CustomerId = @CustomerId AND Code = 'SKU-PARA-500')
BEGIN
    INSERT INTO MasterData.Skus
    (CustomerId, Code, Name, Category, Uom, StandardBatchSize, ShelfLifeMonths, RatedSpeedRpm, UnitWeightG, ErpSource, ErpMaterialCode, LastErpSyncAt, IsActive)
    VALUES (@CustomerId, 'SKU-PARA-500', 'Paracetamol 500mg Tablets', 'Finished Product', 'tablet', 100000, 24, 90, 0.6500, 1, 'SAP-FG-0001', SYSUTCDATETIME(), 1);
END
SELECT @SkuId = Id FROM MasterData.Skus WHERE CustomerId = @CustomerId AND Code = 'SKU-PARA-500';

IF NOT EXISTS (SELECT 1 FROM MasterData.Skus WHERE CustomerId = @CustomerId AND Code = 'SKU-AMOX-250')
BEGIN
    INSERT INTO MasterData.Skus (CustomerId, Code, Name, Category, Uom, StandardBatchSize, ShelfLifeMonths, RatedSpeedRpm, UnitWeightG, ErpSource, ErpMaterialCode, LastErpSyncAt, IsActive)
    VALUES (@CustomerId, 'SKU-AMOX-250', 'Amoxicillin 250mg Capsules', 'Finished Product', 'capsule', 80000, 18, 75, 0.4100, 1, 'SAP-FG-0002', SYSUTCDATETIME(), 1);
END
SELECT @Sku2Id = Id FROM MasterData.Skus WHERE CustomerId = @CustomerId AND Code = 'SKU-AMOX-250';

IF NOT EXISTS (SELECT 1 FROM MasterData.Materials WHERE CustomerId = @CustomerId AND Code = 'RM-PARA')
BEGIN
    INSERT INTO MasterData.Materials (CustomerId, Code, Name, MaterialType, Uom, ErpCode, SupplierCode, ReorderPointKg, MinStockKg, IsActive)
    VALUES (@CustomerId, 'RM-PARA', 'Paracetamol Active Ingredient', 'RM', 'kg', 'SAP-RM-0001', 'SUP-001', 500.000, 250.000, 1);
END
SELECT @MaterialId = Id FROM MasterData.Materials WHERE CustomerId = @CustomerId AND Code = 'RM-PARA';

IF NOT EXISTS (SELECT 1 FROM MasterData.Materials WHERE CustomerId = @CustomerId AND Code = 'RM-MCC')
BEGIN
    INSERT INTO MasterData.Materials (CustomerId, Code, Name, MaterialType, Uom, ErpCode, SupplierCode, ReorderPointKg, MinStockKg, IsActive)
    VALUES (@CustomerId, 'RM-MCC', 'Microcrystalline Cellulose', 'RM', 'kg', 'SAP-RM-0002', 'SUP-002', 800.000, 300.000, 1);
END
SELECT @Material2Id = Id FROM MasterData.Materials WHERE CustomerId = @CustomerId AND Code = 'RM-MCC';

IF NOT EXISTS (SELECT 1 FROM MasterData.BillOfMaterials WHERE SkuId = @SkuId AND MaterialId = @MaterialId)
    INSERT INTO MasterData.BillOfMaterials (SkuId, MaterialId, QtyPerBatch, TolerancePct, SequenceNo, IsCritical) VALUES (@SkuId, @MaterialId, 75.5000, 2.00, 1, 1);
SELECT @BomId = Id FROM MasterData.BillOfMaterials WHERE SkuId = @SkuId AND MaterialId = @MaterialId;

IF NOT EXISTS (SELECT 1 FROM MasterData.BillOfMaterials WHERE SkuId = @SkuId AND MaterialId = @Material2Id)
    INSERT INTO MasterData.BillOfMaterials (SkuId, MaterialId, QtyPerBatch, TolerancePct, SequenceNo, IsCritical) VALUES (@SkuId, @Material2Id, 120.0000, 2.00, 2, 0);
SELECT @Bom2Id = Id FROM MasterData.BillOfMaterials WHERE SkuId = @SkuId AND MaterialId = @Material2Id;

IF NOT EXISTS (SELECT 1 FROM MasterData.SkuProcessParameters WHERE SkuId = @SkuId AND Name = 'Mix Time')
    INSERT INTO MasterData.SkuProcessParameters (SkuId, Name, Value, Unit, DataType) VALUES (@SkuId, 'Mix Time', '15', 'min', 'number');
IF NOT EXISTS (SELECT 1 FROM MasterData.SkuProcessParameters WHERE SkuId = @SkuId AND Name = 'Drying Temp')
    INSERT INTO MasterData.SkuProcessParameters (SkuId, Name, Value, Unit, DataType) VALUES (@SkuId, 'Drying Temp', '60', 'C', 'number');

------------------------------------------------------------
-- 5) SCADA: machines, states, silo, scale
------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Scada.Machines WHERE LineId = @LineId AND Code = 'MIX-01')
BEGIN
    INSERT INTO Scada.Machines (LineId, DeviceId, Code, Name, MachineType, Manufacturer, ModelNumber, RatedSpeedRpm, RatedPowerKw, InstallDate, IsActive)
    VALUES (@LineId, @DeviceId, 'MIX-01', 'Mixer 01', 'Mixer', 'GENIESYS', 'MX-100', 120, 15.50, '2026-01-01', 1);
END
SELECT @MachineId = Id FROM Scada.Machines WHERE LineId = @LineId AND Code = 'MIX-01';

IF NOT EXISTS (SELECT 1 FROM Scada.Machines WHERE LineId = @LineId AND Code = 'TAB-01')
BEGIN
    INSERT INTO Scada.Machines (LineId, DeviceId, Code, Name, MachineType, Manufacturer, ModelNumber, RatedSpeedRpm, RatedPowerKw, InstallDate, IsActive)
    VALUES (@LineId, NULL, 'TAB-01', 'Tablet Press 01', 'Tablet Press', 'GENIESYS', 'TP-200', 100, 22.00, '2026-01-01', 1);
END
SELECT @Machine2Id = Id FROM Scada.Machines WHERE LineId = @LineId AND Code = 'TAB-01';

IF NOT EXISTS (SELECT 1 FROM Scada.MachineStates WHERE MachineId = @MachineId)
BEGIN
    INSERT INTO Scada.MachineStates (MachineId, State, SpeedRpm, TempCelsius, VibrationMms, EnergyKwh, ActiveBatchId, StateStartedAt, TelemetryAt, LastPayload)
    VALUES (@MachineId, 'running', 85, 42.30, 1.250, 120.750, NULL, SYSUTCDATETIME(), SYSUTCDATETIME(), '{"source":"seed","machine":"MIX-01"}');
END
ELSE
BEGIN
    UPDATE Scada.MachineStates SET State='running', SpeedRpm=85, TempCelsius=42.30, VibrationMms=1.250, EnergyKwh=120.750, TelemetryAt=SYSUTCDATETIME(), UpdatedAt=SYSUTCDATETIME() WHERE MachineId=@MachineId;
END

IF NOT EXISTS (SELECT 1 FROM Scada.MachineStates WHERE MachineId = @Machine2Id)
    INSERT INTO Scada.MachineStates (MachineId, State, SpeedRpm, TempCelsius, VibrationMms, EnergyKwh, ActiveBatchId, StateStartedAt, TelemetryAt, LastPayload)
    VALUES (@Machine2Id, 'idle', 0, 36.00, 0.300, 80.500, NULL, SYSUTCDATETIME(), SYSUTCDATETIME(), '{"source":"seed","machine":"TAB-01"}');

IF NOT EXISTS (SELECT 1 FROM Scada.MachineStateHistories WHERE MachineId = @MachineId AND ToState = 'running' AND TriggerSource = 'seed')
    INSERT INTO Scada.MachineStateHistories (MachineId, FromState, ToState, TransitionedAt, TriggerSource, TriggeredByUserId, Notes)
    VALUES (@MachineId, 'idle', 'running', DATEADD(MINUTE, -45, SYSUTCDATETIME()), 'seed', NULL, 'Seed transition for state-history endpoint.');

IF NOT EXISTS (SELECT 1 FROM Scada.Silos WHERE FactoryId = @FactoryId AND Code = 'SILO-01')
BEGIN
    INSERT INTO Scada.Silos (FactoryId, DeviceId, Code, Name, SiloType, Zone, CapacityKg, LowLevelPct, CriticalLevelPct, CurrentQtyKg, CurrentMaterialId, CurrentLotUid, TelemetryAt)
    VALUES (@FactoryId, @SiloDeviceId, 'SILO-01', 'Paracetamol API Silo', 'raw', 'warehouse', 2000.00, 20.00, 10.00, 1250.000, @MaterialId, 'RM-2607-00001-EGY', SYSUTCDATETIME());
END
SELECT @SiloId = Id FROM Scada.Silos WHERE FactoryId = @FactoryId AND Code = 'SILO-01';

IF NOT EXISTS (SELECT 1 FROM Scada.Scales WHERE FactoryId = @FactoryId AND Code = 'SCALE-01')
BEGIN
    INSERT INTO Scada.Scales (FactoryId, DeviceId, Code, Name, Zone, CapacityKg, ResolutionG, Protocol, TareKg, WeightKg, ScaleState, ActiveBatchId, ActiveMaterialId, CalibratedAt, CalibrationCert, TelemetryAt)
    VALUES (@FactoryId, @ScaleDeviceId, 'SCALE-01', 'Precision Scale 01', 'dispensing', 150.000, 0.010, 'REST', 0.000, 75.5000, 'ready', NULL, @MaterialId, DATEADD(DAY, -15, SYSUTCDATETIME()), 'CERT-SEED-001', SYSUTCDATETIME());
END
SELECT @ScaleId = Id FROM Scada.Scales WHERE FactoryId = @FactoryId AND Code = 'SCALE-01';

------------------------------------------------------------
-- 6) Production route, batches, batch materials/stage log/schedule/MRP
------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Planning.Routes WHERE CustomerId = @CustomerId AND Code = 'ROUTE-PARA-TAB')
BEGIN
    INSERT INTO Planning.Routes (CustomerId, Code, Name, SkuId, CycleTimeHours, Status, Version)
    VALUES (@CustomerId, 'ROUTE-PARA-TAB', 'Paracetamol Tablet Standard Route', @SkuId, 8.00, 'active', 1);
END
SELECT @RouteId = Id FROM Planning.Routes WHERE CustomerId = @CustomerId AND Code = 'ROUTE-PARA-TAB';

IF NOT EXISTS (SELECT 1 FROM Planning.RouteSteps WHERE RouteId = @RouteId AND StepNo = 1)
    INSERT INTO Planning.RouteSteps (RouteId, StepNo, StepName, MachineType, EstDurationMin) VALUES (@RouteId, 1, 'Mixing', 'Mixer', 60);
SELECT @RouteStepId = Id FROM Planning.RouteSteps WHERE RouteId = @RouteId AND StepNo = 1;

IF NOT EXISTS (SELECT 1 FROM Planning.RouteStepParameters WHERE RouteStepId = @RouteStepId AND Name = 'Target RPM')
    INSERT INTO Planning.RouteStepParameters (RouteStepId, Name, Value, Unit, DataType) VALUES (@RouteStepId, 'Target RPM', '85', 'rpm', 'number');

IF NOT EXISTS (SELECT 1 FROM Production.BatchUidSequences WHERE CustomerId = @CustomerId AND Prefix = 'BT' AND YearMonth = '2607')
    INSERT INTO Production.BatchUidSequences (CustomerId, Prefix, YearMonth, LastSeq) VALUES (@CustomerId, 'BT', '2607', 2);

IF NOT EXISTS (SELECT 1 FROM Production.Batches WHERE Uid = 'BT-2607-00001-EGY')
BEGIN
    INSERT INTO Production.Batches (Uid, CustomerId, SkuId, LineId, RouteId, Status, Stage, PlannedQty, ProducedQty, RejectedQty, PlannedStart, PlannedEnd, ActualStart, ActualEnd, ShiftId, Priority, ParentBatchId, SplitReason, CreatedById, Notes)
    VALUES ('BT-2607-00001-EGY', @CustomerId, @SkuId, @LineId, @RouteId, 'in_progress', 'production', 100000, 45000, 120, DATEADD(HOUR, -2, SYSUTCDATETIME()), DATEADD(HOUR, 6, SYSUTCDATETIME()), DATEADD(HOUR, -2, SYSUTCDATETIME()), NULL, @ShiftMorningId, 2, NULL, NULL, NULL, 'Seed in-progress batch for frontend known output');
END
SELECT @BatchId = Id FROM Production.Batches WHERE Uid = 'BT-2607-00001-EGY';

UPDATE Scada.MachineStates SET ActiveBatchId = @BatchId WHERE MachineId = @MachineId;
UPDATE Scada.Scales SET ActiveBatchId = @BatchId WHERE Id = @ScaleId;

IF NOT EXISTS (SELECT 1 FROM Production.Batches WHERE Uid = 'BT-2607-00002-EGY')
BEGIN
    INSERT INTO Production.Batches (Uid, CustomerId, SkuId, LineId, RouteId, Status, Stage, PlannedQty, ProducedQty, RejectedQty, PlannedStart, PlannedEnd, ActualStart, ActualEnd, ShiftId, Priority, ParentBatchId, SplitReason, CreatedById, Notes)
    VALUES ('BT-2607-00002-EGY', @CustomerId, @Sku2Id, @Line2Id, NULL, 'planned', 'warehouse', 80000, 0, 0, DATEADD(HOUR, 4, SYSUTCDATETIME()), DATEADD(HOUR, 12, SYSUTCDATETIME()), NULL, NULL, @ShiftEveningId, 3, NULL, NULL, NULL, 'Seed planned capsule batch');
END
SELECT @Batch2Id = Id FROM Production.Batches WHERE Uid = 'BT-2607-00002-EGY';

IF NOT EXISTS (SELECT 1 FROM Production.BatchMaterials WHERE BatchId = @BatchId AND MaterialId = @MaterialId)
    INSERT INTO Production.BatchMaterials (BatchId, MaterialId, MaterialUid, SiloId, ScaleId, PlannedQty, DispensedQty, DeviationPct, DispenseStartedAt, DispenseCompletedAt, DispensedById, Status)
    VALUES (@BatchId, @MaterialId, 'RM-2607-00001-EGY', @SiloId, @ScaleId, 75.5000, 75.4000, -0.132, DATEADD(MINUTE, -80, SYSUTCDATETIME()), DATEADD(MINUTE, -65, SYSUTCDATETIME()), NULL, 'complete');

IF NOT EXISTS (SELECT 1 FROM Production.BatchMaterials WHERE BatchId = @BatchId AND MaterialId = @Material2Id)
    INSERT INTO Production.BatchMaterials (BatchId, MaterialId, MaterialUid, SiloId, ScaleId, PlannedQty, DispensedQty, DeviationPct, DispenseStartedAt, DispenseCompletedAt, DispensedById, Status)
    VALUES (@BatchId, @Material2Id, 'RM-2607-00002-EGY', NULL, NULL, 120.0000, 0, NULL, NULL, NULL, NULL, 'pending');

IF NOT EXISTS (SELECT 1 FROM Production.BatchStageLogs WHERE BatchId = @BatchId AND ToStage = 'production')
    INSERT INTO Production.BatchStageLogs (BatchId, FromStage, ToStage, TransitionedAt, TransitionedById, MachineId, Notes)
    VALUES (@BatchId, 'dispense', 'production', DATEADD(MINUTE, -60, SYSUTCDATETIME()), NULL, @MachineId, 'Seed transition into production.');

IF NOT EXISTS (SELECT 1 FROM Planning.ProductionSchedules WHERE BatchId = @BatchId AND LineId = @LineId)
    INSERT INTO Planning.ProductionSchedules (CustomerId, BatchId, LineId, ScheduledStart, ScheduledEnd, IsStoppage, StoppageType, Notes)
    VALUES (@CustomerId, @BatchId, @LineId, DATEADD(HOUR, -2, SYSUTCDATETIME()), DATEADD(HOUR, 6, SYSUTCDATETIME()), 0, NULL, 'Seed production schedule.');

IF NOT EXISTS (SELECT 1 FROM Planning.ProductionSchedules WHERE CustomerId = @CustomerId AND LineId = @LineId AND IsStoppage = 1 AND StoppageType = 'cleaning')
    INSERT INTO Planning.ProductionSchedules (CustomerId, BatchId, LineId, ScheduledStart, ScheduledEnd, IsStoppage, StoppageType, Notes)
    VALUES (@CustomerId, @BatchId, @LineId, DATEADD(HOUR, 7, SYSUTCDATETIME()), DATEADD(HOUR, 8, SYSUTCDATETIME()), 1, 'cleaning', 'Seed planned cleaning stoppage.');

IF NOT EXISTS (SELECT 1 FROM Planning.MrpRequirements WHERE CustomerId = @CustomerId AND MaterialId = @MaterialId AND PlanDate = CONVERT(date, SYSUTCDATETIME()))
    INSERT INTO Planning.MrpRequirements (CustomerId, MaterialId, PlanDate, RequiredQty, AvailableQty, GapQty, MrpStatus, PoReference, PoExpectedDate)
    VALUES (@CustomerId, @MaterialId, CONVERT(date, SYSUTCDATETIME()), 500.000, 1250.000, 0.000, 'ok', 'PO-SEED-001', DATEADD(DAY, 5, SYSUTCDATETIME()));

------------------------------------------------------------
-- 7) Alarms, maintenance, assets, analytics, config, security
------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Alarm.AlarmRules WHERE CustomerId = @CustomerId AND Name = 'High Vibration Seed Rule')
BEGIN
    INSERT INTO Alarm.AlarmRules (CustomerId, Name, RuleType, ScopeType, ScopeEntityId, TagName, MinimumValue, MaximumValue, Severity, CreateWorkOrder, WorkOrderTeam, IsActive, CreatedById)
    VALUES (@CustomerId, 'High Vibration Seed Rule', 'user', 'machine', @MachineId, 'vibration', 0.0000, 1.0000, 'high', 1, 'Maintenance Team', 1, NULL);
END
SELECT @AlarmRuleId = Id FROM Alarm.AlarmRules WHERE CustomerId = @CustomerId AND Name = 'High Vibration Seed Rule';

IF NOT EXISTS (SELECT 1 FROM Alarm.AlarmRuleEmailTargets WHERE AlarmRuleId = @AlarmRuleId AND Email = 'maintenance@eipico-test.com')
    INSERT INTO Alarm.AlarmRuleEmailTargets (AlarmRuleId, Email) VALUES (@AlarmRuleId, 'maintenance@eipico-test.com');
IF NOT EXISTS (SELECT 1 FROM Alarm.AlarmRuleSmsTargets WHERE AlarmRuleId = @AlarmRuleId AND PhoneNumber = '+201000000001')
    INSERT INTO Alarm.AlarmRuleSmsTargets (AlarmRuleId, PhoneNumber) VALUES (@AlarmRuleId, '+201000000001');

IF NOT EXISTS (SELECT 1 FROM Alarm.AlarmEvents WHERE CustomerId = @CustomerId AND Title = 'Mixer vibration above seed threshold' AND ResolvedAt IS NULL)
BEGIN
    INSERT INTO Alarm.AlarmEvents (RuleId, CustomerId, DeviceId, MachineId, SiloId, Title, Severity, TagName, TriggerValue, TriggeredAt, ResolvedAt, AcknowledgedAt, AcknowledgedById, NotificationSent, Notes)
    VALUES (@AlarmRuleId, @CustomerId, @DeviceId, @MachineId, NULL, 'Mixer vibration above seed threshold', 'high', 'vibration_mms', 1.2500, DATEADD(MINUTE, -15, SYSUTCDATETIME()), NULL, NULL, NULL, 1, 'Seed active alarm.');
END
SELECT TOP 1 @AlarmEventId = Id FROM Alarm.AlarmEvents WHERE CustomerId = @CustomerId AND Title = 'Mixer vibration above seed threshold' ORDER BY Id DESC;

IF NOT EXISTS (SELECT 1 FROM Asset.Assets WHERE CustomerId = @CustomerId AND Code = 'AST-MIX-01')
BEGIN
    INSERT INTO Asset.Assets (CustomerId, FactoryId, Code, Name, AssetType, MachineId, DeviceId, SerialNumber, Manufacturer, InstallDate, WarrantyExpires, HealthScore, Status, UtilizationPct, IsActive)
    VALUES (@CustomerId, @FactoryId, 'AST-MIX-01', 'Mixer 01 Asset', 'machine', @MachineId, @DeviceId, 'SN-MIX-01', 'GENIESYS', '2026-01-01', '2028-01-01', 82, 'running', 76.5, 1);
END
SELECT @AssetId = Id FROM Asset.Assets WHERE CustomerId = @CustomerId AND Code = 'AST-MIX-01';

IF NOT EXISTS (SELECT 1 FROM Maintenance.SpareParts WHERE CustomerId = @CustomerId AND Code = 'BRG-6205')
BEGIN
    INSERT INTO Maintenance.SpareParts (CustomerId, Code, Name, Category, Uom, QtyOnHand, MinStock, UnitCost, SupplierName, LeadTimeDays, StockStatus)
    VALUES (@CustomerId, 'BRG-6205', 'Bearing Kit 6205', 'Mechanical', 'pc', 4.000, 5.000, 850.00, 'Seed Supplier', 7, 'low');
END
SELECT @SparePartId = Id FROM Maintenance.SpareParts WHERE CustomerId = @CustomerId AND Code = 'BRG-6205';

IF NOT EXISTS (SELECT 1 FROM Maintenance.SparePartMachineTypes WHERE SparePartId = @SparePartId AND MachineType = 'Mixer')
    INSERT INTO Maintenance.SparePartMachineTypes (SparePartId, MachineType) VALUES (@SparePartId, 'Mixer');

IF NOT EXISTS (SELECT 1 FROM Maintenance.PmSchedules WHERE CustomerId = @CustomerId AND TaskName = 'Mixer lubrication service')
BEGIN
    INSERT INTO Maintenance.PmSchedules (CustomerId, MachineId, AssetId, TaskName, Frequency, IntervalDays, LastDoneAt, NextDueAt, AssignedTeam, EstimatedDurationHours, RequiredPartsNotes, Instructions, IsActive)
    VALUES (@CustomerId, @MachineId, @AssetId, 'Mixer lubrication service', 'monthly', 30, DATEADD(DAY, -25, SYSUTCDATETIME()), DATEADD(DAY, 5, SYSUTCDATETIME()), 'Mechanical Team', 1.50, 'BRG-6205 if needed', 'Inspect seals and lubrication points.', 1);
END
SELECT @PmScheduleId = Id FROM Maintenance.PmSchedules WHERE CustomerId = @CustomerId AND TaskName = 'Mixer lubrication service';

IF NOT EXISTS (SELECT 1 FROM Maintenance.WorkOrders WHERE Number = 'WO-SEED-0001')
BEGIN
    INSERT INTO Maintenance.WorkOrders (Number, CustomerId, FactoryId, WorkOrderType, Priority, MachineId, ScaleId, AssetId, PmScheduleId, AlarmEventId, Description, AssignedToId, Team, PlannedStart, PlannedDurationHours, ActualStart, ActualEnd, Status, EstimatedCost, ActualCost, FailureMode, ResolutionNotes, CreatedById)
    VALUES ('WO-SEED-0001', @CustomerId, @FactoryId, 'corrective', 'high', @MachineId, NULL, @AssetId, @PmScheduleId, @AlarmEventId, 'Investigate high vibration on mixer.', NULL, 'Maintenance Team', DATEADD(HOUR, 1, SYSUTCDATETIME()), 2.00, NULL, NULL, 'open', 2500.00, NULL, 'vibration', NULL, NULL);
END
SELECT @WorkOrderId = Id FROM Maintenance.WorkOrders WHERE Number = 'WO-SEED-0001';

IF NOT EXISTS (SELECT 1 FROM Maintenance.WoPartsUsed WHERE WorkOrderId = @WorkOrderId AND SparePartId = @SparePartId)
    INSERT INTO Maintenance.WoPartsUsed (WorkOrderId, SparePartId, QtyUsed, UnitCostAtUse) VALUES (@WorkOrderId, @SparePartId, 1.000, 850.00);

IF NOT EXISTS (SELECT 1 FROM Maintenance.BreakdownLogs WHERE Reference = 'BRK-SEED-0001')
    INSERT INTO Maintenance.BreakdownLogs (Reference, MachineId, AlarmEventId, WorkOrderId, BreakdownAt, ResolvedAt, MttrHours, RootCause, FailureMode, Status)
    VALUES ('BRK-SEED-0001', @MachineId, @AlarmEventId, @WorkOrderId, DATEADD(HOUR, -3, SYSUTCDATETIME()), NULL, NULL, 'Under investigation', 'vibration', 'open');

IF NOT EXISTS (SELECT 1 FROM Analytics.OeeSnapshots WHERE MachineId = @MachineId AND ShiftId = @ShiftMorningId AND ShiftDate = CONVERT(date, SYSUTCDATETIME()))
    INSERT INTO Analytics.OeeSnapshots (MachineId, ShiftId, ShiftDate, AvailabilityPct, PerformancePct, QualityPct, OeePct, AverageSpeedRpm, TotalOutput, RejectedUnits, DowntimeMin, EnergyKwh, ComputedAt)
    VALUES (@MachineId, @ShiftMorningId, CONVERT(date, SYSUTCDATETIME()), 92.00, 88.00, 99.00, 80.15, 85.00, 45000, 120, 18, 120.750, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM Analytics.AiInsights WHERE CustomerId = @CustomerId AND Title = 'Seed OEE optimization opportunity')
    INSERT INTO Analytics.AiInsights (CustomerId, InsightType, EntityType, EntityId, Title, Body, ImpactValue, ImpactCurrency, ConfidencePct, ModelVersion, GeneratedAt, ExpiresAt, IsDismissed)
    VALUES (@CustomerId, 'oee', 'machine', @MachineId, 'Seed OEE optimization opportunity', 'Mixer 01 has a seed performance gap. Adjusting ramp-up sequence can improve output.', 4200.00, 'EGP', 78.50, 'seed-v1', SYSUTCDATETIME(), DATEADD(DAY, 7, SYSUTCDATETIME()), 0);

IF NOT EXISTS (SELECT 1 FROM Config.MachineConfigs WHERE MachineId = @MachineId)
    INSERT INTO Config.MachineConfigs (MachineId, RatedSpeedRpm, MinSpeedRpm, MaxTempCelsius, MaxVibrationMms, RatedPowerKw)
    VALUES (@MachineId, 120, 20, 80.00, 2.500, 15.50);

IF NOT EXISTS (SELECT 1 FROM Config.MetricThresholds WHERE CustomerId = @CustomerId AND Metric = 'OEE' AND Grade = 'Good')
    INSERT INTO Config.MetricThresholds (CustomerId, Metric, Grade, MinValue, MaxValue) VALUES (@CustomerId, 'OEE', 'Good', 75.00, 100.00);
IF NOT EXISTS (SELECT 1 FROM Config.MetricThresholds WHERE CustomerId = @CustomerId AND Metric = 'OEE' AND Grade = 'Warning')
    INSERT INTO Config.MetricThresholds (CustomerId, Metric, Grade, MinValue, MaxValue) VALUES (@CustomerId, 'OEE', 'Warning', 50.00, 74.99);

IF NOT EXISTS (SELECT 1 FROM Config.PlatformSettings WHERE CustomerId = @CustomerId AND [Key] = 'DefaultDuration')
    INSERT INTO Config.PlatformSettings (CustomerId, [Key], [Value], IsSensitive) VALUES (@CustomerId, 'DefaultDuration', '0', 0);
IF NOT EXISTS (SELECT 1 FROM Config.PlatformSettings WHERE CustomerId = @CustomerId AND [Key] = 'Timezone')
    INSERT INTO Config.PlatformSettings (CustomerId, [Key], [Value], IsSensitive) VALUES (@CustomerId, 'Timezone', 'Africa/Cairo', 0);

IF NOT EXISTS (SELECT 1 FROM IoT.TelemetryRaw WHERE DeviceId = @DeviceId AND RecordedAt = @TelemetryAt)
BEGIN
    INSERT INTO IoT.TelemetryRaw (DeviceId, RecordedAt, ReceivedAt, RawPayload, Protocol, Quality)
    VALUES (@DeviceId, @TelemetryAt, SYSUTCDATETIME(), '{"state":"running","speed_rpm":85,"energy_kwh":120.75}', 'seed', 1);
END

IF NOT EXISTS (SELECT 1 FROM IoT.TelemetryTags WHERE DeviceId = @DeviceId AND TagName = 'speed_rpm' AND RecordedAt = @TelemetryAt)
    INSERT INTO IoT.TelemetryTags (DeviceId, TagName, RecordedAt, ValueNum, ValueStr, Unit)
    VALUES (@DeviceId, 'speed_rpm', @TelemetryAt, 85.000000, NULL, 'rpm');
IF NOT EXISTS (SELECT 1 FROM IoT.TelemetryTags WHERE DeviceId = @DeviceId AND TagName = 'state' AND RecordedAt = @TelemetryAt)
    INSERT INTO IoT.TelemetryTags (DeviceId, TagName, RecordedAt, ValueNum, ValueStr, Unit)
    VALUES (@DeviceId, 'state', @TelemetryAt, NULL, 'running', NULL);

IF NOT EXISTS (SELECT 1 FROM IoT.TelemetryAggregates WHERE DeviceId = @DeviceId AND TagName = 'speed_rpm' AND Resolution = '1hr' AND Bucket = DATEADD(HOUR, DATEDIFF(HOUR, 0, SYSUTCDATETIME()), 0))
    INSERT INTO IoT.TelemetryAggregates (DeviceId, TagName, Bucket, Resolution, AvgVal, MinVal, MaxVal, SumVal, SampleCount)
    VALUES (@DeviceId, 'speed_rpm', DATEADD(HOUR, DATEDIFF(HOUR, 0, SYSUTCDATETIME()), 0), '1hr', 82.000000, 70.000000, 90.000000, 4920.000000, 60);

IF NOT EXISTS (SELECT 1 FROM Security.AuditLogs WHERE CustomerId = @CustomerId AND Action = 'seed_data_insert')
    INSERT INTO Security.AuditLogs (CustomerId, UserId, Action, EntityType, EntityId, OldValues, NewValues, IpAddress, OccurredAt)
    VALUES (@CustomerId, NULL, 'seed_data_insert', 'Database', NULL, NULL, '{"source":"Seed_All_Test_Data.sql"}', '127.0.0.1', SYSUTCDATETIME());

SELECT
    @CustomerId AS CustomerId,
    @FactoryId AS FactoryId,
    @Factory2Id AS Factory2Id,
    @LineId AS LineId,
    @Line2Id AS Line2Id,
    @ShiftMorningId AS ShiftMorningId,
    @ShiftEveningId AS ShiftEveningId,
    @ShiftNightId AS ShiftNightId,
    @SkuId AS SkuId,
    @Sku2Id AS Sku2Id,
    @MaterialId AS MaterialId,
    @Material2Id AS Material2Id,
    @BatchId AS BatchId,
    @Batch2Id AS Batch2Id,
    @MachineId AS MachineId,
    @Machine2Id AS Machine2Id,
    @SiloId AS SiloId,
    @ScaleId AS ScaleId,
    @RouteId AS RouteId,
    @AlarmRuleId AS AlarmRuleId,
    @AlarmEventId AS AlarmEventId,
    @AssetId AS AssetId,
    @WorkOrderId AS WorkOrderId,
    @PmScheduleId AS PmScheduleId,
    @SparePartId AS SparePartId,
    @UserId AS SeedUserId,
    @RoleId AS SeedRoleId;
GO
