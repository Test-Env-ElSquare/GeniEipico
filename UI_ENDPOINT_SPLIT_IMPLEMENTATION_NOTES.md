# UI Endpoint Split Implementation Notes

This build applies the requested rule: one read endpoint should represent one frontend component / use-case group, instead of returning a large raw table or mixed page payload.

## What changed

1. Added split methods to `IFrontendDataService` and `FrontendDataService`.
2. Updated `BatchesController` so:
   - `GET /v1/batches` returns table rows only.
   - `GET /v1/batches/kanban` returns Kanban DTO only (UC-B13).
   - `GET /v1/batches/{id}/detail` returns Batch Detail DTO only (UC-B14).
3. Updated key controllers so frontend-facing GET endpoints return DTO sections, not raw EF entities.
4. Kept workflow/action endpoints separate and typed as B or C with comments.
5. Kept `Program.cs`, `API.csproj`, and `launchSettings.json` stable to avoid Swagger/startup errors.

## Important

Some page-level aggregate endpoints still exist under `/v1/ui/{page}` for optional initial hydration. They are not the recommended use-case-by-use-case endpoints. The split endpoints listed in `FRONTEND_BINDING_GUIDE.md` are the primary frontend binding contract.
