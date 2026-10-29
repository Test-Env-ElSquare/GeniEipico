# GENIESYS MES Frontend Binding Guide - Split Endpoint Version

This version keeps the stable `Program.cs`, `launchSettings.json`, and package setup, but changes frontend binding strategy:

- Do **not** bind the UI to raw database/entity CRUD outputs.
- Prefer split DTO endpoints where each endpoint returns one frontend component / one use-case group.
- Page aggregate endpoints are still available for initial hydration, but component endpoints are clearer for use-case mapping and manager review.

## Main split DTO endpoints

### Overview
- `GET /v1/ui/overview/kpis`
- `GET /v1/ui/overview/oee-trend`
- `GET /v1/ui/overview/production-output`
- `GET /v1/ui/overview/energy-by-machine`
- `GET /v1/ui/overview/active-alarms`

### SCADA Live
- `GET /v1/ui/scada-live/status-counts` or `GET /v1/scada/status-counts`
- `GET /v1/ui/scada-live/warehouse-zone` or `GET /v1/scada/warehouse-zone`
- `GET /v1/ui/scada-live/dispensing-zone` or `GET /v1/scada/dispensing-zone`
- `GET /v1/ui/scada-live/production-zone` or `GET /v1/scada/production-zone`
- `GET /v1/ui/scada-live/finished-goods-zone` or `GET /v1/scada/finished-goods-zone`

### Operations
- `GET /v1/ui/operations/energy-load`
- `GET /v1/ui/operations/machine-utilization`
- `GET /v1/ui/operations/machine-status-table`

### Batch Management
- `GET /v1/batches` or `GET /v1/batches/table` = table rows only
- `GET /v1/batches/kanban` = UC-B13 Kanban only
- `GET /v1/batches/{id}` or `GET /v1/batches/{id}/detail` = UC-B14 detail only
- `GET /v1/batches/{id}/materials` = batch material/BOM rows only
- `GET /v1/batches/{id}/stage-log` = stage timeline only
- `GET /v1/batches/by-uid/{uid}` = UID lookup returning detail DTO

### Planning
- `GET /v1/ui/planning/gantt` or `GET /v1/schedule`
- `GET /v1/ui/planning/routing` or `GET /v1/routes`
- `GET /v1/ui/planning/mrp` or `GET /v1/mrp/requirements`
- `GET /v1/ui/planning/stoppages` or `GET /v1/schedule/stoppages`

### Assets
- `GET /v1/assets` = asset table DTO
- `GET /v1/assets/health-summary` = asset KPI cards

### Alarms
- `GET /v1/alarms/summary` = severity cards only
- `GET /v1/alarms/active` = active alarms table only
- `GET /v1/alarm-rules` = user-defined alarm rules
- `GET /v1/alarm-rules/system` = system alarm rules

### Analytics
- `GET /v1/analytics/insights`
- `GET /v1/analytics/production`
- `GET /v1/analytics/energy`
- `GET /v1/analytics/quality`
- `GET /v1/analytics/failure-risk`
- `GET /v1/oee/trend`
- `GET /v1/oee/factory-summary`
- `GET /v1/oee/losses`

### SKU Management
- `GET /v1/skus` = SKU table DTO
- `GET /v1/skus/erp-banner` = ERP sync banner
- `GET /v1/materials` = material table DTO
- `GET /v1/bill-of-materials` = BOM list DTO
- `GET /v1/skus/{id}/bom` = BOM rows for one SKU

### Configuration
- `GET /v1/config/platform-settings` = general settings form rows
- `GET /v1/config/rating-thresholds` = rating thresholds rows
- `GET /v1/config/machine/{machineId}` = machine parameter rows
- `GET /v1/config/color-codes` = color code rows
- `GET /v1/ui/configuration/shifts` = shift rows

### Hierarchy
- `GET /v1/ui/hierarchy/stats`
- `GET /v1/ui/hierarchy/tree`
- `GET /v1/factories/{id}/lines`
- `GET /v1/factories/{id}/summary`

### Maintenance
- `GET /v1/ui/maintenance/kpis` or `GET /v1/work-orders/kpis`
- `GET /v1/work-orders` = work-order table rows
- `GET /v1/pm-schedules` = PM schedule rows
- `GET /v1/breakdowns` = breakdown history rows
- `GET /v1/spare-parts` = spare parts inventory rows

### Signage
- `GET /v1/ui/signage/screens`
- `GET /v1/ui/signage/andon-board`
- `GET /v1/ui/signage/kpi-display-config`
- `GET /v1/ui/signage/templates`

## Workflow/action endpoints

These are Type B or Type C action endpoints. They are separate from read endpoints:

- `POST /v1/batches/{id}/start`
- `POST /v1/batches/{id}/advance-stage`
- `POST /v1/batches/{id}/complete`
- `POST /v1/batches/{id}/split`
- `POST /v1/batches/{id}/materials/{materialId}/dispense`
- `POST /v1/work-orders/{id}/start`
- `POST /v1/work-orders/{id}/complete`
- `POST /v1/work-orders/{id}/cancel`
- `POST /v1/pm-schedules/{id}/complete`
- `POST /v1/spare-parts/{id}/issue`
- `POST /v1/alarms/{id}/acknowledge`
- `POST /v1/alarms/{id}/resolve`
- `POST /v1/alarms/{id}/create-wo`
- `PUT /v1/routes/{id}/steps`
- `PUT /v1/routes/{id}/status`
- `PUT /v1/config/platform-settings`
- `PUT /v1/config/rating-thresholds`
- `PUT /v1/config/machine/{machineId}`

## Type C rule

Type C endpoints include comments explaining the split:

- Manual/fallback path now, so the UI can work.
- Real-time source later, such as telemetry_tags, MachineState, stable scale readings, or SignalR.

Example: dispense uses request `dispensedQty` now. Later UC-MF06 should replace that value with the latest stable scale telemetry reading.

## Type D rule

Type D endpoints return DTOs now. Later the same service methods can read from SQL Views or real-time/materialized projections without changing frontend contracts.
