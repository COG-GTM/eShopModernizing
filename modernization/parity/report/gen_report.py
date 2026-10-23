#!/usr/bin/env python3
"""Builds the self-contained parity report (parity-report.html + parity-report.json) from side-by-side runs.

  python3 gen_report.py --inputs report-inputs.json \
      --run mock=<sidebyside report dir> --run sql=<dir> [--run mock-container=<dir>] [--run sql-container=<dir>] \
      --screenshots screenshots/

Each run dir is the --report-dir of sidebyside.py (sidebyside-report.json). report-inputs.json holds the curated parts
(scope, environment, test counts, differences with category/root cause/status, links). Screenshots
<side>-<app>-<page>.jpg are embedded as base64 data URIs; the HTML makes no external requests.
"""
import argparse
import base64
import html
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
PAGES = [("mvc", "index", "MVC index"), ("mvc", "index-page2", "MVC page 2"), ("mvc", "details", "MVC details"),
         ("mvc", "create", "MVC create"), ("mvc", "create-invalid", "MVC create - validation errors"),
         ("mvc", "edit", "MVC edit"), ("mvc", "delete", "MVC delete"), ("mvc", "api-brands", "/api/brands"),
         ("webforms", "index", "WebForms list"), ("webforms", "details", "WebForms details"),
         ("webforms", "create", "WebForms create"), ("webforms", "edit", "WebForms edit"),
         ("webforms", "delete", "WebForms delete")]
CSS = """
body{font:14px/1.45 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;margin:0;color:#1d2433;background:#f6f7f9}
main{max-width:1280px;margin:0 auto;padding:24px 32px 64px}
h1{font-size:26px;margin:0 0 4px}h2{font-size:20px;margin:36px 0 10px;border-bottom:2px solid #d9dde5;padding-bottom:4px}
h3{font-size:16px;margin:20px 0 8px}code{background:#eef0f4;padding:1px 4px;border-radius:3px;font-size:12.5px}
table{border-collapse:collapse;width:100%;background:#fff;margin:8px 0 16px;font-size:12.5px}
th,td{border:1px solid #dde1e8;padding:4px 7px;text-align:left;vertical-align:top;overflow-wrap:anywhere}th{background:#eef0f4;position:sticky;top:0}
.cards{display:flex;flex-wrap:wrap;gap:12px;margin:14px 0}.card{background:#fff;border:1px solid #dde1e8;border-radius:8px;padding:12px 16px;min-width:180px;flex:1}
.card b{display:block;font-size:24px}.card span{color:#5b6475;font-size:12.5px}
.ok{color:#11753a;font-weight:600}.bad{color:#b42318;font-weight:600}.warn{color:#9a6700;font-weight:600}.muted{color:#5b6475}
.pill{display:inline-block;padding:0 6px;border-radius:9px;font-size:11.5px;background:#eef0f4;white-space:nowrap}
.p-identical{background:#d3f5df}.p-equivalent-after-normalisation{background:#e3f0ff}.p-different{background:#ffe1dc}.p-known{background:#fff1c2}
.shots{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-bottom:18px}.shots figure{margin:0;background:#fff;border:1px solid #dde1e8;padding:6px}
.shots img{width:100%;height:auto;display:block;border:1px solid #eef0f4}figcaption{font-size:12px;color:#5b6475;margin-bottom:4px}
details summary{cursor:pointer;font-weight:600;margin:6px 0}.small{font-size:12px}
"""


def rich(v):
    out = esc(v)
    for t in ("code", "b"):
        out = out.replace("&lt;%s&gt;" % t, "<%s>" % t).replace("&lt;/%s&gt;" % t, "</%s>" % t)
    return out


def esc(v):
    return html.escape("" if v is None else str(v))


def load_run(path):
    p = pathlib.Path(path)
    run = json.loads((p / "sidebyside-report.json").read_text())
    # count only flows with an actual per-flow check; the rest are covered by the end-of-sequence snapshot
    s, res = run["summary"], run["results"]
    s["data_state_checked"] = sum(bool(r.get("data_state")) for r in res)
    s["data_state_equal"] = sum(bool(r.get("data_state")) and r["data_state_equal"] for r in res)
    return run


