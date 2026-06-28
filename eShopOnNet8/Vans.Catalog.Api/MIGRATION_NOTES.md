# Vans Catalog API — Migration Notes

Part of the VF Corporation executive demo showing that the same legacy
**.NET Framework → .NET 8** migration pattern fans out across multiple brand
stacks in parallel, headcount-neutral. This brand stack is **VANS**.

## Source

Legacy module: `eShopLegacyMVCSolution/src/eShopLegacyMVC`
(.NET Framework 4.7.2, ASP.NET MVC 5, `System.Web.Mvc`, Entity Framework 6,
Autofac, log4net).

## Target

New, self-contained, cross-platform project: `eShopOnNet8/Vans.Catalog.Api`
(.NET 8, ASP.NET Core Web API). Builds and tests run on Linux with the .NET 8
SDK via `dotnet build` / `dotnet test`.

## What was ported

| Legacy | .NET 8 port |
| --- | --- |
| `Models/CatalogItem.cs`, `CatalogBrand.cs`, `CatalogType.cs` | `Models/*.cs` (nullable-aware, no `System.Web` dependency) |
| `Models/Infrastructure/PreconfiguredData.cs` | `Models/Infrastructure/PreconfiguredData.cs` — **same 12 items, 5 brands, 4 types**, faithfully preserved (ids, names, prices, picture filenames) |
| `ViewModel/PaginatedItemsViewModel.cs` | `ViewModel/PaginatedItemsViewModel.cs` (identical pagination math) |
| `Services/ICatalogService.cs` | `Services/ICatalogService.cs` (dropped `IDisposable`; `Create` now returns the created item with its assigned id) |
| `Services/CatalogServiceMock.cs` | `Services/CatalogServiceMock.cs` — in-memory equivalent, same seed + `ComposeCatalogItems` brand/type hydration |
| (EF6 `CatalogDBContext` / `CatalogService`) | `Models/CatalogDbContext.cs` + `Services/CatalogService.cs` using **EF Core InMemory** with `HasData` seeding |
| `Controllers/CatalogController.cs` (MVC, returns Views) | `Controllers/CatalogController.cs` — ASP.NET Core `[ApiController]` returning JSON |
| log4net logging | `ILogger<T>` (built-in structured logging) |
| Autofac DI | built-in `Microsoft.Extensions.DependencyInjection` |

### REST endpoints

- `GET /api/catalog?pageSize=&pageIndex=` → `PaginatedItemsViewModel<CatalogItem>`
- `GET /api/catalog/{id}`
- `POST /api/catalog`
- `PUT /api/catalog/{id}`
- `DELETE /api/catalog/{id}`
- `GET /api/catalog/brands`
- `GET /api/catalog/types`
- `GET /health` → `{ status, service: "Vans Catalog API" }`
- Swagger UI at `/swagger`, titled **Vans Catalog API**.

### Data source toggle

Defaults to **EF Core InMemory** (`CatalogService`). Set `Catalog:UseMock=true`
in configuration to use the in-memory `CatalogServiceMock` instead — mirroring
the legacy app's "Mock Data Mode" toggle.

## Tests

`Vans.Catalog.Api.Tests` (xUnit) — 8 tests covering pagination, find-by-id,
create, and delete across both the mock and EF Core service implementations.

## What a full production migration would still need

- **Real relational EF Core provider** (e.g. SQL Server / Azure SQL) replacing
  the InMemory provider, plus EF Core migrations (the legacy app used the EF6
  HiLo id generator and SQL sequences).
- **Authentication / authorization** (none in the legacy reference app).
- **Image / picture storage**: the legacy `PicController` served images from
  disk; a cloud port would use blob storage and real `PictureUri` generation.
- Production concerns: validation hardening, error-handling middleware,
  health-check probes, telemetry/observability, CORS, rate limiting, CI/CD.

## Build constraint (legacy side)

The legacy `eShopLegacyMVC` project targets **.NET Framework 4.7.2** and depends
on `System.Web` / `System.Web.Mvc`. It can only be built with **Windows + MSBuild**
(and is containerized via Windows Containers). The new `Vans.Catalog.Api` is
deliberately self-contained with **no dependency on the legacy project**, so it
builds and tests cleanly on Linux/macOS/Windows with the cross-platform .NET 8 SDK.
