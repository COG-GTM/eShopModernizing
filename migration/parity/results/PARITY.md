# Legacy vs migrated parity (side-by-side replay)

LEGACY (.NET Framework 4.7.2 on Mono 6.12 + xsp4) and MIGRATED (.NET 8 Linux containers built from src-net8) were run side by side on identical seed data and every transcript in migration/baseline/transcripts/ (126 flows + 2 initial-state checks = 128 checks) was replayed against BOTH sides with migration/parity/side_by_side.py (direct legacy-vs-migrated diff) and migration/parity/replay.py (each side vs the recorded legacy baseline). Both apps were restarted before each app's mutating sequence. Nothing below is claimed beyond what these runs observed.

## Headline

- MOCK mode (UseMockData=true, PreconfiguredData seed): 128/128 checks contract-identical, 24/24 full data-state snapshots identical in row order; legacy vs baseline 128/128, migrated vs baseline 128/128. Verdicts: 53 identical, 75 equivalent, 0 different (unchanged by the S2/D5 fixes, as expected: S2 is SQL-only and bundle bytes are a strict-level check).
- MOCK mode strict (whole normalised response incl. error bodies + full Content-Type) 53/128 identical; headers 55/128 identical. Every remaining strict/header diff is classified below; none changes status, Location, page content, form fields, validation messages, JSON or /api/files bytes.
- Before the src-net8 header fixes in PR #78 the same mock run gave strict 40/128 and headers 2/128 (F1-F4).
- SQL mode (SQL Server 2022, UseMockData=false, both apps on the same database, reset before each sequence): 126/128 contract-identical (was 120/128 before the S2 fix), 24/24 data-state identical (was 19/24). Verdicts: 53 identical, 73 equivalent, 2 different (was 53/67/8). The 2 remaining contract diffs are S1 negative page (pre-existing legacy defect: EF6 negative OFFSET -> 500).
- S2 (EF6 validate-on-save) FIXED and observed: wf-create-post-invalid-negative-stock is now 500 on both sides with no row written, and no downstream WebForms data-state drift. D5 (bundles) FIXED and observed: all four script bundles are minified (/bundles/jqueryval and /bundles/modernizr byte-identical to WebGrease, /bundles/jquery 87,464 vs 87,460 bytes, /bundles/bootstrap same length, different bytes); /Content/css is minified on the migrated side while legacy WebGrease fails on Bootstrap 4 custom properties and serves it unminified (D5b, legacy defect).
- OPEN regressions: none.
- Migrated tests: 205 passed / 0 failed / 0 skipped. Legacy: no unit/integration test project exists; the 128-check transcript suite is the legacy baseline suite and passed 128/128 on legacy in mock mode.

## Environment

- Legacy MVC: throwaway copy /tmp/legacy-mvc via migration/parity/legacy/setup-legacy.sh, xsp4 :5100. Harness-only edits (never committed): UseMockData=true, Views/Web.config System.Web.Mvc 5.2.3.0->5.2.7.0, Content/Site.css copied to site.css, Pics symlink for case-sensitive paths. msbuild warns it cannot resolve System.Web.Entity (unused; app runs).
- Legacy WebForms: throwaway copy, xsp4 :5101, Mono compatibility edits applied by setup-legacy.sh (documented there).
- Migrated: docker compose -f src-net8/docker-compose.yml --profile mock up (images eshopnet8/mvc:local, eshopnet8/webforms:local on mcr.microsoft.com/dotnet/aspnet:8.0), MVC :5200, WebForms :5201. Container route worked; dotnet run was not needed.
- SQL: --profile sql (mcr.microsoft.com/mssql/server:2022-latest on :1433, migrated apps UseMockData=false). Legacy pointed at the same database by migration/parity/sql/setup-legacy-sql.sh (UseMockData=false, System.Data.SqlClient, Encrypt=False). Mono CONNECTED successfully.
- Restart = state reset: mock -> restart app (fresh singleton); SQL -> migration/parity/sql/reset-sql.sh drops the catalog DB, then the restarted app's initializer recreates and reseeds it.

## Normalisations (the only things not compared)

- Antiforgery / viewstate fields (__RequestVerificationToken, __VIEWSTATE, __VIEWSTATEGENERATOR, __VIEWSTATEENCRYPTED, __EVENTVALIDATION, __EVENTTARGET, __EVENTARGUMENT, __LASTFOCUS, __PREVIOUSPAGE): values dropped; fetched fresh from each side's form page before each POST.
- Host/port: each side's own origin in absolute URLs (Location, links, img src, text) -> {base}.
- Footer session info `<machine>, <M/d/yyyy h:mm:ss tt>` -> `<machine>, <session-start>` (machine name + session start time).
- HTML is compared as a structured projection (title, visible text with whitespace collapsed and script/style/hidden elements removed, headings, links, images, tables, dl, forms, validation messages, assets); markup formatting is not compared.
- Transport headers not compared: date, server, content-length, connection, keep-alive, transfer-encoding, x-aspnet-version, x-aspnetmvc-version, x-powered-by, x-sourcefiles, etag, last-modified, accept-ranges. Set-Cookie compared by cookie name only (values are random).
- Absolute `Expires` dates (bundles: request time + 1 year) -> `<http-date>`; `Expires: -1` compared literally.
- Contract level (CONTRACT.md parity rules) additionally ignores 3xx/>=400 bodies, Content-Type parameters, and bundle bytes; the strict and header levels do NOT, and their diffs are all listed and classified.

## MOCK mode results

