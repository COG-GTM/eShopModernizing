# Legacy inventory

Sources (the only migration sources): `eShopLegacyMVCSolution/src/eShopLegacyMVC` (+ `eShopLegacyMVCSolution/eShopLegacy.Utilities`) and `eShopLegacyWebFormsSolution/src/eShopLegacyWebForms`.
Out of scope: `eShopLegacyMVCSolution/eShopPorted` (also referenced by `eShopLegacyMVC.sln`), `eShopModernized*`, `eShopLegacyNTier`, `eShopModernizedNTier`.

**Legacy test projects: none.** Neither solution contains a test project (`eShopLegacyMVC.sln` = eShopLegacyMVC + eShopPorted + eShopLegacy.Utilities; `eShopLegacyWebForms.sln` = eShopLegacyWebForms). The baseline is therefore the recorded HTTP transcripts in `modernization/baseline/`.

## 1. eShopLegacyMVC (ASP.NET MVC 5.2.7 + Web API 2, .NET Framework 4.7.2)

### 1.1 Routes / endpoints
Route registration order (`Global.asax.cs` Application_Start): Autofac container -> `WebApiConfig.Register` (attribute routes + `api/{controller}/{id}`) -> areas -> `HandleErrorAttribute` global filter -> `RouteConfig` (`MapMvcAttributeRoutes`, ignore `{resource}.axd/{*pathInfo}`, `Default` = `{controller}/{action}/{id}` defaults Catalog/Index) -> bundles -> DB initializer (SQL mode only).

| Method | Path | Handler | Behaviour |
|---|---|---|---|
| GET | `/`, `/Catalog`, `/Catalog/Index` | `CatalogController.Index(int pageSize = 10, int pageIndex = 0)` | Paginated table (`Index.cshtml` + partial `CatalogTable.cshtml`). Non-int query values -> model binding fails -> default used (MVC treats unparsable as default). Pager text `Showing {ItemsPerPage} of {TotalItems} products - Page {ActualPage+1} - {TotalPages}` (shows requested pageSize, not item count on page). Previous/Next always rendered, hidden with `esh-pager-item--hidden`. pageSize=0 -> divide-by-zero in `Math.Ceiling(count/pageSize)` -> 500. Negative pageIndex -> `Skip(negative)` (LINQ-to-objects: skip 0; EF: exception). |
| GET | `/Catalog/Details/{id}` | `Details(int? id)` | no id -> 400 (`HttpStatusCodeResult(BadRequest)`); unknown id -> 404 (`HttpNotFound`); non-int id -> binds null -> 400. |
| GET | `/Catalog/Create` | `Create()` | Form with brand/type `SelectList`s (ViewBag), antiforgery token. |
| POST | `/Catalog/Create` | `Create([Bind(Include="Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder")])` | `[ValidateAntiForgeryToken]`. Valid -> `CreateCatalogItem` -> 302 to `/` (RedirectToAction Index => `/`). Invalid -> 200 re-render with messages. Missing token -> `HttpAntiForgeryException` -> HandleErrorAttribute -> Error view (500). |
| GET | `/Catalog/Edit/{id}` | `Edit(int? id)` | 400/404 like Details. PictureFileName readonly input. |
| POST | `/Catalog/Edit/{id}` | `Edit([Bind(...)] CatalogItem)` | antiforgery; valid -> `UpdateCatalogItem` -> 302 `/`. Id comes from hidden field/route. |
| GET | `/Catalog/Delete/{id}` | `Delete(int? id)` | 400/404 like Details. |
| POST | `/Catalog/Delete/{id}` | `DeleteConfirmed(int id)` (`ActionName("Delete")`) | antiforgery; `FindCatalogItem` then `RemoveCatalogItem` (no null check: mock = no-op, EF = exception) -> 302 `/`. |
| GET | `/items/{catalogItemId:int}/pic` (route name `GetPicRouteTemplate`) | `PicController.Index` (attribute route) | id<=0 -> 400; unknown -> 404; else bytes of `~/Pics/{PictureFileName}` with mime by extension (png/gif/jpg/jpeg/bmp/tiff/wmf/jp2/svg else octet-stream). Non-int id -> route constraint fails -> falls to Default route -> 404. Negative ids do not match `{catalogItemId:int}`? (they do: `int` constraint accepts `-1`) -> 400. |
| GET | `/api/brands` | Web API `BrandsController.Get()` | JSON (Json.NET, content negotiation; XML if `Accept: application/xml`) array of `{Id, Brand}`. |
| GET | `/api/brands/{id}` | `Get(int id)` | 200 brand / 404. |
| DELETE | `/api/brands/{id}` | `Delete(int id)` | 404 if unknown, otherwise 200 empty body - **does not delete** ("demo only"). |
| GET | `/api/files` | Web API `FilesController.Get()` | `BinaryFormatter`-serialised `List<FilesController.BrandDTO>` (`[Serializable] {Id, Brand}`) via `eShopLegacy.Utilities.Serializing.SerializeBinary`; StreamContent, no content-type header. **Insecure-deserialisation sink; BinaryFormatter is removed/disabled in .NET 8 and must stay disabled.** |
| GET | `/api` | `Controllers/Api/CatalogController.cs` class `CatalogController2 : Controller` with `[Route("api")]` | **Never discovered**: MVC only discovers types whose name ends in `Controller`; `CatalogController2` does not. `/api` -> 404. Treat as dead code (do not port; document). |
| GET | `/bundles/modernizr`, `/bundles/jquery`, `/bundles/jqueryval`, `/bundles/bootstrap`, `/Content/css` | System.Web.Optimization bundles | debug=true in Web.config => individual `<script>/<link>` tags rendered instead of bundle URLs. Bundles: jquery=`jquery-{version}.js`, jqueryval=`jquery.validate*`, modernizr=`modernizr-*`, bootstrap=`bootstrap.js`+`respond.js`, css=`bootstrap.css`,`custom.css`,`base.css`,`site.css` (file is `Site.css` - case bug on Linux). |
| GET | static: `/Content/*`, `/Scripts/*`, `/Images/*` (layout links `/images/*`), `/fonts/*`, `/Pics/*`, `/favicon.ico` | static files | |

