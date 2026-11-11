# Running the legacy apps on Linux (Mono 6.12 + xsp4) and capturing the parity baseline

`run-legacy.sh` automates everything below. It works on a **scratch copy** (`$LEGACY_RUN_DIR`, default
`~/mecca-legacy-run/<mode>`); the legacy source tree in the repo is never modified.

```bash
modern/parity/run-legacy.sh                 # mock mode: build, smoke GET / on both apps, capture -> baseline/mock/
modern/parity/run-legacy.sh --skip-build    # reuse the previous scratch build
modern/parity/run-legacy.sh --keep-running  # leave MVC on :5100 and WebForms on :5101 for manual poking
modern/parity/run-legacy.sh --stop          # stop the servers of a --keep-running run (by PID)
LEGACY_SQL_PASSWORD=... modern/parity/run-legacy.sh --sql   # EF6 + SQL Server -> baseline/sql/
```

Env overrides: `LEGACY_RUN_DIR`, `LEGACY_MVC_PORT` (5100), `LEGACY_WEBFORMS_PORT` (5101),
`LEGACY_SQL_SERVER` (`127.0.0.1,1433`), `LEGACY_SQL_PASSWORD`.

## Prerequisites

Mono 6.12 (`mono`, `msbuild`, `xsp4`, `cert-sync`), `nuget` (or the repo-root `nuget.exe`), `python3`, `curl`.
For `--sql`: Docker with SQL Server 2022:

