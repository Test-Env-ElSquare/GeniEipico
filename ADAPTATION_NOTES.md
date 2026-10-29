# Adaptation Notes

## Removed completely

- SignageScreens
- SignageTemplates
- KpiWidgets
- ColorCodes
- UI-only color/width/height/orientation/display-format fields

## Changed from original document

- `Guid` / UUID IDs -> `int` identity IDs.
- PostgreSQL `TIMESTAMPTZ` -> SQL Server `datetime2` mapped by C# `DateTime`.
- PostgreSQL arrays like `TEXT[]` -> normalized child tables:
  - `AlarmRuleEmailTargets`
  - `AlarmRuleSmsTargets`
  - `SparePartMachineTypes`
- `JSONB` / `JsonDocument` -> normalized tables or plain text columns where raw ingestion/audit needs to preserve payload:
  - `DeviceTypeTagDefinitions` replaces device telemetry schema JSON.
  - `SkuProcessParameters` replaces SKU MES params JSON.
  - `RouteStepParameters` replaces route-step params JSON.
  - `TelemetryRaw.RawPayload`, `MachineState.LastPayload`, `AuditLog.OldValues`, and `AuditLog.NewValues` are `string` / `nvarchar(max)`, not `JsonDocument`.

## Kept because it is backend/business data

- weight/capacity fields for scales, silos, materials, and dispensing.
- thresholds such as max temperature, vibration, min/max metric values.
- telemetry raw/tags/aggregates because IoT and SCADA depend on them.


# Customer-only update

- Removed `Tenant` entity/table.
- `Customer` is now the root data ownership/scope table.
- Replaced downstream `TenantId` fields with `CustomerId`.
- Factories still reference `CustomerId`.
- Removed `IMesDbContext` dependency from `MesDbContext`.