### 1.2 Views
`_Layout.cshtml`: title `{ViewBag.Title} - Catalog manager (MVC)`, header brand link `Url.Action("index","catalog")`, `<img src="~/images/brand.png">`, hero `Catalog manager (MVC)`, footer with `brand_dark.png`, `main_footer_text.png` and **session info** `{Session["MachineName"]}, {Session["SessionStartTime"]}` (non-deterministic). Views: Index, CatalogTable (partial), Details, Create (picture: "Uploading images not allowed for this version."), Edit, Delete, Shared/Error ("Error." / "An error occurred while processing your request."). Price rendered via `DisplayFor` with `[DataType(Currency)]` => en-US currency (`$19.50`) in MVC; Edit input shows `19.50`.

## 2. eShopLegacyWebForms (WebForms, .NET Framework 4.7.2)

### 2.1 Routes (`App_Start/RouteConfig.cs`, `MapPageRoute`; FriendlyUrls package referenced but `EnableFriendlyUrls` is **not** called)
| Route name | URL | Page |
|---|---|---|
| (unnamed) | `Default` | `~/Default.aspx` |
| `ProductsByPageRoute` | `Default/index/{index}/size/{size}` | `~/Default.aspx` |
| `CreateProductRoute` | `Catalog/Create` | `~/Catalog/Create.aspx` |
| `EditProductRoute` | `Catalog/Edit/{id}` | `~/Catalog/Edit.aspx` |
| `ProductDetailsRoute` | `Catalog/Details/{id}` | `~/Catalog/Details.aspx` |
| `DeleteProductRoute` | `Catalog/Delete/{id}` | `~/Catalog/Delete.aspx` |
Also reachable physically: `/` and `/Default.aspx` (default document), `/About.aspx`, `/Contact.aspx` (template pages), `/Catalog/*.aspx` without route data (id -> 0). `Site.Mobile.Master` + `ViewSwitcher.ascx` are unused template leftovers (switch route not registered).