| metric | value |
|---|---|
| generated_at | 2026-10-23T05:52:42+00:00 |
| checks | 128 |
| contract_match | 128 |
| strict_match | 53 |
| headers_match | 55 |
| data_state_checks | 24 |
| data_state_match | 24 |
| legacy_vs_baseline_pass | 128 |
| migrated_vs_baseline_pass | 128 |

Verdicts: identical 53, equivalent 75

| flow | description | legacy | migrated | verdict | diff summary |
|---|---|---|---|---|---|
| `mvc-initial-state` | Initial seeded catalog (full list) | 200 | 200 | identical | none |
| `mvc-index-root` | Catalog index, default paging (GET /) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-controller` | Catalog index via /Catalog | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-action` | Catalog index via /Catalog/Index | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-size5-page0` | Index pageSize=5 pageIndex=0 | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-size5-page1` | Index pageSize=5 pageIndex=1 | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-size5-page2` | Index pageSize=5 pageIndex=2 (last, partial) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-size10-page1` | Index pageSize=10 pageIndex=1 | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-size100` | Index pageSize=100 (all rows) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-out-of-range` | Index pageIndex beyond last page (empty table) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-negative-page` | Index negative pageIndex | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-size0` | Index pageSize=0 (legacy divides by zero) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s) |
| `mvc-index-size-invalid` | Index pageSize not an int (binding falls back to default) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-details-1` | Details GET, valid id | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-details-12` | Details GET, last seeded id | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-details-missing` | Details GET, missing id -> 404 | 404 | 404 | identical | none |
| `mvc-details-noid` | Details GET, absent id -> 400 | 400 | 400 | identical | none |
| `mvc-details-badid` | Details GET, non-int id -> 400 | 400 | 400 | identical | none |
| `mvc-edit-get-1` | Edit GET, valid id | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-edit-get-missing` | Edit GET, missing id -> 404 | 404 | 404 | identical | none |
| `mvc-edit-get-noid` | Edit GET, absent id -> 400 | 400 | 400 | identical | none |
| `mvc-delete-get-1` | Delete GET, valid id | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-delete-get-missing` | Delete GET, missing id -> 404 | 404 | 404 | identical | none |
| `mvc-delete-get-noid` | Delete GET, absent id -> 400 | 400 | 400 | identical | none |
| `mvc-create-get` | Create GET (form + antiforgery token) | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-pic-1` | Pic route, valid id | 200 | 200 | identical | none |
| `mvc-pic-12` | Pic route, last seeded id | 200 | 200 | identical | none |
| `mvc-pic-zero` | Pic route, id 0 -> 400 | 400 | 400 | identical | none |
| `mvc-pic-negative` | Pic route, negative id -> 400 | 400 | 400 | identical | none |
| `mvc-pic-missing` | Pic route, missing item -> 404 | 404 | 404 | identical | none |
| `mvc-pic-nonint` | Pic route, non-int id fails the route constraint -> 404 | 404 | 404 | equivalent | D4-error-body: 3 diff(s) |
| `mvc-api-brands` | GET /api/brands (JSON) | 200 | 200 | identical | none |
| `mvc-api-brand-found` | GET /api/brands/2 | 200 | 200 | identical | none |
| `mvc-api-brand-missing` | GET /api/brands/99 -> 404 | 404 | 404 | identical | none |
| `mvc-api-brand-delete-found` | DELETE /api/brands/2 -> 200 (demo: nothing deleted) | 200 | 200 | identical | none |
| `mvc-api-brand-delete-missing` | DELETE /api/brands/99 -> 404 | 404 | 404 | identical | none |
| `mvc-api-brands-after-delete` | GET /api/brands after DELETE (still 5 brands) | 200 | 200 | identical | none |
| `mvc-api-files` | GET /api/files (BinaryFormatter/NRBF payload, byte-exact) | 200 | 200 | equivalent | D7-api-files-mime: 3 diff(s) |
| `mvc-api-hello` | GET /api (CatalogController2: never discovered by MVC 5 -> 404) | 404 | 404 | equivalent | D4-error-body: 3 diff(s) |
| `mvc-static-content-bootstrap-css` | Static asset /Content/bootstrap.css | 200 | 200 | identical | none |
| `mvc-static-content-custom-css` | Static asset /Content/custom.css | 200 | 200 | identical | none |
| `mvc-static-content-base-css` | Static asset /Content/base.css | 200 | 200 | identical | none |
| `mvc-static-content-site-css` | Static asset /Content/site.css | 200 | 200 | identical | none |
| `mvc-static-scripts-modernizr-2-6-2-js` | Static asset /Scripts/modernizr-2.6.2.js | 200 | 200 | identical | none |
| `mvc-static-scripts-modernizr-2-8-3-js` | Static asset /Scripts/modernizr-2.8.3.js | 200 | 200 | identical | none |
| `mvc-static-scripts-jquery-3-3-1-js` | Static asset /Scripts/jquery-3.3.1.js | 200 | 200 | identical | none |
| `mvc-static-scripts-bootstrap-js` | Static asset /Scripts/bootstrap.js | 200 | 200 | identical | none |
| `mvc-static-scripts-respond-js` | Static asset /Scripts/respond.js | 200 | 200 | identical | none |
| `mvc-static-scripts-jquery-validate-js` | Static asset /Scripts/jquery.validate.js | 200 | 200 | identical | none |
| `mvc-static-scripts-jquery-validate-unobtrusive-js` | Static asset /Scripts/jquery.validate.unobtrusive.js | 200 | 200 | identical | none |
| `mvc-static-images-brand-png` | Static asset /images/brand.png | 200 | 200 | identical | none |
| `mvc-static-images-brand_dark-png` | Static asset /images/brand_dark.png | 200 | 200 | identical | none |
| `mvc-static-images-main_footer_text-png` | Static asset /images/main_footer_text.png | 200 | 200 | identical | none |
| `mvc-static-favicon-ico` | Static asset /favicon.ico | 200 | 200 | identical | none |
| `mvc-static-pics-1-png` | Static asset /Pics/1.png | 200 | 200 | identical | none |
| `mvc-bundle-content-css` | Bundle endpoint /Content/css (status + type only; WebGrease minified output is not a contract) | 200 | 200 | equivalent | D1-charset: 2 diff(s); D5b-css-minify-failure: 2 diff(s) |
| `mvc-bundle-bundles-jquery` | Bundle endpoint /bundles/jquery (status + type only; WebGrease minified output is not a contract) | 200 | 200 | equivalent | D1-charset: 2 diff(s); D5-bundles: 2 diff(s) |
| `mvc-bundle-bundles-jqueryval` | Bundle endpoint /bundles/jqueryval (status + type only; WebGrease minified output is not a contract) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-bundle-bundles-modernizr` | Bundle endpoint /bundles/modernizr (status + type only; WebGrease minified output is not a contract) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-bundle-bundles-bootstrap` | Bundle endpoint /bundles/bootstrap (status + type only; WebGrease minified output is not a contract) | 200 | 200 | equivalent | D1-charset: 2 diff(s); D5-bundles: 1 diff(s) |
| `mvc-create-post-valid` | Create POST valid -> 302 / and item 13 added | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `mvc-details-created` | Details of created item 13 | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-pic-created` | Pic of created item (dummy.png) | 200 | 200 | identical | none |
| `mvc-create-post-invalid-missing-name` | Create POST invalid (missing name) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-create-post-invalid-bad-price` | Create POST invalid (non-numeric price) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-create-post-invalid-price-over-max` | Create POST invalid (price above 1,000,000) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-create-post-invalid-price-3dp` | Create POST invalid (price with 3 decimals) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-create-post-invalid-stock-out-of-range` | Create POST invalid (stock above 10 million) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-create-post-invalid-restock-negative` | Create POST invalid (negative restock) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-create-post-no-antiforgery` | Create POST without antiforgery token -> rejected | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s) |
| `mvc-edit-post-valid` | Edit POST valid (item 1) -> 302 / | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `mvc-details-edited` | Details of edited item 1 | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-edit-post-invalid-missing-name` | Edit POST invalid (missing name, item 2) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-edit-post-invalid-negative-stock` | Edit POST invalid (negative stock, item 2) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-edit-post-invalid-bad-price` | Edit POST invalid (non-numeric price, item 2) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-delete-get-created` | Delete GET of created item 13 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-delete-post` | Delete POST item 13 -> 302 / | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `mvc-delete-post-missing` | Delete POST missing id 999 | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `mvc-details-deleted` | Details of deleted item 13 -> 404 | 404 | 404 | identical | none |
| `mvc-index-after-mutations` | Index after all mutations | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `webforms-initial-state` | Initial seeded catalog (full list) | 200 | 200 | identical | none |
| `wf-list-root` | WebForms catalog list, GET / | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-default` | WebForms catalog list, /Default | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size10-page0` | WebForms catalog list, index 0 size 10 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size10-page1` | WebForms catalog list, index 1 size 10 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size5-page0` | WebForms catalog list, index 0 size 5 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size5-page1` | WebForms catalog list, index 1 size 5 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size5-page2` | WebForms catalog list, index 2 size 5 (partial) | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size100` | WebForms catalog list, size 100 (all rows) | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-out-of-range` | WebForms catalog list, index beyond last page | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-negative-page` | WebForms catalog list, negative index | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size0` | WebForms catalog list, size 0 (divide by zero) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-details-get-1` | WebForms Details page, valid id | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-details-get-missing` | WebForms Details page, missing id (legacy NullReferenceException -> 500) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-edit-get-1` | WebForms Edit page, valid id | 200 | 200 | equivalent | D1-charset: 2 diff(s); D6-wf-validator-script: 1 diff(s); D2-nocache: 2 diff(s) |
| `wf-edit-get-missing` | WebForms Edit page, missing id (legacy NullReferenceException -> 500) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-delete-get-1` | WebForms Delete page, valid id | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-delete-get-missing` | WebForms Delete page, missing id (legacy NullReferenceException -> 500) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-details-get-12` | WebForms Details page, last seeded id | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-details-get-nonint` | WebForms Details page, non-int id (legacy FormatException -> 500) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-create-get` | WebForms Create page | 200 | 200 | equivalent | D1-charset: 2 diff(s); D6-wf-validator-script: 1 diff(s); D2-nocache: 2 diff(s) |
| `wf-static-pics-1-png` | WebForms static asset /Pics/1.png | 200 | 200 | identical | none |
| `wf-static-pics-12-png` | WebForms static asset /Pics/12.png | 200 | 200 | identical | none |
| `wf-static-pics-dummy-png` | WebForms static asset /Pics/dummy.png | 200 | 200 | identical | none |
| `wf-static-images-brand-png` | WebForms static asset /images/brand.png | 200 | 200 | identical | none |
| `wf-static-images-brand_dark-png` | WebForms static asset /images/brand_dark.png | 200 | 200 | identical | none |
| `wf-static-images-main_footer_text-png` | WebForms static asset /images/main_footer_text.png | 200 | 200 | identical | none |
| `wf-static-favicon-ico` | WebForms static asset /favicon.ico | 200 | 200 | identical | none |
| `wf-static-content-bootstrap-css` | WebForms static asset /Content/bootstrap.css | 200 | 200 | identical | none |
| `wf-static-content-custom-css` | WebForms static asset /Content/custom.css | 200 | 200 | identical | none |
| `wf-static-content-base-css` | WebForms static asset /Content/base.css | 200 | 200 | identical | none |
| `wf-static-content-site-css` | WebForms static asset /Content/site.css | 200 | 200 | identical | none |
| `wf-static-scripts-jquery-3-3-1-js` | WebForms static asset /Scripts/jquery-3.3.1.js | 200 | 200 | identical | none |
| `wf-static-scripts-bootstrap-js` | WebForms static asset /Scripts/bootstrap.js | 200 | 200 | identical | none |
| `wf-static-scripts-respond-js` | WebForms static asset /Scripts/respond.js | 200 | 200 | identical | none |
| `wf-static-scripts-modernizr-2-8-3-js` | WebForms static asset /Scripts/modernizr-2.8.3.js | 200 | 200 | identical | none |
| `wf-create-post-valid` | WebForms Create postback valid -> redirect / | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `wf-details-created` | WebForms Details of created item 13 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-create-post-invalid-missing-name` | WebForms Create postback (missing name; server-side checks only) | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `wf-create-post-invalid-bad-price` | WebForms Create postback (non-numeric price; server-side checks only) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-create-post-invalid-negative-stock` | WebForms Create postback (negative stock; server-side checks only) | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `wf-edit-post-valid` | WebForms Edit postback valid (item 3) -> redirect / | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `wf-details-edited` | WebForms Details of edited item 3 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-edit-post-invalid-bad-price` | WebForms Edit postback (non-numeric price, item 4) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-edit-post-invalid-missing-name` | WebForms Edit postback (missing name, item 4) | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `wf-delete-get-created` | WebForms Delete page of created item 13 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-delete-post` | WebForms Delete postback item 13 -> redirect / | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `wf-list-after-mutations` | WebForms list after all postbacks | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |

## SQL mode results

| metric | value |
|---|---|
| generated_at | 2026-10-23T05:54:20+00:00 |
| checks | 128 |
| contract_match | 126 |
| strict_match | 53 |
| headers_match | 55 |
| data_state_checks | 24 |
| data_state_match | 24 |
| legacy_vs_baseline_pass | 102 |
| migrated_vs_baseline_pass | 102 |

Verdicts: identical 53, equivalent 73, different 2

SQL-mode limitations:

- The recorded baseline transcripts were captured in mock mode, so SQL-mode replay.py vs baseline is 102/128 on BOTH sides with the same 26 flows failing on each (SQL decimal(18,2) renders 19.50 where the mock renders 19.5 on WebForms; delete of a missing id is 500 in SQL vs 302 in mock on both sides; plus S1/S2/F5 flows). SQL parity is therefore judged legacy-vs-migrated directly.
- EF6 seed scripts are referenced with Windows backslash paths (Models\Infrastructure\dbo.*.Sequence.sql); on Linux the legacy SQL setup copies them to literal-backslash filenames in the throwaway copy so EF6 can seed (harness-only).
- Both apps share one database; reset-sql.sh drops it before each side's sequence and the restarted app reseeds it, so each sequence starts from the app's own seed. Seeds matched (initial-state checks identical on both apps).
- SA password generated per run into an untracked env file (src-net8/docker/sql.env.example documents the variable); not committed.

| flow | description | legacy | migrated | verdict | diff summary |
|---|---|---|---|---|---|
| `mvc-initial-state` | Initial seeded catalog (full list) | 200 | 200 | identical | none |
| `mvc-index-root` | Catalog index, default paging (GET /) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-controller` | Catalog index via /Catalog | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-action` | Catalog index via /Catalog/Index | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-size5-page0` | Index pageSize=5 pageIndex=0 | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-size5-page1` | Index pageSize=5 pageIndex=1 | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-size5-page2` | Index pageSize=5 pageIndex=2 (last, partial) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-size10-page1` | Index pageSize=10 pageIndex=1 | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-size100` | Index pageSize=100 (all rows) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-out-of-range` | Index pageIndex beyond last page (empty table) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-index-negative-page` | Index negative pageIndex | 500 | 200 | different | S1-negative-page: 8 diff(s); D1-charset: 2 diff(s) |
| `mvc-index-size0` | Index pageSize=0 (legacy divides by zero) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s) |
| `mvc-index-size-invalid` | Index pageSize not an int (binding falls back to default) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-details-1` | Details GET, valid id | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-details-12` | Details GET, last seeded id | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-details-missing` | Details GET, missing id -> 404 | 404 | 404 | identical | none |
| `mvc-details-noid` | Details GET, absent id -> 400 | 400 | 400 | identical | none |
| `mvc-details-badid` | Details GET, non-int id -> 400 | 400 | 400 | identical | none |
| `mvc-edit-get-1` | Edit GET, valid id | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-edit-get-missing` | Edit GET, missing id -> 404 | 404 | 404 | identical | none |
| `mvc-edit-get-noid` | Edit GET, absent id -> 400 | 400 | 400 | identical | none |
| `mvc-delete-get-1` | Delete GET, valid id | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-delete-get-missing` | Delete GET, missing id -> 404 | 404 | 404 | identical | none |
| `mvc-delete-get-noid` | Delete GET, absent id -> 400 | 400 | 400 | identical | none |
| `mvc-create-get` | Create GET (form + antiforgery token) | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-pic-1` | Pic route, valid id | 200 | 200 | identical | none |
| `mvc-pic-12` | Pic route, last seeded id | 200 | 200 | identical | none |
| `mvc-pic-zero` | Pic route, id 0 -> 400 | 400 | 400 | identical | none |
| `mvc-pic-negative` | Pic route, negative id -> 400 | 400 | 400 | identical | none |
| `mvc-pic-missing` | Pic route, missing item -> 404 | 404 | 404 | identical | none |
| `mvc-pic-nonint` | Pic route, non-int id fails the route constraint -> 404 | 404 | 404 | equivalent | D4-error-body: 3 diff(s) |
| `mvc-api-brands` | GET /api/brands (JSON) | 200 | 200 | identical | none |
| `mvc-api-brand-found` | GET /api/brands/2 | 200 | 200 | identical | none |
| `mvc-api-brand-missing` | GET /api/brands/99 -> 404 | 404 | 404 | identical | none |
| `mvc-api-brand-delete-found` | DELETE /api/brands/2 -> 200 (demo: nothing deleted) | 200 | 200 | identical | none |
| `mvc-api-brand-delete-missing` | DELETE /api/brands/99 -> 404 | 404 | 404 | identical | none |
| `mvc-api-brands-after-delete` | GET /api/brands after DELETE (still 5 brands) | 200 | 200 | identical | none |
| `mvc-api-files` | GET /api/files (BinaryFormatter/NRBF payload, byte-exact) | 200 | 200 | equivalent | D7-api-files-mime: 3 diff(s) |
| `mvc-api-hello` | GET /api (CatalogController2: never discovered by MVC 5 -> 404) | 404 | 404 | equivalent | D4-error-body: 3 diff(s) |
| `mvc-static-content-bootstrap-css` | Static asset /Content/bootstrap.css | 200 | 200 | identical | none |
| `mvc-static-content-custom-css` | Static asset /Content/custom.css | 200 | 200 | identical | none |
| `mvc-static-content-base-css` | Static asset /Content/base.css | 200 | 200 | identical | none |
| `mvc-static-content-site-css` | Static asset /Content/site.css | 200 | 200 | identical | none |
| `mvc-static-scripts-modernizr-2-6-2-js` | Static asset /Scripts/modernizr-2.6.2.js | 200 | 200 | identical | none |
| `mvc-static-scripts-modernizr-2-8-3-js` | Static asset /Scripts/modernizr-2.8.3.js | 200 | 200 | identical | none |
| `mvc-static-scripts-jquery-3-3-1-js` | Static asset /Scripts/jquery-3.3.1.js | 200 | 200 | identical | none |
| `mvc-static-scripts-bootstrap-js` | Static asset /Scripts/bootstrap.js | 200 | 200 | identical | none |
| `mvc-static-scripts-respond-js` | Static asset /Scripts/respond.js | 200 | 200 | identical | none |
| `mvc-static-scripts-jquery-validate-js` | Static asset /Scripts/jquery.validate.js | 200 | 200 | identical | none |
| `mvc-static-scripts-jquery-validate-unobtrusive-js` | Static asset /Scripts/jquery.validate.unobtrusive.js | 200 | 200 | identical | none |
| `mvc-static-images-brand-png` | Static asset /images/brand.png | 200 | 200 | identical | none |
| `mvc-static-images-brand_dark-png` | Static asset /images/brand_dark.png | 200 | 200 | identical | none |
| `mvc-static-images-main_footer_text-png` | Static asset /images/main_footer_text.png | 200 | 200 | identical | none |
| `mvc-static-favicon-ico` | Static asset /favicon.ico | 200 | 200 | identical | none |
| `mvc-static-pics-1-png` | Static asset /Pics/1.png | 200 | 200 | identical | none |
| `mvc-bundle-content-css` | Bundle endpoint /Content/css (status + type only; WebGrease minified output is not a contract) | 200 | 200 | equivalent | D1-charset: 2 diff(s); D5b-css-minify-failure: 2 diff(s) |
| `mvc-bundle-bundles-jquery` | Bundle endpoint /bundles/jquery (status + type only; WebGrease minified output is not a contract) | 200 | 200 | equivalent | D1-charset: 2 diff(s); D5-bundles: 2 diff(s) |
| `mvc-bundle-bundles-jqueryval` | Bundle endpoint /bundles/jqueryval (status + type only; WebGrease minified output is not a contract) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-bundle-bundles-modernizr` | Bundle endpoint /bundles/modernizr (status + type only; WebGrease minified output is not a contract) | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-bundle-bundles-bootstrap` | Bundle endpoint /bundles/bootstrap (status + type only; WebGrease minified output is not a contract) | 200 | 200 | equivalent | D1-charset: 2 diff(s); D5-bundles: 1 diff(s) |
| `mvc-create-post-valid` | Create POST valid -> 302 / and item 13 added | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `mvc-details-created` | Details of created item 13 | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-pic-created` | Pic of created item (dummy.png) | 200 | 200 | identical | none |
| `mvc-create-post-invalid-missing-name` | Create POST invalid (missing name) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-create-post-invalid-bad-price` | Create POST invalid (non-numeric price) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-create-post-invalid-price-over-max` | Create POST invalid (price above 1,000,000) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-create-post-invalid-price-3dp` | Create POST invalid (price with 3 decimals) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-create-post-invalid-stock-out-of-range` | Create POST invalid (stock above 10 million) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-create-post-invalid-restock-negative` | Create POST invalid (negative restock) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-create-post-no-antiforgery` | Create POST without antiforgery token -> rejected | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s) |
| `mvc-edit-post-valid` | Edit POST valid (item 1) -> 302 / | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `mvc-details-edited` | Details of edited item 1 | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `mvc-edit-post-invalid-missing-name` | Edit POST invalid (missing name, item 2) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-edit-post-invalid-negative-stock` | Edit POST invalid (negative stock, item 2) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-edit-post-invalid-bad-price` | Edit POST invalid (non-numeric price, item 2) -> 200 with validation messages | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-delete-get-created` | Delete GET of created item 13 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `mvc-delete-post` | Delete POST item 13 -> 302 / | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `mvc-delete-post-missing` | Delete POST missing id 999 | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s) |
| `mvc-details-deleted` | Details of deleted item 13 -> 404 | 404 | 404 | identical | none |
| `mvc-index-after-mutations` | Index after all mutations | 200 | 200 | equivalent | D1-charset: 2 diff(s) |
| `webforms-initial-state` | Initial seeded catalog (full list) | 200 | 200 | identical | none |
| `wf-list-root` | WebForms catalog list, GET / | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-default` | WebForms catalog list, /Default | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size10-page0` | WebForms catalog list, index 0 size 10 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size10-page1` | WebForms catalog list, index 1 size 10 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size5-page0` | WebForms catalog list, index 0 size 5 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size5-page1` | WebForms catalog list, index 1 size 5 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size5-page2` | WebForms catalog list, index 2 size 5 (partial) | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size100` | WebForms catalog list, size 100 (all rows) | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-out-of-range` | WebForms catalog list, index beyond last page | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-negative-page` | WebForms catalog list, negative index | 500 | 200 | different | S1-negative-page: 8 diff(s); D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-list-size0` | WebForms catalog list, size 0 (divide by zero) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-details-get-1` | WebForms Details page, valid id | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-details-get-missing` | WebForms Details page, missing id (legacy NullReferenceException -> 500) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-edit-get-1` | WebForms Edit page, valid id | 200 | 200 | equivalent | D1-charset: 2 diff(s); D6-wf-validator-script: 1 diff(s); D2-nocache: 2 diff(s) |
| `wf-edit-get-missing` | WebForms Edit page, missing id (legacy NullReferenceException -> 500) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-delete-get-1` | WebForms Delete page, valid id | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-delete-get-missing` | WebForms Delete page, missing id (legacy NullReferenceException -> 500) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-details-get-12` | WebForms Details page, last seeded id | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-details-get-nonint` | WebForms Details page, non-int id (legacy FormatException -> 500) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-create-get` | WebForms Create page | 200 | 200 | equivalent | D1-charset: 2 diff(s); D6-wf-validator-script: 1 diff(s); D2-nocache: 2 diff(s) |
| `wf-static-pics-1-png` | WebForms static asset /Pics/1.png | 200 | 200 | identical | none |
| `wf-static-pics-12-png` | WebForms static asset /Pics/12.png | 200 | 200 | identical | none |
| `wf-static-pics-dummy-png` | WebForms static asset /Pics/dummy.png | 200 | 200 | identical | none |
| `wf-static-images-brand-png` | WebForms static asset /images/brand.png | 200 | 200 | identical | none |
| `wf-static-images-brand_dark-png` | WebForms static asset /images/brand_dark.png | 200 | 200 | identical | none |
| `wf-static-images-main_footer_text-png` | WebForms static asset /images/main_footer_text.png | 200 | 200 | identical | none |
| `wf-static-favicon-ico` | WebForms static asset /favicon.ico | 200 | 200 | identical | none |
| `wf-static-content-bootstrap-css` | WebForms static asset /Content/bootstrap.css | 200 | 200 | identical | none |
| `wf-static-content-custom-css` | WebForms static asset /Content/custom.css | 200 | 200 | identical | none |
| `wf-static-content-base-css` | WebForms static asset /Content/base.css | 200 | 200 | identical | none |
| `wf-static-content-site-css` | WebForms static asset /Content/site.css | 200 | 200 | identical | none |
| `wf-static-scripts-jquery-3-3-1-js` | WebForms static asset /Scripts/jquery-3.3.1.js | 200 | 200 | identical | none |
| `wf-static-scripts-bootstrap-js` | WebForms static asset /Scripts/bootstrap.js | 200 | 200 | identical | none |
| `wf-static-scripts-respond-js` | WebForms static asset /Scripts/respond.js | 200 | 200 | identical | none |
| `wf-static-scripts-modernizr-2-8-3-js` | WebForms static asset /Scripts/modernizr-2.8.3.js | 200 | 200 | identical | none |
| `wf-create-post-valid` | WebForms Create postback valid -> redirect / | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `wf-details-created` | WebForms Details of created item 13 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-create-post-invalid-missing-name` | WebForms Create postback (missing name; server-side checks only) | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `wf-create-post-invalid-bad-price` | WebForms Create postback (non-numeric price; server-side checks only) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-create-post-invalid-negative-stock` | WebForms Create postback (negative stock; server-side checks only) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-edit-post-valid` | WebForms Edit postback valid (item 3) -> redirect / | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `wf-details-edited` | WebForms Details of edited item 3 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-edit-post-invalid-bad-price` | WebForms Edit postback (non-numeric price, item 4) | 500 | 500 | equivalent | D1-charset: 2 diff(s); D4-error-body: 3 diff(s); D2-nocache: 3 diff(s) |
| `wf-edit-post-invalid-missing-name` | WebForms Edit postback (missing name, item 4) | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `wf-delete-get-created` | WebForms Delete page of created item 13 | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |
| `wf-delete-post` | WebForms Delete postback item 13 -> redirect / | 302 | 302 | equivalent | D1-charset: 2 diff(s); D3-redirect-text: 2 diff(s) |
| `wf-list-after-mutations` | WebForms list after all postbacks | 200 | 200 | equivalent | D1-charset: 2 diff(s); D2-nocache: 2 diff(s) |

