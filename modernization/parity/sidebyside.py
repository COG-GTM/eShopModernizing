#!/usr/bin/env python3
"""Stage-4 side-by-side parity: replay every baseline flow against the LIVE legacy app and the LIVE migrated app in
lockstep (both restarted before every restart-sequence) and diff each response three ways:
legacy-live vs baseline, migrated-live vs baseline, migrated-live vs legacy-live. After every mutating or snapshot flow
the catalog data state is captured from both apps' pages (runner.snapshot) and, with --sql-db-*, from both databases
via sqlcmd, and compared.

  python3 sidebyside.py --mode mock \
      --legacy-mvc http://127.0.0.1:5100 --legacy-webforms http://127.0.0.1:5101 \
      --net8-mvc http://127.0.0.1:5200 --net8-webforms http://127.0.0.1:5201 \
      --restart-legacy-mvc "legacy/start-legacy.sh mvc" ... --restart-net8-mvc "net8/start-net8.sh mvc" ... \
      [--sql-db-legacy NAME --sql-db-net8 NAME] [--sql-server-legacy S --sql-server-net8 S]
      (sa password from SQLCMDPASSWORD; server default SQL_SERVER or 127.0.0.1,1433)
      [--known-divergences JSON] --report-dir DIR

Writes DIR/sidebyside-report.json (+ .txt) and DIR/transcripts/{legacy,net8}/<flow>.json. Exit 1 when any flow is
`different` against legacy-live and is not a known divergence, or when the data states differ.
"""
import argparse
import collections
import json
import os
import pathlib
import subprocess
import sys
import time

import requests

import normalize as N
import runner
from replay import is_known_divergence

SQLCMD = os.environ.get("SQLCMD") or "/opt/mssql-tools18/bin/sqlcmd"
DUMP_SQL = """SET NOCOUNT ON;
DECLARE @j nvarchar(max) = (SELECT
 (SELECT i.Id, i.Name, i.Description, CAST(i.Price AS varchar(40)) AS Price, i.PictureFileName, i.CatalogTypeId, t.Type AS TypeName,
         i.CatalogBrandId, b.Brand AS BrandName, i.AvailableStock, i.RestockThreshold, i.MaxStockThreshold, i.OnReorder
  FROM dbo.Catalog i JOIN dbo.CatalogType t ON t.Id = i.CatalogTypeId JOIN dbo.CatalogBrand b ON b.Id = i.CatalogBrandId
  ORDER BY i.Id FOR JSON PATH, INCLUDE_NULL_VALUES) AS items,
 (SELECT Id, Brand FROM dbo.CatalogBrand ORDER BY Id FOR JSON PATH) AS brands,
 (SELECT Id, Type FROM dbo.CatalogType ORDER BY Id FOR JSON PATH) AS types
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
SELECT @j;"""


def sql_dump(db, server=None):
    server = server or os.environ.get("SQL_SERVER", "127.0.0.1,1433")
    r = subprocess.run([SQLCMD, "-S", server, "-U", "sa", "-C", "-b", "-d", db, "-y", "0", "-Q", DUMP_SQL],
                       capture_output=True, text=True, env=os.environ)
    if r.returncode != 0:
        return {"error": (r.stdout + r.stderr).strip()[-300:]}
    # -y 0 (unlimited width) cannot be combined with -h -1, so skip the header/dash lines before the JSON document.
    lines = r.stdout.split("\n")
    start = next(i for i, l in enumerate(lines) if l.startswith("{"))
    doc = json.loads("".join(lines[start:]).strip())
    return {k: json.loads(doc[k]) if isinstance(doc.get(k), str) else doc.get(k, []) for k in ("items", "brands", "types")}


def safe_snapshot(base, app_cfg):
    try:
        return runner.snapshot(requests.Session(), base, app_cfg)
    except requests.RequestException as e:
        return {"error": "%s: %s" % (type(e).__name__, e), "items": []}


def compare_snap(a, b):
    if a == b:
        return []
    if (a or {}).get("items") != (b or {}).get("items"):
        return ["items: %s" % N.first_diff((a or {}).get("items", []), (b or {}).get("items", []))]
    return ["lookups/status differ: %s" % N.first_diff(a, b)]


def compare_sql(a, b):
    if "error" in a or "error" in b:
        return ["sql dump error: legacy=%s net8=%s" % (a.get("error"), b.get("error"))]
    return ["%s: %s" % (k, N.first_diff(a[k], b[k])) for k in ("items", "brands", "types") if a[k] != b[k]]