### 2.2 Pages
- `Default.aspx`: `ListView` of items (thumbnail `src='/Pics/{PictureFileName}'` - static file, not the pic route), columns Name/Description/Brand/Type/Price (`<span class="esh-price">` raw decimal ToString, e.g. `19.5`, `8.50`, `12`)/Picture name/Stock/Restock/Max stock, links Edit/Details/Delete via route URLs. Paging via route values `index`/`size` (defaults 0/10, `Convert.ToInt32` -> non-numeric = 500). Pager same text as MVC, links `/Default/index/{i}/size/{s}`. Unmatched `</a>` in item template (markup defect).
- `Catalog/Details.aspx`, `Delete.aspx`: `Convert.ToInt32(RouteData["id"])`, `FindCatalogItem`, `DataBind`. Unknown id -> null -> **NullReferenceException -> 500** (no 404 handling). Delete postback `Delete_Click` -> `RemoveCatalogItem` -> `Response.Redirect("~")` (302 to `/`).
- `Catalog/Create.aspx`: TextBoxes Name/Description/Price(`0.00`)/Stock(`0`)/Restock(`0`)/Maxstock(`0`), DropDownLists Brand/Type (`SelectMethod=GetBrands/GetTypes`), validators: RequiredFieldValidator Name "The Name field is required."; RangeValidator Price Currency 0..1000000 "The Price must be a positive number with maximum two decimals between 0 and 1 million."; RangeValidator Integer 0..10000000 Stock/Restock/Max stock with "The field Stock|Restock|Max stock must be between 0 and 10 million.". `Create_Click`: if `ModelState.IsValid` (Page.ModelState - always valid here, validators are evaluated by `Page.IsValid`, which the handler never checks => **server-side validators are not enforced by the handler**; client-side validation blocks in a browser). Invalid numbers -> `decimal.Parse/int.Parse` FormatException -> 500. PictureFileName defaults to `dummy.png`. Redirect `~`.
- `Catalog/Edit.aspx`: same fields + readonly PictureFileName; populates on `!IsPostBack`; `Save_Click` builds a new CatalogItem from form + route id and calls `UpdateCatalogItem`, redirect `~`. Unknown id on GET -> NRE 500.
- `Site.Master`: `<form runat="server">` + `ScriptManager` (MsAjaxBundle, jquery, bootstrap, respond, WebForms*.js), `webopt:BundleReference ~/Content/css`, modernizr bundle, header `/images/brand.png`, hero `Catalog manager (Web Forms)`, title `{Page.Title} - Catalog manager (Web Forms)`, footer session label (MachineName, SessionStartTime).
- Postbacks carry `__VIEWSTATE`, `__VIEWSTATEGENERATOR`, `__EVENTVALIDATION`, `__EVENTTARGET`, `__EVENTARGUMENT`.

### 2.3 Bundles
`BundleConfig.cs`: `~/bundles/WebFormsJs`, `~/bundles/MsAjaxJs`, `~/bundles/modernizr`, `~/Content/css` (bootstrap, custom, base, site - file is lowercase `site.css` here) + ScriptResourceMapping `respond`. `Bundle.config` also declares `~/Content/css` (bootstrap + `Site.css`). `images/` folder is lowercase in WebForms.

## 3. Shared domain model (both apps; namespaces differ)
`CatalogItem` (table `Catalog`): `Id` (int, `DatabaseGeneratedOption.None`, HiLo), `Name` (required, max 50 in EF), `Description`, `Price` (decimal(18,2), required), `PictureFileName` (required in EF, default `dummy.png`), `PictureUri` (ignored), `CatalogTypeId`/`CatalogType` (required FK), `CatalogBrandId`/`CatalogBrand` (required FK), `AvailableStock`, `RestockThreshold`, `MaxStockThreshold`, `OnReorder`. `CatalogBrand` (table `CatalogBrand`: `Id`, `Brand` required max 100). `CatalogType` (table `CatalogType`: `Id`, `Type` required max 100).

