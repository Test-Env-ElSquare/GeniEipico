# Time Filter + Dependency Injection Update

## What changed

- Removed controller-level `[FromServices]` and `HttpContext.RequestServices` usage from API controllers.
- Controllers now use normal constructor injection for workflow/query services.
- Added `ITimeUtilities` + `TimeUtilities` in Application layer.
- Added `FrontendFilterQuery` for frontend query parameters:
  - `duration`
  - `factoryId`
  - `lineId`
  - `from`
  - `to`
- Added `FrontendFilter`/`ResolvedTimeFilter` metadata returned in API response `meta` for filtered read endpoints.
- Kept `Program.cs`, `launchSettings.json`, and package references stable.

## Duration mapping

- `0` = current shift
- `1` = last shift
- `2` = last day
- `3` = last week
- `4` = last month

`from` and `to` can be supplied manually. If both are supplied, they override the duration calculation.

## Pages excluded from mandatory duration filtering

- SCADA Live: live/current read model.
- Configuration: setup data, not operational time-window data.
- Hierarchy: static organization tree.
- Analytics: kept separate because long-term analytics normally reads aggregates/views.

## Seed data

Use `scripts/Seed_All_Test_Data.sql` to inject predictable test data across current schemas/tables.
