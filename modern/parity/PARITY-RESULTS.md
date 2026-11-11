# MBA-3849 side-by-side parity results

Commit `cdd3ba69421b9750308236765ba3eba2b3e6253a`, generated 2026-11-11 23:10 UTC. Full report (request-level table, body diffs, screenshots): `parity-report.html` (attached to the PR / Jira, not committed).

| App | Mode | PASS | INTENTIONAL | FAIL |
|---|---|---|---|---|
| mvc | mock | 55 | 14 | 0 |
| webforms | mock | 30 | 20 | 0 |
| mvc | sql | 58 | 11 | 0 |
| webforms | sql | 38 | 12 | 0 |

## Environment

- Mono: Mono JIT compiler version 6.12.0.200 (tarball Tue Jul 11 21:37:50 UTC 2023)
- xsp4: Mono.WebServer2.dll 0.4.0.0
- .NET SDK: 10.0.301 (installed: 8.0.427,10.0.301)
- SQL Server image: mcr.microsoft.com/mssql/server:2022-latest (mcr.microsoft.com/mssql/server@sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd)
- Chromium (screenshots): Google Chrome for Testing 137.0.7118.2
- Python: Python 3.12.13
- OS: Linux 6.8.0-1061-aws

## Commands

```
git -C modern rev-parse HEAD
modern/parity/run-legacy.sh            # legacy scratch builds (mock + sql) under ~/mecca-legacy-run*
docker run -d --name mba3849-mssql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=*** -p 127.0.0.1:1433:1433 mcr.microsoft.com/mssql/server:2022-latest
python3 -m unittest discover -s modern/parity/tests -v
dotnet test modern/eShop.Catalog.Modern.sln
PARITY_SQL_PASSWORD=*** modern/parity/run-parity.sh all   # publish modern, fresh DBs, replay mock + sql
PARITY_NO_REPLAY=1 modern/parity/run-parity.sh mock --keep-running && python3 modern/parity/screenshots.py --out ~/parity-work/screenshots && modern/parity/run-parity.sh --stop
python3 modern/parity/report.py --results ~/parity-work/results --screenshots ~/parity-work/screenshots --html ~/parity-work/parity-report.html --md modern/parity/PARITY-RESULTS.md
```

## Non-PASS steps