def step(session, base, flow):
    try:
        return runner.run_flow(session, base, flow)
    except Exception as e:  # recorded as a failed flow, never skipped
        return {"id": flow["id"], "error": "%s: %s" % (type(e).__name__, e)}


def cmp(exp, act, mode):
    if "error" in exp or "error" in act:
        return "different", ["error: %s / %s" % (exp.get("error"), act.get("error"))]
    return N.compare(exp["response"], act["response"], mode)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--mode", required=True, help="label: mock | sql | ...")
    for side in ("legacy", "net8"):
        for app in ("mvc", "webforms"):
            ap.add_argument("--%s-%s" % (side, app))
            ap.add_argument("--restart-%s-%s" % (side, app))
        ap.add_argument("--sql-db-%s" % side)
        ap.add_argument("--sql-server-%s" % side, help="default SQL_SERVER or 127.0.0.1,1433")
    ap.add_argument("--baseline", default=str(runner.BASELINE))
    ap.add_argument("--known-divergences")
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--report-dir", required=True)
    a = vars(ap.parse_args())
    known = json.loads(pathlib.Path(a["known_divergences"]).read_text())["flows"] if a["known_divergences"] else {}
    base = pathlib.Path(a["baseline"])
    doc = runner.load_flows(base / "flows.json")
    out = pathlib.Path(a["report_dir"])
    apps = [app for app in ("mvc", "webforms") if a["legacy_%s" % app] and a["net8_%s" % app]]
    sql = bool(a["sql_db_legacy"] and a["sql_db_net8"])
    results, seq_end, started = [], [], time.time()
    for seq in doc["sequences"]:
        app = seq["app"]
        if app not in apps or (a["only"] and seq["id"] not in a["only"]):
            continue
        lb, nb = a["legacy_%s" % app].rstrip("/"), a["net8_%s" % app].rstrip("/")
        print("sequence %s (%s, %d flows)" % (seq["id"], app, len(seq["flows"])), flush=True)
        if seq.get("restart", True):
            runner.restart(a["restart_legacy_%s" % app], print)
            runner.restart(a["restart_net8_%s" % app], print)
        ls, ns = requests.Session(), requests.Session()
        for s in (ls, ns):
            s.headers["User-Agent"] = "eshop-parity-harness/1"
        for flow in seq["flows"]:
            mode = flow.get("compare", "full")
            lt, nt = step(ls, lb, flow), step(ns, nb, flow)
            exp = json.loads((base / "transcripts" / ("%s.json" % flow["id"])).read_text())
            res = {"id": flow["id"], "sequence": seq["id"], "app": app, "method": flow.get("method", "GET"),
                   "path": flow.get("path") or flow.get("form", {}).get("page"), "quirk": flow.get("quirk"),
                   "baseline_status": exp["response"]["status"],
                   "legacy_status": lt.get("response", {}).get("status"), "net8_status": nt.get("response", {}).get("status")}
            for key, (x, y) in {"legacy_vs_baseline": (exp, lt), "net8_vs_baseline": (exp, nt), "net8_vs_legacy": (lt, nt)}.items():
                v, r = cmp(x, y, mode)
                res[key] = {"verdict": v, "reasons": r}
            mutating = flow.get("snapshot") or flow.get("method", "GET").upper() != "GET"
            if mutating:
                res["data_state"] = {}
            # page snapshots render the list, which warms mock navigation properties (Q4), so they are taken only where
            # the baseline took them; other mutating flows are covered by the SQL dump and the end-of-sequence snapshot
            if flow.get("snapshot"):
                lsnap, nsnap = safe_snapshot(lb, doc["apps"][app]), safe_snapshot(nb, doc["apps"][app])
                bsnap = json.loads((base / exp["data_state"]).read_text()) if exp.get("data_state") else None
                res["data_state"].update({"net8_vs_legacy": compare_snap(lsnap, nsnap),
                                          "legacy_vs_baseline": compare_snap(bsnap, lsnap) if bsnap else None,
                                          "net8_vs_baseline": compare_snap(bsnap, nsnap) if bsnap else None,
                                          "items_legacy": len(lsnap["items"]), "items_net8": len(nsnap["items"])})
            if mutating:
                if sql:
                    ld, nd = sql_dump(a["sql_db_legacy"], a["sql_server_legacy"]), sql_dump(a["sql_db_net8"], a["sql_server_net8"])
                    res["data_state"]["sql_net8_vs_legacy"] = compare_sql(ld, nd)
                    res["data_state"]["sql_items_legacy"] = len(ld.get("items", []))
                    res["data_state"]["sql_items_net8"] = len(nd.get("items", []))
                    runner.write_json(out / "sql-dumps" / "legacy" / ("%s.json" % flow["id"]), ld)
                    runner.write_json(out / "sql-dumps" / "net8" / ("%s.json" % flow["id"]), nd)
            pair = dict(res["net8_vs_legacy"], status=res["net8_status"], expected_status=res["legacy_status"])
            res["known_divergence"] = pair["verdict"] == "different" and is_known_divergence(pair, known.get(flow["id"]))
            ds = res.get("data_state") or {}
            res["data_state_equal"] = not ds.get("net8_vs_legacy") and not ds.get("sql_net8_vs_legacy")
            res["failing"] = (res["net8_vs_legacy"]["verdict"] == "different" and not res["known_divergence"]) or not res["data_state_equal"]
            results.append(res)
            runner.write_json(out / "transcripts" / "legacy" / ("%s.json" % flow["id"]), lt)
            runner.write_json(out / "transcripts" / "net8" / ("%s.json" % flow["id"]), nt)
            print("  %-48s legacy %s net8 %s  pair=%s%s%s" % (flow["id"], res["legacy_status"], res["net8_status"],
                  res["net8_vs_legacy"]["verdict"], " (known divergence)" if res["known_divergence"] else "",
                  "" if res["data_state_equal"] else " DATA-STATE DIFFERS"), flush=True)
            if res["failing"]:
                print("    " + "; ".join(res["net8_vs_legacy"]["reasons"] + ds.get("net8_vs_legacy", []) + ds.get("sql_net8_vs_legacy", []))[:600])
        if any(f.get("snapshot") or f.get("method", "GET").upper() != "GET" for f in seq["flows"]):
            lsnap, nsnap = safe_snapshot(lb, doc["apps"][app]), safe_snapshot(nb, doc["apps"][app])
            seq_end.append({"sequence": seq["id"], "app": app, "net8_vs_legacy": compare_snap(lsnap, nsnap),
                            "items_legacy": len(lsnap["items"]), "items_net8": len(nsnap["items"])})
            print("  end-of-sequence data state: %s" % (seq_end[-1]["net8_vs_legacy"] or "equal"), flush=True)
    summary = {"mode": a["mode"], "flows": len(results), "duration_s": round(time.time() - started, 1),
               "baseline_flows": sum(len(s["flows"]) for s in doc["sequences"] if s["app"] in apps and (not a["only"] or s["id"] in a["only"])),
               "failing": sum(r["failing"] for r in results), "known_divergences": sum(r["known_divergence"] for r in results),
               "mutating_flows": sum("data_state" in r for r in results),
               # flows with no per-flow check (mock mode, no baseline snapshot) are covered by the sequence-end snapshot only
               "data_state_checked": sum(bool(r.get("data_state")) for r in results),
               "data_state_equal": sum(r["data_state_equal"] for r in results if r.get("data_state")),
               "sql_dumps": sql, "sequence_end_snapshots": len(seq_end),
               "sequence_end_equal": sum(not e["net8_vs_legacy"] for e in seq_end)}
    for key in ("legacy_vs_baseline", "net8_vs_baseline", "net8_vs_legacy"):
        summary[key] = dict(collections.Counter(r[key]["verdict"] for r in results))
    runner.write_json(out / "sidebyside-report.json", {"summary": summary, "results": results, "sequence_end": seq_end})
    lines = ["eShop side-by-side parity (%s)" % a["mode"], json.dumps(summary), ""]
    for r in results:
        lines.append("%-24s %-48s L%s N%s  LvB=%s NvB=%s NvL=%s%s%s" % (r["sequence"], r["id"], r["legacy_status"], r["net8_status"],
                     r["legacy_vs_baseline"]["verdict"][:5], r["net8_vs_baseline"]["verdict"][:5], r["net8_vs_legacy"]["verdict"][:5],
                     " [known]" if r["known_divergence"] else "", " [DATA]" if not r["data_state_equal"] else ""))
        for key in ("legacy_vs_baseline", "net8_vs_baseline", "net8_vs_legacy"):
            for reason in r[key]["reasons"]:
                lines.append("      %s: %s" % (key, reason[:300]))
    (out / "sidebyside-report.txt").write_text("\n".join(lines) + "\n")
    print(json.dumps(summary, indent=1))
    return 1 if summary["failing"] or summary["sequence_end_equal"] < len(seq_end) or summary["flows"] < summary["baseline_flows"] else 0


if __name__ == "__main__":
    sys.exit(main())
