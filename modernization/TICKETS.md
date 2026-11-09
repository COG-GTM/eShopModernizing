# Tickets — .NET 8 storefront modernization

Merge order = wave. Every ticket targets the integration branch `devin/net8-storefront-modernization`, only edits its `owned_paths`,
and must keep `dotnet build net8/EShopStorefront.sln` green on its own (stubs in other tickets' files keep the scaffold compiling).

## Read-only for all tickets (frozen scaffold + shared contract)

- `net8/global.json`, `net8/Directory.Build.props`, `net8/EShopStorefront.sln`
- `net8/src/EShop.Storefront.Mvc/Program.cs`, `net8/src/EShop.Storefront.WebForms/Program.cs` (FROZEN: only call ticket extension methods)
- `net8/src/*/EShop.Storefront.*.csproj`, `net8/src/*/appsettings*.json`, `net8/src/*/Properties/**`, `Views/_ViewImports.cshtml`, `Views/_ViewStart.cshtml`, `Pages/_ViewImports.cshtml`, `Pages/_ViewStart.cshtml`
- `net8/src/EShop.Catalog/Contracts/**` (models + validation messages, `ICatalogService`, `CatalogOptions`)
- `modernization/INVENTORY.md`, `modernization/CONTRACT.md`, `modernization/TICKETS.md`, `modernization/tickets.json`, `modernization/baseline/**`, `modernization/tools/**`, `modernization/legacy-harness/**`

Need a change in a frozen file? Raise it on the umbrella PR instead of editing it in a ticket.

| Id | Wave | Branch | Title |
|---|---|---|---|
| T1 | 1 | `devin/net8-sf-t1-catalog-data` | Catalog domain and data layer (EF Core 8, mock service, HiLo, seed, UseMockData/UseCustomizationData) |
| T2 | 2 | `devin/net8-sf-t2-mvc-catalog` | MVC catalog UI: list/paging, details, create, edit, delete with legacy validation |
| T3 | 2 | `devin/net8-sf-t3-legacy-api` | Legacy Web API and pictures: /api/brands (JSON+XML), /api/files (MS-NRBF), /items/{id}/pic |
| T4 | 2 | `devin/net8-sf-t4-razor-catalog` | WebForms catalog as Razor Pages: Default list/paging + Catalog Create/Details/Edit/Delete |
| T5 | 2 | `devin/net8-sf-t5-hosting-static` | Web hosting shell for both apps: layouts, static content and bundles, session footer, error handling, ILogger |
| T6 | 3 | `devin/net8-sf-t6-tests-parity` | Automated tests and parity runner for the .NET 8 apps |
| T7 | 3 | `devin/net8-sf-t7-docker` | Linux containers: Dockerfiles and compose for mock and SQL modes |

## T1 — Catalog domain and data layer (EF Core 8, mock service, HiLo, seed, UseMockData/UseCustomizationData)

- Branch: `devin/net8-sf-t1-catalog-data`
- Wave: 1
- Owned paths:
  - `net8/src/EShop.Catalog/Data/**`
  - `net8/src/EShop.Catalog/Services/**`
  - `net8/src/EShop.Catalog/Setup/**`
  - `net8/src/EShop.Catalog/CatalogDataExtensions.cs`
  - `net8/src/EShop.Catalog/EShop.Catalog.csproj`

**Scope.** Implement AddCatalogData/InitializeCatalogDataAsync: CatalogServiceMock (in-memory singleton seeded with legacy PreconfiguredData: 12 items, 5 brands, 4 types; create = max(Id)+1; brand/type navigation resolved) when UseMockData=true; CatalogService on EF Core 8 SqlServer (tables Catalog/CatalogBrand/CatalogType, explicit keys, sequences catalog_hilo/catalog_brand_hilo/catalog_type_hilo, HiLo block of 10, Include brand/type, OrderBy Id paging, decimal(18,2)) when false; CreateDatabaseIfNotExists-equivalent seeding (CSV + zip pictures when UseCustomizationData=true); serialise DB init with sp_getapplock if apps share a DB. Implements the frozen EShop.Catalog.Contracts.ICatalogService; does not change Contracts/**.

**Acceptance.** Unit-level: GetCatalogItemsPaginated returns the rows/counts recorded in mvc/004-016 and webforms/004-015 (e.g. size 10 page 1 -> 2 rows; size 1 page 11 -> item 12; pageSize 0 throws DivideByZeroException like mvc/019); create/update/remove produce exactly the rows in mvc/038, mvc/046, mvc/052 and webforms/031, 038, 043 (new item id 13, brand/type names resolved). SQL mode seeds 12/5/4 rows + 3 sequences as in baseline/sql-mode. dotnet build of net8/EShopStorefront.sln green; both apps start in mock and SQL mode.

## T2 — MVC catalog UI: list/paging, details, create, edit, delete with legacy validation

- Branch: `devin/net8-sf-t2-mvc-catalog`
- Wave: 2
- Owned paths:
  - `net8/src/EShop.Storefront.Mvc/Catalog/**`
  - `net8/src/EShop.Storefront.Mvc/Views/Catalog/**`

**Scope.** Implement AddCatalogMvc/MapCatalogMvc: CatalogController (Index(pageSize=10,pageIndex=0), Details, Create GET/POST, Edit GET/POST, Delete GET + DeleteConfirmed) with [Bind] lists, [ValidateAntiForgeryToken] (failure must be 500), 400 for missing id and 404 for unknown id, redirect to / after mutations; views Index/_CatalogTable/Create/Edit/Details/Delete reproducing legacy markup, pager text, currency formatting ($19.50, en-US), brand/type SelectLists and server-side validation messages; default route {controller=Catalog}/{action=Index}/{id?}.

**Acceptance.** Transcripts mvc/001-053 replay green against :5200 (transcript.py replay --app mvc; list + every page/page size, details 1/5/12/missing/no-id/non-numeric, create GET + every validation failure + missing antiforgery (500) + valid POST, edit GET/invalid/valid, delete GET/POST, state-after-create/edit/delete). Characterization tests test_mvc.py list/details/create/edit/delete tests pass.

## T3 — Legacy Web API and pictures: /api/brands (JSON+XML), /api/files (MS-NRBF), /items/{id}/pic

- Branch: `devin/net8-sf-t3-legacy-api`
- Wave: 2
- Owned paths:
  - `net8/src/EShop.Storefront.Mvc/Api/**`

**Scope.** Implement AddCatalogApi/MapCatalogApi: GET /api/brands and /api/brands/{id} with Web API 2-compatible content negotiation (JSON PascalCase default; DataContract XML for Accept: application/xml), DELETE /api/brands/{id} (200 if exists, 404 otherwise, non-mutating), 405 JSON message for POST/PUT, 404 JSON message shape for unknown api controllers (/api/types, /api/catalog, /api); GET /api/files returning byte-identical MS-NRBF serialization of the brand list without enabling BinaryFormatter (hand-written writer), Content-Type as legacy; GET /items/{id:int}/pic serving Pics/<PictureFileName> as image/png (404 unknown, 400 for id<=0, dummy.png for new items). Pictures are read from wwwroot/Pics (owned by T5).

**Acceptance.** Transcripts mvc/054-073 replay green against :5200 (pics 1/12/created-13/missing/non-numeric, brands json/xml/default accept, brand 1 json/xml, missing, delete 1/missing, brands after delete, POST/PUT 405, api-types/api-root/api-catalog 404, api-files SHA-256 identical). test_mvc.py api/pic tests pass.

## T4 — WebForms catalog as Razor Pages: Default list/paging + Catalog Create/Details/Edit/Delete

- Branch: `devin/net8-sf-t4-razor-catalog`
- Wave: 2
- Owned paths:
  - `net8/src/EShop.Storefront.WebForms/Catalog/**`
  - `net8/src/EShop.Storefront.WebForms/Pages/Catalog/**`

**Scope.** Implement AddCatalogPages/MapCatalogPages: Razor Pages reproducing Default.aspx (/, /Default, /Default.aspx, /Default/index/{index}/size/{size}; non-numeric route -> 500) and Catalog/Create|Details|Edit|Delete (/Catalog/<Page>/{id}; missing/invalid id -> 500), legacy markup/labels/raw price formatting (19.5, 8.50, 12 in mock mode), RequiredField/Range validator messages, postback-equivalent POST handlers redirecting to / on success; antiforgery enabled but WebForms hidden-state fields not required.

**Acceptance.** Transcripts webforms/001-044 replay green against :5201 (list + all index/size routes, details 1/5/12/missing/non-numeric, create GET + every validation failure + valid POST, edit GET/missing/invalid/valid, delete GET/missing/POST, state-after-create/edit/delete). test_webforms.py list/details/create/roundtrip tests pass.

## T5 — Web hosting shell for both apps: layouts, static content and bundles, session footer, error handling, ILogger

- Branch: `devin/net8-sf-t5-hosting-static`
- Wave: 2
- Owned paths:
  - `net8/src/EShop.Storefront.Mvc/Hosting/**`
  - `net8/src/EShop.Storefront.Mvc/Views/Shared/**`
  - `net8/src/EShop.Storefront.Mvc/wwwroot/**`
  - `net8/src/EShop.Storefront.WebForms/Hosting/**`
  - `net8/src/EShop.Storefront.WebForms/Pages/Shared/**`
  - `net8/src/EShop.Storefront.WebForms/wwwroot/**`

**Scope.** Implement AddMvcHosting/UseMvcHosting and AddWebFormsHosting/UseWebFormsHosting: AddControllersWithViews / AddRazorPages, session (machine name + session start time in the footer as legacy _Layout/Site.Master), en-US request culture, legacy layouts (_Layout.cshtml for both apps, titles 'X - Catalog manager (MVC)' / '(Web Forms)'), copy legacy Content/Scripts/fonts/images/Pics/favicon into wwwroot (both casings images/Images, site.css/Site.css), serve bundle URLs /Content/css, /bundles/jquery, /bundles/jqueryval, /bundles/modernizr, /bundles/bootstrap (MVC) and keep WebForms /Content/css 404, status-code behaviour for unknown paths (404) and unhandled exceptions (500), log4net call sites replaced by ILogger with request scope (activity id, request path).

**Acceptance.** Transcripts mvc/074-086 and webforms/045-052 replay green (bundles, static files byte-identical, favicon, unknown path/action 404). Layout header/footer text matches in every HTML transcript once T2/T4 land. test_mvc.py::test_static_content and test_webforms.py::test_static pass.

## T6 — Automated tests and parity runner for the .NET 8 apps

- Branch: `devin/net8-sf-t6-tests-parity`
- Wave: 3
- Owned paths:
  - `net8/tests/**`
  - `modernization/parity/**`

**Scope.** Own net8/tests/** (xUnit + WebApplicationFactory for both apps: service unit tests, endpoint tests mirroring the characterization suite, mock-mode reset per test class) and modernization/parity/** (scripts that build/run the .NET 8 apps on :5200/:5201 from their project dirs with absolute --contentRoot, restart between replays, run transcript.py replay for both apps and the pytest characterization suite, write results/*.json and PARITY-REPORT.md with pass/total vs legacy).

**Acceptance.** dotnet test net8/EShopStorefront.sln green; parity script replays all 138 transcripts (mvc 86 + webforms 52) and runs the 77 characterization tests against the .NET 8 apps, reporting pass/total; after waves 1-2 are merged the report shows 138/138 transcripts and 77/77 tests (any accepted difference must be listed with its CONTRACT.md reference).

## T7 — Linux containers: Dockerfiles and compose for mock and SQL modes

- Branch: `devin/net8-sf-t7-docker`
- Wave: 3
- Owned paths:
  - `net8/docker/**`

**Scope.** Own net8/docker/**: multi-stage Dockerfiles (mcr.microsoft.com/dotnet/sdk:8.0 -> aspnet:8.0, non-root) for both apps; compose for mock mode (UseMockData=true, ports 5200/5201) and SQL mode (SQL Server 2022 mcr.microsoft.com/mssql/server:2022-latest, Encrypt=False, one DB per app or serialised init, healthchecks); scripts to run transcript replay against the containers in both modes.

**Acceptance.** docker compose -f net8/docker/compose.mock.yml up serves both apps and the full transcript set replays green against the containers (138/138); SQL compose seeds 12 items/5 brands/4 types and replay matches legacy SQL-mode results in modernization/baseline/sql-mode (MVC 86/86; WebForms differs from mock only in price formatting as documented).
