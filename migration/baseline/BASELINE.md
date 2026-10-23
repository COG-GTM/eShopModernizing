# Legacy baseline (recorded against the running .NET Framework apps)

## Environment
| | |
|---|---|
| Host | Ubuntu 22.04.5 LTS (Devin VM) |
| Legacy runtime | .NET Framework 4.7.2 assemblies on **Mono 6.12.0.200** + **xsp4** (Mono.WebServer2 0.4), MSBuild 16.10, NuGet 6.6.1 |
| Legacy MVC | `eShopLegacyMVCSolution/src/eShopLegacyMVC` -> builds (0 errors), runs on `http://localhost:5100` |
| Legacy WebForms | `eShopLegacyWebFormsSolution/src/eShopLegacyWebForms` -> builds (0 errors), runs on `http://localhost:5101` |
| Data mode | `UseMockData=true` (in-memory `CatalogServiceMock`; LocalDB does not exist on Linux) |
| Harness | Python 3.12, requests 2.32, beautifulsoup4 4.15, Playwright 1.64 (chromium headless shell) |

Reproduce: `migration/parity/legacy/setup-legacy.sh all` (throwaway copies in `/tmp/legacy-mvc`, `/tmp/legacy-wf`), then `migration/parity/legacy/start-legacy.sh mvc|webforms` (restart = fresh mock catalog; first request compiles Razor/ASPX, ~10-60 s).

### Harness-only edits (applied to the throwaway copies by `setup-legacy.sh`, never committed)
MVC:
1. `Web.config` `UseMockData` false -> true (no LocalDB on Linux).
2. `Views/Web.config` Razor host factory `System.Web.Mvc 5.2.3.0` -> `5.2.7.0` (Mono ignores the binding redirect there).
3. Symlinks `images -> Images` and `Content/site.css -> Site.css`: IIS is case-insensitive, Linux is not; the layout links `/images/*` and the css bundle includes `site.css`.

WebForms (`UseMockData` is already true):
1. `BundleConfig.cs`: drop the `ScriptManager.ScriptResourceMapping.AddDefinition("respond", ...)` call and delete the `AspNet.ScriptManager.*` / `Microsoft.ScriptManager.*` DLLs from `bin`: Mono's `System.Web.Extensions` has no `ScriptResourceDefinition` (TypeLoadException on every page).
2. `Site.Master`: replace the `<asp:ScriptManager>` block with the plain `jquery-3.3.1.js` / `bootstrap.js` / `respond.js` script tags it would emit.
3. `Default.aspx`: `<%#: expr %>` -> `<%# HttpUtility.HtmlEncode(expr) %>` and strongly typed `Item.` -> `((CatalogItem)Container.DataItem).` (Mono's page parser predates ASP.NET 4.5; output is identical).
4. `Catalog/Create.aspx(.cs)`: `SelectMethod="GetBrands|GetTypes"` replaced by binding the same methods on first load (Mono ignores `SelectMethod`, leaving the dropdowns empty).
5. `Catalog/Create.aspx.cs`, `Catalog/Edit.aspx.cs`: `this.ModelState.IsValid` -> `true` (Mono's `Page` has no `ModelState`; these pages never model-bind, so on .NET Framework it is always true).
6. Nuget restore needs Mono's CA store: `cert-sync --user /etc/ssl/certs/ca-certificates.crt`.

## What was captured
| Artefact | Count |
|---|---|
| Flows in `flows.json` | **126** (MVC 79, WebForms 47): 92 read-only + 34 in the ordered mutation sequences |
| Transcripts `transcripts/<flow-id>.json` | **126** |
| Data-state snapshots `data-state/*.json` | **24** (2 initial catalogs + 22 after each mutating flow) |
| Screenshots `screenshots/legacy-*.png` | **14** (index, page 2, details, create, edit, delete and client-side validation for each app, fresh catalog) |

Status mix recorded: MVC 200x58, 302x4, 400x6, 404x9, 500x2; WebForms 200x34, 302x6, 500x7.

Each transcript holds the request (method, path, posted form fields; which antiforgery / viewstate token fields were obtained dynamically from the form page), the status, selected headers (Content-Type, Location with origin -> `{base}`), and the normalised body (HTML structure + visible text, parsed JSON, or sha256 + length + base64 for small binaries such as the `/api/files` NRBF payload). Normalisation rules: [`../CONTRACT.md`](../CONTRACT.md#parity-rules-what-replaypy-enforces); code: `migration/parity/parity_common.py`.

## Legacy tests
**There is no legacy test project** in either solution (no `*Test*` project in `eShopLegacyMVC.sln` or `eShopLegacyWebForms.sln`; `eShopPorted` is an abandoned net461/ASP.NET Core 2.2 port, not a test suite). The transcript suite above is the baseline test suite.

## Baseline self-replay
`migration/parity/replay.py` replayed every transcript against the legacy apps it was recorded from (both restarted first):

```
python3 migration/parity/replay.py --mvc-url http://localhost:5100 --webforms-url http://localhost:5101 \
  --restart-cmd-mvc "migration/parity/legacy/start-legacy.sh mvc" \
  --restart-cmd-webforms "migration/parity/legacy/start-legacy.sh webforms" --report-dir migration/baseline/self-replay
```

Result: **128/128 checks passed** (MVC 80/80 = 79 flows + initial catalog; WebForms 48/48 = 47 flows + initial catalog), reproduced on two consecutive runs. Report: [`self-replay/parity-report.txt`](self-replay/parity-report.txt) / `.json`.

## Flows deliberately left out
- `GET /catalog/details/1` (lower-case route): 404 on Mono only, because Razor view lookup hits the case-sensitive file system; IIS serves it. Not legacy behaviour.
- Create POST with `Price=-5`: fails both `[RegularExpression]` and `[Range]`, and MVC 5 shows whichever error reflection returns first; on Mono that order changed between process starts, so the legacy output itself is not deterministic. Range-only (`1000001`) and regex-only (`12.345`) cases are captured instead.
- `About.aspx` / `Contact.aspx` (template pages outside the catalog, not reachable via the FriendlyUrls routes on Mono).
