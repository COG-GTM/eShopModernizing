# Legacy baseline: runtime and capture

The legacy apps were built and run on Linux with Mono (no Windows / IIS Express exists on this VM, so the
"reference" runtime is Mono + xsp4 with the harness-only edits listed below). Everything here is reproducible with
`modernization/parity/legacy/setup-legacy.sh` then `start-legacy.sh mvc|webforms|mvc-sql`.

## 1. Host
| | |
|---|---|
| OS | Ubuntu 22.04.5 LTS, kernel 6.8.0-1061-aws, x86_64 |
| Mono | 6.12.0.200 |
| Web server | xsp4 (Mono.WebServer2 0.4.0.0), `--nonstop`, bound to 127.0.0.1 |
| Build | Mono MSBuild 16.10.1.31701; NuGet 6.6.1.2 (`/usr/bin/nuget`) |
| SQL (SQL mode only) | Docker `mcr.microsoft.com/mssql/server:2022-latest`, container `sql`, `127.0.0.1:1433`, local-only SA password (env `LEGACY_SQL_PASSWORD`, never committed) |
| Harness | Python 3.12, requests, beautifulsoup4 + lxml, Playwright (Chrome) |
| Not available | Windows, IIS / IIS Express, LocalDB, .NET Framework runtime: the original hosting could not be run; Mono is the closest runnable reference |

Ports: legacy MVC 5100, legacy WebForms 5101, legacy MVC SQL mode 5110 (target apps: 5200 / 5201).

## 2. Recipe
```bash
modernization/parity/legacy/setup-legacy.sh          # copies sources to $LEGACY_RUN_DIR (default ~/legacy-run), restores, builds, applies harness edits
modernization/parity/legacy/start-legacy.sh mvc      # http://127.0.0.1:5100 (mock data)
modernization/parity/legacy/start-legacy.sh webforms # http://127.0.0.1:5101 (mock data)
LEGACY_SQL_PASSWORD=... LEGACY_SQL_RESET=1 modernization/parity/legacy/start-legacy.sh mvc-sql   # http://127.0.0.1:5110 (EF6 + SQL Server)
modernization/parity/legacy/start-legacy.sh stop mvc
```
`start-legacy.sh` (re)starts the app (kills whatever holds the port) and returns once `/api/brands` (MVC) or
`/About.aspx` (WebForms) answers 200; it never touches a catalog page while probing (Q4). First-request Razor/ASPX
compilation takes ~5-20 s on this host.

## 3. Harness-only edits (applied to the throwaway copy only; the repo sources are untouched)
Build:
1. NuGet: the repo's `nuget.exe` (4.x under Mono) fails TLS (`crypt32.dll`); system NuGet 6.6.1 is used, after `cert-sync --user`.
2. MVC mixes `packages.config` HintPaths with `PackageReference`: `nuget restore packages.config` into `../../packages`
   **before** `msbuild /restore`, otherwise `System.Web.Mvc` falls back to Mono's GAC 3.0.0.0 and the build fails with CS0433.

MVC runtime:
3. `Web.config` `UseMockData` -> `true` (repo default is `false` + LocalDB, which does not exist on Linux).
4. `Views/Web.config`: Razor host factory / page base type references `System.Web.Mvc 5.2.3.0` -> `5.2.7.0` (Mono ignores the binding redirect there).
5. Case-sensitivity copies (real copies; xsp4 truncates symlinked files): `Images/` -> `images/`, `Content/Site.css` -> `Content/site.css`, `log4Net.xml` -> `log4net.xml`.

MVC SQL mode (`mvc-sql` copy):
6. `UseMockData=false`; connection string `CatalogDBContext` -> SQL Server container (password from env at start).
7. `CatalogDBInitializer` builds sequence-script paths with backslashes; files named with literal backslashes
   (`Models\Infrastructure\dbo.catalog_*_hilo.Sequence.sql`) are created next to the app so the unmodified initializer finds them.

