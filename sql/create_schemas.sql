IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Org') EXEC('CREATE SCHEMA [Org]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'IoT') EXEC('CREATE SCHEMA [IoT]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Scada') EXEC('CREATE SCHEMA [Scada]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'MasterData') EXEC('CREATE SCHEMA [MasterData]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Production') EXEC('CREATE SCHEMA [Production]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Planning') EXEC('CREATE SCHEMA [Planning]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Alarm') EXEC('CREATE SCHEMA [Alarm]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Maintenance') EXEC('CREATE SCHEMA [Maintenance]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Asset') EXEC('CREATE SCHEMA [Asset]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Analytics') EXEC('CREATE SCHEMA [Analytics]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Config') EXEC('CREATE SCHEMA [Config]');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Security') EXEC('CREATE SCHEMA [Security]');
GO
