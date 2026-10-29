# MES Logic Layer Implementation Notes

This version starts converting static workflow endpoints into real Application Service logic based on the Use Case Architecture Mapping.

## Type Model Applied

- Type A = CRUD / Direct DB Query. Existing generic CRUD remains in `CrudController` with endpoint type comments.
- Type B = Workflow / Business Logic. Implemented now in Application Services where feasible.
- Type C = Hybrid / Real-Time Dependent. Implemented manual/fallback path where possible and left clear comments for future real-time integration.
- Type D = Read Model / View / Dashboard. Left as static placeholders with comments because these should be Query Services or SQL Views later.

## New Application Services

Added interfaces in:

- `Application/Abstractions/Services/WorkflowServiceInterfaces.cs`

Added implementations in:

- `Application/Services/WorkflowServices.cs`

Registered in:

- `Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`

## Implemented Type B Logic

### Batch Workflow

- `POST /v1/batches/{id}/start`
- `POST /v1/batches/{id}/complete`
- `POST /v1/batches/{id}/advance-stage`
- `POST /v1/batches/{id}/split`

Implemented in `BatchWorkflowService`.

### Route Workflow

- `PUT /v1/routes/{id}/steps`
- `PUT /v1/routes/{id}/status`

Implemented in `RouteWorkflowService`.

### Maintenance Workflow

- `POST /v1/work-orders/{id}/start`
- `POST /v1/work-orders/{id}/complete`
- `POST /v1/work-orders/{id}/cancel`
- `POST /v1/pm-schedules/{id}/complete`
- `POST /v1/spare-parts/{id}/issue`

Implemented in `MaintenanceWorkflowService`.

### Alarm Workflow

- `PUT /v1/alarm-rules/{id}/toggle`
- `POST /v1/alarms/{id}/acknowledge`
- `POST /v1/alarms/{id}/resolve`
- `POST /v1/alarms/{id}/create-wo`

Implemented in `AlarmWorkflowService`.

### Configuration Workflow

- `PUT /v1/config/machine/{machineId}`
- `PUT /v1/config/rating-thresholds`
- `PUT /v1/config/platform-settings`

Implemented in `ConfigWorkflowService`.

### Silo Load

- `POST /v1/silos/{id}/load`

Implemented in `ScadaManualWorkflowService`.

## Implemented Type C Manual/Fallback Logic

These endpoints are real-time dependent in final MES, but have a manual/fallback path now:

- `POST /v1/batches/{id}/materials/{materialId}/dispense`
  - Manual `DispensedQty` from request is recorded now.
  - Future: replace with latest stable scale telemetry reading.

- `POST /v1/machines/{id}/state`
  - Manual machine state update now.
  - Future: SCADA State Updater updates state from telemetry tags.

- `POST /v1/scales/{id}/tare`
  - Manual tare now.
  - Future: scale session telemetry.

- `POST /v1/scales/{id}/assign-batch`
  - Manual assignment now.
  - Future: active dispense session tied to live scale readings.

- `POST /v1/ingest/heartbeat`
  - Device `LastSeenAt` and `ConnectionStatus` updated now.

- `GET /v1/ingest/config`
  - Basic device config from DB now.

- `POST /v1/ingest/ota-ack`
  - Firmware version and heartbeat fields updated now.

## Type D Skipped Intentionally

Examples intentionally left as static/read-model placeholders:

- `/v1/batches/kanban`
- `/v1/work-orders/kpis`
- `/v1/alarms/summary`
- `/v1/scada/dashboard`
- `/v1/scada/energy`
- `/v1/oee/*`
- `/v1/analytics/*`
- `/v1/assets/health-summary`
- `/v1/breakdowns/stats`

These should be implemented later as Query Services or SQL Views.

## Not Fully Implemented Yet

- Identity/Auth real JWT login, refresh, MFA, password reset.
- ERP payload parsing/import because current `ErpInboundRequest.Payload` is generic string and final JSON contract is not defined yet.
- Full telemetry pipeline: Tag Extractor, SCADA State Updater, Alarm Evaluator, OEE Aggregator, SignalR push.

## Build Note

I could not run `dotnet build` in this sandbox because the .NET SDK is not installed in the execution environment. Please run this locally:

```powershell
dotnet restore
dotnet build
```

or in Visual Studio: Build Solution.
