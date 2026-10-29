# Eipico definitions and calculated mapping

## Populated definitions synchronized by the worker

| Eipico source | Genie target | Mapping rule |
|---|---|---|
| `Definitions.Factories` | `Organization.Factories` | Every source factory is upserted. The first keeps the configured `EIPICO` code; additional factories use `EIPICO-F{sourceId}`. Name/location are copied. |
| `Definitions.Lines` | `Organization.ProductionLines` | The immutable source line id becomes `EIP-L{id}` because `Number` repeats within a factory; `FactoryId` attaches the line to the matching target factory; name/type are copied. |
| `Definitions.Machines` | `Scada.Machines` | `EIP-M{Id}` is the stable code; name, functionality and rated speed are copied. |
| `Definitions.Materials` | `MasterData.Materials` | `ItemCode` is the ERP/code; missing codes use `EIP-MAT-{Id}`. |
| `Definitions.SKUs` | `MasterData.Skus` | `SkuCode`, then `TechnicalSKU`, is the stable code. |
| `Definitions.Scales` | `RealTime.Scales` | `EIP-S{Id}` is the stable code; source `FactoryId` selects the target factory; name and room become name/zone. |
| `Definitions.Transformers` | `MasterData.Transformers` | Transformer name is preserved; source `FactoryId` selects the target factory. |
| `Definitions.LineTransformers` | `Org.ProductionLineTransformers` | The source many-to-many pair (`LineId`, `TransformerId`) is synchronized as a composite-key link. |
| `Definitions.Tag` | `IoT.DeviceTypes` / `IoT.DeviceTypeTagDefinitions` | Tags are registered under `EIPICO_MACHINE`; they are metadata only. |
| `RealTime.Energies` | `Analytics.EnergyReadings` | New source IDs only; cumulative phase energy and electrical fields are copied, with source timestamps normalized from Africa/Cairo to UTC. |
| `RealTime.Harmonics` | `Analytics.EnergyHarmonicReadings` | New source IDs only; harmonic fields are copied and linked to the synchronized transformer. |

The sync is an upsert of the current populated definition snapshot. It does not delete target rows that are absent from a later source snapshot.

## Intentionally not copied as standalone Genie tables

- `Definitions.FactoryMachines`: the current source row has only factory/name/functionality and no source machine key, while Genie machines are owned through a production line. We need a confirmed business rule before using this table to create or move machines.
- `Definitions.LineTransformers`: synchronized into `Org.ProductionLineTransformers` as a many-to-many link. Energy rows still carry their own optional line attribution and are not rewritten by this master-data sync.
- `Definitions.ScaleTypes`: Genie has protocol/capacity fields on `Scales`, but no independent scale-type entity; the description is copied into `Scales.Protocol`.
- `Definitions.Room`: Genie stores the operational zone on the scale/device where it is used.
- `Definitions.MachineFunctinoalyTag`: the source relation has no target machine-to-tag relation; tags are currently registered as device-type metadata only.
- `Definitions.Shifts` / `Definitions.MachineShifts`: the source snapshot was empty in the inspected Eipico database. Genie shifts are therefore configured explicitly per target factory; if Eipico starts populating these tables, we need to confirm whether they should replace the two 08:00/20:00 Genie shifts.
- Stoppage reason definition tables: Genie stores `ProductionSchedule.StoppageType` as text and has no approved standalone reason catalog, so they are not copied automatically.
- `Definitions.Recipes`: this source table was empty in the inspected database. If it becomes populated, it is the likely source for `MasterData.BillOfMaterials`; quantity/unit semantics must be confirmed first.
- `Definitions.MasterRecipeHeaders/Items/Operations/DbOperations`: these are SAP routing/master-recipe records and have no direct 1:1 target tables. The closest Genie model is `Planning.ProductionRoutes` + `Planning.RouteSteps` + `MasterData.BillOfMaterials`; we need approval of that mapping before importing them.
- `Definitions.MachineShifts`: this is scheduling/history data, not static machine master data; no automatic import is enabled.
- `MaterialBalances`, `ReconciliationItems`, `ReconciliationSyncHistory`, and `QuarantineRecords`: these are Eipico reconciliation/inventory workflows, not Genie master data.
- `RealTime.WeightedMaterials`: source table is empty and is intentionally not synchronized; handheld and split streams are the active weighing sources.

## Calculated tables and views

The large `Calculated.*` tables are not migrated as historical rows. Their source inputs are:

- production calculations: `RealTime.Production` and `Scada.MachineStateHistories`;
- energy calculations: `Analytics.EnergyReadings` and `Analytics.EnergyHarmonicReadings`;
- shift/hour aggregation: Genie `Shifts`, `PowerAnalysis`, `PowerAnalysisPerHour`, and `OeeSnapshots`.

The Eipico view formulas will be compared with the corresponding Genie query/services and implemented as Genie views or projections only where the result is still needed by the application. The populated Eipico `Calculated` tables are not bulk-copied because they contain derived historical rows; Genie will calculate from the synchronized raw/business tables.