## Differences and classification

### D1-charset - intentional (.NET 8 forced)

Content-Type carries `; charset=utf-8` on migrated text responses

Root cause: ASP.NET Core (Razor views, Razor Pages, Results.Content/static content) always appends the charset to text media types; System.Web wrote the bare media type. Same media type, both bodies are UTF-8.

Affected: mock: 72 flow(s); sql: 72 flow(s) (per-flow list in parity-results.json)

### D2-nocache - intentional (.NET 8 forced)

Cache-Control `no-cache,no-store` + `Pragma: no-cache` / `Expires: -1` instead of `private`

Root cause: ASP.NET Core's SessionMiddleware (when it issues the ASP.NET_SessionId cookie), DefaultAntiforgery (when it renders a token) and ExceptionHandlerMiddleware (500 pages) force no-cache headers; System.Web left HttpCachePolicy at Private. Stricter caching only; no functional effect.

Affected: mock: 39 flow(s); sql: 40 flow(s) (per-flow list in parity-results.json)

### D3-redirect-text - pre-existing legacy defect

302 body reads `Object moved to here.` (trailing period) vs `Object moved to here`

Root cause: Legacy-harness artifact: Mono's System.Web redirect template omits the period that .NET Framework's HttpResponse.Redirect writes; src-net8 ObjectMovedResult/LegacyRedirectResult reproduce the .NET Framework text. Status and Location are identical.

