# MES Platform Generated Backend Models

Generated clean backend version adapted from the uploaded GENIESYS MES schema for:

- .NET Core / EF Core
- SQL Server
- `int` identity keys instead of `Guid`
- ASP.NET Core Identity with `ApplicationUser : IdentityUser<int>`
- `DateTime` / `datetime2` instead of `DateTimeOffset` / PostgreSQL `TIMESTAMPTZ`
- No `JsonDocument`
- No Signage / display-management tables
- No UI-only fields such as `ColorHex`, screen width/height, orientation, or display format

## Suggested 5-layer placement

```text
Domain
  Common/
  Entities/

Application
  Abstractions/

Infrastructure
  Identity/
  Persistence/

MesPlatform.Api
  Program.cs registration

MesPlatform.Shared
  DTOs / constants / responses (not generated here)
```

## Required EF packages in Infrastructure project

Use the versions that match your target .NET version:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="x.x.x" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="x.x.x" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="x.x.x" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="x.x.x" PrivateAssets="all" />
```

## Program.cs registration sample

```csharp
builder.Services.AddDbContext<MesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>()
    .AddEntityFrameworkStores<MesDbContext>()
    .AddDefaultTokenProviders();
```

## Migration commands

```bash
dotnet ef migrations add InitialMesSchema -p Infrastructure -s MesPlatform.Api
dotnet ef database update -p Infrastructure -s MesPlatform.Api
```

Run `sql/create_schemas.sql` before migration if you do not want EF to create schemas automatically.


## Customer-only update

This version merges the previous `Tenant` and `Customer` entities into one top-level `Customer` entity. All downstream scope fields now use `CustomerId` instead of `TenantId`. The old `Tenants` DbSet/table and `Tenant` entity were removed.

`IMesDbContext` was also removed from `MesDbContext` to avoid the previous missing-interface compile error and keep the persistence setup straightforward.
