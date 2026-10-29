# MESPlatForm Final Full Application

This package contains the final combined backend application:

- API project with static endpoint controllers.
- Application layer with service interfaces and CRUD services.
- Domain layer with CustomerOnly entity model.
- Infrastructure layer with EF Core DbContext, Fluent configurations, Repository, UnitOfWork, Identity, and DI.
- Shared project placeholder.
- SQL schema creation script.

Applied constraints:

- CustomerOnly version: no Tenant table.
- No Signage tables/controllers.
- No SignalR.
- No Background Jobs.
- No PATCH endpoints; update endpoints use PUT.
- int Id, DateTime, SQL Server, ASP.NET Core Identity.

Connection string key in appsettings.json:

```json
"MCPAConn": "Server=192.168.1.25;Database=MESPlatForm;Integrated Security=true;User Id=sa; Password=123@GMEA.net; Trusted_Connection=False; MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Start from:

```bash
dotnet restore
dotnet build
```

Then set API as startup project in Visual Studio.
