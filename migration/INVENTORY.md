# Legacy inventory (eShopLegacyMVC + eShopLegacyWebForms)

## eShopLegacyMVCSolution
Solution `eShopLegacyMVC.sln`: `src/eShopLegacyMVC` (net472 MVC 5.2.7 + Web API 2.2, EF 6.2, Autofac 4.9 + Autofac.Mvc5/WebApi2, log4net 2.0.10, App Insights 2.9, System.Web.Optimization bundles), `eShopLegacy.Utilities` (net472 class lib: `Serializing` = BinaryFormatter serialise/deserialise), `eShopPorted` (an abandoned partial port: net461 + ASP.NET Core 2.2 + EF Core 2.2 + Autofac.Extensions.DI; Startup/Program, same controllers/views, an EF Core `Initial` migration; does not target .NET 8 and is not referenced by the new code).

### Routes / controllers / actions
| Route | Controller.Action | Behaviour |
|---|---|---|
| `GET /` , `GET /Catalog`, `GET /Catalog/Index?pageSize=10&pageIndex=0` | `CatalogController.Index(int pageSize=10,int pageIndex=0)` | paginated table (ordered by Id), pager "Showing {ItemsPerPage} of {TotalItems} products - Page {ActualPage+1} - {TotalPages}", Previous/Next links always rendered (hidden class when not navigable); links are `/?pageSize=X&pageIndex=Y` |
| `GET /Catalog/Details/{id}` | `Details(int? id)` | 400 if id missing, 404 if not found, else details view |
| `GET /Catalog/Create` | `Create()` | form with brand/type `SelectList`s, antiforgery token |
| `POST /Catalog/Create` | `Create([Bind(Include=...)] CatalogItem)` + `[ValidateAntiForgeryToken]` | valid -> service.Create + 302 to `/`; invalid -> re-render with validation messages |
| `GET /Catalog/Edit/{id}` | `Edit(int? id)` | 400/404/edit form (hidden Id, readonly PictureFileName) |
| `POST /Catalog/Edit/{id}` | `Edit([Bind] CatalogItem)` + antiforgery | valid -> Update + 302 `/`; invalid -> re-render |
| `GET /Catalog/Delete/{id}` | `Delete(int? id)` | 400/404/confirm view |
| `POST /Catalog/Delete/{id}` | `DeleteConfirmed(int id)` (ActionName Delete) + antiforgery | Remove + 302 `/` |
| `GET /items/{catalogItemId:int}/pic` (route name `GetPicRouteTemplate`) | `PicController.Index` | 400 if id<=0, 404 if item missing, else file bytes from `~/Pics/{PictureFileName}` with mime by extension |
| `GET /api/brands` | Web API `BrandsController.Get()` | JSON array `[{"Id":1,"Brand":"Azure"},...]` (PascalCase) |
| `GET /api/brands/{id}` | `Get(int id)` | 200 JSON object / 404 |
| `DELETE /api/brands/{id}` | `Delete(int id)` | 404 if missing, else 200 empty body (demo: does not delete) |
| `GET /api/files` | `FilesController.Get()` | BinaryFormatter (NRBF) payload of `List<FilesController+BrandDTO>` (no content-type set by the app) |
| `GET /api` | `Api.CatalogController2.Index` (`[Route("api")]`, returns `Json(new {Message="Hello World!"})`) | **404 in legacy**: MVC 5 only discovers controllers whose type name ends in `Controller`; `CatalogController2` never resolves. ASP.NET Core discovers any `Controller` subclass, so a naive port would start answering 200 - parity requires 404. |

`PictureUri` = `Url.RouteUrl("GetPicRouteTemplate", new{catalogItemId}, Request.Url.Scheme)` -> absolute URL (`http://host:port/items/1/pic`).