Affected: mock: 10 flow(s); sql: 8 flow(s) (per-flow list in parity-results.json)

### D4-error-body - intentional (.NET 8 forced)

Error (404/500) response bodies differ

Root cause: System.Web's yellow-screen / host error pages do not exist in ASP.NET Core; the migrated app returns its own error page (or an empty 404 for unmatched routes). Status codes are identical; CONTRACT.md excludes framework error HTML from parity.

Affected: mock: 11 flow(s); sql: 13 flow(s) (per-flow list in parity-results.json)

### D5-bundles - fixed-in-parity-pr

Script bundles are now minified; /bundles/jquery and /bundles/bootstrap differ from WebGrease by a few bytes of minifier output (/bundles/jqueryval and /bundles/modernizr are byte-identical)

Root cause: Legacy /bundles/* always apply System.Web.Optimization JsMinify (even with compilation debug=true, which only changes how Scripts.Render emits tags). The migrated bundle endpoints now minify once at startup with NUglify (the maintained fork of the Microsoft Ajax Minifier that WebGrease wraps), joining files with ';' like ScriptBundle. Before this fix /bundles/jquery was 271,752 vs 87,460 bytes; now 87,464 vs 87,460. Not a contract (CONTRACT.md excludes bundle bytes); the residual diffs are minifier output only.

Affected: mock: 2 flow(s); sql: 2 flow(s) (per-flow list in parity-results.json)

### D5b-css-minify-failure - pre-existing legacy defect

/Content/css: legacy serves the stylesheet unminified behind a `/* Minification failed. Returning unminified contents. */` header; migrated serves it minified

Root cause: WebGrease CssMinify cannot parse Bootstrap 4 CSS custom properties (`--blue: #007bff;` -> run-time error CSS1062), so System.Web.Optimization falls back to the raw concatenation prefixed with the error list (199,898 bytes). NUglify parses it and the migrated bundle is minified (159,208 bytes; 197,431 before this fix). Same rules, smaller payload; bundle bytes are not a contract.

