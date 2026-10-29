# User Management Implementation Notes

This version replaces the static Auth/Users placeholders with real ASP.NET Core Identity user management.

## Seeded super admin

The app seeds the following account on startup:

- Email/UserName: `Admin@admin.com`
- Password: `Admin@123`
- Role: `Admin`

The previous generated seed user `seed.admin@local` is deleted during identity seeding.

## Authentication

Implemented endpoints:

- `POST /v1/auth/login`
- `POST /v1/auth/refresh`
- `POST /v1/auth/logout`
- `GET /v1/auth/me`
- `POST /v1/auth/forgot-password`
- `POST /v1/auth/reset-password`

`login` returns JWT access token + refresh token.

## User management

Implemented endpoints:

- `GET /v1/users`
- `GET /v1/users/{id}`
- `POST /v1/users`
- `PUT /v1/users/{id}`
- `DELETE /v1/users/{id}` soft-deactivates the user
- `PUT /v1/users/{id}/change-password`
- `PUT /v1/users/{id}/reset-password`

## Role management

Implemented endpoints:

- `GET /v1/roles`
- `GET /v1/roles/{id}`
- `POST /v1/roles`
- `PUT /v1/roles/{id}`
- `DELETE /v1/roles/{id}`

Roles support claims. Claims are added to the JWT at login.

## Important

No `[Authorize]` attributes were added to the existing business endpoints. You can choose later which endpoints need authorization and which roles/claims should be required.

`DbContext.SaveChangesAsync(CancellationToken cancellationToken = default)` remains because EF Core requires this signature for override. Controllers/services/repositories remain without CancellationToken.
