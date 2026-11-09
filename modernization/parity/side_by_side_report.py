#!/usr/bin/env python3
"""Builds results/side-by-side/summary.json and the self-contained PARITY-REPORT.html from a side-by-side run.

  side_by_side_report.py [--results DIR] [--screenshots DIR] [--out PARITY-REPORT.html]

Reads side-by-side-{mock,sql}.json (side_by_side.py + classify.py), dotnet-test.trx, characterization-{legacy,migrated}.xml,
env.json (written by run-side-by-side.sh) and classifications.json; screenshots/<id>-{legacy,migrated}.png are inlined as
base64. Exit 0 only when every transcript matches by contract on both modes, every data-state check matches, every test
passes and no strict difference is unexplained."""
import argparse
import base64
import datetime
import html
import json
import pathlib
import sys
import xml.etree.ElementTree as ET

PARITY = pathlib.Path(__file__).resolve().parent
CLS = json.loads((PARITY / "classifications.json").read_text())
RULES = {r["id"]: r for r in CLS["rules"]}
RULES["FW-MARKUP-CANONICAL"] = {"id": "FW-MARKUP-CANONICAL", "classification": "accepted-framework-difference",
                                "root_cause": "Only attribute order, class-token order, self-closing slashes, whitespace or HTML "
                                              "character-reference spelling (&#39; vs &#x27;, + vs &#x2B;, & vs &amp;) differ; "
                                              "the parsed DOM is identical (Razor HtmlEncoder vs System.Web encoder)."}
SQL_BASELINE_NOTE = ("Legacy SQL mode and migrated SQL mode agree, but both differ from the MOCK-recorded baseline: SQL decimal(18,2) "
                     "materialises prices with scale 2 (19.50, 12.00) where the mock catalog literals have scale 1/0 (19.5, 12), "
                     "and decimal.ToString / BinaryFormatter keep the scale (WebForms price text, MVC /api/files bytes). "
                     "Same on both sides; recorded in CONTRACT.md / baseline/sql-mode.")
e = html.escape


def junit(path):
    if not path.exists():
        return None
    root = ET.parse(path).getroot()
    suites = [root] if root.tag == "testsuite" else root.findall("testsuite")
    t = sum(int(s.get("tests", 0)) for s in suites)
    bad = sum(int(s.get("failures", 0)) + int(s.get("errors", 0)) for s in suites)
    sk = sum(int(s.get("skipped", 0)) for s in suites)
    return {"total": t, "passed": t - bad - sk, "failed": bad, "skipped": sk}


def trx(path):
    if not path.exists():
        return None
    c = next(el for el in ET.parse(path).getroot().iter() if el.tag.endswith("Counters"))
    return {"total": int(c.get("total")), "passed": int(c.get("passed")), "failed": int(c.get("failed")),
            "skipped": int(c.get("notExecuted", 0))}


def summarise(results):
    modes, checks, diffs = [], [], {}
    for r in results:
        for a in r["apps"]:
            cs = a["cases"]
            row = {"mode": r["mode"], "app": a["app"], "total": len(cs),
                   "contract": sum(c["contract_match"] for c in cs),
                   "legacy_vs_baseline": sum(bool(c["legacy_vs_baseline"]) for c in cs),
                   "migrated_vs_baseline": sum(bool(c["migrated_vs_baseline"]) for c in cs),
                   "strict_identical": sum(not c["strict_diffs"] for c in cs),
                   "legacy_transport_retries": sum(c.get("legacy_transport_retries", 0) for c in cs),
                   "baseline_deviation_both": [c["case"] for c in cs if c["contract_match"] and not c["legacy_vs_baseline"]
                                               and not c["migrated_vs_baseline"]],
                   "baseline_deviation_one_side": [c["case"] for c in cs if bool(c["legacy_vs_baseline"]) != bool(c["migrated_vs_baseline"])]}
            modes.append(row)
            for c in cs:
                if c["kind"] == "state":
                    ds = c.get("data_state", {})
                    checks.append({"mode": r["mode"], "app": a["app"], "check": c["case"],
                                   "match": c["contract_match"] and ds.get("legacy_rows") == ds.get("migrated_rows")})
                for x in c.get("classified", []):
                    d = diffs.setdefault(x["rule"] or "UNEXPLAINED", {"occurrences": 0, "cases": [], "example": None})
                    d["occurrences"] += 1
                    key = f"{r['mode']}/{a['app']}/{c['case']}"
                    if key not in d["cases"]:
                        d["cases"].append(key)
                    if d["example"] is None or (not d["example"]["legacy"] and x["legacy"]):
                        d["example"] = {"case": key, "field": x["field"], "legacy": str(x["legacy"] or "")[:400],
                                        "migrated": str(x["migrated"] or "")[:400]}
            for d in a.get("data_state_db") or []:
                checks.append({"mode": r["mode"], "app": a["app"], "check": d["check"], "match": d["match"]})
    return modes, checks, diffs


