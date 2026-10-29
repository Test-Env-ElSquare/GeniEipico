# Production append-only correction

`RealTime.Production` stores readings, not one mutable snapshot per machine.
Telemetry projection, Settings mapping execution and manual state changes now use
`IProductionRecordWriter`. Existing readings are never updated. SCADA still selects
the newest timestamp (then Id) for each machine. The overview counts machines, not
historical rows.

## Database step (run by the developer, NOT applied by this change)

Stop the API/projector while applying the change. In Visual Studio Package Manager
Console, with Infrastructure as the default project and API as startup project:

```powershell
Add-Migration MakeProductionAppendOnly -Project Infrastructure -StartupProject API
Update-Database -Project Infrastructure -StartupProject API
```

Review the generated migration before applying. The intended schema difference is:

- Drop `IX_Production_FactoryId_MachineId` (unique).
- Create a **non-unique** index on `FactoryId, MachineId, TimeStamp`.
- No table recreation, row deletion or data rewrite.

No migration, model snapshot edit, SQL execution or database update was performed
as part of this change. Do not deploy the new writer against the old unique index.
If the generated migration contains unrelated changes, review those separately.

## Behavior and boundaries

- The SQL Server transaction-owned application lock serializes appends per machine
  across API instances. Reading and state-transition history commit together.
- Replaying a machine reading with the same source timestamp reuses its row,
  including when both the automatic projector and Settings mapping execute it.
  Different timestamps always append, even with identical measurements.
- Tag ingestion currently groups by source name + RecordedAt, with no RawId on tags.
  It cannot distinguish two distinct upstream signals with identical source and
  timestamp; this existing ingestion limitation is not a durable raw-event identity.
- CountDiff is `max(0, current - previous)` for ordered signals. The first reading
  and manual state-only records use zero. Delayed readings are retained with zero
  incremental count when a later sample is already stored, so the already-accounted
  interval isn't counted twice. Exact historical buckets should be derived from
  timestamp-ordered Count values, not by summing arrival-time deltas blindly.
- A delayed signal repairs state-history boundaries without updating Production.
- Manual changes copy existing measurements into a new row (CountDiff = 0,
  Qreject = 0); neither produced nor rejected units are counted twice.
- On application restart the existing projector scans retained tags from the start.
  It can therefore append historical signals not already represented in Production.
  Old overwritten readings cannot be recovered if the source tags are gone.
- EF SaveChanges rejects Modified MachineState entities as a safety net. Direct
  SQL/ExecuteUpdate bypasses that application guard and must not be used to overwrite
  Production readings.

## Verification

```powershell
dotnet build MESPlatForm.sln -c Release --no-restore
dotnet run --project tests/ProductionAppendOnly.RegressionTests -c Release
```

The regression executable performs no database access. It checks append construction,
delta/reset behavior, manual changes, index metadata, all SaveChanges guard overloads,
latest-state selection, projector append delegation, late arrivals and retry behavior.
SQL locking, transactional idempotency and the migration still need integration
verification against a disposable SQL Server database before production deployment.
