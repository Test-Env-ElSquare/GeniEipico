# Alarm & Maintenance API

All endpoints require a Bearer token. Customer, factory, and line access are resolved
from the token; the frontend must not send a `customerId`.

Enum values are serialized as lowercase snake case, for example:

```json
{
  "severity": "critical",
  "status": "active",
  "actionType": "create_work_order",
  "workOrderType": "corrective",
  "priority": "high",
  "status": "in_progress"
}
```

## UI mapping

| UI section | Endpoint | Data source |
|---|---|---|
| Alarm KPI cards | `GET /v1/alarms/summary` | `Alarm.AlarmEvents` |
| Active Alarms | `GET /v1/alarms/active` | Unacknowledged rows from `Alarm.AlarmEvents` |
| History | `GET /v1/alarms/history` | Acknowledged or resolved rows |
| Acknowledge | `POST /v1/alarms/{id}/acknowledge` | Updates `AcknowledgedAt/AcknowledgedById` |
| Alarm details | `GET /v1/alarms/{id}/detail` | Alarm tables plus `IoT.TelemetryTags` |
| Probable Cause | `PUT /v1/alarms/{id}/probable-cause` | `Alarm.AlarmEvents.ProbableCause` |
| Create WO from alarm | `POST /v1/alarms/{id}/create-wo` | Creates `Maintenance.WorkOrders` |
| User-defined rules | `GET /v1/alarm-rules` | `Alarm.AlarmRules` |
| System rules | `GET /v1/alarm-rules/system` | `Alarm.AlarmRules` |
| Create rule | `POST /v1/alarm-rules` | Rule, scopes, actions, and email targets |
| Edit rule | `PUT /v1/alarm-rules/{id}` | User-defined rules only |
| Enable/disable rule | `PUT /v1/alarm-rules/{id}/toggle` | User and system rules |
| Delete rule | `DELETE /v1/alarm-rules/{id}` | User-defined rules only; soft delete |
| Rule dropdowns | `GET /v1/alarm-rules/form-options` | Database plus enum catalogs |
| Work Order KPIs | `GET /v1/work-orders/kpis` | `Maintenance.WorkOrders` |
| All Work Orders | `GET /v1/work-orders` | Database, paginated |
| WO dropdowns | `GET /v1/work-orders/form-options` | Assets, machines, users, enum catalogs |
| WO details | `GET /v1/work-orders/{id}/detail` | Database |
| Create WO | `POST /v1/work-orders` | Database |
| Edit WO | `PUT /v1/work-orders/{id}` | Database |
| Close WO | `POST /v1/work-orders/{id}/close` | Database |

The Export button is intentionally not implemented.

## Active and history definition

- Active: unresolved rows (`ResolvedAt == null`), whether acknowledged or not.
- History: acknowledged or resolved. An acknowledged but unresolved alarm appears
  in both Active and History.
- Lists are sorted newest first and support pagination.
- `FactoryId` is required for summary, active, and history.
- `LineId` is optional. When omitted, the selected factory's accessible lines are included.
- Active and history list rows use the database `id`; the display-only `eventId` is returned by alarm detail only.

## Alarm time filters

The following filters are supported by:

- `GET /v1/alarms/summary`
- `GET /v1/alarms/active`
- `GET /v1/alarms/history`

| Duration | Meaning |
|---:|---|
| `0` | Current factory shift |
| `1` | Previous factory shift |
| `2` | Last 24 hours |
| `3` | Last 7 days |
| `4` | Last month from the current time |
| `5` | Custom range; both `From` and `To` are required |

Example:

```http
GET /v1/alarms/active?Duration=5&FactoryId=1&LineId=2&From=2026-07-01T00:00:00Z&To=2026-07-27T23:59:59Z&Page=1&PageSize=20
```

## Alarm detail graphs

`GET /v1/alarms/{id}/detail?windowMinutes=30` returns up to two waveform series:

- The alarm's primary telemetry tag.
- A correlated tag from the same device when available.

Each series contains:

```json
{
  "tagName": "voltage",
  "label": "voltage",
  "unit": "V",
  "isPrimary": true,
  "points": [
    {
      "timestamp": "2026-07-27T08:10:00Z",
      "value": 380.5,
      "isTriggerPoint": true
    }
  ]
}
```

No static chart values are returned. When the database has no matching telemetry,
`waveforms` is empty and `meta.hasTelemetry` is `false`.

## Create or edit alarm rule example

```json
{
  "name": "Machine voltage range",
  "machineIds": [1, 2],
  "parameter": "voltage",
  "minimumValue": 360,
  "maximumValue": 420,
  "severity": 3,
  "status": 1,
  "createWorkOrder": true,
  "assignedToId": 4,
  "workOrderPriority": 3
}
```

`severity`, `status`, and `workOrderPriority` are numeric enum values. A rule
requires at least one bound, and when both are supplied `minimumValue` must be
less than `maximumValue`.

Use `GET /v1/machines/{id}/parameters` to load the parameters available for a
machine. Automatic work-order creation requires `assignedToId`.

Numeric properties submitted in the JSON object stored in `rawPayload` by
`POST /v1/ingest/telemetry` are extracted into `IoT.TelemetryTags` and evaluated
against active machine rules:

- A value below `minimumValue` or above `maximumValue` creates one open alarm.
- Repeated out-of-range values do not duplicate an unresolved alarm.
- Returning inside the configured range resolves the open alarm.
- An enabled automatic work-order action creates one corrective work order for
  the new alarm and assigns it to the configured user.

## Create Work Order example

Use either `POST /v1/work-orders` or
`POST /v1/alarms/{alarmId}/create-wo`.

```json
{
  "workOrderType": "corrective",
  "priority": "critical",
  "machineId": 1,
  "alarmEventId": 1,
  "assignedToId": 4,
  "plannedStart": "2026-07-27T12:00:00Z",
  "description": "Inspect voltage sag and motor current."
}
```

For the alarm route, `alarmEventId` in the body is ignored and the route `{alarmId}`
is used. A work order location is resolved from `machineId` or from the linked
alarm; work orders do not use `assetId`.
