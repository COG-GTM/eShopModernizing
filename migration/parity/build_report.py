#!/usr/bin/env python3
"""Builds results/parity-results.json and results/PARITY.md from the raw side-by-side reports
(results/raw/{mock,sql}/side-by-side.json). Every legacy-vs-migrated diff must match a rule in RULES;
an unmatched diff is reported as UNCLASSIFIED and makes the script exit non-zero."""
from __future__ import annotations

import json
import re
import sys
from collections import Counter, OrderedDict
from pathlib import Path

RESULTS = Path(__file__).resolve().parent / "results"
FIXED, FORCED, LEGACY, OPEN = ("fixed-in-parity-pr", "intentional (.NET 8 forced)",
                               "pre-existing legacy defect", "open regression")

# (rule id, classification, summary, root cause, predicate(mode, flow_id, kind, diff))
RULES = [
    ("D1-charset", FORCED, "Content-Type carries `; charset=utf-8` on migrated text responses",
     "ASP.NET Core (Razor views, Razor Pages, Results.Content/static content) always appends the charset to text "
     "media types; System.Web wrote the bare media type. Same media type, both bodies are UTF-8.",
     lambda m, f, k, d: "content-type" in d and "charset=utf-8" in d and "!= expected" in d),
    ("D2-nocache", FORCED, "Cache-Control `no-cache,no-store` + `Pragma: no-cache` / `Expires: -1` instead of `private`",
     "ASP.NET Core's SessionMiddleware (when it issues the ASP.NET_SessionId cookie), DefaultAntiforgery (when it "
     "renders a token) and ExceptionHandlerMiddleware (500 pages) force no-cache headers; System.Web left "
     "HttpCachePolicy at Private. Stricter caching only; no functional effect.",
     lambda m, f, k, d: k == "header" and (("cache-control" in d and "no-cache" in d) or "pragma" in d
                                           or ("expires" in d and '"-1"' in d))),
    ("D3-redirect-text", LEGACY, "302 body reads `Object moved to here.` (trailing period) vs `Object moved to here`",
     "Legacy-harness artifact: Mono's System.Web redirect template omits the period that .NET Framework's "
     "HttpResponse.Redirect writes; src-net8 ObjectMovedResult/LegacyRedirectResult reproduce the .NET Framework "
     "text. Status and Location are identical.",
     lambda m, f, k, d: "Object moved to here" in d),
    ("D4-error-body", FORCED, "Error (404/500) response bodies differ",
     "System.Web's yellow-screen / host error pages do not exist in ASP.NET Core; the migrated app returns its own "
     "error page (or an empty 404 for unmatched routes). Status codes are identical; CONTRACT.md excludes "
     "framework error HTML from parity.",
     lambda m, f, k, d: k == "strict" and ("$.response.body" in d)
                        and (f in ERROR_FLOWS or (m == "sql" and f in SQL_ERROR_FLOWS))),
    ("D5-bundles", FIXED, "Script bundles are now minified; /bundles/jquery and /bundles/bootstrap differ from WebGrease "
     "by a few bytes of minifier output (/bundles/jqueryval and /bundles/modernizr are byte-identical)",
     "Legacy /bundles/* always apply System.Web.Optimization JsMinify (even with compilation debug=true, which only "
     "changes how Scripts.Render emits tags). The migrated bundle endpoints now minify once at startup with NUglify "
     "(the maintained fork of the Microsoft Ajax Minifier that WebGrease wraps), joining files with ';' like "
     "ScriptBundle. Before this fix /bundles/jquery was 271,752 vs 87,460 bytes; now 87,464 vs 87,460. Not a "
     "contract (CONTRACT.md excludes bundle bytes); the residual diffs are minifier output only.",
     lambda m, f, k, d: f in JS_BUNDLE_FLOWS and k == "strict" and bundle_body_match(d, 1.01)),
    ("D5b-css-minify-failure", LEGACY, "/Content/css: legacy serves the stylesheet unminified behind a "
     "`/* Minification failed. Returning unminified contents. */` header; migrated serves it minified",
     "WebGrease CssMinify cannot parse Bootstrap 4 CSS custom properties (`--blue: #007bff;` -> run-time error "
     "CSS1062), so System.Web.Optimization falls back to the raw concatenation prefixed with the error list "
     "(199,898 bytes). NUglify parses it and the migrated bundle is minified (159,208 bytes; 197,431 before this fix). "
     "Same rules, smaller payload; bundle bytes are not a contract.",
     lambda m, f, k, d: f == "mvc-bundle-content-css" and k == "strict" and bundle_body_match(d, 1.0)),
    ("D6-wf-validator-script", FORCED, "Migrated WebForms edit/create pages load one extra script (/js/webforms-validators.js)",
     "WebForms' WebResource.axd client validators (WebUIValidation.js) do not exist in ASP.NET Core; the Razor "
     "Pages port ships an equivalent validator script.",
     lambda m, f, k, d: "assets.scripts" in d),
    ("D7-api-files-mime", LEGACY, "/api/files Content-Type: legacy `text/html`, migrated `application/octet-stream`",
     "Legacy FilesController returns StreamContent without a Content-Type, so xsp4 falls back to text/html for a "
     "binary BinaryFormatter payload; the migrated endpoint declares application/octet-stream. Bytes identical.",
     lambda m, f, k, d: f == "mvc-api-files" and "octet-stream" in d),
    ("S1-negative-page", LEGACY, "Negative pageIndex: legacy 500, migrated 200 (empty page)",
     "Legacy CatalogService.GetCatalogItemsPaginated does Skip(pageSize * pageIndex): LINQ-to-Objects (mock) "
     "treats a negative Skip as 0, but EF6 emits a negative OFFSET and SQL Server throws. The migrated "
     "CatalogService guards pageIndex < 0, giving the same result in SQL mode as in mock mode (the recorded baseline).",
     lambda m, f, k, d: (m == "sql" and f in ("mvc-index-negative-page", "wf-list-negative-page")
                         and k in ("contract", "strict") and d.startswith(RESPONSE_PATHS))),
    ("S2-ef-validate-on-save", OPEN,
     "SQL mode: WebForms create with negative Stock -> legacy 500 (row rejected), migrated 302 (row inserted); "
     "the extra row then shifts every later WebForms data-state snapshot",
     "EF6 DbContext validates DataAnnotations on SaveChanges (ValidateOnSaveEnabled), so the legacy WebForms "
     "CatalogItem [Range(0, 10000000)] on Stock throws DbEntityValidationException; EF Core does not validate on "
     "save and the Razor Pages handler does not check ranges server-side. Not small: a generic SaveChanges "
     "validator over the shared eShop.Catalog.Core model also applies the MVC [Required] Name attribute, which the "
     "legacy WebForms model lacks (tried in this run: it turned wf-create/edit-post-invalid-missing-name into 500s), "
     "so the fix needs a WebForms-specific rule set. Mock mode is unaffected (legacy mock also accepts the row).",
     lambda m, f, k, d: m == "sql" and s2_match(f, k, d)),
]
ERROR_FLOWS = {"mvc-index-size0", "mvc-delete-post-missing", "mvc-create-post-no-antiforgery", "mvc-pic-nonint", "mvc-api-hello",
               "wf-list-size0", "wf-details-get-missing", "wf-edit-get-missing", "wf-delete-get-missing",
               "wf-details-get-nonint", "wf-create-post-invalid-bad-price", "wf-edit-post-invalid-bad-price"}
