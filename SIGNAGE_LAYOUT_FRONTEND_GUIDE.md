# Signage layouts: Eipico-compatible backend

The three existing frontend layouts can share the same factory data, as in Eipico. Keep their current grouping/rendering logic. This change adds read-only compatibility routes; it does not build the frontend pages or change the existing `/hubs/scada` contract.

## Database setup (run manually)

From the repository root, with the real connection and JWT configuration loaded:

```powershell
dotnet ef database update AddMachineLoadLineForSignage --project src/Infrastructure --startup-project src/API --context MesDbContext
```

Run `sql/CreateLatestShiftPerMachineForLayout.sql` against `GenieEipico_Test` to create the view. The application does not automatically create it. Run `sql/CopyMachineLoadSignageLines.sql` once if existing synchronized machine loads need their scheduler line copied. This second script updates only missing `MachineMaterialLoads.LineId` values using source record IDs and definition names. It does not update Production history. The ongoing sync refreshes scheduler assignments and dominant flags.

Do not run `BackfillProductionShiftStartTime.sql` for this rollout: old Production readings with a null shift are intentionally ignored.

Deploy/restart the API after database setup. `RuntimeFeatures:EnableRealtimeMonitor` must be true for periodic signage signals. No database update or view creation was executed while preparing this change.

## Factory IDs

Use Genie IDs, not Eipico's IDs (2 and 3). At inspection time:

| Page | Factory name | Genie factoryId |
| --- | --- | --- |
| Eipico 1 Layout - 1 | Eipico1 | 327 |
| Eipico 1 Layout - 2 | Eipico1 | 327 |
| Eipico 2 | Eipico2 | 328 |

Resolve IDs from definitions in other deployments; these are not universal constants. Layout 1 and Layout 2 subscribe to the same factory, then use the original frontend grouping. The supplied screenshots show Penam/Cephalo/Beta Lactam/Dry Mix/Ampole/Drops on Layout 1, and Tablet/Syrup/Ointment on Layout 2. Backend line metadata includes `name` (ViewName fallback Name), `aliasName`, `area`, `number`, and nested machine `lines`, including `isDominant`. The layout code must use the Genie line/machine IDs returned by these definitions.

## Machine signals

Hub: `/signalsHub`. Use a Genie access token via `accessTokenFactory`. The hub is authenticated and checks customer, factory, and line permissions. Users with different line permissions do not share a broadcast group.

The existing method names are retained:

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${apiBase}/signalsHub`, { accessTokenFactory: () => token })
  .withAutomaticReconnect()
  .build();

connection.on("ReceiveFactorySignals", data => {
  // Bare array, same nesting as Eipico. No data/success wrapper.
  renderFactorySignals(data);
});

async function subscribe() {
  await connection.invoke("JoinFactoryGroup", factoryId);
  await connection.invoke("GetFactorySignals", factoryId);
}
connection.onreconnected(subscribe);
await connection.start();
await subscribe();
// On page exit: invoke LeaveFactoryGroup(factoryId), then stop().
```

Joining replaces the previous factory subscription on that connection. The initial snapshot is delivered by `GetFactorySignals`. Full snapshots are broadcast every two seconds while subscribed, following the original Eipico background service. An empty array is a valid response. Transport errors reject the invocation rather than returning an array containing an error object.

```json
[
  {
    "lineId": 8524,
    "machineCount": 1,
    "machines": [
      {
        "machineName": "E1_Amp_L1_Fill",
        "shiftStartTime": "2026-10-29T05:00:00Z",
        "totalCountDiff": 1234,
        "latestTimeStamp": "2026-10-29T07:00:00Z",
        "latestSignal": {
          "id": 42,
          "machineName": "E1_Amp_L1_Fill",
          "state": 1,
          "count": 1234,
          "qreject": 0,
          "speed": 100,
          "stateType": null,
          "stateReason": "0",
          "sku": "0",
          "countDiff": 10,
          "timeStamp": "2026-10-29T07:00:00Z",
          "shiftStartTime": "2026-10-29T05:00:00Z",
          "machineId": 17279,
          "factoryId": 327,
          "lineId": 8524,
          "fault": 0,
          "functionality": "Fill",
          "qrejectDiff": 0
        }
      }
    ]
  }
]
```