Affected: mock: 1 flow(s); sql: 1 flow(s) (per-flow list in parity-results.json)

### D6-wf-validator-script - intentional (.NET 8 forced)

Migrated WebForms edit/create pages load one extra script (/js/webforms-validators.js)

Root cause: WebForms' WebResource.axd client validators (WebUIValidation.js) do not exist in ASP.NET Core; the Razor Pages port ships an equivalent validator script.

Affected: mock: 2 flow(s); sql: 2 flow(s) (per-flow list in parity-results.json)

### D7-api-files-mime - pre-existing legacy defect

/api/files Content-Type: legacy `text/html`, migrated `application/octet-stream`

Root cause: Legacy FilesController returns StreamContent without a Content-Type, so xsp4 falls back to text/html for a binary BinaryFormatter payload; the migrated endpoint declares application/octet-stream. Bytes identical.

Affected: mock: 1 flow(s); sql: 1 flow(s) (per-flow list in parity-results.json)

### S1-negative-page - pre-existing legacy defect

Negative pageIndex: legacy 500, migrated 200 (empty page)

Root cause: Legacy CatalogService.GetCatalogItemsPaginated does Skip(pageSize * pageIndex): LINQ-to-Objects (mock) treats a negative Skip as 0, but EF6 emits a negative OFFSET and SQL Server throws. The migrated CatalogService guards pageIndex < 0, giving the same result in SQL mode as in mock mode (the recorded baseline).

