# GeniePark to GenieEipico endpoint parity

## Contract result

- Reference: `MESPlatForm_RealUserManagement_AdminSeed.rar`.
- Target: `GenieEipico`.
- Controller files compared: 49.
- Controller differences: 0.
- OpenAPI operations in each application: 253.
- Missing or additional operations: 0.
- OpenAPI component schemas in each application: 87.
- Missing or additional schema names: 0.

The HTTP method, route, request model and declared response contract remain compatible with the GeniePark frontend. The frontend should only need the GenieEipico API/SignalR base URLs and a GenieEipico login.

## Operation inventory

| Method | Count |
|---|---:|
| GET | 160 |
| POST | 65 |
| PUT | 21 |
| DELETE | 8 |
| Total | 253 |

| Controller group | Operations | Controller group | Operations |
|---|---:|---|---:|
| AlarmRules | 9 | Alarms | 9 |
| Analytics | 6 | API root | 1 |
| Assets | 2 | Audit | 1 |
| Auth | 9 | Batches | 18 |
| BillOfMaterials | 5 | Breakdowns | 1 |
| Config | 7 | Devices | 4 |
| ErpAdapter | 16 | Factories | 3 |
| Ingest | 7 | Insights | 2 |
| Lines | 2 | Machines | 7 |
| Materials | 5 | Mrp | 2 |
| Oee | 3 | Operation | 13 |
| Planning | 11 | PmSchedules | 1 |
| Roles | 5 | Routes | 3 |
| Scada | 8 | Scales | 4 |
| Schedule | 2 | Settings | 13 |
| Shifts | 2 | Silos | 1 |
| Skus | 12 | SpareParts | 1 |
| Ui | 40 | Users | 8 |
| WorkOrders | 10 |  |  |

## Eipico data behavior

- Organization and master-data endpoints read the Genie tables populated from Eipico definitions.
- SAP batches and materials read `Production.Batches` and `Production.BatchMaterials`, populated by the Eipico SAP synchronization.
- SCADA and production endpoints read Eipico readings synchronized into `RealTime.Production` and `RealTime.Scales`.
- Weighing, split and machine-load data read the Eipico integration tables added to the Genie structure.
- Live Overview reads `Analytics.VW_EipicoOee` and `Analytics.VW_EipicoOee_PerHour`, which calculate the original GeniePark OEE read model from Eipico `RealTime.Production`.
- UI endpoints no longer return fabricated factory, machine, SKU, planning, maintenance or analytics records when a table is empty. They return a valid empty array or null metric.
- Empty modules such as maintenance, assets or planning remain valid Genie endpoints and begin returning rows when data is entered through GenieEipico.
- The legacy `/Batterjee/signal` and `/Batterjee/energy` routes remain only because removing them would break endpoint parity. Eipico synchronization does not depend on them.

## Baseline placeholder operations

The original GeniePark project contains 28 operations implemented with `StaticOk`. They expose a stable route and response envelope but do not contain completed domain logic. GenieEipico preserves those contracts; they are not counted as missing regressions. They include ERP-adapter commands, analytics export, AI generation/dismiss, audit export and several future device/planning/history actions.

## Verification completed

- Solution build: succeeded with 0 warnings and 0 errors.
- Energy mapping regression checks: 35 passed.
- Overview regression checks: 60 passed.
- Planning regression checks: 38 passed.
- Production append-only checks: 30 passed.
- Candidate Swagger compared with the running GenieEipico Swagger: 253/253 operations and 87/87 schemas matched.

## Server runtime verification still required

The development machine cannot currently reach SQL Server `10.1.1.240`, so database-backed HTTP responses must be smoke-tested after publishing/restarting on the Eipico IIS server. Use a GenieEipico admin JWT and test protected GET endpoints first; execute mutating POST/PUT/DELETE actions only against disposable test records.
