# Backend Next Step — Repository, UoW, Services, DI

This version adds the next backend layer after the current Models / Schema / Fluent Configurations step.

## Added

- `Application/Abstractions/Repositories/IRepository.cs`
- `Application/Abstractions/Repositories/IUnitOfWork.cs`
- `Application/Abstractions/Services/ICrudService.cs`
- `Application/Abstractions/Services/EntityServiceInterfaces.cs`
- `Application/Services/CrudService.cs`
- `Application/Services/EntityServices.cs`
- `Application/Common/Paging/QueryParameters.cs`
- `Application/Common/Paging/PagedResult.cs`
- `Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `Infrastructure/Repository/Repository.cs`
- `Infrastructure/Repository/UnitOfWork.cs`
- `Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `API/Extensions/ApiServiceCollectionExtensions.cs`
- `API/appsettings.json`
- `API/Program.example.cs`

## Important decisions

- No `Tenant` table was re-added.
- Everything uses the merged `Customer` model from the last version.
- No SignalR was added.
- No Background Jobs / Hangfire was added.
- No `Patch` method was created. Update operations are named `UpdateAsync`.
- Identity is registered with `ApplicationUser`, `ApplicationRole`, and `MesDbContext`.
- Repositories and services are registered as Scoped, matching the request-based Unit of Work pattern.

## Program.cs usage

Use this in your real API `Program.cs`:

```csharp
using API.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMesPlatformApiServices(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

## Required project references

API should reference:

- Application
- Infrastructure

Infrastructure should reference:

- Application
- Domain

Application should reference:

- Domain

## Required packages

API:

- Microsoft.AspNetCore.OpenApi or Swashbuckle.AspNetCore if you keep Swagger

Infrastructure:

- Microsoft.EntityFrameworkCore
- Microsoft.EntityFrameworkCore.SqlServer
- Microsoft.AspNetCore.Identity.EntityFrameworkCore

Application:

- Microsoft.Extensions.DependencyInjection.Abstractions
