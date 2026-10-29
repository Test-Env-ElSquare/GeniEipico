USE [GenieEipico_Test];
GO

IF DB_NAME() <> N'GenieEipico_Test'
BEGIN
    THROW 50001, N'Safety check failed: migrations must run only on GenieEipico_Test.', 1;
END;
GO

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    IF SCHEMA_ID(N'Analytics') IS NULL EXEC(N'CREATE SCHEMA [Analytics];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    IF SCHEMA_ID(N'Alarm') IS NULL EXEC(N'CREATE SCHEMA [Alarm];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    IF SCHEMA_ID(N'Asset') IS NULL EXEC(N'CREATE SCHEMA [Asset];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    IF SCHEMA_ID(N'Security') IS NULL EXEC(N'CREATE SCHEMA [Security];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    IF SCHEMA_ID(N'Production') IS NULL EXEC(N'CREATE SCHEMA [Production];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    IF SCHEMA_ID(N'MasterData') IS NULL EXEC(N'CREATE SCHEMA [MasterData];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    IF SCHEMA_ID(N'Maintenance') IS NULL EXEC(N'CREATE SCHEMA [Maintenance];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    IF SCHEMA_ID(N'Org') IS NULL EXEC(N'CREATE SCHEMA [Org];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    IF SCHEMA_ID(N'IoT') IS NULL EXEC(N'CREATE SCHEMA [IoT];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    IF SCHEMA_ID(N'Config') IS NULL EXEC(N'CREATE SCHEMA [Config];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    IF SCHEMA_ID(N'Scada') IS NULL EXEC(N'CREATE SCHEMA [Scada];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    IF SCHEMA_ID(N'Planning') IS NULL EXEC(N'CREATE SCHEMA [Planning];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Org].[Customers] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Tier] nvarchar(20) NOT NULL,
        [SubscriptionStarts] datetime2 NULL,
        [SubscriptionEnds] datetime2 NULL,
        [CountryCode] nvarchar(3) NULL,
        [ContactEmail] nvarchar(200) NULL,
        [Industry] nvarchar(80) NULL,
        [ErpSystem] nvarchar(60) NULL,
        [ErpEndpointUrl] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [IoT].[DeviceTypes] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(40) NOT NULL,
        [Label] nvarchar(100) NOT NULL,
        [Protocol] nvarchar(30) NULL,
        [DefaultPollIntervalMs] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_DeviceTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Security].[Roles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Analytics].[AiInsights] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [InsightType] nvarchar(40) NOT NULL,
        [EntityType] nvarchar(20) NOT NULL,
        [EntityId] int NULL,
        [Title] nvarchar(200) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [ImpactValue] decimal(14,2) NULL,
        [ImpactCurrency] nvarchar(3) NULL,
        [ConfidencePct] decimal(5,2) NULL,
        [ModelVersion] nvarchar(30) NULL,
        [GeneratedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [ExpiresAt] datetime2 NULL,
        [IsDismissed] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_AiInsights] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiInsights_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Alarm].[AlarmRules] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [RuleType] nvarchar(20) NOT NULL,
        [ScopeType] nvarchar(20) NOT NULL,
        [ScopeEntityId] int NULL,
        [TagName] nvarchar(100) NULL,
        [ConditionType] nvarchar(20) NOT NULL,
        [Operator] nvarchar(10) NULL,
        [ThresholdValue] decimal(14,4) NULL,
        [FrequencyCount] int NULL,
        [FrequencyWindowMin] int NULL,
        [DurationMin] int NULL,
        [Severity] nvarchar(15) NOT NULL,
        [CreateWorkOrder] bit NOT NULL,
        [WorkOrderTeam] nvarchar(60) NULL,
        [IsActive] bit NOT NULL,
        [CreatedById] int NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_AlarmRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AlarmRules_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Security].[AuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [UserId] int NULL,
        [Action] nvarchar(60) NOT NULL,
        [EntityType] nvarchar(40) NULL,
        [EntityId] int NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [IpAddress] nvarchar(64) NULL,
        [OccurredAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Production].[BatchUidSequences] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [Prefix] nvarchar(2) NOT NULL,
        [YearMonth] nvarchar(4) NOT NULL,
        [LastSeq] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_BatchUidSequences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BatchUidSequences_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Org].[Factories] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [City] nvarchar(80) NULL,
        [CountryCode] nvarchar(3) NULL,
        [TimeZone] nvarchar(60) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Factories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Factories_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [MasterData].[Materials] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [MaterialType] nvarchar(10) NOT NULL,
        [Uom] nvarchar(20) NOT NULL,
        [ErpCode] nvarchar(60) NULL,
        [SupplierCode] nvarchar(60) NULL,
        [ReorderPointKg] decimal(10,3) NULL,
        [MinStockKg] decimal(10,3) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Materials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Materials_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Config].[MetricThresholds] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [Metric] nvarchar(60) NOT NULL,
        [Grade] nvarchar(20) NOT NULL,
        [MinValue] decimal(8,3) NULL,
        [MaxValue] decimal(8,3) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_MetricThresholds] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MetricThresholds_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Config].[PlatformSettings] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [Key] nvarchar(80) NOT NULL,
        [Value] nvarchar(max) NULL,
        [IsSensitive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_PlatformSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlatformSettings_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [MasterData].[Skus] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Category] nvarchar(80) NULL,
        [Uom] nvarchar(20) NOT NULL,
        [StandardBatchSize] int NOT NULL,
        [ShelfLifeMonths] smallint NULL,
        [RatedSpeedRpm] int NULL,
        [UnitWeightG] decimal(8,4) NULL,
        [ErpSource] bit NOT NULL,
        [ErpMaterialCode] nvarchar(60) NULL,
        [LastErpSyncAt] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Skus] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Skus_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Maintenance].[SpareParts] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Category] nvarchar(60) NULL,
        [Uom] nvarchar(20) NOT NULL,
        [QtyOnHand] decimal(10,3) NOT NULL DEFAULT 0.0,
        [MinStock] decimal(10,3) NULL,
        [UnitCost] decimal(10,2) NULL,
        [SupplierName] nvarchar(150) NULL,
        [LeadTimeDays] smallint NULL,
        [StockStatus] nvarchar(10) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_SpareParts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SpareParts_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [IoT].[DeviceTypeTagDefinitions] (
        [Id] int NOT NULL IDENTITY,
        [DeviceTypeId] int NOT NULL,
        [TagName] nvarchar(100) NOT NULL,
        [DataType] nvarchar(20) NOT NULL,
        [Unit] nvarchar(20) NULL,
        [IsRequired] bit NOT NULL,
        [MinValue] decimal(14,4) NULL,
        [MaxValue] decimal(14,4) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_DeviceTypeTagDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeviceTypeTagDefinitions_DeviceTypes_DeviceTypeId] FOREIGN KEY ([DeviceTypeId]) REFERENCES [IoT].[DeviceTypes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Security].[RoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_RoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Security].[Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Alarm].[AlarmRuleEmailTargets] (
        [Id] int NOT NULL IDENTITY,
        [AlarmRuleId] int NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_AlarmRuleEmailTargets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AlarmRuleEmailTargets_AlarmRules_AlarmRuleId] FOREIGN KEY ([AlarmRuleId]) REFERENCES [Alarm].[AlarmRules] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Alarm].[AlarmRuleSmsTargets] (
        [Id] int NOT NULL IDENTITY,
        [AlarmRuleId] int NOT NULL,
        [PhoneNumber] nvarchar(30) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_AlarmRuleSmsTargets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AlarmRuleSmsTargets_AlarmRules_AlarmRuleId] FOREIGN KEY ([AlarmRuleId]) REFERENCES [Alarm].[AlarmRules] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Org].[ProductionLines] (
        [Id] int NOT NULL IDENTITY,
        [FactoryId] int NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [ProductType] nvarchar(60) NULL,
        [MaxSpeedUpm] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ProductionLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductionLines_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Planning].[Shifts] (
        [Id] int NOT NULL IDENTITY,
        [FactoryId] int NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        [StartTime] time NOT NULL,
        [EndTime] time NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Shifts_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Planning].[MrpRequirements] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [MaterialId] int NOT NULL,
        [PlanDate] datetime2 NOT NULL,
        [RequiredQty] decimal(12,3) NOT NULL,
        [AvailableQty] decimal(12,3) NULL,
        [GapQty] decimal(12,3) NULL,
        [MrpStatus] nvarchar(15) NOT NULL,
        [PoReference] nvarchar(60) NULL,
        [PoExpectedDate] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MrpRequirements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MrpRequirements_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MrpRequirements_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [MasterData].[Materials] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [MasterData].[BillOfMaterials] (
        [Id] int NOT NULL IDENTITY,
        [SkuId] int NOT NULL,
        [MaterialId] int NOT NULL,
        [QtyPerBatch] decimal(12,4) NOT NULL,
        [TolerancePct] decimal(5,2) NOT NULL DEFAULT 2.0,
        [SequenceNo] smallint NULL,
        [IsCritical] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_BillOfMaterials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BillOfMaterials_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [MasterData].[Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BillOfMaterials_Skus_SkuId] FOREIGN KEY ([SkuId]) REFERENCES [MasterData].[Skus] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Planning].[Routes] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [SkuId] int NOT NULL,
        [CycleTimeHours] decimal(6,2) NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'draft',
        [Version] smallint NOT NULL DEFAULT CAST(1 AS smallint),
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Routes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Routes_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Routes_Skus_SkuId] FOREIGN KEY ([SkuId]) REFERENCES [MasterData].[Skus] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [MasterData].[SkuProcessParameters] (
        [Id] int NOT NULL IDENTITY,
        [SkuId] int NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [Value] nvarchar(200) NOT NULL,
        [Unit] nvarchar(20) NULL,
        [DataType] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_SkuProcessParameters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SkuProcessParameters_Skus_SkuId] FOREIGN KEY ([SkuId]) REFERENCES [MasterData].[Skus] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Maintenance].[SparePartMachineTypes] (
        [Id] int NOT NULL IDENTITY,
        [SparePartId] int NOT NULL,
        [MachineType] nvarchar(60) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_SparePartMachineTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SparePartMachineTypes_SpareParts_SparePartId] FOREIGN KEY ([SparePartId]) REFERENCES [Maintenance].[SpareParts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [IoT].[Devices] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [FactoryId] int NOT NULL,
        [LineId] int NULL,
        [DeviceTypeId] int NOT NULL,
        [SerialNumber] nvarchar(100) NOT NULL,
        [MqttClientId] nvarchar(100) NULL,
        [MqttTopicPrefix] nvarchar(200) NULL,
        [ApiKeyHash] nvarchar(200) NULL,
        [FirmwareVersion] nvarchar(40) NULL,
        [HardwareModel] nvarchar(80) NULL,
        [PollIntervalMs] int NULL,
        [LastSeenAt] datetime2 NULL,
        [ConnectionStatus] nvarchar(20) NOT NULL,
        [ProvisionedAt] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Devices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Devices_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Devices_DeviceTypes_DeviceTypeId] FOREIGN KEY ([DeviceTypeId]) REFERENCES [IoT].[DeviceTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Devices_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Devices_ProductionLines_LineId] FOREIGN KEY ([LineId]) REFERENCES [Org].[ProductionLines] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Security].[Users] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [FactoryId] int NULL,
        [LineId] int NULL,
        [FullName] nvarchar(150) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [LastLoginAt] datetime2 NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Users_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Users_ProductionLines_LineId] FOREIGN KEY ([LineId]) REFERENCES [Org].[ProductionLines] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Planning].[ShiftFactories] (
        [Id] int NOT NULL IDENTITY,
        [ShiftId] int NOT NULL,
        [FactoryId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ShiftFactories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShiftFactories_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShiftFactories_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Planning].[Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Production].[Batches] (
        [Id] int NOT NULL IDENTITY,
        [Uid] nvarchar(30) NOT NULL,
        [CustomerId] int NOT NULL,
        [SkuId] int NOT NULL,
        [LineId] int NOT NULL,
        [RouteId] int NULL,
        [Status] nvarchar(20) NOT NULL,
        [Stage] nvarchar(20) NOT NULL,
        [PlannedQty] int NOT NULL,
        [ProducedQty] int NOT NULL,
        [RejectedQty] int NOT NULL,
        [PlannedStart] datetime2 NOT NULL,
        [PlannedEnd] datetime2 NULL,
        [ActualStart] datetime2 NULL,
        [ActualEnd] datetime2 NULL,
        [ShiftId] int NULL,
        [Priority] smallint NOT NULL,
        [ParentBatchId] int NULL,
        [SplitReason] nvarchar(max) NULL,
        [CreatedById] int NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Batches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Batches_Batches_ParentBatchId] FOREIGN KEY ([ParentBatchId]) REFERENCES [Production].[Batches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Batches_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Batches_ProductionLines_LineId] FOREIGN KEY ([LineId]) REFERENCES [Org].[ProductionLines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Batches_Routes_RouteId] FOREIGN KEY ([RouteId]) REFERENCES [Planning].[Routes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Batches_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Planning].[Shifts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Batches_Skus_SkuId] FOREIGN KEY ([SkuId]) REFERENCES [MasterData].[Skus] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Planning].[RouteSteps] (
        [Id] int NOT NULL IDENTITY,
        [RouteId] int NOT NULL,
        [StepNo] smallint NOT NULL,
        [StepName] nvarchar(80) NOT NULL,
        [MachineType] nvarchar(60) NULL,
        [EstDurationMin] int NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_RouteSteps] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RouteSteps_Routes_RouteId] FOREIGN KEY ([RouteId]) REFERENCES [Planning].[Routes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Scada].[Machines] (
        [Id] int NOT NULL IDENTITY,
        [LineId] int NOT NULL,
        [DeviceId] int NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [MachineType] nvarchar(60) NOT NULL,
        [Manufacturer] nvarchar(100) NULL,
        [ModelNumber] nvarchar(100) NULL,
        [RatedSpeedRpm] int NULL,
        [RatedPowerKw] decimal(8,2) NULL,
        [InstallDate] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Machines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Machines_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [IoT].[Devices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Machines_ProductionLines_LineId] FOREIGN KEY ([LineId]) REFERENCES [Org].[ProductionLines] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Scada].[Silos] (
        [Id] int NOT NULL IDENTITY,
        [FactoryId] int NOT NULL,
        [DeviceId] int NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [SiloType] nvarchar(20) NULL,
        [Zone] nvarchar(30) NULL,
        [CapacityKg] decimal(10,2) NOT NULL,
        [LowLevelPct] decimal(5,2) NOT NULL DEFAULT 20.0,
        [CriticalLevelPct] decimal(5,2) NOT NULL DEFAULT 10.0,
        [CurrentQtyKg] decimal(10,3) NULL,
        [CurrentMaterialId] int NULL,
        [CurrentLotUid] nvarchar(30) NULL,
        [TelemetryAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Silos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Silos_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [IoT].[Devices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Silos_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Silos_Materials_CurrentMaterialId] FOREIGN KEY ([CurrentMaterialId]) REFERENCES [MasterData].[Materials] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [IoT].[TelemetryAggregates] (
        [Id] int NOT NULL IDENTITY,
        [DeviceId] int NOT NULL,
        [TagName] nvarchar(100) NOT NULL,
        [Bucket] datetime2 NOT NULL,
        [Resolution] nvarchar(10) NOT NULL,
        [AvgVal] decimal(18,6) NULL,
        [MinVal] decimal(18,6) NULL,
        [MaxVal] decimal(18,6) NULL,
        [SumVal] decimal(18,6) NULL,
        [SampleCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TelemetryAggregates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TelemetryAggregates_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [IoT].[Devices] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [IoT].[TelemetryRaw] (
        [Id] int NOT NULL IDENTITY,
        [DeviceId] int NOT NULL,
        [RecordedAt] datetime2 NOT NULL,
        [ReceivedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [RawPayload] nvarchar(max) NOT NULL,
        [Protocol] nvarchar(20) NULL,
        [Quality] smallint NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TelemetryRaw] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TelemetryRaw_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [IoT].[Devices] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [IoT].[TelemetryTags] (
        [Id] int NOT NULL IDENTITY,
        [DeviceId] int NOT NULL,
        [TagName] nvarchar(100) NOT NULL,
        [RecordedAt] datetime2 NOT NULL,
        [ValueNum] decimal(18,6) NULL,
        [ValueStr] nvarchar(200) NULL,
        [Unit] nvarchar(20) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_TelemetryTags] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TelemetryTags_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [IoT].[Devices] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Security].[UserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Security].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Security].[UserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Security].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Security].[UserRoles] (
        [UserId] int NOT NULL,
        [RoleId] int NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Security].[Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Security].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Security].[UserTokens] (
        [UserId] int NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Security].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Planning].[ProductionSchedules] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [BatchId] int NOT NULL,
        [LineId] int NOT NULL,
        [ScheduledStart] datetime2 NOT NULL,
        [ScheduledEnd] datetime2 NOT NULL,
        [IsStoppage] bit NOT NULL,
        [StoppageType] nvarchar(30) NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ProductionSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductionSchedules_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Production].[Batches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductionSchedules_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductionSchedules_ProductionLines_LineId] FOREIGN KEY ([LineId]) REFERENCES [Org].[ProductionLines] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Scada].[Scales] (
        [Id] int NOT NULL IDENTITY,
        [FactoryId] int NOT NULL,
        [DeviceId] int NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Zone] nvarchar(20) NULL,
        [CapacityKg] decimal(8,3) NULL,
        [ResolutionG] decimal(6,3) NULL,
        [Protocol] nvarchar(30) NULL,
        [TareKg] decimal(8,3) NOT NULL DEFAULT 0.0,
        [WeightKg] decimal(10,4) NULL,
        [ScaleState] nvarchar(20) NOT NULL,
        [ActiveBatchId] int NULL,
        [ActiveMaterialId] int NULL,
        [CalibratedAt] datetime2 NULL,
        [CalibrationCert] nvarchar(200) NULL,
        [TelemetryAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Scales] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Scales_Batches_ActiveBatchId] FOREIGN KEY ([ActiveBatchId]) REFERENCES [Production].[Batches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Scales_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [IoT].[Devices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Scales_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Scales_Materials_ActiveMaterialId] FOREIGN KEY ([ActiveMaterialId]) REFERENCES [MasterData].[Materials] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Planning].[RouteStepParameters] (
        [Id] int NOT NULL IDENTITY,
        [RouteStepId] int NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [Value] nvarchar(200) NOT NULL,
        [Unit] nvarchar(20) NULL,
        [DataType] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_RouteStepParameters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RouteStepParameters_RouteSteps_RouteStepId] FOREIGN KEY ([RouteStepId]) REFERENCES [Planning].[RouteSteps] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Asset].[Assets] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [FactoryId] int NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [AssetType] nvarchar(60) NULL,
        [MachineId] int NULL,
        [DeviceId] int NULL,
        [SerialNumber] nvarchar(100) NULL,
        [Manufacturer] nvarchar(100) NULL,
        [InstallDate] datetime2 NULL,
        [WarrantyExpires] datetime2 NULL,
        [HealthScore] smallint NULL,
        [Status] nvarchar(20) NULL,
        [UtilizationPct] decimal(5,2) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Assets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Assets_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Assets_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [IoT].[Devices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Assets_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Assets_Machines_MachineId] FOREIGN KEY ([MachineId]) REFERENCES [Scada].[Machines] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Production].[BatchStageLogs] (
        [Id] int NOT NULL IDENTITY,
        [BatchId] int NOT NULL,
        [FromStage] nvarchar(20) NULL,
        [ToStage] nvarchar(20) NOT NULL,
        [TransitionedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [TransitionedById] int NULL,
        [MachineId] int NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_BatchStageLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BatchStageLogs_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Production].[Batches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BatchStageLogs_Machines_MachineId] FOREIGN KEY ([MachineId]) REFERENCES [Scada].[Machines] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Config].[MachineConfigs] (
        [Id] int NOT NULL IDENTITY,
        [MachineId] int NOT NULL,
        [RatedSpeedRpm] int NULL,
        [MinSpeedRpm] int NULL,
        [MaxTempCelsius] decimal(6,2) NULL,
        [MaxVibrationMms] decimal(6,3) NULL,
        [RatedPowerKw] decimal(8,2) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MachineConfigs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MachineConfigs_Machines_MachineId] FOREIGN KEY ([MachineId]) REFERENCES [Scada].[Machines] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Scada].[MachineStateHistories] (
        [Id] int NOT NULL IDENTITY,
        [MachineId] int NOT NULL,
        [FromState] nvarchar(20) NULL,
        [ToState] nvarchar(20) NOT NULL,
        [TransitionedAt] datetime2 NOT NULL,
        [TriggerSource] nvarchar(30) NULL,
        [TriggeredByUserId] int NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MachineStateHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MachineStateHistories_Machines_MachineId] FOREIGN KEY ([MachineId]) REFERENCES [Scada].[Machines] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Scada].[MachineStates] (
        [Id] int NOT NULL IDENTITY,
        [MachineId] int NOT NULL,
        [State] nvarchar(20) NOT NULL,
        [SpeedRpm] int NULL,
        [OeePct] decimal(5,2) NULL,
        [TempCelsius] decimal(6,2) NULL,
        [VibrationMms] decimal(6,3) NULL,
        [EnergyKwh] decimal(10,3) NULL,
        [ActiveBatchId] int NULL,
        [StateStartedAt] datetime2 NULL,
        [TelemetryAt] datetime2 NULL,
        [LastPayload] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MachineStates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MachineStates_Batches_ActiveBatchId] FOREIGN KEY ([ActiveBatchId]) REFERENCES [Production].[Batches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MachineStates_Machines_MachineId] FOREIGN KEY ([MachineId]) REFERENCES [Scada].[Machines] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Analytics].[OeeSnapshots] (
        [Id] int NOT NULL IDENTITY,
        [MachineId] int NOT NULL,
        [ShiftId] int NOT NULL,
        [ShiftDate] datetime2 NOT NULL,
        [AvailabilityPct] decimal(5,2) NULL,
        [PerformancePct] decimal(5,2) NULL,
        [QualityPct] decimal(5,2) NULL,
        [OeePct] decimal(5,2) NULL,
        [TotalOutput] int NOT NULL,
        [RejectedUnits] int NOT NULL,
        [DowntimeMin] int NOT NULL,
        [EnergyKwh] decimal(10,3) NULL,
        [ComputedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_OeeSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OeeSnapshots_Machines_MachineId] FOREIGN KEY ([MachineId]) REFERENCES [Scada].[Machines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OeeSnapshots_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Planning].[Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Alarm].[AlarmEvents] (
        [Id] int NOT NULL IDENTITY,
        [RuleId] int NULL,
        [CustomerId] int NOT NULL,
        [DeviceId] int NULL,
        [MachineId] int NULL,
        [SiloId] int NULL,
        [Title] nvarchar(200) NOT NULL,
        [Severity] nvarchar(15) NOT NULL,
        [TagName] nvarchar(100) NULL,
        [TriggerValue] decimal(14,4) NULL,
        [TriggeredAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [ResolvedAt] datetime2 NULL,
        [AcknowledgedAt] datetime2 NULL,
        [AcknowledgedById] int NULL,
        [NotificationSent] bit NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_AlarmEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AlarmEvents_AlarmRules_RuleId] FOREIGN KEY ([RuleId]) REFERENCES [Alarm].[AlarmRules] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AlarmEvents_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AlarmEvents_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [IoT].[Devices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AlarmEvents_Machines_MachineId] FOREIGN KEY ([MachineId]) REFERENCES [Scada].[Machines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AlarmEvents_Silos_SiloId] FOREIGN KEY ([SiloId]) REFERENCES [Scada].[Silos] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Production].[BatchMaterials] (
        [Id] int NOT NULL IDENTITY,
        [BatchId] int NOT NULL,
        [MaterialId] int NOT NULL,
        [MaterialUid] nvarchar(30) NULL,
        [SiloId] int NULL,
        [ScaleId] int NULL,
        [PlannedQty] decimal(12,4) NOT NULL,
        [DispensedQty] decimal(12,4) NOT NULL DEFAULT 0.0,
        [DeviationPct] decimal(6,3) NULL,
        [DispenseStartedAt] datetime2 NULL,
        [DispenseCompletedAt] datetime2 NULL,
        [DispensedById] int NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_BatchMaterials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BatchMaterials_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Production].[Batches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BatchMaterials_Materials_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [MasterData].[Materials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BatchMaterials_Scales_ScaleId] FOREIGN KEY ([ScaleId]) REFERENCES [Scada].[Scales] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BatchMaterials_Silos_SiloId] FOREIGN KEY ([SiloId]) REFERENCES [Scada].[Silos] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Maintenance].[PmSchedules] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [MachineId] int NULL,
        [AssetId] int NULL,
        [TaskName] nvarchar(150) NOT NULL,
        [Frequency] nvarchar(20) NOT NULL,
        [IntervalDays] int NOT NULL,
        [LastDoneAt] datetime2 NULL,
        [NextDueAt] datetime2 NOT NULL,
        [AssignedTeam] nvarchar(60) NULL,
        [EstimatedDurationHours] decimal(5,2) NULL,
        [RequiredPartsNotes] nvarchar(max) NULL,
        [Instructions] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_PmSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PmSchedules_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Asset].[Assets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PmSchedules_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PmSchedules_Machines_MachineId] FOREIGN KEY ([MachineId]) REFERENCES [Scada].[Machines] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Maintenance].[WorkOrders] (
        [Id] int NOT NULL IDENTITY,
        [Number] nvarchar(20) NOT NULL,
        [CustomerId] int NOT NULL,
        [FactoryId] int NOT NULL,
        [WorkOrderType] nvarchar(15) NOT NULL,
        [Priority] nvarchar(10) NOT NULL,
        [MachineId] int NULL,
        [ScaleId] int NULL,
        [AssetId] int NULL,
        [PmScheduleId] int NULL,
        [AlarmEventId] int NULL,
        [Description] nvarchar(max) NOT NULL,
        [AssignedToId] int NULL,
        [Team] nvarchar(60) NULL,
        [PlannedStart] datetime2 NULL,
        [PlannedDurationHours] decimal(6,2) NULL,
        [ActualStart] datetime2 NULL,
        [ActualEnd] datetime2 NULL,
        [Status] nvarchar(15) NOT NULL DEFAULT N'open',
        [EstimatedCost] decimal(12,2) NULL,
        [ActualCost] decimal(12,2) NULL,
        [FailureMode] nvarchar(100) NULL,
        [ResolutionNotes] nvarchar(max) NULL,
        [CreatedById] int NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_WorkOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkOrders_AlarmEvents_AlarmEventId] FOREIGN KEY ([AlarmEventId]) REFERENCES [Alarm].[AlarmEvents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkOrders_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Asset].[Assets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkOrders_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkOrders_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkOrders_Machines_MachineId] FOREIGN KEY ([MachineId]) REFERENCES [Scada].[Machines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkOrders_PmSchedules_PmScheduleId] FOREIGN KEY ([PmScheduleId]) REFERENCES [Maintenance].[PmSchedules] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkOrders_Scales_ScaleId] FOREIGN KEY ([ScaleId]) REFERENCES [Scada].[Scales] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Maintenance].[BreakdownLogs] (
        [Id] int NOT NULL IDENTITY,
        [Reference] nvarchar(20) NULL,
        [MachineId] int NOT NULL,
        [AlarmEventId] int NULL,
        [WorkOrderId] int NULL,
        [BreakdownAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        [MttrHours] decimal(6,2) NULL,
        [RootCause] nvarchar(max) NULL,
        [FailureMode] nvarchar(100) NULL,
        [Status] nvarchar(15) NOT NULL DEFAULT N'open',
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_BreakdownLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BreakdownLogs_AlarmEvents_AlarmEventId] FOREIGN KEY ([AlarmEventId]) REFERENCES [Alarm].[AlarmEvents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BreakdownLogs_Machines_MachineId] FOREIGN KEY ([MachineId]) REFERENCES [Scada].[Machines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BreakdownLogs_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [Maintenance].[WorkOrders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE TABLE [Maintenance].[WoPartsUsed] (
        [Id] int NOT NULL IDENTITY,
        [WorkOrderId] int NOT NULL,
        [SparePartId] int NOT NULL,
        [QtyUsed] decimal(10,3) NOT NULL,
        [UnitCostAtUse] decimal(10,2) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_WoPartsUsed] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WoPartsUsed_SpareParts_SparePartId] FOREIGN KEY ([SparePartId]) REFERENCES [Maintenance].[SpareParts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WoPartsUsed_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [Maintenance].[WorkOrders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_AiInsights_CustomerId_EntityType_EntityId] ON [Analytics].[AiInsights] ([CustomerId], [EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_AlarmEvents_CustomerId_TriggeredAt] ON [Alarm].[AlarmEvents] ([CustomerId], [TriggeredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AlarmEvents_DeviceId_ResolvedAt] ON [Alarm].[AlarmEvents] ([DeviceId], [ResolvedAt]) WHERE [ResolvedAt] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_AlarmEvents_MachineId] ON [Alarm].[AlarmEvents] ([MachineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_AlarmEvents_RuleId] ON [Alarm].[AlarmEvents] ([RuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_AlarmEvents_SiloId] ON [Alarm].[AlarmEvents] ([SiloId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AlarmRuleEmailTargets_AlarmRuleId_Email] ON [Alarm].[AlarmRuleEmailTargets] ([AlarmRuleId], [Email]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_AlarmRules_CustomerId_IsActive] ON [Alarm].[AlarmRules] ([CustomerId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AlarmRuleSmsTargets_AlarmRuleId_PhoneNumber] ON [Alarm].[AlarmRuleSmsTargets] ([AlarmRuleId], [PhoneNumber]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Assets_CustomerId_Code] ON [Asset].[Assets] ([CustomerId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Assets_DeviceId] ON [Asset].[Assets] ([DeviceId]) WHERE [DeviceId] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Assets_FactoryId] ON [Asset].[Assets] ([FactoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Assets_MachineId] ON [Asset].[Assets] ([MachineId]) WHERE [MachineId] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CustomerId_OccurredAt] ON [Security].[AuditLogs] ([CustomerId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [Security].[AuditLogs] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Batches_CustomerId_Status_PlannedStart] ON [Production].[Batches] ([CustomerId], [Status], [PlannedStart]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Batches_LineId] ON [Production].[Batches] ([LineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Batches_ParentBatchId] ON [Production].[Batches] ([ParentBatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Batches_RouteId] ON [Production].[Batches] ([RouteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Batches_ShiftId] ON [Production].[Batches] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Batches_SkuId] ON [Production].[Batches] ([SkuId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Batches_Uid] ON [Production].[Batches] ([Uid]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_BatchMaterials_BatchId] ON [Production].[BatchMaterials] ([BatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_BatchMaterials_BatchId_MaterialId] ON [Production].[BatchMaterials] ([BatchId], [MaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_BatchMaterials_MaterialId] ON [Production].[BatchMaterials] ([MaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_BatchMaterials_ScaleId] ON [Production].[BatchMaterials] ([ScaleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_BatchMaterials_SiloId] ON [Production].[BatchMaterials] ([SiloId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_BatchStageLogs_BatchId_TransitionedAt] ON [Production].[BatchStageLogs] ([BatchId], [TransitionedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_BatchStageLogs_MachineId] ON [Production].[BatchStageLogs] ([MachineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BatchUidSequences_CustomerId_Prefix_YearMonth] ON [Production].[BatchUidSequences] ([CustomerId], [Prefix], [YearMonth]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_BillOfMaterials_MaterialId] ON [MasterData].[BillOfMaterials] ([MaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BillOfMaterials_SkuId_MaterialId] ON [MasterData].[BillOfMaterials] ([SkuId], [MaterialId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_BreakdownLogs_AlarmEventId] ON [Maintenance].[BreakdownLogs] ([AlarmEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_BreakdownLogs_MachineId] ON [Maintenance].[BreakdownLogs] ([MachineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BreakdownLogs_Reference] ON [Maintenance].[BreakdownLogs] ([Reference]) WHERE [Reference] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_BreakdownLogs_WorkOrderId] ON [Maintenance].[BreakdownLogs] ([WorkOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Customers_Code] ON [Org].[Customers] ([Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Customers_Name] ON [Org].[Customers] ([Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Devices_CustomerId_FactoryId_LineId] ON [IoT].[Devices] ([CustomerId], [FactoryId], [LineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Devices_DeviceTypeId] ON [IoT].[Devices] ([DeviceTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Devices_FactoryId] ON [IoT].[Devices] ([FactoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Devices_LineId] ON [IoT].[Devices] ([LineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Devices_MqttClientId] ON [IoT].[Devices] ([MqttClientId]) WHERE [MqttClientId] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Devices_SerialNumber] ON [IoT].[Devices] ([SerialNumber]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DeviceTypes_Code] ON [IoT].[DeviceTypes] ([Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DeviceTypeTagDefinitions_DeviceTypeId_TagName] ON [IoT].[DeviceTypeTagDefinitions] ([DeviceTypeId], [TagName]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Factories_CustomerId_Code] ON [Org].[Factories] ([CustomerId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MachineConfigs_MachineId] ON [Config].[MachineConfigs] ([MachineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Machines_DeviceId] ON [Scada].[Machines] ([DeviceId]) WHERE [DeviceId] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Machines_LineId_Code] ON [Scada].[Machines] ([LineId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_MachineStateHistories_MachineId_TransitionedAt] ON [Scada].[MachineStateHistories] ([MachineId], [TransitionedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_MachineStates_ActiveBatchId] ON [Scada].[MachineStates] ([ActiveBatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MachineStates_MachineId] ON [Scada].[MachineStates] ([MachineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Materials_CustomerId_Code] ON [MasterData].[Materials] ([CustomerId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MetricThresholds_CustomerId_Metric_Grade] ON [Config].[MetricThresholds] ([CustomerId], [Metric], [Grade]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MrpRequirements_CustomerId_MaterialId_PlanDate] ON [Planning].[MrpRequirements] ([CustomerId], [MaterialId], [PlanDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_MrpRequirements_MaterialId] ON [Planning].[MrpRequirements] ([MaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_OeeSnapshots_MachineId_ShiftDate] ON [Analytics].[OeeSnapshots] ([MachineId], [ShiftDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OeeSnapshots_MachineId_ShiftId_ShiftDate] ON [Analytics].[OeeSnapshots] ([MachineId], [ShiftId], [ShiftDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_OeeSnapshots_ShiftId] ON [Analytics].[OeeSnapshots] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlatformSettings_CustomerId_Key] ON [Config].[PlatformSettings] ([CustomerId], [Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_PmSchedules_AssetId] ON [Maintenance].[PmSchedules] ([AssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_PmSchedules_CustomerId_NextDueAt_IsActive] ON [Maintenance].[PmSchedules] ([CustomerId], [NextDueAt], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_PmSchedules_MachineId] ON [Maintenance].[PmSchedules] ([MachineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProductionLines_FactoryId_Code] ON [Org].[ProductionLines] ([FactoryId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_ProductionSchedules_BatchId] ON [Planning].[ProductionSchedules] ([BatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_ProductionSchedules_CustomerId] ON [Planning].[ProductionSchedules] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_ProductionSchedules_LineId_ScheduledStart_ScheduledEnd] ON [Planning].[ProductionSchedules] ([LineId], [ScheduledStart], [ScheduledEnd]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_RoleClaims_RoleId] ON [Security].[RoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [Security].[Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Routes_CustomerId_Code] ON [Planning].[Routes] ([CustomerId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Routes_SkuId] ON [Planning].[Routes] ([SkuId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_RouteStepParameters_RouteStepId_Name] ON [Planning].[RouteStepParameters] ([RouteStepId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_RouteSteps_RouteId_StepNo] ON [Planning].[RouteSteps] ([RouteId], [StepNo]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Scales_ActiveBatchId] ON [Scada].[Scales] ([ActiveBatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Scales_ActiveMaterialId] ON [Scada].[Scales] ([ActiveMaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Scales_DeviceId] ON [Scada].[Scales] ([DeviceId]) WHERE [DeviceId] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Scales_FactoryId_Code] ON [Scada].[Scales] ([FactoryId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_ShiftFactories_FactoryId] ON [Planning].[ShiftFactories] ([FactoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShiftFactories_ShiftId_FactoryId] ON [Planning].[ShiftFactories] ([ShiftId], [FactoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Shifts_FactoryId_Name] ON [Planning].[Shifts] ([FactoryId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Silos_CurrentMaterialId] ON [Scada].[Silos] ([CurrentMaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Silos_DeviceId] ON [Scada].[Silos] ([DeviceId]) WHERE [DeviceId] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Silos_FactoryId_Code] ON [Scada].[Silos] ([FactoryId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SkuProcessParameters_SkuId_Name] ON [MasterData].[SkuProcessParameters] ([SkuId], [Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Skus_CustomerId_Code] ON [MasterData].[Skus] ([CustomerId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SparePartMachineTypes_SparePartId_MachineType] ON [Maintenance].[SparePartMachineTypes] ([SparePartId], [MachineType]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SpareParts_CustomerId_Code] ON [Maintenance].[SpareParts] ([CustomerId], [Code]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TelemetryAggregates_DeviceId_TagName_Resolution_Bucket] ON [IoT].[TelemetryAggregates] ([DeviceId], [TagName], [Resolution], [Bucket]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TelemetryRaw_DeviceId_RecordedAt] ON [IoT].[TelemetryRaw] ([DeviceId], [RecordedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TelemetryTags_DeviceId_TagName_RecordedAt] ON [IoT].[TelemetryTags] ([DeviceId], [TagName], [RecordedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_UserClaims_UserId] ON [Security].[UserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_UserLogins_UserId] ON [Security].[UserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [Security].[UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [Security].[Users] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Users_CustomerId] ON [Security].[Users] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Users_FactoryId] ON [Security].[Users] ([FactoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_Users_LineId] ON [Security].[Users] ([LineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [Security].[Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_WoPartsUsed_SparePartId] ON [Maintenance].[WoPartsUsed] ([SparePartId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_WoPartsUsed_WorkOrderId] ON [Maintenance].[WoPartsUsed] ([WorkOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_AlarmEventId] ON [Maintenance].[WorkOrders] ([AlarmEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_AssetId] ON [Maintenance].[WorkOrders] ([AssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_CustomerId_Status_PlannedStart] ON [Maintenance].[WorkOrders] ([CustomerId], [Status], [PlannedStart]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_FactoryId] ON [Maintenance].[WorkOrders] ([FactoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_MachineId] ON [Maintenance].[WorkOrders] ([MachineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WorkOrders_Number] ON [Maintenance].[WorkOrders] ([Number]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_PmScheduleId] ON [Maintenance].[WorkOrders] ([PmScheduleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    CREATE INDEX [IX_WorkOrders_ScaleId] ON [Maintenance].[WorkOrders] ([ScaleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721114315_AddShiftFactoryLinks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260721114315_AddShiftFactoryLinks', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727093020_AddAlarmMaintenanceUiModel'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Maintenance].[WorkOrders]') AND [c].[name] = N'Status');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Maintenance].[WorkOrders] DROP CONSTRAINT [' + @var0 + '];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727093020_AddAlarmMaintenanceUiModel'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Alarm].[AlarmRules]') AND [c].[name] = N'ConditionType');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Alarm].[AlarmRules] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Alarm].[AlarmRules] ALTER COLUMN [ConditionType] nvarchar(30) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727093020_AddAlarmMaintenanceUiModel'
)
BEGIN
    ALTER TABLE [Alarm].[AlarmEvents] ADD [ProbableCause] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727093020_AddAlarmMaintenanceUiModel'
)
BEGIN
    CREATE TABLE [Alarm].[AlarmRuleActions] (
        [Id] int NOT NULL IDENTITY,
        [AlarmRuleId] int NOT NULL,
        [SequenceNo] smallint NOT NULL,
        [ActionType] nvarchar(30) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [WorkOrderPriority] nvarchar(15) NULL,
        [AssignedToId] int NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_AlarmRuleActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AlarmRuleActions_AlarmRules_AlarmRuleId] FOREIGN KEY ([AlarmRuleId]) REFERENCES [Alarm].[AlarmRules] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727093020_AddAlarmMaintenanceUiModel'
)
BEGIN
    CREATE TABLE [Alarm].[AlarmRuleScopes] (
        [Id] int NOT NULL IDENTITY,
        [AlarmRuleId] int NOT NULL,
        [ScopeType] nvarchar(20) NOT NULL,
        [EntityId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_AlarmRuleScopes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AlarmRuleScopes_AlarmRules_AlarmRuleId] FOREIGN KEY ([AlarmRuleId]) REFERENCES [Alarm].[AlarmRules] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727093020_AddAlarmMaintenanceUiModel'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AlarmRuleActions_AlarmRuleId_SequenceNo] ON [Alarm].[AlarmRuleActions] ([AlarmRuleId], [SequenceNo]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727093020_AddAlarmMaintenanceUiModel'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AlarmRuleScopes_AlarmRuleId_ScopeType_EntityId] ON [Alarm].[AlarmRuleScopes] ([AlarmRuleId], [ScopeType], [EntityId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727093020_AddAlarmMaintenanceUiModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260727093020_AddAlarmMaintenanceUiModel', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803090000_AddAlarmRuleRange'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Alarm].[AlarmRules]') AND [c].[name] = N'Operator');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Alarm].[AlarmRules] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Alarm].[AlarmRules] ALTER COLUMN [Operator] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803090000_AddAlarmRuleRange'
)
BEGIN
    ALTER TABLE [Alarm].[AlarmRules] ADD [MaximumValue] decimal(14,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803090000_AddAlarmRuleRange'
)
BEGIN
    ALTER TABLE [Alarm].[AlarmRules] ADD [MinimumValue] decimal(14,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803090000_AddAlarmRuleRange'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260803090000_AddAlarmRuleRange', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803093000_RemoveLegacyAlarmRuleConditions'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Alarm].[AlarmRules]') AND [c].[name] = N'ConditionType');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Alarm].[AlarmRules] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Alarm].[AlarmRules] DROP COLUMN [ConditionType];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803093000_RemoveLegacyAlarmRuleConditions'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Alarm].[AlarmRules]') AND [c].[name] = N'DurationMin');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Alarm].[AlarmRules] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Alarm].[AlarmRules] DROP COLUMN [DurationMin];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803093000_RemoveLegacyAlarmRuleConditions'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Alarm].[AlarmRules]') AND [c].[name] = N'FrequencyCount');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Alarm].[AlarmRules] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [Alarm].[AlarmRules] DROP COLUMN [FrequencyCount];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803093000_RemoveLegacyAlarmRuleConditions'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Alarm].[AlarmRules]') AND [c].[name] = N'FrequencyWindowMin');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Alarm].[AlarmRules] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [Alarm].[AlarmRules] DROP COLUMN [FrequencyWindowMin];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803093000_RemoveLegacyAlarmRuleConditions'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Alarm].[AlarmRules]') AND [c].[name] = N'Operator');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Alarm].[AlarmRules] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [Alarm].[AlarmRules] DROP COLUMN [Operator];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803093000_RemoveLegacyAlarmRuleConditions'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Alarm].[AlarmRules]') AND [c].[name] = N'ThresholdValue');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Alarm].[AlarmRules] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [Alarm].[AlarmRules] DROP COLUMN [ThresholdValue];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803093000_RemoveLegacyAlarmRuleConditions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260803093000_RemoveLegacyAlarmRuleConditions', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806113718_MoveMachineOeeToSnapshots'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Scada].[MachineStates]') AND [c].[name] = N'OeePct');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Scada].[MachineStates] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [Scada].[MachineStates] DROP COLUMN [OeePct];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806113718_MoveMachineOeeToSnapshots'
)
BEGIN
    ALTER TABLE [Analytics].[OeeSnapshots] ADD [AverageSpeedRpm] decimal(10,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806113718_MoveMachineOeeToSnapshots'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806113718_MoveMachineOeeToSnapshots', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    IF SCHEMA_ID(N'RealTime') IS NULL EXEC(N'CREATE SCHEMA [RealTime];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    DROP TABLE [IoT].[TelemetryTags];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    IF OBJECT_ID(N'[Analytics].[EnergyReadings]', N'U') IS NOT NULL
       AND EXISTS
       (
           SELECT 1
           FROM sys.foreign_keys
           WHERE [name] = N'FK_EnergyReadings_TelemetryRaw_TelemetryRawId'
             AND [parent_object_id] = OBJECT_ID(N'[Analytics].[EnergyReadings]')
       )
    BEGIN
        ALTER TABLE [Analytics].[EnergyReadings]
        DROP CONSTRAINT [FK_EnergyReadings_TelemetryRaw_TelemetryRawId];
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    DROP TABLE [IoT].[TelemetryRaw];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    DROP TABLE [Scada].[MachineStates];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    ALTER SCHEMA [RealTime] TRANSFER [Scada].[Scales];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    ALTER SCHEMA [RealTime] TRANSFER [Scada].[Silos];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    CREATE TABLE [RealTime].[Production] (
        [Id] int NOT NULL IDENTITY,
        [MachineId] nvarchar(150) NOT NULL,
        [State] int NOT NULL,
        [Speed] int NOT NULL,
        [Count] int NOT NULL,
        [CountDiff] decimal(18,4) NOT NULL,
        [LineId] int NOT NULL,
        [FactoryId] int NOT NULL,
        [Temp] decimal(10,3) NULL,
        [Qreject] int NOT NULL,
        [StateReason] int NOT NULL,
        [SKU] int NOT NULL,
        [Fault] int NOT NULL,
        [TimeStamp] datetime2 NOT NULL,
        CONSTRAINT [PK_Production] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Production_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Production_ProductionLines_LineId] FOREIGN KEY ([LineId]) REFERENCES [Org].[ProductionLines] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    CREATE TABLE [RealTime].[TelemetryRaw] (
        [Id] int NOT NULL IDENTITY,
        [SourcePath] nvarchar(500) NOT NULL,
        [RawPayload] nvarchar(max) NOT NULL,
        [ReceivedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_TelemetryRaw] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    CREATE TABLE [RealTime].[TelemetryTags] (
        [Id] int NOT NULL IDENTITY,
        [DeviceId] nvarchar(150) NOT NULL,
        [TagName] nvarchar(100) NOT NULL,
        [ValueNum] decimal(18,6) NULL,
        [ValueStr] nvarchar(500) NULL,
        [RecordedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TelemetryTags] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    IF OBJECT_ID(N'[Analytics].[EnergyReadings]', N'U') IS NOT NULL
       AND COL_LENGTH(N'[Analytics].[EnergyReadings]', N'TelemetryRawId') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1
           FROM sys.foreign_keys
           WHERE [name] = N'FK_EnergyReadings_TelemetryRaw_TelemetryRawId'
             AND [parent_object_id] = OBJECT_ID(N'[Analytics].[EnergyReadings]')
       )
    BEGIN
        ALTER TABLE [Analytics].[EnergyReadings] WITH CHECK
        ADD CONSTRAINT [FK_EnergyReadings_TelemetryRaw_TelemetryRawId]
        FOREIGN KEY ([TelemetryRawId])
        REFERENCES [RealTime].[TelemetryRaw] ([Id]);

        ALTER TABLE [Analytics].[EnergyReadings]
        CHECK CONSTRAINT [FK_EnergyReadings_TelemetryRaw_TelemetryRawId];
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    CREATE INDEX [IX_Production_FactoryId_LineId] ON [RealTime].[Production] ([FactoryId], [LineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Production_FactoryId_MachineId] ON [RealTime].[Production] ([FactoryId], [MachineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    CREATE INDEX [IX_Production_LineId] ON [RealTime].[Production] ([LineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    CREATE INDEX [IX_TelemetryRaw_ReceivedAt] ON [RealTime].[TelemetryRaw] ([ReceivedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    CREATE INDEX [IX_TelemetryRaw_SourcePath_ReceivedAt] ON [RealTime].[TelemetryRaw] ([SourcePath], [ReceivedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TelemetryTags_DeviceId_TagName_RecordedAt] ON [RealTime].[TelemetryTags] ([DeviceId], [TagName], [RecordedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809084116_RefactorRealtimeBusinessTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260809084116_RefactorRealtimeBusinessTables', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809111135_AddTransformerMasterData'
)
BEGIN
    CREATE TABLE [MasterData].[Transformers] (
        [Id] int NOT NULL IDENTITY,
        [TransformerName] nvarchar(150) NOT NULL,
        [FactoryId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Transformers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Transformers_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809111135_AddTransformerMasterData'
)
BEGIN
    CREATE INDEX [IX_Transformers_FactoryId] ON [MasterData].[Transformers] ([FactoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809111135_AddTransformerMasterData'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Transformers_TransformerName] ON [MasterData].[Transformers] ([TransformerName]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809111135_AddTransformerMasterData'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260809111135_AddTransformerMasterData', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809123000_RemoveWorkOrderAssetId'
)
BEGIN
    UPDATE workOrder
    SET MachineId = asset.MachineId
    FROM Maintenance.WorkOrders AS workOrder
    INNER JOIN Asset.Assets AS asset ON asset.Id = workOrder.AssetId
    WHERE workOrder.MachineId IS NULL
      AND asset.MachineId IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809123000_RemoveWorkOrderAssetId'
)
BEGIN
    ALTER TABLE [Maintenance].[WorkOrders] DROP CONSTRAINT [FK_WorkOrders_Assets_AssetId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809123000_RemoveWorkOrderAssetId'
)
BEGIN
    DROP INDEX [IX_WorkOrders_AssetId] ON [Maintenance].[WorkOrders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809123000_RemoveWorkOrderAssetId'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Maintenance].[WorkOrders]') AND [c].[name] = N'AssetId');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Maintenance].[WorkOrders] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [Maintenance].[WorkOrders] DROP COLUMN [AssetId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809123000_RemoveWorkOrderAssetId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260809123000_RemoveWorkOrderAssetId', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809130724_RefactorEnergyReadingsBusinessTable'
)
BEGIN
    IF OBJECT_ID(N'[Analytics].[EnergyReadings]', N'U') IS NOT NULL
    BEGIN
        DROP TABLE [Analytics].[EnergyReadings];
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809130724_RefactorEnergyReadingsBusinessTable'
)
BEGIN
    CREATE TABLE [Analytics].[EnergyReadings] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [FactoryId] int NOT NULL,
        [LineId] int NULL,
        [TransformerId] int NOT NULL,
        [Quality] smallint NULL,
        [VoltageL1] decimal(18,6) NULL,
        [VoltageL2] decimal(18,6) NULL,
        [VoltageL3] decimal(18,6) NULL,
        [CurrentL1] decimal(18,6) NULL,
        [CurrentL2] decimal(18,6) NULL,
        [CurrentL3] decimal(18,6) NULL,
        [PowerFactorL1] decimal(18,6) NULL,
        [PowerFactorL2] decimal(18,6) NULL,
        [PowerFactorL3] decimal(18,6) NULL,
        [ActivePowerL1] decimal(18,6) NULL,
        [ActivePowerL2] decimal(18,6) NULL,
        [ActivePowerL3] decimal(18,6) NULL,
        [ReactivePowerL1] decimal(18,6) NULL,
        [ReactivePowerL2] decimal(18,6) NULL,
        [ReactivePowerL3] decimal(18,6) NULL,
        [ApparentPowerL1] decimal(18,6) NULL,
        [ApparentPowerL2] decimal(18,6) NULL,
        [ApparentPowerL3] decimal(18,6) NULL,
        [EnergyL1] decimal(18,6) NULL,
        [EnergyL2] decimal(18,6) NULL,
        [EnergyL3] decimal(18,6) NULL,
        [VoltageThdL1] decimal(18,6) NULL,
        [VoltageThdL2] decimal(18,6) NULL,
        [VoltageThdL3] decimal(18,6) NULL,
        [CurrentThdL1] decimal(18,6) NULL,
        [CurrentThdL2] decimal(18,6) NULL,
        [CurrentThdL3] decimal(18,6) NULL,
        [Timestamp] datetime2 NOT NULL,
        CONSTRAINT [PK_EnergyReadings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EnergyReadings_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EnergyReadings_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EnergyReadings_ProductionLines_LineId] FOREIGN KEY ([LineId]) REFERENCES [Org].[ProductionLines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EnergyReadings_Transformers_TransformerId] FOREIGN KEY ([TransformerId]) REFERENCES [MasterData].[Transformers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809130724_RefactorEnergyReadingsBusinessTable'
)
BEGIN
    CREATE INDEX [IX_EnergyReadings_CustomerId_FactoryId_LineId_Timestamp] ON [Analytics].[EnergyReadings] ([CustomerId], [FactoryId], [LineId], [Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809130724_RefactorEnergyReadingsBusinessTable'
)
BEGIN
    CREATE INDEX [IX_EnergyReadings_FactoryId] ON [Analytics].[EnergyReadings] ([FactoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809130724_RefactorEnergyReadingsBusinessTable'
)
BEGIN
    CREATE INDEX [IX_EnergyReadings_LineId] ON [Analytics].[EnergyReadings] ([LineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809130724_RefactorEnergyReadingsBusinessTable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EnergyReadings_TransformerId_Timestamp] ON [Analytics].[EnergyReadings] ([TransformerId], [Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260809130724_RefactorEnergyReadingsBusinessTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260809130724_RefactorEnergyReadingsBusinessTable', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927143453_AddExpertBoardAccessClaims'
)
BEGIN
    INSERT INTO [Security].[UserClaims] ([UserId], [ClaimType], [ClaimValue])
                      SELECT [user].[Id], N'expertBoard', [board].[ClaimValue]
                      FROM [Security].[Users] AS [user]
                      CROSS JOIN
                      (
                          VALUES (N'1'), (N'2'), (N'3')
                      ) AS [board] ([ClaimValue])
                      WHERE NOT EXISTS
                      (
                          SELECT 1
                          FROM [Security].[UserClaims] AS [existing]
                          WHERE [existing].[UserId] = [user].[Id]
                            AND [existing].[ClaimType] = N'expertBoard'
                            AND [existing].[ClaimValue] = [board].[ClaimValue]
                      );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927143453_AddExpertBoardAccessClaims'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927143453_AddExpertBoardAccessClaims', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928102935_MakeProductionAppendOnly'
)
BEGIN
    DROP INDEX [IX_Production_FactoryId_MachineId] ON [RealTime].[Production];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928102935_MakeProductionAppendOnly'
)
BEGIN
    CREATE INDEX [IX_Production_FactoryId_MachineId_TimeStamp] ON [RealTime].[Production] ([FactoryId], [MachineId], [TimeStamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928102935_MakeProductionAppendOnly'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260928102935_MakeProductionAppendOnly', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080016_AddCustomerIdToProduction'
)
BEGIN
    ALTER TABLE [RealTime].[Production] ADD [CustomerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080016_AddCustomerIdToProduction'
)
BEGIN
    UPDATE production
    SET production.[CustomerId] = factory.[CustomerId]
    FROM [RealTime].[Production] AS production
    INNER JOIN [Org].[Factories] AS factory
        ON factory.[Id] = production.[FactoryId];

    IF EXISTS (
        SELECT 1
        FROM [RealTime].[Production]
        WHERE [CustomerId] IS NULL
    )
        THROW 51001, 'Could not resolve CustomerId for every Production row.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080016_AddCustomerIdToProduction'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RealTime].[Production]') AND [c].[name] = N'CustomerId');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [RealTime].[Production] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [RealTime].[Production] ALTER COLUMN [CustomerId] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080016_AddCustomerIdToProduction'
)
BEGIN
    CREATE INDEX [IX_Production_CustomerId] ON [RealTime].[Production] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080016_AddCustomerIdToProduction'
)
BEGIN
    ALTER TABLE [RealTime].[Production] ADD CONSTRAINT [FK_Production_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080016_AddCustomerIdToProduction'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929080016_AddCustomerIdToProduction', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929093408_AddPlanningStoppageSupport'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Planning].[ProductionSchedules]') AND [c].[name] = N'BatchId');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Planning].[ProductionSchedules] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [Planning].[ProductionSchedules] ALTER COLUMN [BatchId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929093408_AddPlanningStoppageSupport'
)
BEGIN
    ALTER TABLE [Planning].[ProductionSchedules] ADD [Title] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929093408_AddPlanningStoppageSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929093408_AddPlanningStoppageSupport', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929114504_AddProductionQRejectDiff'
)
BEGIN
    ALTER TABLE [RealTime].[Production] ADD [QrejectDiff] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929114504_AddProductionQRejectDiff'
)
BEGIN
    WITH RejectReadings AS
    (
        SELECT
            [Id],
            [Qreject],
            LAG([Qreject]) OVER
            (
                PARTITION BY [CustomerId], [FactoryId], [LineId], [MachineId]
                ORDER BY [TimeStamp], [Id]
            ) AS [PreviousQreject]
        FROM [RealTime].[Production]
    )
    UPDATE production
    SET production.[QrejectDiff] =
        CASE
            WHEN reading.[PreviousQreject] IS NULL THEN 0
            WHEN reading.[Qreject] <= reading.[PreviousQreject] THEN 0
            ELSE CONVERT(decimal(18,4), reading.[Qreject] - reading.[PreviousQreject])
        END
    FROM [RealTime].[Production] AS production
    INNER JOIN RejectReadings AS reading
        ON reading.[Id] = production.[Id];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929114504_AddProductionQRejectDiff'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RealTime].[Production]') AND [c].[name] = N'QrejectDiff');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [RealTime].[Production] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [RealTime].[Production] ALTER COLUMN [QrejectDiff] decimal(18,4) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929114504_AddProductionQRejectDiff'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929114504_AddProductionQRejectDiff', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123320_AddOeeSnapshotLastState'
)
BEGIN
    ALTER TABLE [Analytics].[OeeSnapshots] ADD [LastState] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929123320_AddOeeSnapshotLastState'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929123320_AddOeeSnapshotLastState', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929125651_AddOeeSnapshotScopeFields'
)
BEGIN
    ALTER TABLE [Analytics].[OeeSnapshots] ADD [CustomerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929125651_AddOeeSnapshotScopeFields'
)
BEGIN
    ALTER TABLE [Analytics].[OeeSnapshots] ADD [FactoryId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929125651_AddOeeSnapshotScopeFields'
)
BEGIN
    ALTER TABLE [Analytics].[OeeSnapshots] ADD [LineId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929125651_AddOeeSnapshotScopeFields'
)
BEGIN
    ALTER TABLE [Analytics].[OeeSnapshots] ADD [MachineName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929125651_AddOeeSnapshotScopeFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929125651_AddOeeSnapshotScopeFields', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930102503_add nullable in Downtome OEE'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Analytics].[OeeSnapshots]') AND [c].[name] = N'DowntimeMin');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [Analytics].[OeeSnapshots] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [Analytics].[OeeSnapshots] ALTER COLUMN [DowntimeMin] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930102503_add nullable in Downtome OEE'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930102503_add nullable in Downtome OEE', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930105401_AddOeeSnapshotsPerHour'
)
BEGIN
    CREATE TABLE [Analytics].[OeeSnapshotsPerHour] (
        [Id] int NOT NULL IDENTITY,
        [MachineId] int NOT NULL,
        [MachineName] nvarchar(100) NULL,
        [CustomerId] int NULL,
        [FactoryId] int NULL,
        [LineId] int NULL,
        [ShiftId] int NOT NULL,
        [ShiftDate] datetime2 NOT NULL,
        [AvailabilityPct] decimal(5,2) NULL,
        [PerformancePct] decimal(5,2) NULL,
        [QualityPct] decimal(5,2) NULL,
        [OeePct] decimal(5,2) NULL,
        [AverageSpeedRpm] decimal(10,2) NULL,
        [LastState] int NULL,
        [TotalOutput] int NOT NULL,
        [RejectedUnits] int NOT NULL,
        [DowntimeMin] int NULL,
        [EnergyKwh] decimal(10,3) NULL,
        [ComputedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_OeeSnapshotsPerHour] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OeeSnapshotsPerHour_Machines_MachineId] FOREIGN KEY ([MachineId]) REFERENCES [Scada].[Machines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OeeSnapshotsPerHour_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Planning].[Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930105401_AddOeeSnapshotsPerHour'
)
BEGIN
    CREATE INDEX [IX_OeeSnapshotsPerHour_MachineId_ShiftDate] ON [Analytics].[OeeSnapshotsPerHour] ([MachineId], [ShiftDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930105401_AddOeeSnapshotsPerHour'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OeeSnapshotsPerHour_MachineId_ShiftId_ShiftDate] ON [Analytics].[OeeSnapshotsPerHour] ([MachineId], [ShiftId], [ShiftDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930105401_AddOeeSnapshotsPerHour'
)
BEGIN
    CREATE INDEX [IX_OeeSnapshotsPerHour_ShiftId] ON [Analytics].[OeeSnapshotsPerHour] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930105401_AddOeeSnapshotsPerHour'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930105401_AddOeeSnapshotsPerHour', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930152153_AddPowerAnalysis'
)
BEGIN
    IF SCHEMA_ID(N'Calculated') IS NULL EXEC(N'CREATE SCHEMA [Calculated];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930152153_AddPowerAnalysis'
)
BEGIN
    CREATE TABLE [Calculated].[PowerAnalysis] (
        [Id] int NOT NULL IDENTITY,
        [TransformerId] int NOT NULL,
        [TransformerName] nvarchar(150) NOT NULL,
        [CustomerId] int NOT NULL,
        [FactoryId] int NOT NULL,
        [LineId] int NULL,
        [ShiftId] int NOT NULL,
        [ShiftName] nvarchar(100) NOT NULL,
        [ShiftStartTime] datetime2(7) NOT NULL,
        [ShiftEndTime] datetime2(7) NOT NULL,
        [TotalEnergyConsumption] decimal(38,6) NOT NULL,
        [Status] varchar(7) NOT NULL,
        [PowerFactor] decimal(28,6) NOT NULL,
        [Voltage] decimal(28,6) NOT NULL,
        [Current] decimal(28,6) NOT NULL,
        [Hermonics] decimal(28,6) NOT NULL,
        [AvgTHDv] decimal(28,6) NOT NULL,
        [AvgTHDi] decimal(28,6) NOT NULL,
        [LastReadingAt] datetime2(7) NULL,
        [ReadingCount] bigint NOT NULL,
        [CalculatedAt] datetime2(7) NOT NULL,
        CONSTRAINT [PK_PowerAnalysis] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PowerAnalysis_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PowerAnalysis_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PowerAnalysis_ProductionLines_LineId] FOREIGN KEY ([LineId]) REFERENCES [Org].[ProductionLines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PowerAnalysis_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Planning].[Shifts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PowerAnalysis_Transformers_TransformerId] FOREIGN KEY ([TransformerId]) REFERENCES [MasterData].[Transformers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930152153_AddPowerAnalysis'
)
BEGIN
    CREATE INDEX [IX_PowerAnalysis_CustomerId_FactoryId_LineId_ShiftStartTime] ON [Calculated].[PowerAnalysis] ([CustomerId], [FactoryId], [LineId], [ShiftStartTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930152153_AddPowerAnalysis'
)
BEGIN
    CREATE INDEX [IX_PowerAnalysis_FactoryId] ON [Calculated].[PowerAnalysis] ([FactoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930152153_AddPowerAnalysis'
)
BEGIN
    CREATE INDEX [IX_PowerAnalysis_LineId] ON [Calculated].[PowerAnalysis] ([LineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930152153_AddPowerAnalysis'
)
BEGIN
    CREATE INDEX [IX_PowerAnalysis_ShiftId] ON [Calculated].[PowerAnalysis] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930152153_AddPowerAnalysis'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PowerAnalysis_TransformerId_ShiftId_ShiftStartTime] ON [Calculated].[PowerAnalysis] ([TransformerId], [ShiftId], [ShiftStartTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930152153_AddPowerAnalysis'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930152153_AddPowerAnalysis', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930153507_AddPowerAnalysisPerHour'
)
BEGIN
    CREATE TABLE [Calculated].[PowerAnalysisPerHour] (
        [Id] int NOT NULL IDENTITY,
        [TransformerId] int NOT NULL,
        [TransformerName] nvarchar(150) NOT NULL,
        [CustomerId] int NOT NULL,
        [FactoryId] int NOT NULL,
        [LineId] int NULL,
        [ShiftId] int NOT NULL,
        [ShiftName] nvarchar(100) NOT NULL,
        [ShiftStartTime] datetime2(7) NOT NULL,
        [ShiftEndTime] datetime2(7) NOT NULL,
        [HourStartTime] datetime2(7) NOT NULL,
        [HourEndTime] datetime2(7) NOT NULL,
        [TotalEnergyConsumption] decimal(38,6) NOT NULL,
        [Status] varchar(7) NOT NULL,
        [PowerFactor] decimal(28,6) NOT NULL,
        [Voltage] decimal(28,6) NOT NULL,
        [Current] decimal(28,6) NOT NULL,
        [Hermonics] decimal(28,6) NOT NULL,
        [AvgTHDv] decimal(28,6) NOT NULL,
        [AvgTHDi] decimal(28,6) NOT NULL,
        [LastReadingAt] datetime2(7) NULL,
        [ReadingCount] bigint NOT NULL,
        [CalculatedAt] datetime2(7) NOT NULL,
        CONSTRAINT [PK_PowerAnalysisPerHour] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PowerAnalysisPerHour_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Org].[Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PowerAnalysisPerHour_Factories_FactoryId] FOREIGN KEY ([FactoryId]) REFERENCES [Org].[Factories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PowerAnalysisPerHour_ProductionLines_LineId] FOREIGN KEY ([LineId]) REFERENCES [Org].[ProductionLines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PowerAnalysisPerHour_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Planning].[Shifts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PowerAnalysisPerHour_Transformers_TransformerId] FOREIGN KEY ([TransformerId]) REFERENCES [MasterData].[Transformers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930153507_AddPowerAnalysisPerHour'
)
BEGIN
    CREATE INDEX [IX_PowerAnalysisPerHour_CustomerId_FactoryId_LineId_HourStartTime] ON [Calculated].[PowerAnalysisPerHour] ([CustomerId], [FactoryId], [LineId], [HourStartTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930153507_AddPowerAnalysisPerHour'
)
BEGIN
    CREATE INDEX [IX_PowerAnalysisPerHour_FactoryId] ON [Calculated].[PowerAnalysisPerHour] ([FactoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930153507_AddPowerAnalysisPerHour'
)
BEGIN
    CREATE INDEX [IX_PowerAnalysisPerHour_LineId] ON [Calculated].[PowerAnalysisPerHour] ([LineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930153507_AddPowerAnalysisPerHour'
)
BEGIN
    CREATE INDEX [IX_PowerAnalysisPerHour_ShiftId] ON [Calculated].[PowerAnalysisPerHour] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930153507_AddPowerAnalysisPerHour'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PowerAnalysisPerHour_TransformerId_ShiftId_HourStartTime] ON [Calculated].[PowerAnalysisPerHour] ([TransformerId], [ShiftId], [HourStartTime]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930153507_AddPowerAnalysisPerHour'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930153507_AddPowerAnalysisPerHour', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    IF SCHEMA_ID(N'Integration') IS NULL EXEC(N'CREATE SCHEMA [Integration];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    ALTER TABLE [Production].[BatchMaterials] ADD [ItemCode] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    ALTER TABLE [Production].[BatchMaterials] ADD [OperationCode] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    ALTER TABLE [Production].[BatchMaterials] ADD [SourceRecordId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    ALTER TABLE [Production].[BatchMaterials] ADD [Uom] nvarchar(30) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Production].[Batches]') AND [c].[name] = N'PlannedStart');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [Production].[Batches] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [Production].[Batches] ALTER COLUMN [PlannedStart] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Production].[Batches]') AND [c].[name] = N'PlannedQty');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [Production].[Batches] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [Production].[Batches] ALTER COLUMN [PlannedQty] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    ALTER TABLE [Production].[Batches] ADD [JobOrderId] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    ALTER TABLE [Production].[Batches] ADD [ReceivedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    ALTER TABLE [Production].[Batches] ADD [SourceRecordId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    ALTER TABLE [Production].[Batches] ADD [SourceType] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    ALTER TABLE [Production].[Batches] ADD [StorageLocation] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    ALTER TABLE [Production].[Batches] ADD [WarehouseCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE TABLE [Analytics].[EnergyHarmonicReadings] (
        [Id] int NOT NULL IDENTITY,
        [TransformerId] int NOT NULL,
        [SourceRecordId] bigint NULL,
        [Timestamp] datetime2 NOT NULL,
        [HarmonicI31] decimal(18,3) NOT NULL,
        [HarmonicI32] decimal(18,3) NOT NULL,
        [HarmonicI33] decimal(18,3) NOT NULL,
        [HarmonicV31] decimal(18,3) NOT NULL,
        [HarmonicV32] decimal(18,3) NOT NULL,
        [HarmonicV33] decimal(18,3) NOT NULL,
        [HarmonicI51] decimal(18,3) NOT NULL,
        [HarmonicI52] decimal(18,3) NOT NULL,
        [HarmonicI53] decimal(18,3) NOT NULL,
        [HarmonicV51] decimal(18,3) NOT NULL,
        [HarmonicV52] decimal(18,3) NOT NULL,
        [HarmonicV53] decimal(18,3) NOT NULL,
        [HarmonicI71] decimal(18,3) NOT NULL,
        [HarmonicI72] decimal(18,3) NOT NULL,
        [HarmonicI73] decimal(18,3) NOT NULL,
        [HarmonicV71] decimal(18,3) NOT NULL,
        [HarmonicV72] decimal(18,3) NOT NULL,
        [HarmonicV73] decimal(18,3) NOT NULL,
        [HarmonicI91] decimal(18,3) NOT NULL,
        [HarmonicI92] decimal(18,3) NOT NULL,
        [HarmonicI93] decimal(18,3) NOT NULL,
        [HarmonicV91] decimal(18,3) NOT NULL,
        [HarmonicV92] decimal(18,3) NOT NULL,
        [HarmonicV93] decimal(18,3) NOT NULL,
        [HarmonicI111] decimal(18,3) NOT NULL,
        [HarmonicI112] decimal(18,3) NOT NULL,
        [HarmonicI113] decimal(18,3) NOT NULL,
        [HarmonicV111] decimal(18,3) NOT NULL,
        [HarmonicV112] decimal(18,3) NOT NULL,
        [HarmonicV113] decimal(18,3) NOT NULL,
        [HarmonicI131] decimal(18,3) NOT NULL,
        [HarmonicI132] decimal(18,3) NOT NULL,
        [HarmonicI133] decimal(18,3) NOT NULL,
        [HarmonicV131] decimal(18,3) NOT NULL,
        [HarmonicV132] decimal(18,3) NOT NULL,
        [HarmonicV133] decimal(18,3) NOT NULL,
        CONSTRAINT [PK_EnergyHarmonicReadings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EnergyHarmonicReadings_Transformers_TransformerId] FOREIGN KEY ([TransformerId]) REFERENCES [MasterData].[Transformers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE TABLE [Production].[MaterialSplits] (
        [Id] int NOT NULL IDENTITY,
        [BatchMaterialId] int NOT NULL,
        [SplitUid] nvarchar(30) NOT NULL,
        [Weight] decimal(18,4) NOT NULL,
        [SplitAt] datetime2 NULL,
        [SourceRecordId] bigint NULL,
        CONSTRAINT [PK_MaterialSplits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaterialSplits_BatchMaterials_BatchMaterialId] FOREIGN KEY ([BatchMaterialId]) REFERENCES [Production].[BatchMaterials] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE TABLE [Integration].[SyncCheckpoints] (
        [Id] int NOT NULL IDENTITY,
        [SourceSystem] nvarchar(50) NOT NULL,
        [StreamName] nvarchar(100) NOT NULL,
        [LastSourceId] bigint NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SyncCheckpoints] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE TABLE [Integration].[SyncErrors] (
        [Id] int NOT NULL IDENTITY,
        [SourceSystem] nvarchar(50) NOT NULL,
        [StreamName] nvarchar(100) NOT NULL,
        [SourceRecordId] bigint NULL,
        [ErrorMessage] nvarchar(max) NOT NULL,
        [AttemptCount] int NOT NULL,
        [FirstOccurredAt] datetime2 NOT NULL,
        [LastOccurredAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        CONSTRAINT [PK_SyncErrors] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE TABLE [Production].[WeighingEvents] (
        [Id] int NOT NULL IDENTITY,
        [BatchId] int NOT NULL,
        [BatchMaterialId] int NOT NULL,
        [ScaleId] int NULL,
        [MaterialUid] nvarchar(30) NULL,
        [Weight] decimal(18,4) NOT NULL,
        [RecordedAt] datetime2 NOT NULL,
        [Dispatched] bit NOT NULL,
        [WeighingSource] nvarchar(20) NOT NULL,
        [SourceRecordId] bigint NULL,
        CONSTRAINT [PK_WeighingEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WeighingEvents_BatchMaterials_BatchMaterialId] FOREIGN KEY ([BatchMaterialId]) REFERENCES [Production].[BatchMaterials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WeighingEvents_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Production].[Batches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WeighingEvents_Scales_ScaleId] FOREIGN KEY ([ScaleId]) REFERENCES [RealTime].[Scales] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE TABLE [Production].[MachineMaterialLoads] (
        [Id] int NOT NULL IDENTITY,
        [BatchId] int NOT NULL,
        [BatchMaterialId] int NOT NULL,
        [MaterialSplitId] int NULL,
        [MachineId] int NOT NULL,
        [MaterialUid] nvarchar(30) NOT NULL,
        [Weight] decimal(18,4) NULL,
        [LoadedAt] datetime2 NOT NULL,
        [ShiftStartTime] datetime2 NOT NULL,
        [ActivationDate] datetime2 NULL,
        [FinishingDate] datetime2 NULL,
        [IsPending] bit NULL,
        [Count] int NULL,
        [SourceRecordId] bigint NULL,
        CONSTRAINT [PK_MachineMaterialLoads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MachineMaterialLoads_BatchMaterials_BatchMaterialId] FOREIGN KEY ([BatchMaterialId]) REFERENCES [Production].[BatchMaterials] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MachineMaterialLoads_Batches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [Production].[Batches] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MachineMaterialLoads_Machines_MachineId] FOREIGN KEY ([MachineId]) REFERENCES [Scada].[Machines] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MachineMaterialLoads_MaterialSplits_MaterialSplitId] FOREIGN KEY ([MaterialSplitId]) REFERENCES [Production].[MaterialSplits] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BatchMaterials_SourceRecordId] ON [Production].[BatchMaterials] ([SourceRecordId]) WHERE [SourceRecordId] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Batches_SourceType_SourceRecordId] ON [Production].[Batches] ([SourceType], [SourceRecordId]) WHERE [SourceRecordId] IS NOT NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EnergyHarmonicReadings_SourceRecordId] ON [Analytics].[EnergyHarmonicReadings] ([SourceRecordId]) WHERE [SourceRecordId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE INDEX [IX_EnergyHarmonicReadings_TransformerId_Timestamp] ON [Analytics].[EnergyHarmonicReadings] ([TransformerId], [Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE INDEX [IX_MachineMaterialLoads_BatchId_MachineId_LoadedAt] ON [Production].[MachineMaterialLoads] ([BatchId], [MachineId], [LoadedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE INDEX [IX_MachineMaterialLoads_BatchMaterialId] ON [Production].[MachineMaterialLoads] ([BatchMaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE INDEX [IX_MachineMaterialLoads_MachineId] ON [Production].[MachineMaterialLoads] ([MachineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE INDEX [IX_MachineMaterialLoads_MaterialSplitId] ON [Production].[MachineMaterialLoads] ([MaterialSplitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MachineMaterialLoads_SourceRecordId] ON [Production].[MachineMaterialLoads] ([SourceRecordId]) WHERE [SourceRecordId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE INDEX [IX_MaterialSplits_BatchMaterialId] ON [Production].[MaterialSplits] ([BatchMaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MaterialSplits_SourceRecordId] ON [Production].[MaterialSplits] ([SourceRecordId]) WHERE [SourceRecordId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MaterialSplits_SplitUid] ON [Production].[MaterialSplits] ([SplitUid]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SyncCheckpoints_SourceSystem_StreamName] ON [Integration].[SyncCheckpoints] ([SourceSystem], [StreamName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE INDEX [IX_SyncErrors_ResolvedAt] ON [Integration].[SyncErrors] ([ResolvedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE INDEX [IX_SyncErrors_SourceSystem_StreamName_SourceRecordId] ON [Integration].[SyncErrors] ([SourceSystem], [StreamName], [SourceRecordId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE INDEX [IX_WeighingEvents_BatchId_RecordedAt] ON [Production].[WeighingEvents] ([BatchId], [RecordedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE INDEX [IX_WeighingEvents_BatchMaterialId] ON [Production].[WeighingEvents] ([BatchMaterialId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    CREATE INDEX [IX_WeighingEvents_ScaleId] ON [Production].[WeighingEvents] ([ScaleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WeighingEvents_WeighingSource_SourceRecordId] ON [Production].[WeighingEvents] ([WeighingSource], [SourceRecordId]) WHERE [SourceRecordId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019094353_AddEipicoSapRealtimeIntegration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019094353_AddEipicoSapRealtimeIntegration', N'9.0.0');
END;

COMMIT;
GO

