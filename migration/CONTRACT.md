# .NET 8 migration contract (shared by every ticket)

Read with [`INVENTORY.md`](INVENTORY.md) (what the legacy apps do) and [`baseline/BASELINE.md`](baseline/BASELINE.md) (how the legacy behaviour was recorded). A ticket is done when the transcripts it owns replay green against the migrated app (`migration/parity/replay.py`), its own tests pass, and `dotnet build src-net8/eShopNet8.sln` stays green.

## Solution layout (`src-net8/`)
| Path | Owner | Notes |
|---|---|---|
| `global.json`, `Directory.Build.props`, `eShopNet8.sln`, `.gitignore` | scaffold (frozen) | net8.0, nullable off, ICU on, `$(EFCoreVersion)`/`$(AspNetCoreVersion)` = 8.0.11 |
| `src/eShop.Catalog.Core/**` | scaffold (frozen) | `CatalogItem`/`CatalogBrand`/`CatalogType` (1:1 with legacy MVC incl. DataAnnotations), `ICatalogService`, `PaginatedItemsViewModel<T>`, `PreconfiguredData`, `CatalogSettings` |
| `src/eShop.Catalog.Infrastructure/eShop.Catalog.Infrastructure.csproj`, `CatalogConfiguration.cs` | scaffold (frozen) | already references EF Core SqlServer + Design 8.0.11 and `Microsoft.AspNetCore.App` |
| `src/eShop.Catalog.Infrastructure/Data/**` | T1 | `AddCatalogData`, `InitializeCatalogDataAsync` |
| `src/eShop.Catalog.Infrastructure/Mock/**`, `DependencyInjection/**` | T2 | `AddCatalogServices`, `CatalogServiceMock` (scaffold ships a faithful working port) |
| `src/eShopNet8.Mvc/{Program.cs,*.csproj,appsettings*.json,Properties/**}` | scaffold (frozen) | |
| `src/eShopNet8.Mvc/Hosting/CatalogMvcExtensions.cs`, `Controllers/**` (not `Api`), `Views/**`, `wwwroot/**` | T3 | |
| `src/eShopNet8.Mvc/Hosting/CatalogApiExtensions.cs`, `Api/**` | T4 | |
| `src/eShopNet8.WebForms/{Program.cs,*.csproj,appsettings*.json,Properties/**}` | scaffold (frozen) | |
| `src/eShopNet8.WebForms/Hosting/CatalogPagesExtensions.cs`, `Pages/**`, `wwwroot/**` | T5 | |
| `src/eShopNet8.*/Hosting/HealthChecksExtensions.cs`, `src-net8/docker/**`, `src-net8/docker-compose*.yml`, `src-net8/.dockerignore`, `.github/workflows/net8-*.yml` | T6 | |
| `src-net8/tests/**` | T7 | three projects already exist and build |

Rules: never edit a frozen or another ticket's file. If a frozen file is genuinely wrong, say so in the PR description instead of editing it. New NuGet packages may only be added to a project you own (test projects: T7); the web/infrastructure csproj files are frozen, so everything runtime-side must come from the shared framework or the packages already referenced.

## Wiring (frozen `Program.cs`)
MVC: `AddCatalogData` -> `AddCatalogServices` -> `AddCatalogMvc` -> `AddCatalogApi` -> `AddCatalogHealthChecks`; then `UseCatalogMvc` (owns the whole middleware pipeline: exception handling, request localization, static files, session, routing, antiforgery) -> `MapCatalogHealthChecks` -> `MapCatalogApi` -> `MapCatalogMvc`; `await app.InitializeCatalogDataAsync()` before `RunAsync`.
WebForms: `AddCatalogData` -> `AddCatalogServices` -> `AddCatalogPages` -> `AddCatalogHealthChecks`; `UseCatalogPages` -> `MapCatalogHealthChecks` -> `MapCatalogPages`.
`Program` is a non-static `partial class` with `Main` in namespace `eShopNet8.Mvc` / `eShopNet8.WebForms`, so `WebApplicationFactory<eShopNet8.Mvc.Program>` works and the two apps never collide.

