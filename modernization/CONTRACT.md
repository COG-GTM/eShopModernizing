# Behaviour contract — legacy eShop storefront → ASP.NET Core 8

Read-only for every ticket. The legacy apps running under `legacy-harness/` (mock mode) are the oracle; the recorded transcripts in
`baseline/transcripts/` are the executable form of this contract. A ticket is done only when its transcripts replay green against the
.NET 8 app (see TICKETS.md for the per-ticket transcript sets).

## 1. Ports and processes

| App | Legacy (Mono/xsp4) | .NET 8 (local `dotnet run` / containers) |
|---|---|---|
| MVC + Web API | `127.0.0.1:5100` | `127.0.0.1:5200` (`net8/src/EShop.Storefront.Mvc`) |
| WebForms catalog / Razor Pages | `127.0.0.1:5101` | `127.0.0.1:5201` (`net8/src/EShop.Storefront.WebForms`) |
| SQL-mode legacy (optional) | `5110` / `5111`, SQL Server 2022 on `127.0.0.1:14330` | per `net8/docker` compose |

Run a built .NET 8 app from its project directory with an absolute content root, e.g.
`cd net8/src/EShop.Storefront.Mvc && dotnet bin/Debug/net8.0/EShop.Storefront.Mvc.dll --urls http://127.0.0.1:5200 --contentRoot $PWD`.

## 2. Configuration (same keys as legacy appSettings)

| Key (appsettings / env var) | Default in net8 | Meaning |
|---|---|---|
| `UseMockData` | `true` | `true`: in-memory singleton catalog seeded from legacy `PreconfiguredData` (12 items, 5 brands, 4 types); restart = reset. `false`: EF Core 8 + SQL Server. |
| `UseCustomizationData` | `false` | SQL seed from `Setup/*.csv` and pictures from `Setup/CatalogItems.zip`. |
| `ConnectionStrings__CatalogDBContext` | local SQL | SQL Server connection (`Encrypt=False` for the local container). |
| `ASPNETCORE_URLS` | — | ports above. |

The shared `EShop.Catalog.Contracts` types (`CatalogItem` incl. DataAnnotations + messages, `CatalogBrand`, `CatalogType`,
`PaginatedItemsViewModel<T>`, `ICatalogService`, `CatalogOptions`) are part of this contract and frozen.

## 3. What must match exactly

Compared on every transcript (default `replay` mode):

- **HTTP status** for every case, including legacy error statuses: MVC `pageSize=0` → 500, missing id → 404, missing/invalid id param → 400,
  missing antiforgery token → **500**, Web API missing brand → 404, unsupported verb → 405; WebForms missing/invalid id → 500, bad paging route → 500.
- **2xx responses:** media type (`text/html`, `application/json`, `application/xml`, `text/css`, `image/png`, ...; charset ignored) and the
  **semantic view**:
  - HTML: `<title>`, normalised visible text (incl. layout header "Catalog manager (MVC)" / "(Web Forms)", pager text, labels, prices
    formatted as legacy: MVC `$19.50`, WebForms mock `19.5` / `8.50` / `12`), form fields (name, type, value, options, selected), visible
    validation messages, table rows.
  - JSON: parsed value (PascalCase `Id`/`Brand`, order preserved). XML: normalised text.
  - Binary (images, `/api/files` MS-NRBF): SHA-256 + length — i.e. byte-identical.
- **3xx:** status + `Location` path (host independent). Redirect bodies ("Object moved") are not compared.
- **Data state:** catalog table rows after create / edit / delete (`state-after-*` transcripts).
- **4xx JSON** (Web API) bodies: parsed value, with absolute base URLs normalised to `{{BASE}}`.

## 4. What is normalised or not compared

