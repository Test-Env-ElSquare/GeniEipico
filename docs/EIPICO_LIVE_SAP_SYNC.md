# SAP live import

`EipicoSync:InitializeMissingSapCheckpoints` defaults to `true`.
On the first start with this version, the worker records the current maximum
source SAPOrders ID in `Integration.SyncCheckpoints` as `SAPOrders.LiveStart`.
It advances the SAPOrders checkpoint to that boundary without deleting target
data or replaying the historical source rows. The boundary persists across
recycles and publishes. This also applies when upgrading an existing test
database that already has an SAPOrders checkpoint but no LiveStart marker.

Unresolved SAPOrders errors at or below that boundary are closed with a
`SkippedHistoricalSapOrder` explanation. They are audit records of skipped
bulk import, not proof of successful synchronization. Existing target batches
are preserved. Recent-order reconciliation imports new orders and refreshes
already imported orders; it does not reopen untouched historical orders.

A new realtime handheld, split, or machine-load record can require an older
SAP order. The realtime service then imports that single source order and its
materials inside the realtime record's existing transaction, without moving
the live SAP checkpoint. It still requires a unique line from source
MachineLoads and canonical synchronized factory/line definitions. Missing or
ambiguous dependencies remain retryable.

Deploy the updated application with the app pool stopped, then start it and
request Swagger. No reset, schema migration, or source database write is
required. Keep RuntimeFeatures:EnableEipicoSync enabled.

```sql
USE [GenieEipico_Test];
SELECT StreamName, LastSourceId, UpdatedAt
FROM Integration.SyncCheckpoints
WHERE SourceSystem = 'Eipico' AND StreamName LIKE 'SAPOrders%';

SELECT TOP (20) SourceRecordId, ErrorMessage, ResolvedAt
FROM Integration.SyncErrors
WHERE SourceSystem = 'Eipico' AND StreamName = 'SAPOrders'
ORDER BY Id DESC;
```

Setting InitializeMissingSapCheckpoints to false retains the historical bulk
import behavior. Do not delete the LiveStart marker during routine deployments;
doing so establishes another boundary on the next startup.
 