# CancellationToken Removed Notes

This version removes `CancellationToken` from controllers, application service contracts, workflow services, repository contracts, and infrastructure repository/unit-of-work implementations.

## What changed

- API action signatures no longer expose `CancellationToken`.
- Application service interfaces no longer require token arguments.
- Repository and UnitOfWork interfaces no longer require token arguments.
- Internal service calls were updated to call methods without cancellation-token parameters.
- Constructor dependency injection remains in place; no `[FromServices]` or `HttpContext.RequestServices` are used in controllers.

## Time filter behavior remains

Frontend operational endpoints still use `FrontendFilterQuery` with:

- `duration = 0` current shift
- `duration = 1` last shift
- `duration = 2` last day
- `duration = 3` last week
- `duration = 4` last month
- nullable `from` / `to` override duration when both are supplied
- optional `factoryId` / `lineId`

## Build note

The runtime environment used to generate this archive does not include the .NET SDK, so a local `dotnet build` could not be executed here. The code was text-checked to ensure no remaining `CancellationToken`, `[FromServices]`, or `HttpContext.RequestServices` usages remain under `src/`.