| Item | Treatment |
|---|---|
| `__RequestVerificationToken`, `__VIEWSTATE`, `__VIEWSTATEGENERATOR`, `__EVENTVALIDATION` values | `{{VOLATILE}}` |
| Footer machine name + session start time (`<host>, <date>`) | `{{HOST}}`, `{{DATETIME}}` |
| `WebResource.axd`/`ScriptResource.axd` query, bundle `?v=` hashes, session cookies | `{{AXD}}`, `{{HASH}}`, `{{SESSIONID}}` |
| Absolute `http://127.0.0.1:<port>` / `localhost:<port>` | `{{BASE}}` |
| Mono module ids / `App_Web_xxxxxxxx` in stack traces | `{{MODULE}}`, `{{DYNAMIC}}` |
| 4xx/5xx HTML bodies (Mono yellow pages vs Core) | status only |
| 3xx bodies | not compared |
| Charset parameter of `Content-Type` | ignored |
| Full normalised body | only with `--strict` (used for the legacy determinism proof; optional for parity) |

**Dropped as non-deterministic:** MVC create with `Price=-5` — it violates both `[RegularExpression]` and `[Range]`, and Mono reports
whichever validator it reflects first, which changed between app starts (observed both messages). The two messages stay covered by
`create-post-invalid-price-three-decimals` and `create-post-invalid-price-too-large`. Determinism proof: `baseline/determinism/`.

## 5. Decisions on known legacy quirks

| Quirk | Decision |
|---|---|
| Antiforgery failure = 500 (Core default 400) | **Reproduce 500.** |
| Redirects carry "Object moved" HTML | Not compared (3xx rule). |
| `/api/brands*` XML via `Accept: application/xml` | **Reproduce** (DataContract XML, same element names/namespaces). |
| `/api/files` = BinaryFormatter (MS-NRBF) bytes of the brand list, `Content-Type: text/html` | **Reproduce byte-identical** without enabling BinaryFormatter (hand-written MS-NRBF writer for the `List<FilesController.BrandDTO>` payload; transcript `mvc/071-api-files`). |
| `DELETE /api/brands/{id}` does not delete | Reproduce (200/404 only). |
| No `/api/types`, `/api/catalog`, `/api` | Reproduce 404 (`mvc/070`, `072`, `073`). |
| WebForms `/Content/css` bundle 404 on Mono | Reproduce 404 (`webforms/045`); direct CSS paths 200. |
| WebForms missing id → 500 | Reproduce 500. |
| MVC `pageSize=0` → 500 | Reproduce 500. |
| WebForms price formatting differs by mode (mock `19.5`, SQL `19.50` because SQL returns decimal(18,2) scale) | Reproduce per mode; mock is the parity mode. |
| WebForms details of an item created *and then* edited → 500 on legacy mock | Not part of the transcript set (characterization test asserts via the list page); not required. |
| MVC client-side validation (blur order) | Not compared (transcripts are server-side POSTs). |

## 6. SQL mode

Legacy SQL mode was run (SQL Server 2022 container, one DB per app). Replaying the **mock** transcripts against **legacy SQL mode**:
MVC 85/86 — all except `/api/files` (719 bytes in mock vs 721 in SQL mode: the serialized `List<BrandDTO>` internals differ); WebForms 29/52, and all 23 differences are price formatting only (`19.50` vs `19.5`) on list/details/edit pages and state snapshots
(`baseline/sql-mode/*.json`). The .NET 8 SQL mode must match legacy SQL mode; T7 (docker) owns the SQL-mode replay.

## 7. How to replay

```bash
pip install -r modernization/tools/requirements.txt
# legacy oracle
modernization/legacy-harness/setup.sh && modernization/legacy-harness/start.sh
# replay all transcripts against any implementation (fresh app state required; --reset-cmd restarts it per run)
python3 modernization/tools/transcript.py replay --app mvc --base-url http://127.0.0.1:5200 \
    --reset-cmd "<command that restarts the MVC app>" --report modernization/parity/results/mvc.json
python3 modernization/tools/transcript.py replay --app webforms --base-url http://127.0.0.1:5201 \
    --reset-cmd "<command that restarts the WebForms app>" --report modernization/parity/results/webforms.json
# characterization suite (77 tests) against any base URLs, apps freshly started in mock mode
MVC_BASE_URL=http://127.0.0.1:5200 WEBFORMS_BASE_URL=http://127.0.0.1:5201 pytest modernization/tools/tests -q
```

Transcripts are replayed in file order: mutating steps (create → edit → delete) depend on earlier steps, so always replay a full app set
from a fresh start. `transcript.py list --app <app>` prints the cases; `--strict` adds full-body comparison.