## Configuration
| Key | Default | Override | Meaning |
|---|---|---|---|
| `UseMockData` | `true` (appsettings.json of both apps) | env `UseMockData=false`, or `--UseMockData=false` | true: `CatalogServiceMock` singleton (in-memory, mutations persist for the process lifetime). false: SQL Server via EF Core (`AddCatalogData` registers everything; `AddCatalogServices` registers nothing). Read only via `IConfiguration.GetCatalogSettings()`. |
| `UseCustomizationData` | `false` | env | SQL seeding from `Setup/*.csv` + `CatalogItems.zip` instead of `PreconfiguredData` |
| `ConnectionStrings:CatalogDBContext` | `Server=localhost,1433;Database=Microsoft.eShopOnContainers.Services.CatalogDb;User Id=sa;TrustServerCertificate=True;MultipleActiveResultSets=True` (no password) | env `ConnectionStrings__CatalogDBContext` (full string incl. password; never commit one) | |
Parity is defined in mock mode (the legacy baseline ran with `UseMockData=true`, the only mode Mono can run). SQL mode must expose the same pages/API, with ids from the `catalog_hilo` sequence instead of `max+1`.

## Ports
| | legacy (Mono/xsp4) | migrated local (`dotnet run`, launchSettings) | container |
|---|---|---|---|
| MVC + Web API | 5100 | 5200 | 8080 (`ASPNETCORE_HTTP_PORTS`, aspnet:8.0 default) |
| WebForms -> Razor Pages | 5101 | 5201 | 8080 |

## Culture decision
Legacy `Web.config` pins `<globalization culture="en-US" uiCulture="en-US">`, so prices render `$19.50` (MVC `DisplayFor` + `DataType.Currency`) and the footer date `10/23/2026 4:00:58 AM`. The migrated apps must force en-US regardless of host locale (`RequestLocalizationOptions` with en-US as the only/default culture and/or `CultureInfo.DefaultThreadCurrentCulture`), keep ICU enabled (`InvariantGlobalization=false`, already in `Directory.Build.props`) and use the Debian-based `mcr.microsoft.com/dotnet/aspnet:8.0` image (Alpine images default to invariant globalization, which renders `¤19.50`). ICU's en-US time format uses U+202F before AM/PM; the normaliser collapses all Unicode whitespace, so either is accepted. WebForms renders raw decimals (`19.5`, `8.50`) without a currency symbol: keep `decimal.ToString()` there, not `C`.

## Parity rules (what `replay.py` enforces)
Flows run per app in `flows.json` order on a freshly started app (restart = reset of the singleton mock). For each flow the projection below of the migrated response must equal the legacy transcript's:
1. **Status code** exactly (including legacy 400/404/500 behaviour, see quirks).
2. **MIME type** (Content-Type without parameters) for status < 400, unless the flow sets `compare.mime=false` (only `mvc-api-files`). Static files keep the legacy MIME map: `.js` -> `application/javascript`, `.css` -> `text/css`, `.ico` -> `image/x-icon`, `.png` -> `image/png`.
3. **Location** header for redirects, with the origin replaced by `{base}` (legacy redirects to `{base}/`).
4. **Body**, ignored for 3xx and >= 400 (those bodies are host error pages). Otherwise:
   - HTML -> normalised structure: `title`; visible `text` (scripts/styles/`display:none` removed, whitespace collapsed, own origin -> `{base}`, footer session info `<machine>, <M/d/yyyy h:mm:ss tt>` -> placeholder); `headings`; `links` (text, resolved href, sorted classes, e.g. `esh-pager-item--hidden`); `images` (resolved src: MVC thumbnails are absolute `{base}/items/{id}/pic`, WebForms uses `/Pics/{file}`); `tables` (every cell's text, row order); `definitions` (dt/dd); `forms` (method, resolved action, every field's tag/name/type/value/options/selected/readonly and its `data-val*` attributes, minus antiforgery/viewstate token fields); `validation` (visible `.field-validation-error`, `.validation-summary-errors li`, `.text-danger` text); `assets` (stylesheet hrefs + script srcs, MVC only; WebForms flows set `compare.assets=false`).
   - JSON -> parsed value (property names and order of array elements matter: PascalCase `Id`/`Brand`).
   - Binary/static -> sha256 + length (byte-identical). Bundle endpoints (`/Content/css`, `/bundles/*`) compare status + MIME only.
