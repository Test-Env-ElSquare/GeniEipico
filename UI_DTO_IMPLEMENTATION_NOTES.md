# UI DTO Implementation Notes

## What changed

This version adds a frontend-first DTO contract and keeps raw entity CRUD as internal/admin only.

### Main frontend controller

`API/Controllers/Frontend/UiController.cs`

Base route:

```http
/v1/ui
```

The design should bind to these page DTO endpoints:

- `GET /v1/ui/overview`
- `GET /v1/ui/scada-live`
- `GET /v1/ui/operations`
- `GET /v1/ui/batches`
- `GET /v1/ui/batches/{id}/detail`
- `GET /v1/ui/planning`
- `GET /v1/ui/assets`
- `GET /v1/ui/alarms`
- `GET /v1/ui/analytics`
- `GET /v1/ui/sku-management`
- `GET /v1/ui/configuration`
- `GET /v1/ui/hierarchy`
- `GET /v1/ui/maintenance`
- `GET /v1/ui/signage`
- `GET /v1/ui/use-case-coverage`

## Type coverage

- Type A: exposed to the UI through DTOs. Raw entity CRUD is not the frontend contract.
- Type B: workflow endpoints are implemented/kept and return `UiActionResultDto`.
- Type C: manual fallback is implemented where possible; comments explain the future real-time replacement.
- Type D: now returns UI DTOs with DB-first + static fallback. Later these can be replaced by SQL Views or real-time projections.

## Important rule

Frontend should not bind to raw domain entities such as `Batch`, `Sku`, `Machine`, or `WorkOrder` directly. Use the `/v1/ui/*` endpoints.

## Swagger / Launch

This version includes:

- `Swashbuckle.AspNetCore` package in `API.csproj`
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` package in `Infrastructure.csproj`
- `Program.cs` with Swagger and `/` redirect to `/swagger`
- `API/Properties/launchSettings.json` with `launchUrl: swagger`
