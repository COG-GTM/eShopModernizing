# Catalog Module Migration — .NET Framework 4.7.2 → .NET 8

**Beat 3 (MODERNIZE) of the VF Corporation demo.** This is a real, compiling, tested
vertical slice that migrates the **Catalog** bounded context from the legacy ASP.NET
MVC 5 app (`eShopLegacyMVCSolution/src/eShopLegacyMVC`, .NET Framework 4.7.2) to a
cross-platform **.NET 8** ASP.NET Core Web API (`eShopOnNet8/Catalog.Api`).

## Why the Catalog module
It is the most representative bounded domain in the app: models, an in-memory seeder,
a service interface with a mock + an EF implementation, pagination, and full CRUD —
small enough to migrate cleanly in one slice, complete enough to prove the pattern.

## What changed (legacy → .NET 8)

| Concern | Legacy (.NET Framework 4.7.2) | Modernized (.NET 8) |
|---|---|---|
| Project system | `.csproj` + `packages.config`, MSBuild/Windows-only | SDK-style `.csproj`, builds on Linux/macOS/Windows |
| Web framework | ASP.NET MVC 5 (`System.Web.Mvc`), server-rendered Views | ASP.NET Core `[ApiController]`, REST/JSON + Swagger |
| DI container | Autofac + `System.Web` integration | Built-in `Microsoft.Extensions.DependencyInjection` |
| Data access | Entity Framework 6 (`System.Data.Entity`) | Entity Framework Core 8 |
| Logging | log4net | `ILogger<T>` (Microsoft.Extensions.Logging) |
| Config | `Web.config` (`ConfigurationManager`) | `appsettings.json` / `IConfiguration` |
| Hosting | IIS / Windows Containers | Kestrel, cross-platform / Linux containers |

## What was ported (done)
- **Models**: `CatalogItem`, `CatalogBrand`, `CatalogType` (data annotations preserved).
- **Seed data**: `PreconfiguredData` — the same 12 catalog items, 5 brands, 4 types.
- **Service layer**: `ICatalogService`, in-memory `CatalogServiceMock`, and an EF Core
  `CatalogService` (`CatalogDbContext`).
- **Pagination**: `PaginatedItemsViewModel<T>`.
- **API**: `CatalogController` with `GET /api/catalog` (paginated), `GET /api/catalog/{id}`,
  `POST`, `PUT /api/catalog/{id}`, `DELETE /api/catalog/{id}`, `GET /api/catalog/brands`,
  `GET /api/catalog/types`, plus `/health` and Swagger UI.
- **Tests**: 14 passing xUnit tests — `CatalogServiceMock` unit tests, EF Core service
  tests (InMemory), and `WebApplicationFactory` integration tests over the real HTTP pipeline.

## Verification (on this Linux VM)
```
dotnet build eShopOnNet8.sln   # 0 errors, 0 warnings
dotnet test  eShopOnNet8.sln   # Passed! 14/14
```

## Done vs. remaining (what a full session would finish)
**Done:** project conversion, models, services (mock + EF Core), controller, pagination,
seed data, build + 14 tests green on Linux.

**Remaining for a production cutover (out of scope for this slice):**
- Swap EF Core InMemory → `UseSqlServer` against the real Azure SQL catalog DB and add EF Core migrations.
- Port the Razor Views to a frontend (Razor Pages / Blazor / React SPA) if a server-rendered UI is required.
- Port `PicController` image upload + Azure Blob Storage integration.
- Port authentication/authorization (the legacy `[Authorize]` actions) to ASP.NET Core Identity / OIDC.
- Wire the Catalog API into the strangler-fig routing so legacy and modern run side by side during cutover.

## Build constraint (documented, not a blocker)
The **legacy** .NET Framework 4.7.2 projects require **Windows + MSBuild / full .NET
Framework** and do not build on this Linux VM. The migration therefore targets the
**cross-platform .NET 8** side, which builds and tests cleanly on Linux (and in CI).
This mirrors a real modernization: the legacy app stays on Windows while new .NET 8
services are stood up cross-platform.