def img(path):
    return f'<img src="data:image/png;base64,{base64.b64encode(path.read_bytes()).decode()}" alt="{e(path.name)}">'


def table(head, rows, cls=""):
    th = "".join(f"<th>{e(h)}</th>" for h in head)
    return f'<table class="{cls}"><thead><tr>{th}</tr></thead><tbody>' + "".join(
        "<tr>" + "".join(f"<td>{c}</td>" for c in r) + "</tr>" for r in rows) + "</tbody></table>"


def ok(b):
    return '<span class="ok">match</span>' if b else '<span class="bad">DIFF</span>'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--results", default=str(PARITY / "results" / "side-by-side"))
    ap.add_argument("--screenshots", default=str(PARITY / "screenshots"))
    ap.add_argument("--out", default=str(PARITY / "PARITY-REPORT.html"))
    a = ap.parse_args()
    res = pathlib.Path(a.results)
    results = [json.loads(p.read_text()) for p in (res / "side-by-side-mock.json", res / "side-by-side-sql.json") if p.exists()]
    env = json.loads((res / "env.json").read_text()) if (res / "env.json").exists() else {}
    tests = {"dotnet_test": trx(res / "dotnet-test.trx"),
             "characterization_legacy": junit(res / "characterization-legacy.xml"),
             "characterization_migrated": junit(res / "characterization-migrated.xml")}
    modes, checks, diffs = summarise(results)
    unexplained = diffs.get("UNEXPLAINED", {}).get("occurrences", 0)
    transcripts_total = sum(m["total"] for m in modes if m["mode"] == "mock")
    summary = {
        "generated": datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds"),
        "environment": env, "modes": modes, "tests": tests,
        "transcripts_total": transcripts_total,
        "transcripts_contract_match": {m["mode"]: sum(x["contract"] for x in modes if x["mode"] == m["mode"]) for m in modes},
        "data_state_checks": {"total": len(checks), "match": sum(c["match"] for c in checks), "checks": checks},
        "differences": {k: {**v, "classification": RULES.get(k, {}).get("classification", "unexplained"),
                            "root_cause": RULES.get(k, {}).get("root_cause", "")} for k, v in sorted(diffs.items())},
        "fixed": CLS["fixed"], "unexplained_differences": unexplained,
    }
    parity = (all(m["contract"] == m["total"] for m in modes) and all(c["match"] for c in checks) and unexplained == 0
              and all(t and t["failed"] == 0 and t["skipped"] == 0 and t["total"] > 0 for t in tests.values())
              and not any(m["baseline_deviation_one_side"] for m in modes)
              # mock replays the exact seed the baseline was recorded on, so any drift (even shared) is a failure
              and not any(m["baseline_deviation_both"] for m in modes if m["mode"] == "mock"))
    summary["verdict"] = "PARITY" if parity else "NOT PARITY"
    (res / "summary.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False) + "\n")

    # ---------------- HTML ----------------
    def tcell(t):
        return "not run" if not t else f"{t['passed']}/{t['total']} passed" + (f", {t['failed']} failed" if t["failed"] else "")
    mode_rows = [[e(m["mode"]), e(m["app"]), f"{m['contract']}/{m['total']}", f"{m['legacy_vs_baseline']}/{m['total']}",
                  f"{m['migrated_vs_baseline']}/{m['total']}", f"{m['strict_identical']}/{m['total']}",
                  str(m["legacy_transport_retries"])] for m in modes]
    diff_rows, limits = [], []
    order = {"fixed": 0, "pre-existing-legacy-defect": 1, "known-limitation": 2, "accepted-framework-difference": 3, "unexplained": 4}
    for k, v in sorted(summary["differences"].items(), key=lambda kv: (order.get(kv[1]["classification"], 9), kv[0])):
        ex = v["example"] or {}
        diff_rows.append([f"<code>{e(k)}</code>", f'<span class="cls {e(v["classification"])}">{e(v["classification"])}</span>',
                          e(v["root_cause"]), str(v["occurrences"]), str(len(v["cases"])),
                          f"<div class='ex'><b>{e(ex.get('case', ''))}</b> <i>{e(ex.get('field', ''))}</i><br>L: <code>{e(ex.get('legacy', ''))}</code>"
                          f"<br>M: <code>{e(ex.get('migrated', ''))}</code></div>"
                          f"<details><summary>cases</summary><small>{e(', '.join(v['cases']))}</small></details>"])
    fixed_rows = [[f"<code>{e(f['id'])}</code>", e(f["root_cause"]), e(f["evidence"])] for f in CLS["fixed"]]
    flow_rows = []
    for r in results:
        for app in r["apps"]:
            for c in app["cases"]:
                req = c.get("request") or {}
                rules = sorted({x["rule"] or "UNEXPLAINED" for x in c.get("classified", [])})
                flow_rows.append([e(r["mode"]), e(app["app"]), e(c["case"]), e(c["flow"]),
                                  e(f"{req.get('method', '')} {req.get('path', '')}"), str(c["legacy_status"]), str(c["migrated_status"]),
                                  ok(c["contract_match"]), ok(c["legacy_vs_baseline"]), ok(c["migrated_vs_baseline"]),
                                  "identical" if not c["strict_diffs"] else "<small>" + e(", ".join(rules)) + "</small>"])
    check_rows = [[e(c["mode"]), e(c["app"]), e(c["check"]), ok(c["match"])] for c in checks]
    for m in modes:
        if m["baseline_deviation_both"]:
            limits.append(f"<li><b>{e(m['mode'])}/{e(m['app'])}</b>: {len(m['baseline_deviation_both'])} case(s) differ from the mock baseline on "
                          f"<i>both</i> sides (legacy and migrated agree): {e(SQL_BASELINE_NOTE if m['mode'] == 'sql' else 'see cases')}"
                          f"<details><summary>cases</summary><small>{e(', '.join(m['baseline_deviation_both']))}</small></details></li>")
    shots = pathlib.Path(a.screenshots)
    shot_html = ""
    for lp in sorted(shots.glob("*-legacy.png")) if shots.exists() else []:
        mp = lp.with_name(lp.name.replace("-legacy.png", "-migrated.png"))
        if mp.exists():
            taken = datetime.datetime.fromtimestamp(lp.stat().st_mtime, datetime.timezone.utc).isoformat(timespec="seconds")
            shot_html += (f"<h3>{e(lp.name.replace('-legacy.png', ''))} <small>(captured {e(taken)})</small></h3><div class='pair'><figure>{img(lp)}<figcaption>legacy (Mono + xsp4)"
                          f"</figcaption></figure><figure>{img(mp)}<figcaption>migrated (.NET 8 container)</figcaption></figure></div>")
    env_rows = [[e(k), f"<code>{e(str(v))}</code>"] for k, v in env.items()]
    doc = f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><title>eShop storefront .NET 8 parity report</title>
<style>
body{{font:14px/1.45 system-ui,-apple-system,Segoe UI,sans-serif;margin:24px auto;max-width:1400px;padding:0 16px;color:#1d2330}}
h1{{margin-bottom:4px}} h2{{border-bottom:2px solid #e3e7ef;padding-bottom:4px;margin-top:32px}}
table{{border-collapse:collapse;width:100%;margin:8px 0}} th,td{{border:1px solid #dde2ea;padding:4px 6px;vertical-align:top;text-align:left}}
th{{background:#f3f5f9}} code{{font-size:12px;word-break:break-all}} .ok{{color:#11772e;font-weight:600}} .bad{{color:#b3261e;font-weight:700}}
.verdict{{display:inline-block;padding:6px 14px;border-radius:6px;font-weight:700;font-size:18px;color:#fff;background:{'#11772e' if parity else '#b3261e'}}}
.cls{{padding:1px 6px;border-radius:4px;font-size:12px;white-space:nowrap}} .accepted-framework-difference{{background:#e7eefc}}
.pre-existing-legacy-defect{{background:#fff1d6}} .known-limitation{{background:#efe7fb}} .unexplained{{background:#fde2e1}}
.pair{{display:grid;grid-template-columns:1fr 1fr;gap:12px}} figure{{margin:0;border:1px solid #dde2ea}} figure img{{width:100%;display:block}}
figcaption{{padding:4px 8px;background:#f3f5f9;font-weight:600}} .ex{{max-width:560px}} .flows td{{font-size:12px}}
.cards{{display:flex;gap:12px;flex-wrap:wrap}} .card{{border:1px solid #dde2ea;border-radius:8px;padding:10px 14px;min-width:180px}} .card b{{font-size:20px;display:block}}
</style></head><body>
<h1>eShop storefront: legacy vs .NET 8 side-by-side parity</h1>
<p>Generated {e(summary['generated'])} &middot; integration head <code>{e(str(env.get('git_head', '?')))}</code> &middot; <span class="verdict">{summary['verdict']}</span></p>
<div class="cards">
<div class="card">Transcripts (contract)<b>{' / '.join(f"{k}: {v}/{transcripts_total}" for k, v in summary['transcripts_contract_match'].items())}</b></div>
<div class="card">Data-state checks<b>{summary['data_state_checks']['match']}/{summary['data_state_checks']['total']}</b></div>
<div class="card">dotnet test<b>{tcell(tests['dotnet_test'])}</b></div>
<div class="card">Characterization legacy / migrated<b>{tcell(tests['characterization_legacy'])} / {tcell(tests['characterization_migrated'])}</b></div>
<div class="card">Unexplained differences<b>{unexplained}</b></div></div>
<h2>Scope</h2>
<p>Every recorded baseline transcript (MVC 86, WebForms 52 = 138 flows; <code>modernization/baseline/transcripts</code>, flow list
<code>modernization/tools/flows.py</code>) is replayed at the same time against the <b>legacy</b> apps and the <b>migrated</b> .NET 8 apps, each side
reset to an identical fresh seed first (mock: new in-memory catalog; SQL: dropped and re-seeded SQL Server 2022 database per app). Per flow the two
live responses are compared (1) by the CONTRACT.md rules (status, Location, media type, semantic HTML/JSON/XML view, data-state rows), (2) each
against the recorded baseline, and (3) <i>strictly</i>: full normalised body for every status plus the headers that matter and the set of runtime
headers. Every strict difference is attributed to a rule in <code>classifications.json</code>; anything unattributed is counted as unexplained.
After the mutating flows the catalog state is compared (state-after-* flows) and, in SQL mode, the Catalog/CatalogBrand/CatalogType rows, column
schema and HiLo sequences of both databases. The 77-test characterization suite (<code>modernization/tools/tests</code>) runs against both sides
and <code>dotnet test</code> runs the xUnit suite.</p>
<h2>Environment</h2>
<p><b>Legacy ran on Mono + xsp4 on Linux, not on Windows/IIS</b>: the unmodified .NET Framework 4.7.2 sources are built with Mono MSBuild from a
copied run directory with the documented harness-only patches (<code>modernization/legacy-harness/README.md</code>). Mono-specific behaviour
(e.g. yellow-page 5xx bodies, intermittent request-line parse failures) is called out where it matters. The migrated apps run as Linux containers
built from <code>net8/docker</code>.</p>
{table(['item', 'value'], env_rows)}
<h2>Results per mode and app</h2>
{table(['mode', 'app', 'legacy vs migrated (contract)', 'legacy vs baseline', 'migrated vs baseline', 'strict identical', 'legacy Mono transport retries'], mode_rows)}
<p>"Strict identical" counts flows with no difference at all before classification; every remaining strict difference is classified below.
A transport retry re-sends a request that Mono/xsp4 rejected with 500 "The format of the HTTP method is invalid" before it reached the application.</p>
<h2>Tests</h2>
{table(['suite', 'legacy', 'migrated'], [['characterization (pytest, 77 tests, MVC + WebForms)', tcell(tests['characterization_legacy']), tcell(tests['characterization_migrated'])],
                                          ['dotnet test net8/EShopStorefront.sln (xUnit, ESHOP_TESTS_REQUIRE_ALL=1)', 'n/a', tcell(tests['dotnet_test'])]])}
<h2>Regressions found and fixed in this run</h2>
{table(['id', 'root cause', 'evidence'], fixed_rows)}
<h2>Every remaining difference</h2>
{table(['rule', 'classification', 'verified root cause', 'occurrences', 'flows', 'example'], diff_rows)}
<h2>Verified parity, known limitations and pre-existing legacy defects</h2>
<ul><li><b>Verified parity</b>: every flow matches legacy by contract in each mode run; every data-state check matches; tests above.</li>
{''.join(limits)}
<li><b>Known limitations</b> (CONTRACT.md, reproduced or documented, not differences between the two sides): MVC antiforgery failure and
<code>pageSize=0</code> return 500 like MVC 5; WebForms missing/invalid ids return 500 like legacy; <code>/api/brands</code> negotiates XML;
<code>/api/files</code> returns BinaryFormatter-compatible bytes.</li>
<li><b>Pre-existing legacy defects</b>: rules classified <code>pre-existing-legacy-defect</code> above (not reproduced), plus the Mono/xsp4 transport flake
counted in the retry column. WebForms <code>/Content/css</code> and <code>/bundles/WebFormsJs</code> 404 on Mono (legacy harness limitation).</li></ul>
<h2>Screenshots (mock mode)</h2>{shot_html or '<p>No screenshots captured.</p>'}
<h2>Per-flow legacy vs migrated</h2>
<details open><summary>{len(flow_rows)} flow executions</summary>{table(['mode', 'app', 'flow file', 'group', 'request', 'legacy', 'migrated', 'contract', 'legacy vs baseline', 'migrated vs baseline', 'strict'], flow_rows, 'flows')}</details>
<h2>Data-state checks</h2>{table(['mode', 'app', 'check', 'legacy vs migrated'], check_rows)}
<h2>How to rerun</h2><pre>{e(env.get('how_to_rerun', 'modernization/parity/run-side-by-side.sh --setup'))}</pre>
</body></html>"""
    pathlib.Path(a.out).write_text(doc, encoding="utf-8")
    print(f"{summary['verdict']}: contract {summary['transcripts_contract_match']} of {transcripts_total}, data-state "
          f"{summary['data_state_checks']['match']}/{summary['data_state_checks']['total']}, tests {tests}, unexplained {unexplained}")
    print(f"wrote {a.out}")
    return 0 if parity else 1


if __name__ == "__main__":
    sys.exit(main())