| Mode | App | Step | Request | Legacy | Modern | Verdict | Reason |
|---|---|---|---|---|---|---|---|
| mock | mvc | `static-fonts-Montserrat-Regular-eot` | GET `/fonts/Montserrat-Regular.eot` | 200 | 200 | **INTENTIONAL** | S2/S6: .eot served as application/vnd.ms-fontobject (Mono serves its own fontobject variant). |
| mock | mvc | `api-files` | GET `/api/files` | 200 | 200 | **INTENTIONAL** | /api/files returns JSON instead of a BinaryFormatter payload (insecure deserialisation removed). |
| mock | mvc | `create-post-no-antiforgery` | POST `/Catalog/Create` | 500 | 400 | **INTENTIONAL** | Missing antiforgery token -> 400 with no data change (legacy 500). |
| mock | mvc | `edit-13-post-no-antiforgery` | POST `/Catalog/Edit/13` | 500 | 400 | **INTENTIONAL** | Missing antiforgery token -> 400 with no data change (legacy 500). |
| mock | mvc | `details-13-edited` | GET `/Catalog/Details/13` | 200 | 200 | **INTENTIONAL** | Decision B: legacy in-memory mock artefact. Legacy's mock service leaves CatalogBrand/CatalogType null on created/edited items until the list page renders, so legacy MVC shows blank Brand/Type and legacy WebForms throws a NullReferenceException (500) on Details/Delete; the later steps on item 13 inherit that state. Modern resolves the lookups (S4). SQL mode matches. |
| mock | mvc | `delete-13-get` | GET `/Catalog/Delete/13` | 200 | 200 | **INTENTIONAL** | Decision B: legacy in-memory mock artefact. Legacy's mock service leaves CatalogBrand/CatalogType null on created/edited items until the list page renders, so legacy MVC shows blank Brand/Type and legacy WebForms throws a NullReferenceException (500) on Details/Delete; the later steps on item 13 inherit that state. Modern resolves the lookups (S4). SQL mode matches. |
| mock | mvc | `delete-13-post-no-antiforgery` | POST `/Catalog/Delete/13` | 500 | 400 | **INTENTIONAL** | Missing antiforgery token -> 400 with no data change (legacy 500). |
| mock | mvc | `details-13-after-noaf-delete` | GET `/Catalog/Details/13` | 200 | 200 | **INTENTIONAL** | Decision B: legacy in-memory mock artefact. Legacy's mock service leaves CatalogBrand/CatalogType null on created/edited items until the list page renders, so legacy MVC shows blank Brand/Type and legacy WebForms throws a NullReferenceException (500) on Details/Delete; the later steps on item 13 inherit that state. Modern resolves the lookups (S4). SQL mode matches. |
| mock | mvc | `pic-1-after-overpost` | GET `/items/1/pic` | 200 | 200 | **INTENTIONAL** | PictureFileName cannot be over-posted or path-traversed: modern ignores the posted value and keeps serving the item's own picture. |
| mock | mvc | `details-1-after-overpost` | GET `/Catalog/Details/1` | 200 | 200 | **INTENTIONAL** | PictureFileName cannot be over-posted or path-traversed: modern ignores the posted value and keeps serving the item's own picture. |
| mock | mvc | `create-post-markup` | POST `/Catalog/Create` | 500 | 302 | **INTENTIONAL** | Markup in text fields never executes: modern accepts the value and renders it HTML-encoded (legacy MVC request validation answers 500). |
| mock | mvc | `details-last-markup` | GET `/Catalog/Details/{last_id}` | 200 | 200 | **INTENTIONAL** | Markup rendered HTML-encoded (&lt;script&gt;), never executed. |
| mock | mvc | `create-post-long-name` | POST `/Catalog/Create` | 302 | 200 | **INTENTIONAL** | PR #117/#118: Name over 50 characters gets a validation message and nothing is saved (legacy: 500 DbEntityValidationException in SQL mode; the legacy mock has no length check and saves it). |
| mock | mvc | `create-post-price-overflow` | POST `/Catalog/Create` | 500 | 200 | **INTENTIONAL** | PR #117: Price beyond Int32 gets the range validation message (legacy: 500 OverflowException). |
| mock | webforms | `about` | GET `/About` | 404 | 200 | **INTENTIONAL** | S6 (PR #111): extensionless /About and /Contact serve the pages (legacy only had the .aspx URL). |
| mock | webforms | `contact` | GET `/Contact` | 404 | 200 | **INTENTIONAL** | S6 (PR #111): extensionless /About and /Contact serve the pages (legacy only had the .aspx URL). |
| mock | webforms | `static-Images-brand-png` | GET `/Images/brand.png` | 404 | 200 | **INTENTIONAL** | S2/S6: static assets are served case-insensitively (legacy on a case-sensitive FS only matches the exact casing). |
| mock | webforms | `static-fonts-Montserrat-Regular-eot` | GET `/fonts/Montserrat-Regular.eot` | 200 | 200 | **INTENTIONAL** | S2/S6: .eot served as application/vnd.ms-fontobject (Mono serves its own fontobject variant). |
| mock | webforms | `create-post-price-text` | POST `/Catalog/Create` | 500 | 200 | **INTENTIONAL** | PR #118: unparseable Price gets the validation message (legacy: 500 FormatException from decimal.Parse when client-side validation is bypassed). |
| mock | webforms | `create-post-no-viewstate` | POST `/Catalog/Create` | 200 | 400 | **INTENTIONAL** | Missing antiforgery token -> 400 with no data change (legacy 500). WebForms: legacy has no antiforgery check on this post (ViewState is optional); modern requires the page-wide token. |
| mock | webforms | `details-13` | GET `/Catalog/Details/13` | 500 | 200 | **INTENTIONAL** | Decision B: legacy in-memory mock artefact. Legacy's mock service leaves CatalogBrand/CatalogType null on created/edited items until the list page renders, so legacy MVC shows blank Brand/Type and legacy WebForms throws a NullReferenceException (500) on Details/Delete; the later steps on item 13 inherit that state. Modern resolves the lookups (S4). SQL mode matches. |
| mock | webforms | `details-13-edited` | GET `/Catalog/Details/13` | 500 | 200 | **INTENTIONAL** | Decision B: legacy in-memory mock artefact. Legacy's mock service leaves CatalogBrand/CatalogType null on created/edited items until the list page renders, so legacy MVC shows blank Brand/Type and legacy WebForms throws a NullReferenceException (500) on Details/Delete; the later steps on item 13 inherit that state. Modern resolves the lookups (S4). SQL mode matches. |
| mock | webforms | `delete-13-get` | GET `/Catalog/Delete/13` | 500 | 200 | **INTENTIONAL** | Decision B: legacy in-memory mock artefact. Legacy's mock service leaves CatalogBrand/CatalogType null on created/edited items until the list page renders, so legacy MVC shows blank Brand/Type and legacy WebForms throws a NullReferenceException (500) on Details/Delete; the later steps on item 13 inherit that state. Modern resolves the lookups (S4). SQL mode matches. |
| mock | webforms | `delete-13-post-no-button` | POST `/Catalog/Delete/13` | None | 200 | **INTENTIONAL** | Decision B: legacy in-memory mock artefact. Legacy's mock service leaves CatalogBrand/CatalogType null on created/edited items until the list page renders, so legacy MVC shows blank Brand/Type and legacy WebForms throws a NullReferenceException (500) on Details/Delete; the later steps on item 13 inherit that state. Modern resolves the lookups (S4). SQL mode matches. \| harness error: no POST form on GET /Catalog/Delete/13 (status 500) / None |
| mock | webforms | `details-13-after-nobutton` | GET `/Catalog/Details/13` | 500 | 200 | **INTENTIONAL** | Decision B: legacy in-memory mock artefact. Legacy's mock service leaves CatalogBrand/CatalogType null on created/edited items until the list page renders, so legacy MVC shows blank Brand/Type and legacy WebForms throws a NullReferenceException (500) on Details/Delete; the later steps on item 13 inherit that state. Modern resolves the lookups (S4). SQL mode matches. |
| mock | webforms | `delete-13-post` | POST `/Catalog/Delete/13` | None | 302 | **INTENTIONAL** | Decision B: legacy in-memory mock artefact. Legacy's mock service leaves CatalogBrand/CatalogType null on created/edited items until the list page renders, so legacy MVC shows blank Brand/Type and legacy WebForms throws a NullReferenceException (500) on Details/Delete; the later steps on item 13 inherit that state. Modern resolves the lookups (S4). SQL mode matches. \| harness error: no POST form on GET /Catalog/Delete/13 (status 500) / None |
| mock | webforms | `list-after-delete` | GET `/Default/index/1/size/10` | 200 | 200 | **INTENTIONAL** | Decision B: legacy in-memory mock artefact. Legacy's mock service leaves CatalogBrand/CatalogType null on created/edited items until the list page renders, so legacy MVC shows blank Brand/Type and legacy WebForms throws a NullReferenceException (500) on Details/Delete; the later steps on item 13 inherit that state. Modern resolves the lookups (S4). SQL mode matches. |
| mock | webforms | `details-13-deleted` | GET `/Catalog/Details/13` | 200 | 500 | **INTENTIONAL** | Decision B: legacy in-memory mock artefact. Legacy's mock service leaves CatalogBrand/CatalogType null on created/edited items until the list page renders, so legacy MVC shows blank Brand/Type and legacy WebForms throws a NullReferenceException (500) on Details/Delete; the later steps on item 13 inherit that state. Modern resolves the lookups (S4). SQL mode matches. |
| mock | webforms | `details-last-markup` | GET `/Catalog/Details/{last_id}` | 200 | 200 | **INTENTIONAL** | Markup rendered HTML-encoded (&lt;script&gt;), never executed. |
| mock | webforms | `create-post-price-unparseable` | POST `/Catalog/Create` | 302 | 200 | **INTENTIONAL** | Decision A: WebForms server-side validation is enforced even when client-side JS is bypassed (same hardening family as #117/#118). Legacy code-behind checks Page.ModelState.IsValid, not Page.IsValid, so without the JS validators it saves the invalid item (302) or fails in EF (500); modern (S8, PR #114) re-renders with the legacy validator message and saves nothing. With JS on, both block the submit identically. |
| mock | webforms | `create-post-noname` | POST `/Catalog/Create` | 302 | 200 | **INTENTIONAL** | Decision A: WebForms server-side validation is enforced even when client-side JS is bypassed (same hardening family as #117/#118). Legacy code-behind checks Page.ModelState.IsValid, not Page.IsValid, so without the JS validators it saves the invalid item (302) or fails in EF (500); modern (S8, PR #114) re-renders with the legacy validator message and saves nothing. With JS on, both block the submit identically. |
| mock | webforms | `create-post-price-range` | POST `/Catalog/Create` | 302 | 200 | **INTENTIONAL** | Decision A: WebForms server-side validation is enforced even when client-side JS is bypassed (same hardening family as #117/#118). Legacy code-behind checks Page.ModelState.IsValid, not Page.IsValid, so without the JS validators it saves the invalid item (302) or fails in EF (500); modern (S8, PR #114) re-renders with the legacy validator message and saves nothing. With JS on, both block the submit identically. |
| mock | webforms | `create-post-stock-range` | POST `/Catalog/Create` | 302 | 200 | **INTENTIONAL** | Decision A: WebForms server-side validation is enforced even when client-side JS is bypassed (same hardening family as #117/#118). Legacy code-behind checks Page.ModelState.IsValid, not Page.IsValid, so without the JS validators it saves the invalid item (302) or fails in EF (500); modern (S8, PR #114) re-renders with the legacy validator message and saves nothing. With JS on, both block the submit identically. |
| mock | webforms | `edit-1-post-price-range` | POST `/Catalog/Edit/1` | 302 | 200 | **INTENTIONAL** | Decision A: WebForms server-side validation is enforced even when client-side JS is bypassed (same hardening family as #117/#118). Legacy code-behind checks Page.ModelState.IsValid, not Page.IsValid, so without the JS validators it saves the invalid item (302) or fails in EF (500); modern (S8, PR #114) re-renders with the legacy validator message and saves nothing. With JS on, both block the submit identically. |
| sql | mvc | `static-fonts-Montserrat-Regular-eot` | GET `/fonts/Montserrat-Regular.eot` | 200 | 200 | **INTENTIONAL** | S2/S6: .eot served as application/vnd.ms-fontobject (Mono serves its own fontobject variant). |
| sql | mvc | `api-files` | GET `/api/files` | 200 | 200 | **INTENTIONAL** | /api/files returns JSON instead of a BinaryFormatter payload (insecure deserialisation removed). |
| sql | mvc | `create-post-no-antiforgery` | POST `/Catalog/Create` | 500 | 400 | **INTENTIONAL** | Missing antiforgery token -> 400 with no data change (legacy 500). |
| sql | mvc | `edit-13-post-no-antiforgery` | POST `/Catalog/Edit/13` | 500 | 400 | **INTENTIONAL** | Missing antiforgery token -> 400 with no data change (legacy 500). |
| sql | mvc | `delete-13-post-no-antiforgery` | POST `/Catalog/Delete/13` | 500 | 400 | **INTENTIONAL** | Missing antiforgery token -> 400 with no data change (legacy 500). |
| sql | mvc | `pic-1-after-overpost` | GET `/items/1/pic` | 200 | 200 | **INTENTIONAL** | PictureFileName cannot be over-posted or path-traversed: modern ignores the posted value and keeps serving the item's own picture. |
| sql | mvc | `details-1-after-overpost` | GET `/Catalog/Details/1` | 200 | 200 | **INTENTIONAL** | PictureFileName cannot be over-posted or path-traversed: modern ignores the posted value and keeps serving the item's own picture. |
| sql | mvc | `create-post-markup` | POST `/Catalog/Create` | 500 | 302 | **INTENTIONAL** | Markup in text fields never executes: modern accepts the value and renders it HTML-encoded (legacy MVC request validation answers 500). |
| sql | mvc | `details-last-markup` | GET `/Catalog/Details/{last_id}` | 200 | 200 | **INTENTIONAL** | Markup rendered HTML-encoded (&lt;script&gt;), never executed. |
| sql | mvc | `create-post-long-name` | POST `/Catalog/Create` | 500 | 200 | **INTENTIONAL** | PR #117/#118: Name over 50 characters gets a validation message and nothing is saved (legacy: 500 DbEntityValidationException in SQL mode; the legacy mock has no length check and saves it). |
| sql | mvc | `create-post-price-overflow` | POST `/Catalog/Create` | 500 | 200 | **INTENTIONAL** | PR #117: Price beyond Int32 gets the range validation message (legacy: 500 OverflowException). |
| sql | webforms | `about` | GET `/About` | 404 | 200 | **INTENTIONAL** | S6 (PR #111): extensionless /About and /Contact serve the pages (legacy only had the .aspx URL). |
| sql | webforms | `contact` | GET `/Contact` | 404 | 200 | **INTENTIONAL** | S6 (PR #111): extensionless /About and /Contact serve the pages (legacy only had the .aspx URL). |
| sql | webforms | `static-Images-brand-png` | GET `/Images/brand.png` | 404 | 200 | **INTENTIONAL** | S2/S6: static assets are served case-insensitively (legacy on a case-sensitive FS only matches the exact casing). |
| sql | webforms | `static-fonts-Montserrat-Regular-eot` | GET `/fonts/Montserrat-Regular.eot` | 200 | 200 | **INTENTIONAL** | S2/S6: .eot served as application/vnd.ms-fontobject (Mono serves its own fontobject variant). |
| sql | webforms | `create-post-price-text` | POST `/Catalog/Create` | 500 | 200 | **INTENTIONAL** | PR #118: unparseable Price gets the validation message (legacy: 500 FormatException from decimal.Parse when client-side validation is bypassed). |
| sql | webforms | `create-post-no-viewstate` | POST `/Catalog/Create` | 200 | 400 | **INTENTIONAL** | Missing antiforgery token -> 400 with no data change (legacy 500). WebForms: legacy has no antiforgery check on this post (ViewState is optional); modern requires the page-wide token. |
| sql | webforms | `details-last-markup` | GET `/Catalog/Details/{last_id}` | 200 | 200 | **INTENTIONAL** | Markup rendered HTML-encoded (&lt;script&gt;), never executed. |
| sql | webforms | `create-post-price-unparseable` | POST `/Catalog/Create` | 302 | 200 | **INTENTIONAL** | Decision A: WebForms server-side validation is enforced even when client-side JS is bypassed (same hardening family as #117/#118). Legacy code-behind checks Page.ModelState.IsValid, not Page.IsValid, so without the JS validators it saves the invalid item (302) or fails in EF (500); modern (S8, PR #114) re-renders with the legacy validator message and saves nothing. With JS on, both block the submit identically. |
| sql | webforms | `create-post-noname` | POST `/Catalog/Create` | 302 | 200 | **INTENTIONAL** | Decision A: WebForms server-side validation is enforced even when client-side JS is bypassed (same hardening family as #117/#118). Legacy code-behind checks Page.ModelState.IsValid, not Page.IsValid, so without the JS validators it saves the invalid item (302) or fails in EF (500); modern (S8, PR #114) re-renders with the legacy validator message and saves nothing. With JS on, both block the submit identically. |
| sql | webforms | `create-post-price-range` | POST `/Catalog/Create` | 302 | 200 | **INTENTIONAL** | Decision A: WebForms server-side validation is enforced even when client-side JS is bypassed (same hardening family as #117/#118). Legacy code-behind checks Page.ModelState.IsValid, not Page.IsValid, so without the JS validators it saves the invalid item (302) or fails in EF (500); modern (S8, PR #114) re-renders with the legacy validator message and saves nothing. With JS on, both block the submit identically. |
| sql | webforms | `create-post-stock-range` | POST `/Catalog/Create` | 500 | 200 | **INTENTIONAL** | Decision A: WebForms server-side validation is enforced even when client-side JS is bypassed (same hardening family as #117/#118). Legacy code-behind checks Page.ModelState.IsValid, not Page.IsValid, so without the JS validators it saves the invalid item (302) or fails in EF (500); modern (S8, PR #114) re-renders with the legacy validator message and saves nothing. With JS on, both block the submit identically. |
| sql | webforms | `edit-1-post-price-range` | POST `/Catalog/Edit/1` | 500 | 200 | **INTENTIONAL** | Decision A: WebForms server-side validation is enforced even when client-side JS is bypassed (same hardening family as #117/#118). Legacy code-behind checks Page.ModelState.IsValid, not Page.IsValid, so without the JS validators it saves the invalid item (302) or fails in EF (500); modern (S8, PR #114) re-renders with the legacy validator message and saves nothing. With JS on, both block the submit identically. |

## Browser recording

Testing-agent recording (SQL mode, all four apps): create -> details -> edit -> delete matched legacy vs modern; the only visible difference was the WebForms footer timestamp. Recording and paired screenshots: https://github.com/COG-GTM/eShopModernizing/pull/120#issuecomment-6809353204

## Not tested

- **Legacy on Windows/IIS**: Legacy runs on Mono 6.12 + xsp4 (the only Linux option); Mono-specific behaviour (host error pages, MIME table) is normalised or listed as intentional.
- **modern/deploy compose stack**: Replay targets the published DLLs on the VM; the containers are exercised by the S9 compose smoke tests, not by this run.
- **Picture upload**: Neither legacy app supports uploads ('Uploading images not allowed for this version').
- **Concurrent users / load**: Out of scope for behavioural parity.
- **S9 SqlServerReadinessTests**: Skipped in `dotnet test` without a SQL connection string; SQL mode itself is covered by the replay against SQL Server 2022.
