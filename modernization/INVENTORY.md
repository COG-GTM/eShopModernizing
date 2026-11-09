# Legacy eShop storefront inventory

Scope: `eShopLegacyMVCSolution/src/eShopLegacyMVC` (ASP.NET MVC 5.2.7 + Web API 2, EF6 6.2, Autofac 4.9, log4net 2.0.10, .NET Framework 4.7.2)
and the catalog pages of `eShopLegacyWebFormsSolution/src/eShopLegacyWebForms` (Web Forms, EF6, Autofac.Web, log4net).
Out of scope: `eShopLegacyMVCSolution/eShopPorted` (an unrelated earlier ASP.NET Core port), WebForms `About.aspx` / `Contact.aspx` /
`Site.Mobile.Master` / `ViewSwitcher.ascx` (not catalog pages; listed for completeness), N-Tier/WCF/WinForms, `eShopModernized*`.

Transcript ids (`mvc/NNN-*`, `webforms/NNN-*`) refer to files in `modernization/baseline/transcripts/`.

## 1. Routes and endpoints

### MVC app (legacy :5100)

| Method + path | Handler | Behaviour (observed on legacy, mock mode) |
|---|---|---|
| `GET /`, `GET /Catalog`, `GET /Catalog/Index?pageSize=&pageIndex=` | `CatalogController.Index(int pageSize = 10, int pageIndex = 0)` | Paginated table ordered by `Id`; pager text + Previous/Next. `pageSize=0` → 500 (divide by zero). Non-numeric params → defaults. Out-of-range page → empty table. |
| `GET /Catalog/Details/{id}` | `Details(int? id)` | 200; missing id → 404; no id / non-numeric → 400. |
| `GET /Catalog/Create` | `Create()` | Form with Brand/Type `SelectList`s, antiforgery token. |
| `POST /Catalog/Create` | `Create([Bind(Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold)])` + `[ValidateAntiForgeryToken]` | Valid → 302 `/` ("Object moved" body). Invalid → 200 form with messages. Missing token → 500 (`HttpAntiForgeryException`). |
| `GET /Catalog/Edit/{id}` | `Edit(int? id)` | 200; missing → 404; no id → 400. |
| `POST /Catalog/Edit/{id}` | `Edit([Bind(...)] CatalogItem)` + antiforgery | Valid → 302 `/`; invalid → 200. |
| `GET /Catalog/Delete/{id}` | `Delete(int? id)` | 200; missing → 404; no id → 400. |
| `POST /Catalog/Delete/{id}` | `[ActionName("Delete")] DeleteConfirmed(int id)` + antiforgery | 302 `/`. |
| `GET /items/{catalogItemId:int}/pic` | `PicController.Index` (attribute route `GetPicRouteTemplate`) | `image/png` from `Pics/<PictureFileName>`; unknown id → 404; id ≤ 0 → 400. New items use `dummy.png`. |
| `GET /api/brands` | `WebApi.BrandsController.Get()` | JSON `[{"Id":1,"Brand":"Azure"},...]`; `Accept: application/xml` → XML (DataContract). |
| `GET /api/brands/{id}` | `Get(int id)` | 200 / 404. |
| `DELETE /api/brands/{id}` | `Delete(int id)` | 200 if brand exists, 404 otherwise. **Does not delete** (demo endpoint). |
| `POST/PUT /api/brands[/id]` | — | 405 JSON `{"Message":"The requested resource does not support http method ..."}`. |
| `GET /api/files` | `WebApi.FilesController.Get()` | 200 with `Content-Type: text/html` (no explicit media type is set) and a body of the brand list serialized with `BinaryFormatter` (MS-NRBF, starts `00 01 00 00 00 FF FF FF FF`) via `eShopLegacy.Utilities.Serializing`. |
| `GET /api/types`, `GET /api/catalog`, `GET /api` | — | 404. There is **no** types API in legacy (types only appear in the Create/Edit dropdowns); `Controllers/Api/CatalogController` is an MVC controller under `/Api/Catalog` not wired to `/api/*`. |
| Bundles | `BundleConfig` | `/bundles/jquery`, `/bundles/jqueryval`, `/bundles/modernizr`, `/bundles/bootstrap`, `/Content/css`. |
| Static | IIS static | `/Content/*`, `/Scripts/*`, `/fonts/*`, `/images/*`, `/Pics/*`, `/favicon.ico`. |
| Unknown path/action | — | 404 (`Views/Shared/Error.cshtml` is only used by `HandleErrorAttribute` when customErrors on; off by default). |

