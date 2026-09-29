# WebForms catalog → Razor Pages mapping

This document maps every catalog page and URL of the legacy WebForms app
(`eShopLegacyWebFormsSolution/src/eShopLegacyWebForms`, .NET Framework 4.7.2) to its ASP.NET Core 8
Razor Pages replacement (`eShopRazorPagesSolution/src/eShopRazorPages`).

The goal of the port is that **every URL that worked before keeps working**, including links that were
bookmarked or embedded elsewhere, and that the pages generate the same URLs for their own links.

## URL mapping

The legacy routes come from `App_Start/RouteConfig.cs`. On the Razor side they are declared once in
`Infrastructure/LegacyRoutes.cs` and attached to the pages by `Infrastructure/LegacyRouteConvention.cs`
(an `IPageRouteModelConvention`), so the table below and the code cannot drift apart silently —
`tests/eShopRazorPages.Tests/RouteTableTests.cs` checks the endpoint table against it.

| Legacy URL (as registered in `RouteConfig`) | Route name | WebForms page | Razor Page | ASP.NET Core template |
|---|---|---|---|---|
| `/` | – | `Default.aspx` (default document) | `Pages/Index.cshtml` | `""` (page default) |
| `/Default` | – | `Default.aspx` | `Pages/Index.cshtml` | `Default` (inbound only) |
| `/Default/index/{index}/size/{size}` | `ProductsByPageRoute` | `Default.aspx` | `Pages/Index.cshtml` | `Default/index/{index:int}/size/{size:int:min(1)}` |
| `/Catalog/Create` | `CreateProductRoute` | `Catalog/Create.aspx` | `Pages/Catalog/Create.cshtml` | `Catalog/Create` |
| `/Catalog/Edit/{id}` | `EditProductRoute` | `Catalog/Edit.aspx` | `Pages/Catalog/Edit.cshtml` | `Catalog/Edit/{id:int}` |
| `/Catalog/Details/{id}` | `ProductDetailsRoute` | `Catalog/Details.aspx` | `Pages/Catalog/Details.cshtml` | `Catalog/Details/{id:int}` |
| `/Catalog/Delete/{id}` | `DeleteProductRoute` | `Catalog/Delete.aspx` | `Pages/Catalog/Delete.cshtml` | `Catalog/Delete/{id:int}` |

Physical `.aspx` paths that worked without route data are kept as inbound-only aliases:

| Legacy physical URL | Razor Page |
|---|---|
| `/Default.aspx` | `Pages/Index.cshtml` |
| `/Catalog/Create.aspx` | `Pages/Catalog/Create.cshtml` |

Routing behaves like WebForms routing: paths are case-insensitive (`/catalog/edit/1` works) and a
trailing slash is accepted. Link generation uses the same route names as the legacy
`GetRouteUrl("…Route", new { id })` calls (`Url.RouteUrl` / `asp-route` in Razor), and the list page
generates `/` rather than `/Default` for "Back to list", exactly as before.

### Static asset URLs

All assets are served from `wwwroot` under the same paths the WebForms site used, so absolute links
to images and stylesheets keep working:

| Legacy path | Razor path |
|---|---|
| `/Pics/{n}.png` (product pictures, `CatalogItem.PictureUri`) | `wwwroot/Pics` |
| `/images/*` (brand, banner, footer) | `wwwroot/images` |
| `/Content/*.css` | `wwwroot/Content` |
| `/fonts/*` | `wwwroot/fonts` |
| `/Scripts/jquery-3.3.1*.js`, `bootstrap.bundle*.js` | `wwwroot/Scripts` |
| `/favicon.ico` | `wwwroot/favicon.ico` |

`wwwroot/lib/jquery-validation*` is new (client-side unobtrusive validation for Razor tag helpers).

## Page/component mapping