```bash
docker run -d --name mecca-legacy-mssql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="$LEGACY_SQL_PASSWORD" \
  -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

The password is only written into the scratch `Web.config` (mode 600), never into the repo or the baseline.

## Exact recipe (what the script does, and why)

1. `cp -r eShopLegacyMVCSolution eShopLegacyWebFormsSolution $LEGACY_RUN_DIR/<mode>/`
2. `cert-sync --user /etc/ssl/certs/ca-certificates.crt` — Mono's cert store is empty, so `nuget.exe` cannot reach nuget.org over TLS without it.
3. **eShopLegacyMVC**
   1. `nuget install src/eShopLegacyMVC/packages.config -OutputDirectory packages` (the csproj HintPaths point at `..\..\packages`).
   2. `msbuild src/eShopLegacyMVC/eShopLegacyMVC.csproj /restore /p:Configuration=Debug`
   3. `Views/Web.config`: Razor host factory `System.Web.Mvc, Version=5.2.3.0` -> `5.2.7.0` (the package is 5.2.7; Mono ignores the binding redirect for the Razor section).
   4. Case-sensitive file system: copy `Images/` -> `images/`, `Content/Site.css` -> `Content/site.css`, `log4Net.xml` -> `log4net.xml`. **Copies, not symlinks**: xsp4 serves a symlink truncated to the symlink's lstat size.
   5. mock mode: `Web.config` `UseMockData=true` (there is no LocalDB on Linux).
4. **eShopLegacyWebForms**
   1. `nuget install src/eShopLegacyWebForms/packages.config -OutputDirectory packages`
   2. `python3 shim_webforms.py <scratch app dir>` — Mono's System.Web lacks parts of .NET 4.5 WebForms; the shim (refuses to run inside a git checkout) only touches the scratch copy:
      - `Global.asax.cs`: drop `ScriptManager.ScriptResourceMapping` registration (`ScriptResourceDefinition` missing).
      - `Site.Master`: `<asp:ScriptManager>` -> plain `<script>` tags for the same bundles.
      - Pages: remove `ItemType` / `SelectMethod` model binding and `Page.ModelState` use (unsupported), binding the controls in code-behind instead.
   3. `msbuild src/eShopLegacyWebForms/eShopLegacyWebForms.csproj /p:Configuration=Debug`
   4. `rm bin/AspNet.ScriptManager.*.dll bin/Microsoft.ScriptManager.*.dll` — their `PreApplicationStart` needs `ScriptResourceDefinition` and turns every request into a 500.
   5. Copy `Content/site.css` -> `Content/Site.css`, `log4Net.xml` -> `log4net.xml`.
5. `--sql` only (both apps): `UseMockData=false`; connection string `CatalogDBContext` -> `Data Source=$LEGACY_SQL_SERVER; Initial Catalog=Microsoft.eShopOnContainers.Services.CatalogDb; User ID=sa; ...`; strip the hard-coded `USE [...]` line from `Models/Infrastructure/*.Sequence.sql`; and copy each script to the literal file name `Models\Infrastructure\<name>.sql` because `CatalogDBInitializer` opens it with a Windows path (backslashes are ordinary file-name characters on Linux). EF6 then creates and seeds the database on first request.
6. Start each app from its scratch dir, detached so the calling shell never waits on its pipes:
   `(cd <app> && exec setsid nohup xsp4 --port <port> --address 127.0.0.1 --root . --nonstop) > <log> 2>&1 < /dev/null &`
   The PID goes to `$LEGACY_RUN_DIR/<app>.pid` and the app is stopped by that PID (process group). **Never `pkill xsp`**: an unrelated www-data xsp runs on :8084.
7. Smoke: `GET /` on both apps must be 200 (the script fails otherwise).
8. Restart both apps (fresh in-memory state), then `capture_baseline.py` requests the routes in `routes.json` in order, without following redirects.

## Baseline format

`baseline/<mode>/<app>/NN-<route>.txt`:

```
GET /Catalog/Details/1
status: 200
content-type: text/html; charset=utf-8
cache-control: private
---
<normalised body>
```

`SUMMARY.tsv` lists route / status / content-type / location per app. Normalisation (same script is pointed at the
modern hosts later, so both sides are normalised identically):

- origin (`http://127.0.0.1:<port>`) -> `{origin}`; antiforgery, `__VIEWSTATE*`, `__EVENTVALIDATION` values -> `{masked}`;
  session id / timestamp in the WebForms footer masked.
- HTML: `<script>`/`<style>` bodies dropped, whitespace collapsed, one tag per line (diff-friendly).
- JSON: parsed and re-serialised with sorted keys.
- Binary bodies (images, the BinaryFormatter `/api/files` payload that legacy labels `text/html`) -> `(binary <mime>) bytes=N sha256=…`.
- Host error pages (Mono yellow screen) -> one line: `title | exception type | message` (no stack traces / paths / versions).

## Known legacy-on-Mono observations (read before treating a diff as a defect)

- WebForms `/Catalog/Details/999` returns **500 NullReferenceException**: `Details.aspx` binds `product.Name` etc.
  after `FindCatalogItem` returned null. Same code path on IIS, so this is legacy behaviour, not a Mono artefact.
- WebForms `/About` and `/Contact` are 404: `App_Start/RouteConfig.cs` only has `MapPageRoute` entries for the catalog
  pages and FriendlyUrls is not enabled, so the pages are reached as `/About.aspx` and `/Contact.aspx`.
- MVC `/api` (`Controllers/Api/CatalogController2`, an MVC controller with `[Route("api")]`; `RouteConfig` does call
  `MapMvcAttributeRoutes()`) returns 404 under Mono. Not verified on IIS: S5 should treat this route as unconfirmed
  (on IIS it is expected to return `{"Message":"Hello World!"}`).
- mock vs sql: prices render as `19.5` (mock decimal literal) vs `19.50` (SQL `decimal(18,2)`); everything else is
  the same. Only `baseline/mock/` is committed (the parity reference, since the modern hosts default to
  `UseMockData=true`); `baseline/sql/` is git-ignored and regenerated with `--sql` when S1 needs it. Mock mode is the primary parity reference because the modern hosts default to `UseMockData=true`.
- MVC `/api/files` is a BinaryFormatter payload served as `text/html`; only its size/hash is recorded. The modern
  replacement is JSON (intentional hardening), so this route is compared by status only.