def pill(verdict, known=False):
    if not verdict:
        return '<span class="muted">-</span>'
    cls = "p-known" if known else "p-" + verdict
    label = {"equivalent-after-normalisation": "equivalent"}.get(verdict, verdict)
    return '<span class="pill %s">%s%s</span>' % (cls, esc(label), " (known)" if known else "")


def table(headers, rows, cls=""):
    out = ['<table class="%s"><thead><tr>%s</tr></thead><tbody>' % (cls, "".join("<th>%s</th>" % h for h in headers))]
    for r in rows:
        out.append("<tr>%s</tr>" % "".join("<td>%s</td>" % c for c in r))
    out.append("</tbody></table>")
    return "".join(out)


def run_summary_rows(runs, inputs):
    rows = []
    for label, run in runs.items():
        s = run["summary"]
        pair = s.get("net8_vs_legacy", {})
        rows.append([
            "<b>%s</b><div class='small muted'>%s</div>" % (esc(label), esc(inputs.get("run_labels", {}).get(label, ""))),
            esc(s["flows"]), '<span class="%s">%s</span>' % ("ok" if s["failing"] == 0 else "bad", esc(s["failing"])),
            esc(s["known_divergences"]), esc(pair.get("identical", 0)), esc(pair.get("equivalent-after-normalisation", 0)),
            esc(pair.get("different", 0)),
            "%s / %s checked (%s mutating; rest via sequence end)" % (s["data_state_equal"], s["data_state_checked"], s["mutating_flows"]),
            "%s / %s" % (s["sequence_end_equal"], s["sequence_end_snapshots"]),
            "yes" if s.get("sql_dumps") else "no",
            ", ".join("%s %s" % (v, k.replace("equivalent-after-normalisation", "equivalent")) for k, v in sorted(s["legacy_vs_baseline"].items())),
            ", ".join("%s %s" % (v, k.replace("equivalent-after-normalisation", "equivalent")) for k, v in sorted(s["net8_vs_baseline"].items())),
        ])
    return rows


def flow_table(runs, known_ids):
    labels = list(runs)
    index = {label: {r["id"]: r for r in run["results"]} for label, run in runs.items()}
    first = runs[labels[0]]["results"]
    headers = ["#", "Flow", "Request", "Quirk", "Baseline"]
    for label in labels:
        headers += ["%s legacy" % esc(label), "%s migrated" % esc(label), "%s verdict" % esc(label)]
    rows = []
    for n, f in enumerate(first, 1):
        row = [str(n), "<code>%s</code><div class='small muted'>%s</div>" % (esc(f["id"]), esc(f["sequence"])),
               "%s %s" % (esc(f["method"]), esc(f["path"])), esc(f.get("quirk") or ""), esc(f.get("baseline_status"))]
        for label in labels:
            r = index[label].get(f["id"])
            if not r:
                row += ["-", "-", "not run"]
                continue
            pv = r["net8_vs_legacy"]["verdict"]
            known = bool(r.get("known_divergence"))
            ds = r.get("data_state", {}).get("net8_vs_legacy")
            ds_txt = "" if ds is None else ("<div class='small ok'>data equal</div>" if not ds else "<div class='small bad'>data differs</div>")
            row += [esc(r.get("legacy_status")), esc(r.get("net8_status")), pill(pv, known) + ds_txt]
        rows.append(row)
    return table(headers, rows)