Affected: sql: 2 flow(s) (per-flow list in parity-results.json)

### F1-webapi-nocache - fixed-in-parity-pr

Web API 2 default headers on /api/brands and /api/files: `Cache-Control: no-cache`, `Pragma: no-cache`, `Expires: -1` (src-net8 Api/LegacyApiCacheHeaders.cs).

### F2-bundle-cache - fixed-in-parity-pr

Bundle responses carry `Cache-Control: public` and a one-year `Expires` like System.Web.Optimization (Hosting/CatalogMvcExtensions.cs).

### F3-cache-private - fixed-in-parity-pr

System.Web default `Cache-Control: private` when nothing else sets caching, and `Content-Type: text/html` on bodiless MVC error results such as HttpNotFound (Hosting/LegacyHeadersExtensions.cs, WebForms Hosting/CatalogPagesExtensions.cs).

### F4-xfo - fixed-in-parity-pr

`X-Frame-Options: SAMEORIGIN` on every response that renders an antiforgery token, as MVC 5 did (ASP.NET Core only adds it when it issues a new cookie token) (Hosting/LegacyHeadersExtensions.cs).

### F5-api-files-sql - fixed-in-parity-pr

SQL mode /api/files bytes: List<T>._items is serialised at capacity; over an EF6 query ToList() grows 4->8 so legacy SQL emits an 8-slot array with 3 null slots. BrandDtoNrbfWriter now takes the capacity (sized = count in mock mode, grown in SQL mode); SQL /api/files went from a byte mismatch (721 vs 719 bytes) to identical.

