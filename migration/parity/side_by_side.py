#!/usr/bin/env python3
"""Side-by-side replay: runs every flow in flows.json against the LEGACY and the MIGRATED app and diffs
them directly (not only each against the recorded transcripts).

    python migration/parity/side_by_side.py \
        --legacy-mvc http://localhost:5100 --legacy-webforms http://localhost:5101 \
        --migrated-mvc http://localhost:5200 --migrated-webforms http://localhost:5201 \
        --restart-legacy-mvc "..." --restart-legacy-webforms "..." \
        --restart-migrated-mvc "..." --restart-migrated-webforms "..." --report-dir migration/parity/results/raw/mock

Per app, each side is restarted (fresh singleton mock catalog) and then runs the app's read flows followed
by its ordered mutation sequence, exactly like replay.py. For every flow four comparisons are recorded:
  contract  : replay.py projection (CONTRACT.md parity rules) legacy vs migrated
  strict    : the whole normalised response (full Content-Type incl. charset, every body field, error
              bodies too) legacy vs migrated -- reported, never hidden
  headers   : every response header except the HOST_HEADERS listed below; Set-Cookie compared by cookie name
  data_state: full catalog snapshot (row order) after each mutating flow, plus the initial catalog
and each side is also checked against the recorded legacy transcript (baseline pass/fail).
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import subprocess
import sys
from pathlib import Path
from typing import Any

import requests

sys.path.insert(0, str(Path(__file__).resolve().parent))
import parity_common as pc  # noqa: E402
import replay  # noqa: E402

# Headers that describe the web server / transport, not the application. Excluded from the header diff
# (the explicit normalisation list; everything else is compared).
HOST_HEADERS = {"date", "server", "content-length", "connection", "keep-alive", "transfer-encoding",
                "x-aspnet-version", "x-aspnetmvc-version", "x-powered-by", "x-sourcefiles", "etag",
                "last-modified", "accept-ranges"}


def run_side(app: str, base: str, flows: list[dict[str, Any]], restart_cmd: str | None) -> dict[str, Any]:
    if restart_cmd:
        subprocess.run(restart_cmd, shell=True, check=True)
    replay.wait_ready(base, app)
    session = requests.Session()
    norm = pc.Normaliser(base)
    out: dict[str, Any] = {"initial_state": pc.fetch_data_state(session, base, app), "flows": {}}
    for flow in sorted((f for f in flows if f["app"] == app), key=lambda f: f["order"]):
        entry: dict[str, Any] = {}
        try:
            resp, record = pc.execute(session, base, flow)
            entry["request"] = record
            entry["response"] = pc.normalise_response(norm, resp, resp.url)
            entry["raw_headers"] = header_view(resp, norm)
        except Exception as exc:
            entry["error"] = f"{type(exc).__name__}: {exc}"
        if flow.get("snapshot"):
            entry["data_state"] = pc.fetch_data_state(session, base, app)
        out["flows"][flow["id"]] = entry
    return out


def header_view(resp: requests.Response, norm: pc.Normaliser) -> dict[str, Any]:
    h: dict[str, Any] = {}
    for k, v in resp.headers.items():
        lk = k.lower()
        if lk in HOST_HEADERS or lk == "set-cookie":
            continue
        if lk == "location":
            v = norm.url(v)
        elif lk == "expires" and v != "-1":
            v = "<http-date>"  # absolute date = request time + max-age; only presence/shape is comparable
        h[lk] = v
    cookies = []
    for c in resp.raw.headers.getlist("Set-Cookie") if hasattr(resp.raw.headers, "getlist") else []:
        cookies.append(c.split("=", 1)[0].strip())
    if cookies:
        h["set-cookie(names)"] = sorted(set(cookies))
    return h


def compare(app: str, legacy: dict[str, Any], migrated: dict[str, Any], flows: list[dict[str, Any]],
            baseline: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    init = json.loads((baseline / "data-state" / f"{app}-initial.json").read_text())
    init.pop("app", None)
    d = pc.diff(legacy["initial_state"], migrated["initial_state"], "$.data_state")
    rows.append({"id": f"{app}-initial-state", "app": app, "title": "Initial seeded catalog (full list)",
                 "mutates": False, "legacy_status": legacy["initial_state"]["status"],
                 "migrated_status": migrated["initial_state"]["status"],
                 "contract_diffs": d, "strict_diffs": d, "header_diffs": [], "data_state_diffs": d,
                 "data_state_checked": True,
                 "legacy_vs_baseline": pc.diff(init, legacy["initial_state"]),
                 "migrated_vs_baseline": pc.diff(init, migrated["initial_state"])})
    for flow in sorted((f for f in flows if f["app"] == app), key=lambda f: f["order"]):
        fid = flow["id"]
        L, M = legacy["flows"][fid], migrated["flows"][fid]
        row: dict[str, Any] = {"id": fid, "app": app, "title": flow["title"], "mutates": bool(flow.get("mutates")),
                               "request": L.get("request", {"method": flow["request"]["method"],
                                                            "path": flow["request"]["path"]})}
        if "error" in L or "error" in M:
            row.update(legacy_status=None, migrated_status=None,
                       contract_diffs=[f"legacy error: {L.get('error')}", f"migrated error: {M.get('error')}"],
                       strict_diffs=[], header_diffs=[], data_state_diffs=[], data_state_checked=False,
                       legacy_vs_baseline=[f"error: {L['error']}"] if "error" in L else [],
                       migrated_vs_baseline=[f"error: {M['error']}"] if "error" in M else [])
        else:
            lr, mr = L["response"], M["response"]
            row["legacy_status"], row["migrated_status"] = lr["status"], mr["status"]
            row["contract_diffs"] = pc.diff(pc.projection(flow, lr), pc.projection(flow, mr), "$.response")
            row["strict_diffs"] = pc.diff(lr, mr, "$.response", limit=60)
            row["header_diffs"] = pc.diff(L["raw_headers"], M["raw_headers"], "$.headers")
            row["data_state_checked"] = bool(flow.get("snapshot"))
            row["data_state_diffs"] = (pc.diff(L.get("data_state"), M.get("data_state"), "$.data_state")
                                       if flow.get("snapshot") else [])
            tpath = baseline / "transcripts" / f"{fid}.json"
            t = json.loads(tpath.read_text())["response"]
            exp_state = None
            if flow.get("snapshot"):
                exp_state = json.loads((baseline / "data-state" / f"{fid}.json").read_text())
                exp_state.pop("app", None)
            for side, e in (("legacy", L), ("migrated", M)):
                dd = pc.diff(pc.projection(flow, t), pc.projection(flow, e["response"]), "$.response")
                if exp_state is not None:
                    dd += pc.diff(exp_state, e.get("data_state"), "$.data_state")
                row[f"{side}_vs_baseline"] = dd
        rows.append(row)
    for r in rows:
        r["contract_match"] = not r["contract_diffs"] and not r["data_state_diffs"]
        r["strict_match"] = r["contract_match"] and not r["strict_diffs"]
        r["headers_match"] = not r["header_diffs"]
    return rows


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    for side in ("legacy", "migrated"):
        for app in replay.APPS:
            ap.add_argument(f"--{side}-{app}")
            ap.add_argument(f"--restart-{side}-{app}")
    ap.add_argument("--baseline", type=Path, default=replay.BASELINE)
    ap.add_argument("--report-dir", type=Path, required=True)
    ap.add_argument("--apps", default="mvc,webforms")
    args = ap.parse_args(argv)
    flows = replay.load_flows(args.baseline)["flows"]
    rows: list[dict[str, Any]] = []
    raw: dict[str, Any] = {}
    targets: dict[str, Any] = {}
    for app in args.apps.split(","):
        lu, mu = getattr(args, f"legacy_{app}"), getattr(args, f"migrated_{app}")
        targets[app] = {"legacy": lu, "migrated": mu}
        legacy = run_side(app, lu, flows, getattr(args, f"restart_legacy_{app}"))
        migrated = run_side(app, mu, flows, getattr(args, f"restart_migrated_{app}"))
        raw[app] = {"legacy": legacy, "migrated": migrated}
        rows += compare(app, legacy, migrated, flows, args.baseline)
    args.report_dir.mkdir(parents=True, exist_ok=True)
    summary = {"generated_at": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"),
               "targets": targets, "host_headers_ignored": sorted(HOST_HEADERS),
               "checks": len(rows),
               "contract_match": sum(r["contract_match"] for r in rows),
               "strict_match": sum(r["strict_match"] for r in rows),
               "headers_match": sum(r["headers_match"] for r in rows),
               "data_state_checks": sum(r["data_state_checked"] for r in rows),
               "data_state_match": sum(r["data_state_checked"] and not r["data_state_diffs"] for r in rows),
               "legacy_vs_baseline_pass": sum(not r["legacy_vs_baseline"] for r in rows if "legacy_vs_baseline" in r),
               "migrated_vs_baseline_pass": sum(not r["migrated_vs_baseline"] for r in rows if "migrated_vs_baseline" in r)}
    (args.report_dir / "side-by-side.json").write_text(json.dumps({"summary": summary, "results": rows}, indent=2))
    (args.report_dir / "side-by-side-raw.json").write_text(json.dumps(raw, indent=1))
    lines = [f"Side-by-side replay {summary['generated_at']}", json.dumps(targets), ""]
    for k in ("checks", "contract_match", "strict_match", "headers_match", "data_state_checks", "data_state_match",
              "legacy_vs_baseline_pass", "migrated_vs_baseline_pass"):
        lines.append(f"  {k:28s} {summary[k]}")
    lines.append("")
    for r in rows:
        tag = "SAME " if r["strict_match"] and r["headers_match"] else ("CONTR" if r["contract_match"] else "DIFF ")
        lines.append(f"{tag} {r['id']}  legacy={r.get('legacy_status')} migrated={r.get('migrated_status')}")
        for d in (r["contract_diffs"] + r["data_state_diffs"])[:10]:
            lines.append(f"        contract: {d}")
        if r["contract_match"]:
            for d in r["strict_diffs"][:6]:
                lines.append(f"        strict:   {d}")
        for d in r["header_diffs"][:6]:
            lines.append(f"        header:   {d}")
    (args.report_dir / "side-by-side.txt").write_text("\n".join(lines) + "\n")
    print("\n".join(lines[:12]))
    return 0 if summary["contract_match"] == summary["checks"] else 1


if __name__ == "__main__":
    sys.exit(main())