# SQL mode only: the WebForms save validator rejects the item, so both sides answer 500 (mock mode saves it: 302).
SQL_ERROR_FLOWS = {"wf-create-post-invalid-negative-stock"}
S2_FLOWS = {"wf-create-post-invalid-negative-stock", "wf-edit-post-valid", "wf-edit-post-invalid-bad-price",
            "wf-edit-post-invalid-missing-name", "wf-delete-post", "wf-list-after-mutations"}

JS_BUNDLE_FLOWS = {"mvc-bundle-bundles-jquery", "mvc-bundle-bundles-jqueryval", "mvc-bundle-bundles-modernizr",
                   "mvc-bundle-bundles-bootstrap"}
RESPONSE_PATHS = ("$.response.status", "$.response.mime", "$.response.location", "$.response.body",
                  "$.response.headers.content-type", "$.response.headers.mime")
EXTRA_ROW_HTML = ("$.response.body.html.text", "$.response.body.html.links", "$.response.body.html.tables",
                  "$.response.body.html.images")
EXTRA_ROW_PATHS = ("$.data_state.pager", "$.data_state.row_count", "$.data_state.rows")


def bundle_body_match(diff: str, max_ratio: float) -> bool:
    """Bundle body hash diffs, and length diffs only while the migrated body stays minified (<= max_ratio x legacy)."""
    if diff.startswith("$.response.body.sha256"):
        return True
    m = re.match(r"\$\.response\.body\.length: (\d+) != expected (\d+)$", diff)
    return bool(m) and int(m.group(1)) <= int(m.group(2)) * max_ratio