### S2-ef-validate-on-save - fixed-in-parity-pr

SQL mode WebForms create/edit now enforces what EF6 ValidateOnSaveEnabled enforced: the SQL CatalogService runs an optional ICatalogSaveValidator before SaveChanges, and only eShopNet8.WebForms registers one (LegacyWebFormsSaveValidator: the legacy WebForms CatalogItem attributes - Price regex + Range(0, 9999999999999999.99), Range(0, 10000000) on the three stock fields, no [Required] on Name - plus the EF6 IsRequired/HasMaxLength(50) facets). MVC and mock mode register none. wf-create-post-invalid-negative-stock went from legacy 500 / migrated 302 (row inserted) to 500 / 500 with no row, and SQL data-state went from 19/24 to 24/24 (wf-*-missing-name stays 302 on both sides).

## Test suites

- Migrated: `dotnet test src-net8/eShopNet8.sln` -> eShop.Catalog.UnitTests 70/70 (incl. Testcontainers SQL; +2 save-validator hook tests), eShopNet8.Mvc.IntegrationTests 75/75 (+7 bundle minification/rebuild tests), eShopNet8.WebForms.IntegrationTests 60/60 (+20 LegacyWebFormsSaveValidator rule tests); total 205 passed, 0 failed, 0 skipped. Build: 0 errors, 4 NU1902 warnings (AngleSharp 1.1.2 advisory in integration-test projects, pre-existing).
- Legacy: no test projects exist in eShopLegacyMVCSolution / eShopLegacyWebFormsSolution. The transcript suite is the legacy baseline suite: legacy 128/128 (mock), migrated 128/128 (mock).