Route config: `{controller}/{action}/{id}` default `Catalog/Index`; `MapMvcAttributeRoutes()`; Web API `api/{controller}/{id}` + attribute routes.

### WebForms catalog (legacy :5101)

| Path | Page | Behaviour (observed) |
|---|---|---|
| `/`, `/Default`, `/Default.aspx` | `Default.aspx` (`ListView` + pager) | First 10 items. |
| `/Default/index/{index}/size/{size}` (`ProductsByPageRoute`) | `Default.aspx` | Paging; non-numeric → 500. |
| `/Catalog/Create` (`CreateProductRoute`) | `Catalog/Create.aspx` | Postback form; valid → 302 `/`; invalid → 200 with validator text. |
| `/Catalog/Details/{id}` | `Catalog/Details.aspx` | 200; missing/non-numeric id → 500 (NullReference). |
| `/Catalog/Edit/{id}` | `Catalog/Edit.aspx` | 200; postback valid → 302 `/`; missing id → 500. |
| `/Catalog/Delete/{id}` | `Catalog/Delete.aspx` | 200; postback → 302 `/`; missing id → 500. |
| `/Content/css` | `Bundle.config` style bundle | 404 on the Mono runtime (bundle not served; direct files are). |
| Static | `/Content/*.css`, `/Scripts/**`, `/images/*`, `/Pics/*`, `/favicon.ico` | 200. |

FriendlyUrls (`RouteConfig.EnableFriendlyUrls`) also maps extension-less `.aspx` paths.

## 2. Views and pages

MVC: `Views/Shared/_Layout.cshtml` (header "Catalog manager (MVC)", footer with machine name + session start time from `Session`),
`Views/Shared/Error.cshtml`, `Views/Catalog/{Index,CatalogTable,Create,Edit,Details,Delete}.cshtml`, `Views/_ViewStart.cshtml`.
`Index` renders `CatalogTable` partial and pager (`Showing {ItemsPerPage} of {TotalItems} products - Page {ActualPage+1} - {TotalPages}`).
Prices formatted with `DataType.Currency` (`$19.50`, en-US culture from `<globalization culture="en-US">`).

WebForms: `Site.Master` (header "Catalog manager (Web Forms)", footer machine name + session start), `Default.aspx`,
`Catalog/{Create,Edit,Details,Delete}.aspx`. Prices rendered raw (`19.5`, `8.50`, `12`) — differs from MVC.

## 3. Models and validation

`CatalogItem`: `Id`, `Name` (`[Required]`), `Description`, `Price` (decimal(18,2);
`[RegularExpression(@"^\d+(\.\d{0,2})*$", "The field Price must be a positive number with maximum two decimals.")]`, `[Range(0, 1000000)]`, `[DataType(Currency)]`),
`PictureFileName` (default `dummy.png`), `PictureUri` (not mapped in EF; set by service), `CatalogTypeId`/`CatalogType`, `CatalogBrandId`/`CatalogBrand`,
`AvailableStock`, `RestockThreshold`, `MaxStockThreshold` (each `[Range(0, 10000000, "The field X must be between 0 and 10 million.")]`), `OnReorder`.
`CatalogBrand { Id, Brand }`, `CatalogType { Id, Type }`. `PaginatedItemsViewModel<T> { ActualPage, ItemsPerPage, TotalItems, TotalPages, Data }`.

MVC messages observed: `The Name field is required.`, `The Price field is required.`, `The value 'abc' is not valid for Price.`,
`The field Price must be between 0 and 1000000.`, regex message above, stock range messages. `-5` fails both regex and range; which message
Mono reports is non-deterministic (excluded from transcripts, see CONTRACT.md).

WebForms validators (Create/Edit): `RequiredFieldValidator` Name ("The Name field is required."); `RangeValidator` Price Type=Currency 0..1000000
("The Price must be a positive number with maximum two decimals between 0 and 1 million."); Stock/Restock/Maxstock Type=Integer 0..10000000.
Server-side `Page.IsValid` gate (source uses `ModelState.IsValid`; harness patch, see legacy-harness/README.md).

## 4. Configuration keys