| WebForms | Razor Pages | Notes |
|---|---|---|
| `Site.Master` / `Site.Master.cs` | `Pages/Shared/_Layout.cshtml` | Same markup, branding and CSS. The footer session label is rendered from `Infrastructure/SessionInfo.cs`. |
| `Global.asax` `Session_Start` (`MachineName`, `SessionStartTime`) | `SessionInfo` middleware (`app.UseLegacySessionInfo()`) + ASP.NET Core session | Same "`{machine}, {start time}`" footer text. |
| `Default.aspx` `ListView` + `Default.aspx.cs` | `Pages/Index.cshtml` + `IndexModel` | Same columns, CSS classes, `No data was returned.` empty template and pager element ids (`PaginationPrevious`, `PaginationNext`). |
| `Catalog/Create.aspx` (+ `.cs`) | `Pages/Catalog/Create.cshtml` + `CreateModel` | Same fields, dropdowns, validators and "Uploading images not allowed for this version." message. |
| `Catalog/Edit.aspx` (+ `.cs`) | `Pages/Catalog/Edit.cshtml` + `EditModel` | Picture name remains read-only; picture, `PictureUri` and `OnReorder` are taken from the stored item, not the form. |
| `Catalog/Details.aspx` (+ `.cs`) | `Pages/Catalog/Details.cshtml` + `DetailsModel` | Shared field list in `Pages/Catalog/_ProductFields.cshtml`. |
| `Catalog/Delete.aspx` (+ `.cs`) | `Pages/Catalog/Delete.cshtml` + `DeleteModel` | GET shows confirmation, POST deletes. |
| `Page_Load` / `Button_Click` handlers | `OnGet` / `OnPost` handlers | `Response.Redirect("~")` → `LocalRedirect("~/")` (302 to `/`). |
| `RequiredFieldValidator`, `RangeValidator`, `RegularExpressionValidator` | Data annotations on `ViewModel/CatalogItemInput.cs` + `asp-validation-for` | Same error messages; enforced server-side and client-side. |
| `ViewState` / postback | Model binding of `CatalogItemInput` + antiforgery token | POSTs without a valid antiforgery token get 400. |
| `Services/ICatalogService`, `CatalogService` (EF6), `CatalogServiceMock` | `Services/*` (EF Core 8) | Same contract, synchronous like the original. |
| `Models/CatalogDBContext` (EF6) + `CatalogDBInitializer` | `Models/CatalogDBContext` (EF Core 8) + `Models/Infrastructure/CatalogDBInitializer` | Same tables (`Catalog`, `CatalogBrand`, `CatalogType`) and `catalog_hilo` sequence, so a database created by the legacy app can be shared. |
| Autofac module selecting mock vs. real service | `Program.cs` | Chosen with `UseMockData`. |
| `Web.config` `appSettings` / connection string | `appsettings.json` / environment variables | `UseMockData`, `UseCustomizationData`, `CatalogDBContext` — same names as `docker-compose.override.yml` uses. |

## Behaviour and assumptions

These are the places where the port intentionally differs from, or had to interpret, the legacy app.

1. **Unknown or malformed ids return 404.** Legacy `Edit/Details/Delete` pages threw a
   `NullReferenceException` (500) for a missing product and `Convert.ToInt32` failures for non-numeric
   ids. The Razor pages return `404 Not Found`. For the same reason `/Catalog/Edit.aspx`,
   `/Catalog/Details.aspx` and `/Catalog/Delete.aspx` (no id in route data) are not aliased and return 404.
2. **Pagination** keeps the legacy semantics: page index and size are read only from route data
   (`/Default?index=1` shows page 1, as before), default index 0 and size 10, negative index is
   clamped to 0, and the Previous/Next links are always rendered (hidden with
   `esh-pager-item--hidden`) with the same `/Default/index/{i}/size/{s}` hrefs — including the
   legacy `index/-1` href on the hidden Previous link. `size=0` is rejected with 404 by the
   `min(1)` constraint instead of the legacy divide-by-zero.
3. **Brand/type ids are validated** against the lookup tables on POST; the legacy page trusted
   the posted dropdown value.
4. **Picture upload stays unsupported**, as in the legacy WebForms app. New items get `dummy.png`.
5. **Numbers and dates are formatted with `en-US`**, matching the default culture of the legacy
   container image, so prices post back as `19.5` / `42.25` regardless of the host culture.
6. **Database initialization** uses `EnsureCreated()` plus seeding (preconfigured data, or the CSV
   and zip files in `Setup/` when `UseCustomizationData=true`), mirroring the legacy
   `CreateDatabaseIfNotExists` initializer. There are no EF Core migrations; an existing legacy
   database is used as-is and not re-seeded.
7. **Mock data is the default** (`UseMockData=true` in `appsettings.json`), as in the legacy
   `Web.config`. The mock catalog is an in-memory singleton, so changes last until the process restarts.
8. **Authentication, Application Insights and the legacy `log4net` activity logging** are not part
   of the catalog pages and were not ported; page loads are logged with `ILogger`
   (`Now loading... /Catalog/Edit.aspx?id=1`, same text as before).

## Running

```bash
cd eShopRazorPagesSolution
dotnet test                                # route, link and CRUD integration tests (mock data)
dotnet run --project src/eShopRazorPages   # http://localhost:5022, mock data

# Against SQL Server (same settings names as docker-compose.override.yml)
UseMockData=false \
CatalogDBContext='Server=localhost,1433;Database=Microsoft.eShopOnContainers.Services.CatalogDb;User Id=sa;Password=<password>;TrustServerCertificate=True' \
dotnet run --project src/eShopRazorPages

# Linux container
docker build -t eshop-razorpages src/eShopRazorPages
docker run -p 8080:8080 eshop-razorpages
```