DataAnnotations (MVC copy - authoritative for the .NET 8 MVC port):
- `Name`: `[Required]` -> "The Name field is required."
- `Price`: `[RegularExpression(@"^\d+(\.\d{0,2})*$", ErrorMessage="The field Price must be a positive number with maximum two decimals.")]`, `[Range(0, 1000000)]` -> "The field Price must be between 0 and 1000000.", `[DataType(DataType.Currency)]`. Non-numeric -> model binder "The value 'x' is not valid for Price."
- `PictureFileName` `[Display(Name="Picture name")]`; `CatalogTypeId`/`CatalogType` `[Display(Name="Type")]`; `CatalogBrandId`/`CatalogBrand` `[Display(Name="Brand")]`
- `AvailableStock` `[Range(0,10000000, ErrorMessage="The field Stock must be between 0 and 10 million.")]` `[Display(Name="Stock")]`
- `RestockThreshold` `[Range(0,10000000, ErrorMessage="The field Restock must be between 0 and 10 million.")]` `[Display(Name="Restock")]`
- `MaxStockThreshold` `[Range(0,10000000, ErrorMessage="The field Max stock must be between 0 and 10 million.")]` `[Display(Name="Max stock")]`
WebForms copy differs (defects): no `[Required]` on Name, Price `[Range(0, 9999999999999999.99)]`, Restock/MaxStock messages both say "Stock". The WebForms UI uses its own validator controls (2.2), not the annotations.

`PaginatedItemsViewModel<T>`: `ActualPage`, `ItemsPerPage`, `TotalItems` (long), `TotalPages = (int)Math.Ceiling((decimal)count / pageSize)`, `Data`.

`ICatalogService : IDisposable`: `FindCatalogItem(int)`, `GetCatalogBrands()`, `GetCatalogItemsPaginated(int pageSize, int pageIndex)`, `GetCatalogTypes()`, `CreateCatalogItem`, `UpdateCatalogItem`, `RemoveCatalogItem`.
- `CatalogServiceMock` (singleton): list seeded from `PreconfiguredData`; paging composes `CatalogBrand`/`CatalogType` navigation onto the **shared list items** (side effect); `FindCatalogItem` returns the list item (navigation null until a list page has been rendered since the item was added/replaced - **order-dependent quirk**: cold Details shows empty Brand/Type in MVC and NRE-500 in WebForms); create = `max(Id)+1`; update replaces the list element by Id (no-op if unknown); remove = `List.Remove` (null-safe no-op).
- `CatalogService` (EF6, per-lifetime-scope): `OrderBy(Id).Skip(pageSize*pageIndex).Take(pageSize)` with Include brand/type; create uses `CatalogItemHiLoGenerator`; update `EntityState.Modified`; remove `Remove` (throws on null). WebForms copy materialises brands/types with `ToList()`.

