# Legacy harness (Mono 6.12 + MSBuild + xsp4 on Linux)

Builds and runs the UNMODIFIED legacy sources from a **copy** (`$LEGACY_ROOT`, default `~/eshop-legacy-run`). Repo sources are never edited;
all compatibility patches below are applied to the copy only.

| App | Source | Port | Mode used for the baseline |
|---|---|---|---|
| MVC 5 + Web API 2 | `eShopLegacyMVCSolution/src/eShopLegacyMVC` | `:5100` | mock (`UseMockData=true`) |
| WebForms catalog | `eShopLegacyWebFormsSolution/src/eShopLegacyWebForms` | `:5101` | mock (`UseMockData=true`) |

## Verified runtime

- Ubuntu VM, Mono `6.12.0.200` (`/usr/bin/mono`), `msbuild` (Mono), `xsp4`, `nuget` (Mono), Docker.
- `cert-sync --user /etc/ssl/certs/ca-certificates.crt` is run by `setup.sh` (nuget.exe TLS on Mono).

## Commands

```bash
modernization/legacy-harness/setup.sh            # copy + nuget restore + patches + msbuild, both apps (~3-5 min)
modernization/legacy-harness/start.sh            # xsp4 on :5100 and :5101, waits for HTTP 200 (first hit compiles views, ~1 min)
modernization/legacy-harness/reset.sh mvc        # restart = reset (mock catalog is an in-memory singleton); sql mode also drops the DB
modernization/legacy-harness/stop.sh             # kills ONLY the harness PIDs / listeners on the harness ports (never `pkill xsp`)
```

Env: `LEGACY_ROOT`, `MVC_PORT` (5100), `WEBFORMS_PORT` (5101), `LEGACY_MODE` (`mock`|`sql`), `USE_CUSTOMIZATION_DATA` (`false`).
Servers are started with `setsid nohup xsp4 ... > $LEGACY_ROOT/<app>.log 2>&1 < /dev/null &`, `MONO_IOMAP=all`.

## Copied-run patches (why each is needed)

| Patch | File(s) in the copy | Reason |
|---|---|---|
| `patch_config.py` | `Web.config` | Sets `UseMockData` / `UseCustomizationData` and (sql) the connection string. |
| `nuget install packages.config -OutputDirectory packages` | MVC | csproj mixes PackageReference with `HintPath=..\..\packages`; without it Mono's GAC `System.Web.Mvc 3.0` collides with 5.2.7 (CS0433). |
| `patch_mvc.py` | MVC `Views/Web.config` | Razor host factory `System.Web.Mvc, Version=5.2.3.0` → `5.2.7.0` (the only MVC assembly present); otherwise every view 500s. |
| `patch_webforms.py` | WebForms `App_Start/BundleConfig.cs`, `Site.Master`, `Default.aspx`, `Catalog/{Create,Edit}.aspx(.cs)` | Mono's System.Web lacks `ScriptResourceDefinition`, strongly-typed binding (`ItemType`/`Item`/`<%#:`), model-binding `SelectMethod`, and `Page.ModelState`. Rewritten to `Eval(...)`+HtmlEncode, code-behind dropdown binding, `Page.IsValid`, plain `<script>` tags instead of ScriptManager mappings. Markup/visible output is otherwise unchanged. |
| case copies | `images`↔`Images`, `Content/site.css`↔`Site.css` | Linux is case-sensitive; real copies (xsp4 truncates symlinks to the link's lstat size). |
| `eShopPorted/` removed from the MVC copy | — | unrelated earlier ASP.NET Core port in the same solution folder. |
| SQL scripts (sql mode) | `Models/Infrastructure/*.sql` + backslash-named copies | `CatalogDBInitializer` reads `Path.Combine(BaseDirectory, @"Models\Infrastructure\x.sql")`; the scripts start with `USE [Microsoft.eShopOnContainers.Services.CatalogDb]`, which is dropped so each app seeds the DB in its own connection string. |

## SQL mode (optional, verified)

SQL Server 2022 in Docker (`mcr.microsoft.com/mssql/server:2022-latest`), `Encrypt=False`, one database per app
(`eShopLegacyCatalog_mvc`, `eShopLegacyCatalog_webforms`) so the two EF6 initializers never race.

```bash
modernization/legacy-harness/sql.sh up           # container eshop-legacy-sql on 127.0.0.1:14330 (throwaway local sa password)
export LEGACY_MODE=sql LEGACY_ROOT=$HOME/eshop-legacy-run-sql MVC_PORT=5110 WEBFORMS_PORT=5111
modernization/legacy-harness/setup.sh && modernization/legacy-harness/start.sh
modernization/legacy-harness/reset.sh all        # stop, DROP both DBs, start (EF6 CreateDatabaseIfNotExists re-seeds)
```

Observed: EF6 6.2 on Mono's `System.Data.SqlClient` creates `Catalog`/`CatalogBrand`/`CatalogType`, the three HiLo sequences and seeds
12 items / 5 brands / 4 types (same as `PreconfiguredData`). `MultipleActiveResultSets=False` is used (Mono SqlClient).
Mock-mode transcripts replayed against SQL mode: see `../baseline/sql-mode/` and CONTRACT.md §6.

## Known runtime behaviour that differs from IIS / is Mono-specific

- 5xx bodies are Mono "yellow pages" (never compared byte-wise).
- WebForms `/Content/css` bundle → 404 on Mono (direct CSS files are served).
- WebForms missing/invalid ids → 500 (NullReferenceException) — also the IIS behaviour per source.
- MVC antiforgery failure → 500.