def s2_match(flow_id: str, kind: str, diff: str) -> bool:
    """The negative-stock create itself (status/body + one extra row), then only extra-row effects downstream."""
    if flow_id == "wf-create-post-invalid-negative-stock":
        return ((kind in ("contract", "strict") and diff.startswith(RESPONSE_PATHS + ("$.response.headers.location",)))
                or (kind == "header" and diff.startswith(("$.headers.location", "$.headers.content-type")))
                or (kind == "data" and diff.startswith(EXTRA_ROW_PATHS)))
    if flow_id == "wf-list-after-mutations":
        return kind in ("contract", "strict") and diff.startswith(EXTRA_ROW_HTML)
    return flow_id in S2_FLOWS and kind == "data" and diff.startswith(EXTRA_ROW_PATHS)


FIXED_IN_PR = [
    ("F1-webapi-nocache", "Web API 2 default headers on /api/brands and /api/files: `Cache-Control: no-cache`, "
     "`Pragma: no-cache`, `Expires: -1` (src-net8 Api/LegacyApiCacheHeaders.cs)."),
    ("F2-bundle-cache", "Bundle responses carry `Cache-Control: public` and a one-year `Expires` like "
     "System.Web.Optimization (Hosting/CatalogMvcExtensions.cs)."),
    ("F3-cache-private", "System.Web default `Cache-Control: private` when nothing else sets caching, and "
     "`Content-Type: text/html` on bodiless MVC error results such as HttpNotFound (Hosting/LegacyHeadersExtensions.cs, "
     "WebForms Hosting/CatalogPagesExtensions.cs)."),
    ("F4-xfo", "`X-Frame-Options: SAMEORIGIN` on every response that renders an antiforgery token, as MVC 5 did "
     "(ASP.NET Core only adds it when it issues a new cookie token) (Hosting/LegacyHeadersExtensions.cs)."),
    ("F5-api-files-sql", "SQL mode /api/files bytes: List<T>._items is serialised at capacity; over an EF6 query "
     "ToList() grows 4->8 so legacy SQL emits an 8-slot array with 3 null slots. BrandDtoNrbfWriter now takes the "
     "capacity (sized = count in mock mode, grown in SQL mode); SQL /api/files went from a byte mismatch "
     "(721 vs 719 bytes) to identical."),
    ("S2-ef-validate-on-save", "SQL mode WebForms create/edit now enforces what EF6 ValidateOnSaveEnabled enforced: the "
     "SQL CatalogService runs an optional ICatalogSaveValidator before SaveChanges, and only eShopNet8.WebForms registers "
     "one (LegacyWebFormsSaveValidator: the legacy WebForms CatalogItem attributes - Price regex + Range(0, "
     "9999999999999999.99), Range(0, 10000000) on the three stock fields, no [Required] on Name - plus the EF6 "
     "IsRequired/HasMaxLength(50) facets). MVC and mock mode register none. wf-create-post-invalid-negative-stock went "
     "from legacy 500 / migrated 302 (row inserted) to 500 / 500 with no row, and SQL data-state went from 19/24 to "
     "24/24 (wf-*-missing-name stays 302 on both sides)."),
]


def classify(mode: str, flow_id: str, kind: str, diff: str) -> str:
    for rule in RULES:
        if rule[4](mode, flow_id, kind, diff):
            return rule[0]
    return "UNCLASSIFIED"


def flow_results(mode: str, report: dict) -> list[dict]:
    out = []
    for r in report["results"]:
        diffs = ([("contract", d) for d in r["contract_diffs"]] + [("strict", d) for d in r["strict_diffs"]]
                 + [("header", d) for d in r["header_diffs"]] + [("data", d) for d in r["data_state_diffs"]])
        rules = OrderedDict()
        for kind, d in diffs:
            rules.setdefault(classify(mode, r["id"], kind, d), []).append(f"[{kind}] {d[:300]}")
        if not diffs:
            verdict = "identical"
        elif r["contract_match"] and not r["data_state_diffs"]:
            verdict = "equivalent (contract + data-state match; strict/header diffs classified)"
        else:
            verdict = "different"
        out.append({
            "id": r["id"], "app": r["app"], "description": r["title"], "mutates": r["mutates"],
            "legacy_status": r["legacy_status"], "migrated_status": r["migrated_status"],
            "verdict": verdict, "contract_match": r["contract_match"], "strict_match": r["strict_match"],
            "headers_match": r["headers_match"],
            "data_state": ("n/a" if not r["data_state_checked"] else ("match" if not r["data_state_diffs"] else "diff")),
            "legacy_vs_baseline": r["legacy_vs_baseline"], "migrated_vs_baseline": r["migrated_vs_baseline"],
            "diff_summary": "; ".join(f"{k}: {len(v)} diff(s)" for k, v in rules.items()) or "none",
            "diffs": rules,
        })
    return out