WebForms runtime (`parity/legacy/shim_webforms.py`, refuses to run inside the repo):
8. `App_Start/BundleConfig.cs`: removed `ScriptManager.ScriptResourceMapping.AddDefinition(...)` (Mono lacks `ScriptResourceDefinition`); incompatible ScriptManager/FriendlyUrls-era assemblies removed from the copy's `bin`.
9. `Site.Master`: `<asp:ScriptManager>` replaced by the equivalent explicit `<script src>` tags (same files, same order).
10. `Default.aspx`, `Catalog/*.aspx`: `ItemType` + strongly-typed `<%#: Item.X %>` bindings replaced by `<%#: Eval("X") %>` (identical rendered text).
11. `Catalog/Create.aspx(.cs)`: `SelectMethod` on the brand/type dropdowns replaced by binding the same service calls in `Page_Load`.
12. `this.ModelState.IsValid` (absent on Mono's `Page`) replaced by a `true` shim, which equals the legacy behaviour: the handlers never enforced validators (Q8).
13. Case copies: `Content/Site.css` (bundle) and `images/` as in 5.

Rendered differences these edits can cause vs IIS: script tags in `<head>`/form (not compared, CONTRACT H1);
ViewState/EventValidation encoding (masked, H6); error page bodies (opaque, R5).

## 4. Baseline capture (mock mode)
`python3 modernization/parity/capture.py --mvc-url http://127.0.0.1:5100 --webforms-url http://127.0.0.1:5101 --restart-cmd-mvc "modernization/parity/legacy/start-legacy.sh mvc" --restart-cmd-webforms "modernization/parity/legacy/start-legacy.sh webforms"`

| | |
|---|---|
| Flows | 155 in 13 sequences (MVC 99, WebForms 56); 1 dropped (below) |
| Transcripts | 155 (`transcripts/`) |
| Data-state snapshots | 17 (`data-state/`) |
| Status mix | 200 x109, 302 x10, 400 x9, 404 x12, 500 x15 |
| Screenshots | 14 (`screenshots/legacy-{mvc,webforms}-*.png`, `parity/screenshots.py --prefix legacy`) |
| Legacy test projects | none |

Self-replay (`replay.py` against the same legacy apps, restart before every restart-sequence), twice:
run1 and run2 each **155/155 flows, 87 identical + 68 equivalent-after-normalisation, 0 different** (`self-replay/run1`, `self-replay/run2`, ~37 s per run).

Dropped as non-deterministic: `mvc-create-post-invalid-price-negative` (Q17: `Price=-1` violates both the regex and
the range attribute; MVC 5 reports one of them and the choice changed between app starts). Covered instead by
`mvc-create-post-invalid-price-regex`, `-price-nonnumeric` and `-price-range`.

## 5. Legacy MVC in SQL mode (EF6 on Mono + SQL Server 2022 container): WORKS
`CatalogDBInitializer` created the database, the three HiLo sequences (`catalog_hilo`, `catalog_brand_hilo`,
`catalog_type_hilo`, increment 10) and seeded 12 items / 5 brands / 4 types (verified with sqlcmd). CRUD, pictures,
`/api/brands` and `/api/files` work. Replaying the **mock** MVC baseline against SQL mode
(`replay.py --mvc-url http://127.0.0.1:5110 --restart-cmd-mvc "LEGACY_SQL_RESET=1 start-legacy.sh mvc-sql"`,
report in `legacy-sql-vs-mock/`): 99 MVC flows, 57 identical, 36 equivalent, **6 different**, all legacy mock-vs-EF behaviour differences (Q18):

| Flow | Mock | SQL |
|---|---|---|
| `mvc-details-cold-12`, `mvc-delete-cold-10` | Brand/Type empty (Q4) | populated (EF `Include`) |
| `mvc-index-negative-page`, `mvc-index-negative-size` | 200 | 500 (negative OFFSET/FETCH) |
| `mvc-edit-post-unknown-id` | 302 (no-op) | 500 |
| `mvc-delete-post-unknown-id` | 302 (no-op) | 500 |

Consequence for the migration: the mock-mode baseline is the primary oracle; SQL mode (T1) is checked against
`legacy-sql-vs-mock/` semantics (same 6 differences expected, everything else equal). Created ids: mock assigns
`max(id)+1`; EF assigns from `catalog_hilo`. In both modes the first created item got id 13 (flow
`mvc-create-post-valid` and its data-state snapshot are equal in the SQL replay).
WebForms SQL mode was not attempted (its EF path is the same `CatalogService`/initializer design; WebForms defaults to mock).