def data_state_section(runs):
    def verdict(diffs):
        return '<span class="ok">equal</span>' if not diffs else '<span class="bad">%s</span>' % esc("; ".join(diffs)[:300])
    out = []
    for label, run in runs.items():
        s = run["summary"]
        sql = bool(s.get("sql_dumps"))
        rows = []
        for r in run["results"]:
            ds = r.get("data_state")
            if not ds:
                continue
            pages = "net8_vs_legacy" in ds
            row = ["<code>%s</code>" % esc(r["id"]),
                   "%s / %s" % (ds.get("items_legacy"), ds.get("items_net8")) if pages else '<span class="muted">-</span>',
                   verdict(ds["net8_vs_legacy"]) if pages else '<span class="muted">not rendered (sqlcmd only)</span>']
            if sql:
                row += ["%s / %s" % (ds.get("sql_items_legacy"), ds.get("sql_items_net8")), verdict(ds.get("sql_net8_vs_legacy"))]
            row.append(verdict(ds.get("legacy_vs_baseline")) if ds.get("legacy_vs_baseline") is not None else '<span class="muted">-</span>')
            rows.append(row)
        headers = ["Flow", "Items legacy / migrated (pages)", "Migrated vs legacy (pages)"]
        if sql:
            headers += ["Rows legacy / migrated (sqlcmd)", "Migrated vs legacy (sqlcmd dump)"]
        headers.append("Legacy-live vs recorded baseline snapshot")
        seq = [["<code>%s</code>" % esc(e.get("sequence")), esc(e.get("app")), "%s / %s" % (e.get("items_legacy"), e.get("items_net8")), verdict(e.get("net8_vs_legacy"))]
               for e in (run.get("sequence_end") or [])]
        out.append("<h3>%s: %s/%s per-flow data-state checks equal (%s mutating flows; %s without a per-flow check are covered by the end-of-sequence snapshot), %s/%s end-of-sequence snapshots equal, SQL dumps: %s</h3>" % (
            esc(label), s["data_state_equal"], s["data_state_checked"], s["mutating_flows"], s["mutating_flows"] - s["data_state_checked"], s["sequence_end_equal"], s["sequence_end_snapshots"],
            "yes (sqlcmd, both databases)" if sql else "no (catalog pages only)"))
        out.append("<details><summary>Per mutating flow (%d)</summary>%s</details>" % (len(rows), table(headers, rows)))
        out.append("<details><summary>End of each mutation sequence (%d)</summary>%s</details>" % (len(seq), table(["Sequence", "App", "Items legacy / migrated", "Migrated vs legacy"], seq)))
    return "".join(out)