## Screenshots

results/screenshots/<flow>-legacy.png and <flow>-migrated.png for index, details, create, create-validation (client-side validation errors), edit, delete-confirm, webforms-list; captured in mock mode by migration/parity/screenshots_side_by_side.py with headless Chrome at 1280px. Not re-captured for the S2/D5 fixes: no captured page changes (the migrated MVC layout references the individual /Scripts and /Content files, as legacy Scripts.Render/Styles.Render do with compilation debug=true, so the minified bundle endpoints are not loaded by these pages; S2 is SQL-only).

## Raw reports

- raw/mock/side-by-side.json|.txt (summary + per-flow diffs), raw/mock/side-by-side-raw.json (both sides' full normalised responses)
- raw/mock/legacy-replay/parity-report.json|.txt, raw/mock/migrated-replay/parity-report.json|.txt (replay.py vs baseline)
- raw/sql/... same layout for SQL mode

## Reproduce

```bash
migration/parity/legacy/setup-legacy.sh all && migration/parity/legacy/start-legacy.sh mvc && migration/parity/legacy/start-legacy.sh webforms
docker compose -f src-net8/docker-compose.yml --profile mock up -d --build --wait
M='docker compose -f src-net8/docker-compose.yml --profile mock'
python3 migration/parity/side_by_side.py --legacy-mvc http://localhost:5100 --legacy-webforms http://localhost:5101 --migrated-mvc http://localhost:5200 --migrated-webforms http://localhost:5201 \
  --restart-legacy-mvc 'migration/parity/legacy/start-legacy.sh mvc' --restart-legacy-webforms 'migration/parity/legacy/start-legacy.sh webforms' \
  --restart-migrated-mvc "$M restart mvc-mock && sleep 3" --restart-migrated-webforms "$M restart webforms-mock && sleep 3" --report-dir migration/parity/results/raw/mock
# SQL (src-net8/docker/sql.env from sql.env.example with a generated MSSQL_SA_PASSWORD; stop the mock profile and legacy mock copies first)
set -a; . src-net8/docker/sql.env; set +a; migration/parity/sql/setup-legacy-sql.sh all
docker compose -f src-net8/docker-compose.yml --env-file src-net8/docker/sql.env --profile sql up -d --build --wait
E=src-net8/docker/sql.env R=migration/parity/sql/reset-sql.sh
python3 migration/parity/side_by_side.py --legacy-mvc http://localhost:5100 --legacy-webforms http://localhost:5101 --migrated-mvc http://localhost:5200 --migrated-webforms http://localhost:5201 \
  --restart-legacy-mvc "$R $E && LEGACY_ROOT=/tmp/sql migration/parity/legacy/start-legacy.sh mvc" --restart-legacy-webforms "$R $E && LEGACY_ROOT=/tmp/sql migration/parity/legacy/start-legacy.sh webforms" \
  --restart-migrated-mvc "$R $E mvc" --restart-migrated-webforms "$R $E webforms" --report-dir migration/parity/results/raw/sql
# replay.py vs baseline per side: --mvc-url/--webforms-url + the same --restart-cmd-<app> commands, --report-dir raw/<mode>/<side>-replay
python3 migration/parity/build_report.py
```
