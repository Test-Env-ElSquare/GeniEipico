# Egypt Pharma frontend test data

Run `scripts/seed_egypharma_pages.sql` in SSMS. This is a self-contained script;
SQLCMD mode is not required. It selects existing factories by BOTH CustomerId=1
and codes EGYPHARMA-F01 / EGYPHARMA-F02, not assumed numeric factory IDs.
It reuses all their active lines, machines and shift definitions. Batterjee is
not targeted. Missing required migrations/organization definitions cause a failure.

## Execution

1. Review the target database and script; take an appropriate backup.
2. `@DaysBack=7` populates a recent week; change it to 31 for a fuller month.
3. `@Commit=0` is a transaction-based preview. Result rows are visible in SSMS,
   but ROLLBACK means the application will NOT see them. Identity counters may advance.
4. Set `@Commit=1` and rerun to save. Final output explicitly says SAVED.
5. Log in with CustomerId=1 and authorized factory/line claims. Use Duration=1
   (last shift) or Duration=2 (last day), with the IDs/URLs printed by the script.

The script does not execute itself as part of API startup. Codex did not run its
INSERT statements, even in rollback mode. Validation used a SQL parser and read-only
database column/default metadata; triggers/runtime constraints were not exercised.

Unique-key review also checked the connected database's actual unique indexes.
The work-order source SELECT produced 756 candidates but only 378 distinct numbers
with the shift data present during validation. Candidates are now ranked by the
final Number before insertion; existing numbers (including soft-deleted orders)
are skipped. This validation used SELECT and session-local table variables only.
Assets additionally check existing MachineId, breakdowns check Reference as well
as WorkOrderId, and transformer names are checked globally, matching their index.
No unique indexes were removed. Full seed execution has not been tested against
the database; schema checks and source-key checks do not exercise write triggers.

## Coverage

| Page / read section | Sources populated |
| --- | --- |
| Overview historical KPIs and factory production | Analytics.OeeSnapshots |
| Overview historical Production Output and OEE Breakdown | Analytics.OeeSnapshotsPerHour |
| Operation production, hourly totals, speed | RealTime.Production (append only) |
| Operation timeline, utilization, stops | Scada.MachineStateHistories |
| Active alarms / alarm history | Alarm.AlarmEvents, active and resolved examples |
| User/System rule lists | Disabled AlarmRules; no recipients/actions are added |
| Batch management/cards/details | Batches, BatchMaterials, BatchStageLogs |
| Planning schedule / planned versus produced / summary | Batches, ProductionSchedules, existing shifts |
| New Schedule | Planned demo batches without an explicit schedule |
| Routing / readiness | Active and draft Routes, RouteSteps |
| MRP | Customer-level MrpRequirements with ok, monitor and critical examples |
| SKU/material/BOM screens | Demo Skus, Materials, BillOfMaterials, SkuProcessParameters |
| Asset pages | Demo machine-linked Asset.Assets |
| Machine configuration | MachineConfigs only when no row exists |
| Maintenance and details | WorkOrders, BreakdownLogs, PmSchedules, SpareParts, WoPartsUsed |
| SCADA warehouse/dispensing | Clearly labelled NEW demo Silos/Scales; existing equipment is untouched |
| Energy history sources | Transformers and EnergyReadings |
| Stored analytics insights | AiInsights marked as synthetic, not real predictions |

## Intentional limits

- Duration=0 Overview remains empty: these factories have no live views, as agreed.
- Energy efficiency / energy by SKU still return empty by application design. Rows
  in EnergyReadings cannot supply the missing formulas/SKU relationship.
- Login/refresh, users/roles/permissions, live subscriptions, API keys, telemetry
  ingestion/mapping definitions, notification destinations and audit logs are not
  fabricated. Action endpoints still require valid requests; data insertion does
  not itself test every API action or every frontend integration.
- Customer-scoped MRP, SKU and route rows can appear anywhere under CustomerId=1;
  they cannot be assigned to a factory that their tables do not model.
- Existing occupied batch/schedule slots are skipped, not overwritten. If demo
  batch counts are zero, inspect line availability or choose a larger DaysBack.
- Data is synthetic. Counts, statuses and insights are UI examples, not operational
  measurements. Existing real/test rows are not deleted, normalized or updated;
  historical ratios stored as old 0–100 values may still affect aggregate results.
- These two factories' current legacy readers compare stored dates in UTC (their
  Overview offset is currently zero); this script matches those existing readers.
  Its printed GeneratedAtLocal is +03:00. This task does not change the application's
  timezone rules or convert pre-existing records.
- Rerunning skips existing natural keys; changing the date extends the dataset.
  Current-hour snapshots and existing equipment demo rows are not refreshed in place.

## Safety

Writes are inside one transaction and serialized against another run of this
script. No business rows are updated/deleted. Test entities are labelled
`TEST ONLY` / `UI-DEMO`, and transactional rows use `EGYPHARMA_UI_DEMO_V1` where
the table supports notes. Raw Production and snapshot tables lack a seed marker;
do not bulk-delete by customer/date to clean up, as that also risks unrelated rows.
