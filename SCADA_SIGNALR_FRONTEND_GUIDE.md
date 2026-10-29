# SCADA SignalR Frontend Integration

## Hub URL

Use the API base URL from the frontend environment configuration:

```text
Local:
https://localhost:51877/hubs/scada

IIS:
http://196.219.184.42/GeniPark/hubs/scada
```

Do not hard-code `/GeniPark` in application code. Build the Hub URL from the
configured API base URL:

```javascript
const hubUrl = `${apiBaseUrl.replace(/\/+$/, "")}/hubs/scada`;
```

## Authentication

The Hub requires the same JWT access token returned by:

```text
POST /v1/auth/login
```

Install the browser client:

```bash
npm install @microsoft/signalr
```

Create the connection:

```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${apiBaseUrl.replace(/\/+$/, "")}/hubs/scada`, {
    accessTokenFactory: () => accessToken,
    withCredentials: false
  })
  .withAutomaticReconnect([0, 2000, 5000, 10000])
  .build();
```

`withCredentials: false` is required for cross-origin frontend development
with the current CORS policy. Authentication uses JWT, not cookies.

## Warehouse zone

Server event:

```text
WarehouseZoneUpdated
```

Subscription method:

```text
SubscribeWarehouse(factoryId, lineId?)
```

Example:

```javascript
connection.on("WarehouseZoneUpdated", message => {
  if (message.success) {
    renderWarehouseZone(message.data);
  }
});

await connection.start();

const initialWarehouse = await connection.invoke(
  "SubscribeWarehouse",
  factoryId,
  lineId ?? null
);

renderWarehouseZone(initialWarehouse.data);
```

Unsubscribe method:

```text
UnsubscribeWarehouse()
```

## Dispensing zone

Server event:

```text
DispensingZoneUpdated
```

Subscription method:

```text
SubscribeDispensing(factoryId, lineId?)
```

Example:

```javascript
connection.on("DispensingZoneUpdated", message => {
  if (message.success) {
    renderDispensingZone(message.data);
  }
});

await connection.start();

const initialDispensing = await connection.invoke(
  "SubscribeDispensing",
  factoryId,
  lineId ?? null
);

renderDispensingZone(initialDispensing.data);
```

Unsubscribe method:

```text
UnsubscribeDispensing()
```

## Reconnection

SignalR group membership is connection-specific. Subscribe again after an
automatic reconnect:

```javascript
connection.onreconnected(async () => {
  await connection.invoke("SubscribeWarehouse", factoryId, lineId ?? null);
  await connection.invoke("SubscribeDispensing", factoryId, lineId ?? null);
  await connection.invoke("SubscribeProduction", factoryId, lineId ?? null);
});
```

## Production zone

Server event:

```text
ProductionZoneUpdated
```

Subscription method:

```text
SubscribeProduction(factoryId, lineId?)
```

Unsubscribe method:

```text
UnsubscribeProduction()
```

The production payload is rebuilt from `RealTime.Production` and current-shift
OEE snapshots. Machine telemetry tags are projected into
`RealTime.Production` by the SCADA production projector.

## Message envelope

Initial subscription results and update events use the same envelope:

```json
{
  "success": true,
  "data": {},
  "error": null,
  "ts": "2026-08-05T10:00:00Z"
}
```

Treat each update as the latest complete zone snapshot. Replace the current
zone state in the frontend instead of appending it.

## REST endpoints retained for diagnostics

```text
GET /v1/scada/warehouse-zone?factoryId=1&lineId=1
GET /v1/scada/dispensing-zone?factoryId=1&lineId=1
GET /v1/scada/production-zone?factoryId=1&lineId=1
```

`factoryId` is required. `lineId` is optional.

## Browser test pages

```text
Local:
https://localhost:51877/signalr-tests/warehouse-test.html
https://localhost:51877/signalr-tests/dispensing-test.html
https://localhost:51877/signalr-tests/production-test.html

IIS:
http://196.219.184.42/GeniPark/signalr-tests/warehouse-test.html
http://196.219.184.42/GeniPark/signalr-tests/dispensing-test.html
http://196.219.184.42/GeniPark/signalr-tests/production-test.html
```

The test pages infer the API base URL automatically. They contain no stored
credentials and load the SignalR browser client locally, so they do not depend
on a CDN or internet access.