5. **Data state**: after every `snapshot` flow the full catalog list (`/Catalog/Index?pageSize=1000&pageIndex=0`, `/Default/index/0/size/1000`) must match row for row (all cells, thumbnail URL, row links) and the pager text.
6. No allow-list of tolerated differences exists. If a ticket believes a transcript encodes a Mono artefact rather than legacy behaviour, it must raise it in its PR (do not edit the baseline).

## Legacy behaviour that must be reproduced deliberately
- **`/api` returns 404.** `Controllers/Api/CatalogController.cs` declares `CatalogController2`; MVC 5 only treats types ending in `Controller` as controllers, so the `[Route("api")]` hello-world never answered. ASP.NET Core discovers every `Controller` subclass, so a literal port would answer 200: do not port it as a routable endpoint.
- **`/api/files` BinaryFormatter payload.** BinaryFormatter is disabled in .NET 8 (`SYSLIB0011`, throws at runtime). T4 must not re-enable it (`EnableUnsafeBinaryFormatterSerialization` is forbidden). Replace it with an explicit MS-NRBF writer that emits the recorded bytes for `List<FilesController+BrandDTO>` (assembly `eShopLegacyMVC, Version=1.0.0.0`): the transcript `mvc-api-files` holds the exact payload in `response.body.base64` (sha256 must match). `System.Formats.Nrbf` (reader) may be used in tests to decode it.
- **Absolute `PictureUri`.** Built from the request scheme + host (`Url.RouteUrl(..., Request.Scheme)` -> `Url.Link`/`Url.RouteUrl(..., protocol)`): `{base}/items/{id}/pic`. Behind a proxy the container must honour `X-Forwarded-*` only if configured; the replay talks to the app directly.
- **Antiforgery.** MVC POSTs require `__RequestVerificationToken` (hidden field + cookie). A POST without it returns **500** in legacy (`HttpAntiforgeryException` -> YSOD); ASP.NET Core's default is 400, so map antiforgery validation failures to 500 for parity (`mvc-create-post-no-antiforgery`). The field name must stay `__RequestVerificationToken`.
- **Client validation.** Create/Edit render jquery.validate + unobtrusive scripts (`/Scripts/jquery.validate.js`, `/Scripts/jquery.validate.unobtrusive.js`) and the `data-val-*` attributes recorded in the transcripts (e.g. `data-val-number="The field Price must be a number."`, `data-val-regex-pattern`, custom range messages, `data-val-required` on non-nullable ints). Server-side messages: `The Name field is required.`, `The value 'abc' is not valid for Price.`, `The field Price must be between 0 and 1000000.`, `The field Price must be a positive number with maximum two decimals.`, custom stock range messages.
- **Mock singleton state.** Create = `max(Id)+1` (13, 14, ...); Update replaces the list element with the posted object (navigations null until the next list call); Delete of a missing id still redirects (302); the list call composes brand/type onto every item.
- **Paging edge cases.** `pageSize=0` -> 500 (divide by zero), negative `pageIndex` -> empty page labelled `Page 0`, out-of-range page -> empty table with both pager links, `pageSize=abc` -> default 10. Previous/Next links are always rendered, with `esh-pager-item--hidden` when not navigable, hrefs `/?pageSize=X&pageIndex=Y` (MVC) and `/Default/index/{i}/size/{s}` (WebForms).
- **MVC 400/404.** Details/Edit/Delete without id or with a non-int id -> 400; unknown id -> 404; pic id <= 0 -> 400, unknown -> 404, non-int -> 404 (route constraint).
- **WebForms quirks (server side only, JavaScript validators aside).** Unknown or non-int id on Details/Edit/Delete -> 500 (NullReferenceException / FormatException); `size/0` -> 500; Create/Edit postbacks skip server validation (`ModelState.IsValid` is always true there): empty name or negative stock is saved and redirects to `/`, a non-numeric price -> 500. Form field names are the WebForms ones (`ctl00$MainContent$Name`, `...$Brand`, `...$BrandDropDownList`, submit buttons `ctl00$MainContent$ctl06` / `ctl07` / `ctl11`), because the transcripts post them.
- **Session footer.** Both layouts print `{MachineName}, {SessionStartTime}` (session start in en-US general format); WebForms wraps it in `<small style="color:white"><span>`.
