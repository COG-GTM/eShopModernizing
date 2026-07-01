# Session 1 — Legacy MVC track + shared library

## Scope

Modernized the three projects that make up the Legacy MVC track to **.NET 8**:

| Project | Before | After |
| --- | --- | --- |
| `eShopLegacy.Utilities` | .NET Framework v4.6.1 class library (legacy csproj) | SDK-style, multi-targeted **`netstandard2.0;net8.0`** |
| `eShopLegacyMVC` | ASP.NET MVC 5 on .NET Framework v4.7.2 (legacy csproj) | ASP.NET Core MVC on **`net8.0`** |
| `eShopPorted` | ASP.NET Core 2.2 on `net461` (SDK csproj) | ASP.NET Core on **`net8.0`** |

No projects outside this track were touched.

## Shared-library decision: **Extract as a multi-targeted shared library** (not duplicated)

`eShopLegacy.Utilities` is referenced via `ProjectReference` by **both** consumers
(`eShopLegacyMVC` and `eShopPorted`). We kept it as a single shared library and
converted it to an SDK-style project multi-targeting `netstandard2.0;net8.0`.

**Rationale**
- **Single source of truth.** Both consumers use the same `Serializing` type; duplicating
  it would create two copies that could drift and defeats the purpose of the shared lib.
- **Multi-targeting keeps the migration incremental.** The `netstandard2.0` TFM means the
  library still works from any remaining .NET Framework consumer during the phased
  (strangler-fig) migration, while `net8.0` gives the modern consumers a first-class build.
  This is why we multi-target rather than move straight to `net8.0`-only.
- **`ProjectReference` is preserved**, so there is no packaging/versioning overhead — the
  consumers compile against the library directly, exactly as before.
- Duplication was rejected: more maintenance, no upside for a tiny BCL-only utility.

## Behavior-preserving change to `Serializing`

The original `Serializing` used `BinaryFormatter`, which is **removed/blocked in .NET 8**
(`BinaryFormatter` serialization throws by default and the APIs are obsolete errors).
The public surface (`Stream SerializeBinary(object)` and `object DeserializeBinary(Stream)`)
is preserved so both call sites (`WebApi/FilesController` in MVC and `Api/FilesController`
in Ported) compile and behave the same way — they still receive a `Stream` of serialized
brand data and return it as `application/octet-stream`. The implementation now uses
`System.Text.Json` instead of `BinaryFormatter`. A generic `DeserializeBinary<T>(Stream)`
overload was added for typed round-tripping.

## Consumer modernization notes

**`eShopLegacyMVC`** (the largest change — full ASP.NET MVC 5 → ASP.NET Core):
- Legacy `.csproj` + `packages.config` → SDK-style `Microsoft.NET.Sdk.Web`, `net8.0`.
- `Global.asax`/`App_Start/*` bootstrapping replaced with `Program.cs` + `Startup.cs`
  (generic host, Autofac via `AutofacServiceProviderFactory`, `ConfigureContainer`).
- `Web.config` app settings/connection strings → `appsettings.json`.
- Static assets (`Content`, `Scripts`, `Images`, `Pics`, `fonts`) moved under `wwwroot/`.
- Controllers ported from `System.Web.Mvc` to `Microsoft.AspNetCore.Mvc`
  (`HttpNotFound()`→`NotFound()`, `HttpStatusCodeResult`→`BadRequest()`,
  `Server.MapPath`→`IWebHostEnvironment.WebRootPath`, `Request.Url.Scheme`→`Request.Scheme`,
  `[Bind(Include=…)]`→`[Bind(…)]`).
- `CatalogDBContext`/`CatalogDBInitializer` no longer depend on `System.Web.Hosting`
  or `ConfigurationManager`; paths/flags are injected from configuration and the host
  environment through the Autofac module.
- Razor views: removed `Scripts.Render`/`Styles.Render` bundling and `HttpContext.Current`
  session access (now `Context.Session.GetString(...)`); added `_ViewImports.cshtml`.
- Packages upgraded to .NET 8-compatible versions (Autofac 8, EntityFramework 6.5.1,
  log4net 3.x, Microsoft.ApplicationInsights.AspNetCore, Newtonsoft.Json 13).

**`eShopPorted`** (already SDK-style and on ASP.NET Core 2.2):
- `net461` → `net8.0`; packages bumped to 8.x (EF Core 8, Autofac 8) and
  `Microsoft.AspNetCore*` metapackages dropped in favor of the shared framework.
- `Program`/`Startup` updated to the generic host + endpoint routing model.
- `PicController` now resolves images via `IWebHostEnvironment.WebRootPath`.
- Removed `app.config`/`Views/Web.config` and stray `System.Web` usings.

## Verification

- `dotnet build eShopLegacyMVC.sln` — **succeeds** (all three projects).
- Both web apps start with mock data (`UseMockData=true`) and serve requests:
  - `eShopLegacyMVC`: `/` → 200, `/api/brands` → 200, `/api/files` → 200.
  - `eShopPorted`: `/` → 200, `/api/brands` → 200.