## 4. Data, HiLo, seeding
- Connection string `CatalogDBContext`: `Data Source=(localdb)\MSSQLLocalDB; Initial Catalog=Microsoft.eShopOnContainers.Services.CatalogDb; Integrated Security=True; MultipleActiveResultSets=True;` (LocalDB - Windows only).
- `CatalogDBInitializer : CreateDatabaseIfNotExists<CatalogDBContext>` (set only when `UseMockData=false`). `Seed`: run `Models\Infrastructure\dbo.catalog_hilo.Sequence.sql`, `dbo.catalog_brand_hilo.Sequence.sql`, `dbo.catalog_type_hilo.Sequence.sql` (each `USE [Microsoft.eShopOnContainers.Services.CatalogDb]` - **DB name hard-coded** - `CREATE SEQUENCE ... AS bigint START WITH 1 INCREMENT BY 10`), then types (ids = `NEXT VALUE FOR catalog_type_hilo` then +1 each -> 1..4), brands (`catalog_brand_hilo` -> 1..5), items (each id via `CatalogItemHiLoGenerator` -> 1..12), then `AddCatalogItemPictures` (customization only: deletes `Pics/*` and extracts `Setup/CatalogItems.zip`).
- `CatalogItemHiLoGenerator` (singleton): `NEXT VALUE FOR catalog_hilo` reserves a block of 10 (`HiLoIncrement = 10`); returns hi, then hi+1..hi+9; lock-protected. After seeding 12 items the next created id in the same process is 13; after an app restart the next is 21 (new block).
- `UseCustomizationData=true`: types/brands/items from `Setup/CatalogTypes.csv` (`CatalogType`), `CatalogBrands.csv` (`CatalogBrand`), `CatalogItems.csv` (required `catalogtypename,catalogbrandname,description,name,price,picturefilename`, optional `availablestock,restockthreshold,maxstockthreshold,onreorder`), falling back to PreconfiguredData when the file is missing; CSV parse errors throw.
- `PreconfiguredData`: types 1 Mug, 2 T-Shirt, 3 Sheet, 4 USB Memory Stick; brands 1 Azure, 2 .NET, 3 Visual Studio, 4 SQL Server, 5 Other; 12 items (Id, Name=Description, type, brand, price, `{id}.png`, AvailableStock 100, thresholds 0, OnReorder false): 1 .NET Bot Black Hoodie T2 B2 19.5; 2 .NET Black & White Mug T1 B2 8.50; 3 Prism White T-Shirt T2 B5 12; 4 .NET Foundation T-shirt T2 B2 12; 5 Roslyn Red Sheet T3 B5 8.5; 6 .NET Blue Hoodie T2 B2 12; 7 Roslyn Red T-Shirt T2 B5 12; 8 Kudu Purple Hoodie T2 B5 8.5; 9 Cup<T> White Mug T1 B5 12; 10 .NET Foundation Sheet T3 B2 12; 11 Cup<T> Sheet T3 B2 8.5; 12 Prism White TShirt T2 B5 12.

## 5. Configuration keys
| Key | MVC default | WebForms default | Meaning |
|---|---|---|---|
| `UseMockData` | `false` | `true` | true -> `CatalogServiceMock` singleton, no DB initializer |
| `UseCustomizationData` | `false` | `false` | seed from `Setup/*.csv` + `CatalogItems.zip` |
| connection string `CatalogDBContext` | LocalDB | LocalDB | EF6 |
| `webpages:Version`, `webpages:Enabled`, `ClientValidationEnabled`, `UnobtrusiveJavaScriptEnabled` | MVC only | - | Razor/validation |
| `<globalization culture="en-US" uiCulture="en-US">` | both | both | currency/number formatting |
| `<sessionState mode="InProc">` | both | both | session footer |
| `<compilation debug="true">` | both | both | bundles render unbundled |
| WebForms `httpRuntime requestValidationMode="2.0"`, `ValidateRequest="false"` on Create/Edit | | | allows `<` in input (e.g. `Cup<T>`) |

## 6. DI (Autofac)
MVC (mixed package styles: `PackageReference` Autofac 6.1.0, Autofac.Mvc5 4.0.2, Autofac.WebApi2 6.0.1, while `packages.config` still lists Autofac 4.9.1; `Modules/ApplicationModule`): controllers + API controllers registered; `CatalogServiceMock as ICatalogService` **SingleInstance** (mock) or `CatalogService as ICatalogService` **InstancePerLifetimeScope** (EF; = per request); `CatalogDBContext` InstancePerLifetimeScope; `CatalogDBInitializer` InstancePerLifetimeScope; `CatalogItemHiLoGenerator` **SingleInstance**. WebForms (`Autofac.Web`, `ContainerDisposalModule` + `PropertyInjectionModule` HTTP modules): same module, property injection of `ICatalogService CatalogService` into pages.
.NET 8 mapping: mock -> `AddSingleton<ICatalogService, CatalogServiceMock>`; EF -> `AddScoped<ICatalogService, CatalogService>` + `AddDbContext` (scoped); HiLo generator -> singleton.

## 7. Logging (log4net 2.0.10)
`[assembly: log4net.Config.XmlConfigurator(ConfigFile="log4net.xml")]` (file is `log4Net.xml` - case mismatch on Linux), root level ALL, RollingFileAppender `logFiles\myapp.log` 10MB x5, pattern `%date [%thread] %property{activity} %level %logger - %property{requestinfo}%newline%message%newline%newline`. `Application_BeginRequest` sets `LogicalThreadContext` props `activityid` (Trace.CorrelationManager.ActivityId) and `requestinfo` (RawUrl + UserAgent). Controllers/pages log `Info` "Now loading... /Catalog/..." / "Now processing...". .NET 8: `ILogger<T>` with a scope carrying activity id + request info; keep the same messages.