Example IDs/counts are illustrative. `latestSignal.count` is deliberately the view's `TotalCountDiff`, not the raw lifetime counter, matching `Domain/Implementation/SignalService.cs` in Eipico. `machineCount` counts machines having a non-null stored shift; merge with machine definitions for machines with no readings. Do not replace missing telemetry with invented zero readings. The frontend retains running/stopped/stale rendering.

`Calculated.VW_LatestShiftPerMachineForLayout` owns MAX(ShiftStartTime), SUM(CountDiff), latest-reading selection, and date-based total reset. It selects each machine's latest **stored** shift, not necessarily the active shift. It keeps the old latest reading but sets its total to zero when that shift's start is not on today's Cairo date. Before 08:00 this can zero the prior evening shift after midnight, exactly as the original date rule does. Equal timestamps are resolved by descending reading ID. Customer filtering is added for tenant isolation.

All timestamps are UTC with `Z`, unlike Eipico's local SQL timestamps. Display them using normal local-date formatting; do not add three hours manually. A view is a live query, not historical snapshot storage. Totals cover only readings that have ShiftStartTime populated, as agreed for this rollout.

## Definitions and current batch HTTP routes

Success responses are bare arrays, matching Eipico's layout endpoints.

| Purpose | Route |
| --- | --- |
| Factory lines with nested machines | `GET /api/Factory/GetFactoryLines?factoryId=327` |
| All authorized machines | `GET /api/Machine/getAllMachines` |
| One line's machines | `GET /api/Machine/GetLineMachines?lineid=8524` |
| Current batches | `GET /api/Dashboards/GetAllLinesActivatedSkus?factoryId=327` |
| Signals snapshot for debugging | `GET /v1/signage/signals?factoryId=327` |
| Scale status snapshot | `GET /v1/signage/scales?factoryId=327` |

Machine definition fields: `id`, `name`, `lineId`, `faunctionality` (legacy spelling), `ratedSpeed`, `isDominant`, `uid`. `uid` is currently null because the Genie machine entity has no source UID column; use `name` for signal matching. This is a layout compatibility projection, not a replacement for the complete Eipico Machine CRUD object.

Current batch fields: `lineId`, `skuName`, `jobOrderId`, `batchId` (batch UID string), `activationDate`, `finishingDate`, `count`, `machineId`, `machineName`. Eligibility follows Eipico: ActivationDate present, IsPending=false, group by scheduler LineId and BatchId, discard a group if any eligible load has a FinishingDate, then choose the last source load. The scheduler line is separate from the physical machine line. Product name/job order come from the synchronized Genie Batch/SKU. Existing source loads must already have their Batch/Material dependencies imported; this feature does not import missing historical batches.

## Scales

Connect separately to `/hubs/scaleStatus?factoryId=327` using the Genie access token. Register `InitialScales` before starting the connection. Its payload is a bare array of:

```json
{"scaleId":1,"scaleName":"E1_W4_L","room":"Room 4","factoryId":327,"lastSignalTime":"2026-10-29T07:00:00Z"}
```

This reproduces the source ScaleStatusHub's initial snapshot behavior; that hub has no periodic update event in the inspected Eipico source. A reconnect gets a fresh snapshot. `room` comes from the synchronized Scale.Zone (which the importer fills from Eipico Room). `lastSignalTime` uses Genie's current TelemetryAt, which may reflect either a weight reading or a heartbeat; Eipico's original repository uses heartbeat messages only. A separate heartbeat timestamp would be required for exact heartbeat-only status semantics.

## Remaining data-model differences

- `stateType` has no Genie Production column and is emitted as null.
- Source StateReason and SKU were converted to integers by the existing importer. The adapter emits their stored values as strings to retain the legacy field types; original nonnumeric text cannot be recovered from those stored integers.
- Genie Speed is an integer; the source decimal precision is not restored by the adapter.
- Genie IDs and UTC date values must be used by the frontend. Do not map old numeric IDs by coincidence.

The SQL SELECT was validated read-only against live Genie data before creating the view. Build and offline regression checks cover wire nesting, view-owned totals, authorized scopes, dominant definitions, scheduler placement, finished/pending batches, scale room formatting, and subscription isolation. End-to-end hub validation requires the manual database setup above.