# Parity contract

How the parity harness (`modernization/parity/`) decides that a migrated app behaves like the legacy one.
The rules are implemented in `parity/normalize.py`; this document and that file change together.

## 1. Inputs
- `baseline/flows.json`: declarative flows grouped in ordered **sequences**. A sequence with `restart: true` restarts the
  target first (fresh mock data); flows inside a sequence run in order on one cookie session.
- `baseline/transcripts/<flow>.json`: the recorded legacy response per flow (request, status, key headers, raw sha256,
  normalised body).
- `baseline/data-state/<flow>.json`: full catalog state after each flow with `"snapshot": true`.

Replay: `python3 modernization/parity/replay.py --mvc-url URL --webforms-url URL [--restart-cmd-mvc CMD]
[--restart-cmd-webforms CMD] --report-dir DIR [--accept-quirks] [--only SEQ...]`. It writes
`DIR/replay-report.json` + `DIR/replay-report.txt` and exits 1 if any flow is `different` (or a flow is missing/errored).

## 2. Requests
- Paths, methods and headers are taken verbatim from `flows.json`.
- Form posts (`"form"`): the harness GETs `form.page`, picks the form containing the `submit` label (or the first POST
  form), collects every successful control with its default value (hidden fields, antiforgery token, ViewState,
  EventValidation, selected options), overrides fields by **logical name**, drops `omit` fields, adds the submit button
  and posts to the form's resolved `action` (or `path` when given). Tokens are therefore harvested dynamically.
- **Logical name** = last segment of the control name split on `$` or `.`: WebForms `ctl00$MainContent$Price`,
  Razor Pages `Item.Price` and MVC `Price` are all `Price`. Targets may use any prefix but the last segment and the
  submit button label must match the legacy page.
- Redirects are not followed.

## 3. Response normalisation
| Rule | What |
|---|---|
| R1 status | compared exactly |
| R2 content type | MIME type only (charset dropped), compared by family: html, json, javascript, `image/*` exact, others exact. Not compared for empty bodies or `brands-semantic` flows |
| R3 Location | absolute URLs on the target host reduced to path+query, then compared exactly |
| R4 empty body | `{"kind":"empty"}` |
| R5 error pages | status >= 500 -> `{"kind":"opaque"}`: only status (+R2/R3) is contract; IIS YSOD, Mono and ASP.NET Core error pages differ by host |
| R6 `compare: "status"` | body ignored (used for optimisation-bundle URLs that debug pages never reference) |
| R7 JSON | parsed; compared structurally (key order irrelevant, property name case relevant) |
| R8 text (css/js/xml/text) | CRLF -> LF, BOM stripped, sha256 + length |
| R9 binary | sha256 + length (+ base64 when <= 256 KiB) |
| R10 `compare: "brands-semantic"` | `/api/files`: the list of brand names, decoded from BinaryFormatter (legacy) or JSON (target) |
| R11 HTML | see section 4 |

## 4. HTML normalisation (R11)
Parsed with lxml via BeautifulSoup, then:
- H1 removed: `<script>` bodies, `<style>`, `<noscript>`, comments, `<meta>`; `<link rel=stylesheet>` and
  `<script src>` URLs are recorded in `assets` (informational, **not compared**; static assets are covered by their own flows).
- H2 `title`: document title, whitespace-collapsed.
- H3 `text`: the ordered list of visible text nodes in `<body>`, whitespace-collapsed, empty nodes dropped.
- H4 `structure`: ordered list of significant elements (`a img form input select option textarea button label title
  h1-h6 table tr th td dt dd ul li span small p`) with only these attributes: `a[href] img[src,alt] form[action,method]
  input[name,type,value,readonly,checked,disabled] select[name] option[value,selected] textarea[name]
  button[type,name,value] label[for]`; plus element text for text-bearing tags; plus behaviour classes only
  (`esh-pager-item--hidden`, `esh-pager-item--navigable`, `field-validation-error`, `input-validation-error`,
  `validation-summary-errors`, `text-danger`, `esh-price`). `id`, `style`, other classes, `data-*`/`aria-*` are ignored.
- H5 control names (`name`, `label[for]`) are reduced to their logical name (section 2).
- H6 masking: values of `__RequestVerificationToken __VIEWSTATE __VIEWSTATEGENERATOR __EVENTVALIDATION __EVENTTARGET
  __EVENTARGUMENT __PREVIOUSPAGE __LASTFOCUS` -> `<masked>`; a text node matching `{MachineName}, {M/d/yyyy h:mm:ss AM|PM}`
  (session footer, INVENTORY Q14) -> `<masked>`.
- H7 URLs in `href`/`src`/`action` reduced like R3 (relative URLs stay as written, e.g. WebForms `action="3"`).

Consequences targets must respect: same visible text and order (labels, headings, button captions such as `[ Save ]`,
pager text `Showing 10 of 12 products - Page 1 - 2`, price formatting such as `$19.50`), same link targets, same form
field logical names and values, same validation messages.

## 5. Data-state snapshots
After a `snapshot` flow the harness (new session) GETs the app's list page with page size 1000
(`/?pageSize=1000&pageIndex=0`, WebForms `/Default/index/0/size/1000`) and records every table row as
`{column header: cell text}` plus `Id` (from the Edit/Details/Delete link), and the brand/type `<select>` options of
`/Catalog/Create` keyed by logical name. Snapshots are compared exactly. Note: rendering the list populates mock
navigation properties (Q4), so flows after a snapshot see "warm" items, on legacy and target alike.

## 6. Verdicts
- `identical`: no differences after normalisation **and** raw body sha256 and full content type are byte-identical.
- `equivalent-after-normalisation`: no differences after normalisation (and data state equal), raw bytes differ.
- `different`: anything else; reasons listed in the report.

Flows tagged `quirk: Qn` reproduce a documented legacy defect (INVENTORY section 10). Preferred: reproduce. Where a
ticket deliberately fixes one (e.g. Q2 BinaryFormatter, which the target must not use), `--accept-quirks` reports the
flow as `different` but does not fail the run; every accepted divergence must be listed in the parity report.

## 7. Restart contract
`--restart-cmd-*` must return only when the app answers, and must not render a catalog page while probing (that would
warm mock navigation properties, Q4). Legacy: `parity/legacy/start-legacy.sh mvc|webforms` probes `/api/brands` and
`/About.aspx`. Targets should probe a health or static endpoint.

## 8. Determinism
The baseline was self-replayed twice against the legacy apps with 0 differences (`baseline/self-replay/`).
Dropped as non-deterministic: `mvc-create-post-invalid-price-negative` (Q17), listed in `flows.json` `dropped_flows`.