| Key | MVC source | WebForms source | Meaning |
|---|---|---|---|
| `UseMockData` | `false` | `true` | `true` → `CatalogServiceMock` (in-memory singleton seeded from `PreconfiguredData`); `false` → `CatalogService` on EF6/SQL. |
| `UseCustomizationData` | `false` | `false` | SQL seed reads `Setup/Catalog{Types,Brands,Items}.csv` and unzips `Setup/CatalogItems.zip` into `Pics/` instead of `PreconfiguredData`. |
| `ConnectionStrings:CatalogDBContext` | LocalDB | LocalDB | SQL Server connection. |
| `ClientValidationEnabled`, `UnobtrusiveJavaScriptEnabled` | true | — | MVC client validation. |
| `webpages:Version/Enabled` | 3.0.0.0 / false | — | Razor. |
| `<globalization culture="en-US">` | yes | yes | Currency/number formatting. |
| `<sessionState mode="InProc">` | yes | yes | Footer session info. |
| ApplicationInsights modules | yes | yes | Telemetry (drop or replace with OpenTelemetry). |

## 5. DI registrations (Autofac `Modules/ApplicationModule`, both apps)

- `UseMockData=true`: `CatalogServiceMock` as `ICatalogService`, `SingleInstance`.
- else: `CatalogService` as `ICatalogService`, `InstancePerLifetimeScope` (per request).
- `CatalogDBContext` per lifetime scope; `CatalogDBInitializer` per lifetime scope; `CatalogItemHiLoGenerator` singleton.
- MVC: `RegisterControllers`, `RegisterApiControllers`, MVC + Web API dependency resolvers. WebForms: `ContainerProvider` + `PropertyInjectionModule` (property injection into pages).
- .NET 8: `AddSingleton<ICatalogService, CatalogServiceMock>()` vs `AddScoped<ICatalogService, CatalogService>()`, `AddDbContext<CatalogDbContext>()`, `AddSingleton<CatalogItemHiLoGenerator>()`.

## 6. EF6 data layer

- `CatalogDBContext : DbContext` (`DbSet<CatalogItem> CatalogItems`, `CatalogBrands`, `CatalogTypes`), tables `Catalog`, `CatalogBrand`, `CatalogType`;
  keys `DatabaseGeneratedOption.None`; `Name` required max 50; `Price` decimal(18,2) required; `PictureUri` ignored; FKs required.
- Initializer `CatalogDBInitializer : CreateDatabaseIfNotExists<CatalogDBContext>` (only when `UseMockData=false`): runs
  `Models/Infrastructure/dbo.catalog_{hilo,brand_hilo,type_hilo}.Sequence.sql` (`START WITH 1 INCREMENT BY 10`), then seeds types/brands
  (`NEXT VALUE FOR catalog_type_hilo / catalog_brand_hilo`, consecutive ids) and items via `CatalogItemHiLoGenerator` (HiLo block of 10 from `catalog_hilo`).
- `CatalogService`: paginated query `Count()` + `OrderBy(Id).Skip(size*index).Take(size)` with `Include(Brand, Type)`; `FindCatalogItem` with includes;
  `CreateCatalogItem` assigns HiLo id; `UpdateCatalogItem` sets `EntityState.Modified`; `RemoveCatalogItem`.
- `CatalogServiceMock`: list in memory; create = `max(Id)+1`; brand/type navigation resolved from preconfigured lists.
- Seed data (`PreconfiguredData`): 5 brands (Azure, .NET, Visual Studio, SQL Server, Other), 4 types (Mug, T-Shirt, Sheet, USB Memory Stick), 12 items with pics `1.png..12.png`.
  Observed SQL mode seeds the same 12/5 rows and the three sequences (see `baseline/sql-mode/`).

## 7. log4net

`log4Net.xml` RollingFileAppender `logFiles\myapp.log` (10 MB × 5, level ALL), pattern with `%property{activity}` and `%property{requestinfo}` set in
`Application_BeginRequest` (`ActivityIdHelper`, `WebRequestInfo`). Call sites: MVC `CatalogController` (Info per action, Debug on dispose), `PicController`,
`Global.asax` (Debug BeginRequest); WebForms `Default`, `Catalog/*`, `About`, `Contact`, `Global`. → `ILogger<T>` with the same message templates,
request scope (activity id / request path) via logging scopes; console provider in containers.