### Model / data
- `CatalogItem` (Id, Name [Required], Description, Price decimal [RegularExpression `^\d+(\.\d{0,2})*$`, Range(0,1000000), DataType.Currency], PictureFileName (default `dummy.png`), PictureUri (not mapped), CatalogTypeId/CatalogType, CatalogBrandId/CatalogBrand, AvailableStock/RestockThreshold/MaxStockThreshold [Range(0,10000000) with custom messages], OnReorder). Display names: Picture name, Type, Brand, Stock, Restock, Max stock.
- `CatalogBrand(Id, Brand)`, `CatalogType(Id, Type)`.
- EF6 `CatalogDBContext("name=CatalogDBContext")`: tables `Catalog` (Id not DB-generated, Name max 50 required, Price required, PictureFileName required, required FKs), `CatalogBrand` (Brand max 100), `CatalogType` (Type max 100).
- HiLo: SQL sequences `catalog_hilo`, `catalog_brand_hilo`, `catalog_type_hilo` (bigint, start 1, increment 10); `CatalogItemHiLoGenerator` singleton hands out ids in blocks of 10 from `catalog_hilo`.
- `CatalogDBInitializer` (CreateDatabaseIfNotExists): runs the sequence scripts, seeds from `Setup/*.csv` when `UseCustomizationData=true` (and unzips `Setup/CatalogItems.zip` into `Pics`), else from `PreconfiguredData` (5 brands, 4 types, 12 items, all AvailableStock 100).
- `CatalogServiceMock` (UseMockData=true): **singleton** list seeded from PreconfiguredData; Create = max(Id)+1; Update replaces list element; Remove; brand/type navs composed only on list (`GetCatalogItemsPaginated`), so `FindCatalogItem` on an edited/created item has null navs until the next list call.
- `CatalogService` (SQL): EF6 Include brand/type, OrderBy Id, Skip/Take; Update via `EntityState.Modified`.

### Config / DI / logging
- `Web.config` appSettings: `UseMockData` (false in repo), `UseCustomizationData` (false), ClientValidationEnabled/UnobtrusiveJavaScriptEnabled; connection string `CatalogDBContext` = LocalDB `Microsoft.eShopOnContainers.Services.CatalogDb`; `<globalization culture="en-US" uiCulture="en-US">`; InProc session (async SessionStateModule).
- Autofac `ApplicationModule(useMockData)`: mock -> `CatalogServiceMock` SingleInstance; else `CatalogService` InstancePerLifetimeScope; `CatalogDBContext`, `CatalogDBInitializer` per scope; `CatalogItemHiLoGenerator` SingleInstance. Controllers + ApiControllers registered by assembly.
- `Global.asax`: Session_Start stores MachineName + SessionStartTime (rendered in the layout footer `<small>{machine}, {M/d/yyyy h:mm:ss tt}</small>`); BeginRequest sets log4net activity/request properties; `HandleErrorAttribute` global filter.
- log4net `log4Net.xml`: RollingFileAppender `logFiles\myapp.log`, 10MB x5.
- Bundles: `~/bundles/jquery`, `jqueryval`, `modernizr`, `bootstrap`, `~/Content/css` (bootstrap, custom, base, site). `compilation debug=true` -> bundles render as individual `<link>/<script>` tags.

## eShopLegacyWebFormsSolution
`src/eShopLegacyWebForms` (net472 WebForms, FriendlyUrls, ScriptManager, Autofac.Web property injection, same Models/Services/PreconfiguredData as MVC; `UseMockData=true` in repo).
Routes (`RouteConfig.MapPageRoute`): `Default` -> Default.aspx; `Default/index/{index}/size/{size}` (ProductsByPageRoute); `Catalog/Create`; `Catalog/Edit/{id}`; `Catalog/Details/{id}`; `Catalog/Delete/{id}`. Also `/` (Default.aspx), About.aspx, Contact.aspx (template leftovers).
- Default.aspx: ListView table (Pics served as static `/Pics/{file}`, price rendered as raw decimal `19.50`, no currency symbol), pager links `/Default/index/{i}/size/{s}`.
- Create/Edit: server controls + RequiredFieldValidator/RangeValidator (client + server), postback with `__VIEWSTATE`/`__EVENTVALIDATION`; success -> `Response.Redirect("~")`.
- Details/Delete: Labels bound via `<%# %>`; Delete postback removes and redirects.
- Site.Master: ScriptManager, session info label.
- Differences vs MVC model: no [Required] on Name (validator in page instead), Price Range(0, 9999999999999999.99), all stock range messages say "Stock".

## Tests
No legacy test project exists in either solution (no *Test*.csproj under eShopLegacyMVCSolution / eShopLegacyWebFormsSolution). The recorded transcript suite under `migration/baseline/` is the baseline test suite.
