# GenieEipico final deployment checklist

## Code state

- Build the solution in Release mode.
- Apply all EF migrations, including `20261025064047_AddEipicoEnergySourceSync`.
- Keep `RuntimeFeatures:EnableEipicoSync=true` in the IIS environment only when the target database is ready.
- Keep `RuntimeFeatures:EnableEnergyProjection=false`; Eipico energy is synchronized directly from `RealTime.Energies` and `RealTime.Harmonics`.
- Keep `EipicoSync:SourceTimeZoneId=Africa/Cairo` (the converter has a Windows `Egypt Standard Time` fallback).

## Existing test data decision

The first realtime implementation stored Eipico datetimes as plant-local values. The current code stores all new Eipico timestamps as UTC. Before the final restart, choose one of these safe paths:

1. For a disposable empty test database, recreate/apply migrations and let the worker start at current source checkpoints.
2. For the existing `GenieEipico_Test` data, run one reviewed, one-time conversion of the already synchronized Eipico rows from Africa/Cairo to UTC. Do not run it twice.

Do not mix old local rows with new UTC rows without making this decision; Overview and shift filters use UTC internally.

## First runtime verification

After IIS restart, verify the following in `GenieEipico_Test`:

- `Integration.SyncCheckpoints` contains `SAPOrders`, `Signals`, `ScaleSignals`, `ScaleLiveMessages`, `HandheldMatarial`, `SplittedMatarial`, `MachineLoads`, `Energies`, and `Harmonics`.
- The Eipico checkpoints advance after new source rows arrive.
- `Integration.SyncErrors` has no new unresolved errors for the latest source IDs.
- `RealTime.Production`, `RealTime.Scales`, `Production.WeighingEvents`, `Production.MaterialSplits`, and `Production.MachineMaterialLoads` continue receiving new rows.
- `Analytics.EnergyReadings` and `Analytics.EnergyHarmonicReadings` receive new rows after the migration.
- Swagger operations remain identical to GeniePark except for the intentionally removed application `/health` endpoint.

## Remaining functional work

- Transformer-to-multiple-line master data is synchronized through `Org.ProductionLineTransformers`; energy calculated views still decide independently whether to attribute a reading to one line or aggregate across linked lines.
- Eipico calculated tables are derived data and are not bulk-copied; equivalent Genie projections should be implemented only for screens that require them.
- Existing GeniePark placeholder endpoints (ERP adapter commands, exports, OTA, future scale history, and similar) remain contract-compatible but are not Eipico source synchronization work.