## 8. Session
`Session_Start` stores `MachineName` and `SessionStartTime` (DateTime.Now); rendered in footers (MVC `_Layout`, WebForms `Site.Master`). No other session use. .NET 8: `AddSession` + distributed memory cache, or equivalent per-session values; normaliser masks the text.

## 9. Other packages
ApplicationInsights 2.9.1 (HTTP modules, `ApplicationInsights.config`, no instrumentation key), TelemetryCorrelation, SessionStateModule async, Web.Optimization, Antlr/WebGrease, Newtonsoft.Json 12, jQuery 3.5.0 (scripts folder ships 3.3.1 files), jQuery.Validation 1.19.4, bootstrap 4.3.1, popper 1.14.3, Modernizr 2.8.3, Respond 1.4.2.

## 10. Known legacy quirks / defects (preserve or document; do not silently "fix")
Quirk IDs `Qn` below are referenced by `baseline/flows.json` (`quirk` field), `CONTRACT.md` and `TICKETS.md`.
1. **Q1** `CatalogController2` never discovered (`/api` -> 404).
2. **Q2** `/api/files` uses BinaryFormatter (security). Target: replace with a safe format (see T4) - an intentional, documented contract change.
3. **Q3** `DELETE /api/brands/{id}` returns 200 without deleting.
4. **Q4** Mock navigation properties populated only as a side effect of listing (cold Details/Edit/Delete render empty Brand/Type in MVC; WebForms NRE -> 500).
5. **Q5** `pageSize=0` -> divide-by-zero 500 (both apps); WebForms non-numeric route values -> 500.
6. **Q6** MVC `DeleteConfirmed` and EF `RemoveCatalogItem` do not null-check (EF throws for unknown id; mock no-op).
7. **Q7** WebForms Details/Edit/Delete unknown id -> 500 (no 404).
8. **Q8** WebForms Create/Edit handlers check `ModelState.IsValid` instead of `Page.IsValid`: server-side validator failures are not enforced; unparsable numbers -> 500; out-of-range numbers are saved.
9. **Q9** WebForms model annotations drift from MVC (Name not required, wrong Restock/MaxStock messages, huge Price range).
10. **Q10** Case mismatches (Linux): MVC layout `/images/*` vs folder `Images`; MVC css bundle `site.css` vs `Site.css`; WebForms `Bundle.config` `Site.css` vs `site.css`; `log4net.xml` vs `log4Net.xml`.
11. **Q11** Pager shows `ItemsPerPage` not actual item count; Previous/Next links always emitted (hidden class).
12. **Q12** Sequence scripts hard-code the database name; `ExecuteScript` paths use backslashes.
13. **Q13** WebForms list thumbnails point at `/Pics/{file}` (static) while MVC uses `/items/{id}/pic`.
14. **Q14** Footer session info is non-deterministic (masked by the normaliser).
15. **Q15** Default.aspx item template has a stray `</a>`.
16. **Q16** Web API content negotiation: `/api/brands` with `Accept: application/xml` returns XML (DataContract serializer). ASP.NET Core returns JSON unless XML formatters are added (flow `mvc-api-brands-xml`).
17. **Q17** When one property fails two DataAnnotations (e.g. `Price=-1`: regex and range), MVC 5 shows only one message and which one varies between app starts; such inputs are excluded from the baseline (see `baseline/BASELINE.md`).
18. **Q18** Mock vs SQL mode differ for some edge cases (negative paging, POST unknown id, cold navigation properties): see `baseline/BASELINE.md` section 5.

## 11. Legacy tests
No legacy test project exists in either solution (`eShopLegacyMVCSolution`, `eShopLegacyWebFormsSolution`): no `*Test*` projects, no test framework packages. The baseline transcripts in `baseline/` are the behavioural oracle.
