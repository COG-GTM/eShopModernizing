# SESSION 2 — WebForms Track Modernization Notes

## Scope

Session 2 of the 5-session parallel work-breakdown (`MODERNIZATION-WORK-BREAKDOWN.md`, PR #47) covers the WebForms track only:

- `eShopLegacyWebFormsSolution/src/eShopLegacyWebForms` — .NET Framework **v4.7.2** (verified in `.csproj`; Antlr 3.5.0.2, AspNet.ScriptManager.bootstrap/jQuery, Autofac 4.9.1 + Autofac.Web, EntityFramework 6.2.0 — verified in `packages.config`).
- `eShopModernizedWebFormsSolution/src/eShopModernizedWebForms` — .NET Framework **v4.7.2** (verified; Autofac 4.9.4, EntityFramework 6.3.0, ApplicationInsights 2.11.x, Azure KeyVault, ConfigurationBuilders 2.0.0-beta, Redis session state, Owin/OpenIdConnect — verified in `packages.config`).

No other projects were touched (MVC track, NTier, eShopCoreAPI, and eShopModernized belong to other sessions).

## Approach: Incremental Port to ASP.NET Core Razor Pages

ASP.NET WebForms has **no direct .NET 8 equivalent** — `.aspx` pages, server controls, ViewState, and `System.Web` do not exist on modern .NET. The chosen incremental strategy is a side-by-side port (strangler-fig style, matching the rest of this repo):

1. Keep both existing WebForms projects intact and buildable on .NET Framework 4.7.2 (they require MSBuild/Windows; they are not modified by this session).
2. Add a new **net8.0 ASP.NET Core Razor Pages** project, `eShopModernizedWebFormsSolution/src/eShopPortedWebForms`, that ports the core catalog flows with working code. Razor Pages was chosen over MVC because its page-per-URL + code-behind (`PageModel`) model is the closest conceptual match to WebForms `.aspx` + `.aspx.cs`, minimizing porting friction for the remaining pages.

## What Was Ported (working code)

| WebForms artifact | Razor Pages equivalent |
|---|---|
| `Default.aspx` (paginated catalog ListView) | `Pages/Index.cshtml` (+ pagination via `pageIndex` query param) |
| `Catalog/Create.aspx` | `Pages/Catalog/Create.cshtml` |
| `Catalog/Edit.aspx` | `Pages/Catalog/Edit.cshtml` |
| `Catalog/Details.aspx` | `Pages/Catalog/Details.cshtml` |
| `Catalog/Delete.aspx` | `Pages/Catalog/Delete.cshtml` |
| `Models/*` + EF6 `CatalogDBContext` | `Models/*` + EF **Core 8** `CatalogDbContext` (SQL Server provider) |
| `CatalogItemHiLoGenerator` (manual HiLo) | EF Core native `UseHiLo("catalog_hilo")` |
| `Services/ICatalogService` (+ real/mock impls) | Same split, fully **async** (`CatalogService` on EF Core, `CatalogServiceMock` in-memory) |
| Autofac + `Web.config` toggles | Built-in ASP.NET Core DI + `appsettings.json` (`UseMockData`, connection string) |
| `Pics/` static images | `wwwroot/Pics/` served by static-files middleware |

Data-annotation validation rules (price regex/range, stock ranges, display names) were carried over from the original `CatalogItem`.

## Runtime behavior

- `UseMockData=true` (default): runs anywhere with no database, seeded with the same `PreconfiguredData` as the legacy apps.
- `UseMockData=false`: uses EF Core SqlServer against `ConnectionStrings:CatalogDBContext`, with `EnsureCreated` + seeding at startup.

Verified locally: `dotnet build` clean (0 warnings/0 errors) and the app serves the catalog list, details, create form, and product images on Kestrel.

## Remaining migration work (future sessions/PRs)

- Image upload (`PicUploader.asmx` → minimal API endpoint or Razor Page handler) and Azure Blob image storage (`ImageAzureStorage`).
- Application Insights, Key Vault config, Redis-backed session, and OpenID Connect auth from the modernized WebForms app (map to `Microsoft.ApplicationInsights.AspNetCore`, `Azure.Extensions.AspNetCore.Configuration.Secrets`, ASP.NET Core distributed Redis cache, and `Microsoft.Identity.Web` respectively — same packages already used by the net8.0 projects in this repo).
- EF Core migrations instead of `EnsureCreated` for production.
- Containerize with a Linux Dockerfile (the ported app no longer needs Windows containers).

## ProjectGuid note

`eShopLegacyWebForms` and `eShopModernizedWebForms` share ProjectGuid `{4416714A-9BB8-480B-95B3-C2599598E3EC}` — harmless while they live in separate solutions; regenerate one if they ever coexist in a single solution. The new `eShopPortedWebForms` project is SDK-style and declares no ProjectGuid, so it introduces no new collision.