def screenshots_section(shots_dir):
    if not shots_dir:
        return "<p class='muted'>No screenshots supplied.</p>", []
    shots_dir = pathlib.Path(shots_dir)
    out, listed = [], []
    for app, page, title in PAGES:
        figs = []
        for side, name in (("legacy", "Legacy (Mono/xsp4)"), ("net8", "Migrated (.NET 8)")):
            f = shots_dir / ("%s-%s-%s.jpg" % (side, app, page))
            if f.exists():
                data = base64.b64encode(f.read_bytes()).decode()
                figs.append('<figure><figcaption>%s - <code>%s</code></figcaption><img alt="%s %s" src="data:image/jpeg;base64,%s"></figure>' % (
                    name, esc(f.name), esc(name), esc(title), data))
                listed.append(f.name)
            else:
                figs.append("<figure><figcaption>%s</figcaption><p class='bad'>missing %s</p></figure>" % (name, esc(f.name)))
        out.append("<h3>%s</h3><div class='shots'>%s</div>" % (esc(title), "".join(figs)))
    return "".join(out), listed


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--inputs", default=str(HERE / "report-inputs.json"))
    ap.add_argument("--run", action="append", default=[], help="label=dir")
    ap.add_argument("--screenshots", default=str(HERE / "screenshots"))
    ap.add_argument("--out-html", default=str(HERE / "parity-report.html"))
    ap.add_argument("--out-json", default=str(HERE / "parity-report.json"))
    a = ap.parse_args()
    inputs = json.loads(pathlib.Path(a.inputs).read_text())
    runs = {}
    for spec in a.run:
        label, path = spec.split("=", 1)
        runs[label] = load_run(path)
    known_ids = {k for d in inputs["differences"] if d["category"] == "harness/runtime artifact" for k in d.get("flow_ids", [])}

    diffs = inputs["differences"]
    regressions_open = sum(1 for d in diffs if d["category"] == "regression" and d["status"].lower().startswith("open"))
    shots_html, shots = screenshots_section(a.screenshots)
    h = inputs["headline"]

    sections = []
    sections.append("<h1>eShop legacy (.NET Framework 4.7.2) vs .NET 8: end-to-end parity report</h1>"
                    "<p class='muted'>Integration branch <code>%s</code> at <code>%s</code>; generated by <code>modernization/parity/report/gen_report.py</code>.</p>" % (
                        esc(inputs["branch"]), esc(inputs["tested_sha"])))
    cards = "".join('<div class="card"><b class="%s">%s</b><span>%s</span></div>' % (esc(c.get("cls", "")), esc(c["value"]), esc(c["label"])) for c in h["cards"])
    sections.append("<h2>1. Executive summary</h2><div class='cards'>%s</div>%s" % (cards, "".join("<p>%s</p>" % p for p in h["paragraphs"])))
    sections.append("<h2>2. Scope</h2>" + "".join("<h3>%s</h3><ul>%s</ul>" % (esc(k), "".join("<li>%s</li>" % i for i in v)) for k, v in inputs["scope"].items()))
    sections.append("<h2>3. Environment</h2>" + table(["Component", "Value"], [[esc(k), v] for k, v in inputs["environment"].items()]))
    sections.append("<h2>4. Method</h2><ol>%s</ol>" % "".join("<li>%s</li>" % m for m in inputs["method"]))
    sections.append("<h2>5. Run summary</h2>" + table(
        ["Run", "Flows", "Failing", "Known div.", "Pair identical", "Pair equivalent", "Pair different", "Data state after mutations",
         "End-of-sequence", "SQL dumps", "Legacy-live vs baseline", "Migrated-live vs baseline"], run_summary_rows(runs, inputs)))
    sections.append("<h2>6. Per-flow results (legacy-live vs migrated-live)</h2><p class='muted small'>Verdict = migrated-live vs legacy-live after the contract normalisation "
                    "(modernization/CONTRACT.md). <span class='pill p-known'>known</span> = documented divergence in known-divergences.json (status equal, body produced by the host). "
                    "Status &ge; 500 bodies are opaque (R5).</p>" + flow_table(runs, known_ids))
    sections.append("<h2>7. Data-state diffs</h2>" + data_state_section(runs))
    t = inputs["tests"]
    sections.append("<h2>8. Test suites</h2>" + table(["Side", "Suite", "Passed", "Failed", "Skipped", "Notes"],
                                                     [[esc(r["side"]), esc(r["suite"]), esc(r["passed"]), esc(r["failed"]), esc(r["skipped"]), esc(r.get("notes", ""))] for r in t]))
    sections.append("<h2>9. Side-by-side screenshots</h2><p class='muted small'>Captured with Playwright (Chrome for Testing) at 1280px, list page first so mock navigation "
                    "properties are warm (Q4). The validation screenshot posts an empty Name and Price \"abc\" with JavaScript disabled to show the server-side messages.</p>" + shots_html)
    sections.append("<h2>10. Every difference</h2>" + table(["ID", "Flows / modes", "Category", "Root cause", "Status"],
                                                          [["<b>%s</b>" % esc(d["id"]), esc(d["flows"]), esc(d["category"]), rich(d["root_cause"]), rich(d["status"])] for d in diffs]))
    sections.append("<h2>11. Verified parity vs known limitations</h2><h3 class='ok'>Verified parity (observed in this run)</h3><ul>%s</ul>"
                    "<h3 class='warn'>Known limitations / pre-existing legacy defects (reproduced deliberately)</h3><ul>%s</ul>" % (
                        "".join("<li>%s</li>" % v for v in inputs["verified"]), "".join("<li>%s</li>" % v for v in inputs["limitations"])))
    sections.append("<h2>12. Links</h2><ul>%s</ul>" % "".join('<li>%s: <a href="%s">%s</a></li>' % (esc(l["label"]), esc(l["url"]), esc(l["url"])) for l in inputs["links"]))

    doc = "<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>" \
          "<title>eShop .NET 8 parity report</title><style>%s</style></head><body><main>%s</main></body></html>" % (CSS, "".join(sections))
    pathlib.Path(a.out_html).write_text(doc)

    machine = {
        "branch": inputs["branch"], "tested_sha": inputs["tested_sha"], "headline": h,
        "runs": {label: {"summary": run["summary"], "sequence_end": run.get("sequence_end"),
                         "flows": [{k: r.get(k) for k in ("id", "sequence", "app", "method", "path", "quirk", "baseline_status", "legacy_status",
                                                         "net8_status", "legacy_vs_baseline", "net8_vs_baseline", "net8_vs_legacy", "data_state", "known_divergence")}
                                   for r in run["results"]]} for label, run in runs.items()},
        "tests": t, "differences": diffs, "regressions_open": regressions_open, "screenshots": shots,
        "environment": inputs["environment_plain"], "links": inputs["links"],
    }
    pathlib.Path(a.out_json).write_text(json.dumps(machine, indent=1) + "\n")
    print("wrote %s (%d KB) and %s; regressions_open=%d" % (a.out_html, len(doc) // 1024, a.out_json, regressions_open))
    return 0


if __name__ == "__main__":
    sys.exit(main())