## 8. Bundling and static assets

MVC `BundleConfig`: `~/bundles/jquery` (jquery-{version}.js), `~/bundles/jqueryval` (jquery.validate*), `~/bundles/modernizr` (modernizr-*),
`~/bundles/bootstrap` (bootstrap.js, respond.js), `~/Content/css` (bootstrap.css, site.css, + others). `BundleTable.EnableOptimizations` off in debug.
WebForms `Bundle.config` `~/Content/css` (bootstrap.css, Site.css), `BundleConfig` ScriptManager mappings (`WebFormsJs`, `MsAjaxJs`, `respond`).
Case-sensitivity: `images` vs `Images`, `Content/site.css` vs `Site.css`.
→ .NET 8: static files under `wwwroot` (both casings kept where referenced) and fixed bundle endpoints serving concatenated files (or the
exact same URLs mapped to static files); no WebOptimization.

## 9. NuGet packages → .NET 8

| Legacy | .NET 8 replacement |
|---|---|
| Microsoft.AspNet.Mvc 5.2.7, Razor 3.2.7, WebPages 3.2.7 | ASP.NET Core MVC (shared framework) |
| Microsoft.AspNet.WebApi 5.2.x (+Client, autofac.webapi2) | ASP.NET Core controllers / minimal APIs; `Microsoft.AspNetCore.Mvc.Formatters.Xml` for XML negotiation |
| Web Forms (System.Web), FriendlyUrls 1.0.2, ScriptManager.* | Razor Pages |
| EntityFramework 6.2.0 | Microsoft.EntityFrameworkCore.SqlServer 8.0.x (+ `UseHiLo`/sequences) |
| Autofac 4.9.1, Autofac.Mvc5, Autofac.Web | Microsoft.Extensions.DependencyInjection |
| log4net 2.0.10 | Microsoft.Extensions.Logging (`ILogger`) |
| Microsoft.AspNet.Web.Optimization 1.1.3, WebGrease, Antlr | static files (`wwwroot`) |
| Microsoft.ApplicationInsights.* 2.9.1, TelemetryCorrelation | dropped (optional OpenTelemetry / AI for ASP.NET Core) |
| Newtonsoft.Json 12.0.1 | System.Text.Json (configure PascalCase: `PropertyNamingPolicy = null`) |
| SessionStateModule 1.1.0 | `AddSession` + `IDistributedCache` (memory) |
| CodeDom.Providers.DotNetCompilerPlatform, Net.Compilers | not needed |
| jQuery 3.5.0, jQuery.Validation 1.19.4, Unobtrusive.Validation 3.2.11, bootstrap 4.3.1, popper.js, Modernizr 2.8.3, Respond 1.4.2 | same files copied to `wwwroot` |
| System.IO.Compression(.ZipFile) 4.3.0 | in-box |
| BinaryFormatter (`eShopLegacy.Utilities.Serializing`) | BinaryFormatter is disabled by default in .NET 8; reproduce MS-NRBF bytes with a hand-written writer or document the difference (decision recorded in CONTRACT.md) |

## 10. Migration risks

1. `/api/files` MS-NRBF payload — BinaryFormatter obsolete/disabled in .NET 8 (SYSLIB0011).
2. Web API 2 content negotiation (XML via DataContractSerializer namespaces) and 405 message shape.
3. MVC antiforgery failure is 500 on legacy vs 400 on Core; "Object moved" redirect bodies.
4. WebForms has no Razor equivalent for ViewState/postbacks; routes with 500-on-missing-id and raw price formatting must be reproduced deliberately.
5. Validation message parity (DataAnnotations vs RangeValidator text; Core model-binding messages differ: `The value 'abc' is not valid for Price.`).
6. EF6 → EF Core: HiLo sequences, `DatabaseGeneratedOption.None`, no validate-on-save, initializer semantics (`CreateDatabaseIfNotExists` + seed); shared DB init race between apps.
7. Case-sensitive static paths on Linux; bundles at fixed URLs.
8. Session-derived footer (machine name, session start time) — volatile, normalised.
9. Mock singleton state semantics (create = max+1, delete removes, brand DELETE no-op).
10. Legacy only runs on Mono with harness patches (documented in `legacy-harness/README.md`) — baseline reflects Mono, not IIS, where they differ.
