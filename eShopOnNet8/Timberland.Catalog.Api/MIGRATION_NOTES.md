# Timberland Catalog API — Migration Notes

Part of the VF Corporation executive demo proving the legacy **.NET Framework → .NET 8**
migration pattern fans out across multiple brand stacks in parallel, headcount-neutral.
**Brand stack: Timberland.**

This project is a new, self-contained, cross-platform **.NET 8 ASP.NET Core Web API** that
ports the Catalog module from the legacy ASP.NET MVC app at
`eShopLegacyMVCSolution/src/eShopLegacyMVC` (.NET Framework 4.7.2, `System.Web.Mvc`).

It builds and tests on Linux with the .NET 8 SDK — no Windows/IIS dependency.

## What was ported

| Legacy (.NET Framework 4.7.2) | .NET 8 port |
| --- | --- |
| `Models/CatalogItem.cs`, `CatalogBrand.cs`, `CatalogType.cs` | `Models/` — same fields, data annotations, and `DefaultPictureName` behavior. Navigation properties are nullable to satisfy nullable reference types. |
| `Models/Infrastructure/PreconfiguredData.cs` | `Models/Infrastructure/PreconfiguredData.cs` — **identical seed data**: 12 catalog items, 5 brands, 4 types. |
| `Services/ICatalogService.cs` | `Services/ICatalogService.cs` — same operations; dropped `IDisposable` (DI container owns lifetimes). `FindCatalogItem` returns a nullable item. |
| `Services/CatalogServiceMock.cs` | `Services/CatalogServiceMock.cs` — faithful in-memory equivalent, including `ComposeCatalogItems` brand/type hydration and `++maxId` create logic. |
| `Services/CatalogService.cs` (EF6 + SQL Server) | `Services/CatalogService.cs` — re-implemented on **EF Core**, wired to the **EF Core InMemory** provider (`Infrastructure/CatalogDbContext.cs`). Optional; off by default. |
| `ViewModel/PaginatedItemsViewModel.cs` | `ViewModel/PaginatedItemsViewModel.cs` — identical pagination shape (`ActualPage`, `ItemsPerPage`, `TotalItems`, `TotalPages`, `Data`). |
| `Controllers/CatalogController.cs` (MVC, server-rendered views) | `Controllers/CatalogController.cs` — rebuilt as an `[ApiController]` exposing a REST surface (see below). |

### REST endpoints

| Verb | Route | Description |
| --- | --- | --- |
| GET | `/api/catalog?pageSize=&pageIndex=` | Paginated items (`PaginatedItemsViewModel<CatalogItem>`) |
| GET | `/api/catalog/{id}` | Single item, 404 if missing |
| POST | `/api/catalog` | Create item |
| PUT | `/api/catalog/{id}` | Update item |
| DELETE | `/api/catalog/{id}` | Delete item, 204 / 404 |
| GET | `/api/catalog/brands` | All brands |
| GET | `/api/catalog/types` | All types |
| GET | `/api/health` | Branding/health: `{ "service": "Timberland Catalog API", ... }` |

- **Swagger / OpenAPI** is enabled (Swashbuckle) and branded **"Timberland Catalog API"** at `/swagger`.
- Data store is selectable via `Catalog:UseMock` (default `true` → `CatalogServiceMock`; `false` → EF Core InMemory).

## What a full production migration would still need

- **Real EF Core SQL provider** — replace the InMemory provider with SQL Server (`Microsoft.EntityFrameworkCore.SqlServer`) or PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`), plus migrations and the HiLo ID generator equivalent (the legacy app used `CatalogItemHiLoGenerator`).
- **Authentication / authorization** — the legacy app relied on `System.Web` auth; a real port needs ASP.NET Core auth (JWT/OIDC, e.g. Entra ID) and `[Authorize]` policies on mutating endpoints.
- **Image storage** — `PictureFileName`/`PictureUri` and the legacy `PicController` served images from disk; production should use blob storage (Azure Blob / S3) with a CDN.
- **Observability & config** — port `log4net`/Application Insights to `ILogger` + OpenTelemetry; move `Web.config`/ConfigBuilders to `appsettings`/Key Vault.
- **Validation & error contracts** — formalize `ProblemDetails`, input DTOs separate from entities, and concurrency handling.

## Build constraint (legacy side)

The legacy **.NET Framework 4.7.2** projects under `eShopLegacyMVCSolution/` depend on
`System.Web`, `System.Web.Mvc`, and MSBuild targets that require **Windows + MSBuild / Visual Studio**.
They **cannot** be built with `dotnet build` on Linux. This new `eShopOnNet8/Timberland.Catalog.Api`
project is intentionally decoupled and cross-platform so it builds and tests anywhere the .NET 8 SDK runs.

## Build & test

```bash
cd eShopOnNet8
dotnet build      # Timberland.Catalog sln — succeeds, 0 warnings
dotnet test       # Timberland.Catalog.Api.Tests — 11 tests pass
```