def main() -> int:
    modes = {}
    for mode in ("mock", "sql"):
        rep = json.loads((RESULTS / "raw" / mode / "side-by-side.json").read_text())
        flows = flow_results(mode, rep)
        modes[mode] = {"summary": rep["summary"], "flows": flows,
                       "verdicts": dict(Counter(f["verdict"].split(" ")[0] for f in flows))}
    used = Counter((m, rule) for m, v in modes.items() for f in v["flows"] for rule in f["diffs"])
    unclassified = [(m, f["id"], f["diffs"]["UNCLASSIFIED"]) for m, v in modes.items() for f in v["flows"]
                    if "UNCLASSIFIED" in f["diffs"]]
    differences = []
    for rid, cls, summary, cause, _ in RULES:
        flows = {m: [f["id"] for f in v["flows"] if rid in f["diffs"]] for m, v in modes.items()}
        if any(flows.values()):
            differences.append({"id": rid, "classification": cls, "difference": summary, "root_cause": cause,
                                "flows": flows})
    for fid, text in FIXED_IN_PR:
        differences.append({"id": fid, "classification": FIXED, "difference": text,
                            "root_cause": "see difference", "flows": {}})
    meta = json.loads((RESULTS / "run-meta.json").read_text())
    out = {"generated_from": ["raw/mock/side-by-side.json", "raw/sql/side-by-side.json"], **meta,
           "modes": modes, "differences": differences, "unclassified": unclassified}
    (RESULTS / "parity-results.json").write_text(json.dumps(out, indent=2) + "\n")
    (RESULTS / "PARITY.md").write_text(render_md(out))
    for m, fid, d in unclassified:
        print(f"UNCLASSIFIED {m} {fid}: {d}", file=sys.stderr)
    print({m: v["verdicts"] for m, v in modes.items()}, dict(used))
    return 1 if unclassified else 0


def render_md(o: dict) -> str:
    L = []
    L += ["# Legacy vs migrated parity (side-by-side replay)", "", o["intro"], ""]
    L += ["## Headline", ""] + [f"- {h}" for h in o["headline"]] + [""]
    L += ["## Environment", ""] + [f"- {e}" for e in o["environment"]] + [""]
    L += ["## Normalisations (the only things not compared)", ""] + [f"- {n}" for n in o["normalisations"]] + [""]
    for mode in ("mock", "sql"):
        v = o["modes"][mode]
        s = v["summary"]
        L += [f"## {mode.upper()} mode results", ""]
        L += ["| metric | value |", "|---|---|"] + [f"| {k} | {s[k]} |" for k in s if not isinstance(s[k], (dict, list))]
        L += ["", "Verdicts: " + ", ".join(f"{k} {n}" for k, n in v["verdicts"].items()), ""]
        if mode == "sql":
            L += ["SQL-mode limitations:", ""] + [f"- {x}" for x in o["sql_limitations"]] + [""]
        L += ["| flow | description | legacy | migrated | verdict | diff summary |", "|---|---|---|---|---|---|"]
        for f in v["flows"]:
            desc = f["description"].replace("|", "\\|")
            L.append(f"| `{f['id']}` | {desc} | {f['legacy_status']} | {f['migrated_status']} | "
                     f"{f['verdict'].split(' ')[0]} | {f['diff_summary']} |")
        L.append("")
    L += ["## Differences and classification", ""]
    for d in o["differences"]:
        fl = "; ".join(f"{m}: {len(ids)} flow(s)" for m, ids in d["flows"].items() if ids)
        L += [f"### {d['id']} - {d['classification']}", "", d["difference"], ""]
        if d["root_cause"] != "see difference":
            L += [f"Root cause: {d['root_cause']}", ""]
        if fl:
            L += [f"Affected: {fl} (per-flow list in parity-results.json)", ""]
    L += ["## Test suites", ""] + [f"- {t}" for t in o["tests"]] + [""]
    L += ["## Screenshots", "", o["screenshots"], ""]
    L += ["## Raw reports", ""] + [f"- {r}" for r in o["raw_reports"]] + [""]
    L += ["## Reproduce", "", "```bash"] + o["reproduce"] + ["```", ""]
    if o.get("unclassified"):
        L += ["## UNCLASSIFIED", ""] + [f"- {m} {fid}" for m, fid, _ in o["unclassified"]] + [""]
    return "\n".join(L)


if __name__ == "__main__":
    sys.exit(main())
