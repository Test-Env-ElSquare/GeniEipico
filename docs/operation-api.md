# Operation API

Controller: `OperationController`, base path `/v1/operation`. All routes require
the normal JWT; the existing token validation also requires a valid Expert Board
assignment. Customer, factory, line and machine scope come from the JWT and the
shared authenticated scope resolver. These are ordinary HTTP endpoints; the UI
refresh button repeats the GET requests.

## Filters

Most GET routes accept `FactoryId` and `LineId` (both optional), `Duration` and,
for `Duration=5`, `From` and `To`. Duration values are 0=current shift,
1=last shift, 2=last 24 hours, 3=last seven days, 4=last month,
5=custom range. If a line is supplied without a factory, its factory is inferred
from the authenticated user's accessible lines. When no factory is supplied,
current/last shift is resolved separately for each accessible factory.
Selected line and factory IDs outside the user's access scope return 403.

JSON routes use the normal `ApiResponse` envelope: `success`, `data`, `meta`,
`error`, `ts`. File export returns a CSV attachment.

| Route | UI part | Data and behavior |
| --- | --- | --- |
| `GET /hierarchy` | Factory/line tree | Accessible factories and lines; dot state from machine readings in the last two minutes. |
| `GET /summary` | Header, four OEE cards, speed, machines, Weighted | OEE/Availability/Performance/Quality average OEE snapshots per machine, then average machines; current shift is also an average. Speed uses `AverageSpeedRpm` and reports `rpm`. Weighted is currently a fixed `96.1`. |
| `GET /parts` | Good/reject bar | `totalQty = SUM(Production.CountDiff)`, `rejectedQty = SUM(Production.Qreject)`, `goodQty = max(0,totalQty-rejectedQty)` in the selected period. |
| `GET /machines?State=all&Page=1&PageSize=10` | Machine table, All/Running switch | Latest reading within the period, machine name/type, last batch on the machine's line whose production window overlaps the period. Missing batch is `null`. `State=running` filters the rows. |
| `GET /machines/export?State=all` | Export button | CSV of all rows matching the machine table's filters. |
| `GET /machine-options` | Optional timeline machine selector | Accessible machine IDs, names, codes and line IDs. |
| `GET /machine-utilization` | Utilization bars | For each machine, `running segment minutes / selected period minutes * 100`; `null` when the timeline has no usable data. |
| `GET /state-timeline` | Aggregate state chart | Sums machine-state segment minutes, then divides by total machine-minutes. Includes `unknown` when part of the selected machine-time has no observation. No individual start/end times. |
| `GET /state-timeline?MachineId=3` | Single machine timeline | State segments with `from`, `to`, and minutes, using the same timeline calculation as SCADA machine detail. |
| `GET /hourly-production` | Hourly production table | Per UTC hour: sums CountDiff and Qreject, counts alarms triggered during the hour and transitions into idle/fault/offline/maintenance. Returns totals. |
| `GET /stoppages?Page=1&PageSize=10` | Stopping table | Non-running state segments, machine, from/to, reason and linked Work Order if a breakdown matches the stop's start time. An ongoing stop has `to=null`. |
| `GET /work-orders/form-options` | WO form from a stop | The same shared form-options builder used by `/v1/work-orders/form-options`. |
| `POST /work-orders` | WO action | Accepts the existing `CreateWorkOrderRequest`, validates access and delegates to the existing maintenance workflow service. Pass the stop's `machineId`; there is currently no dedicated stoppage-to-WO foreign key. |

The removed UI elements have no Operation endpoints: Equivalent, Operation,
THDI/THDV, both energy charts, machine-detail drilldown and OEE trend. The
Vibration column is omitted because `RealTime.Production` has no vibration
reading. The current speed source is RPM; a UPM conversion has not been defined.

Current production quantities follow the agreed convention: `Count` is the sum
of `CountDiff`, and `Qreject` is a per-signal quantity summed directly. A manual
state change writes zero `CountDiff` and zero `Qreject`, avoiding recounting.
Historical late signals can have zero `CountDiff` to avoid duplicating an already
accounted interval; see `production-append-only.md` for this limitation.
