#!/usr/bin/env python3
"""Replays the recorded legacy transcripts against any MVC / WebForms base URL and diffs the
normalised responses and the full-catalog data-state snapshots.

    python migration/parity/replay.py --mvc-url http://localhost:5200 --webforms-url http://localhost:5201 \
        --restart-cmd-mvc "<cmd that restarts the app on a fresh mock catalog>" --report-dir out/

Flows run per app in flows.json order: 'read' flows first, then the ordered 'mutate' sequence. The app
must start from the seeded catalog (verified against data-state/<app>-initial.json); pass
--restart-cmd-<app> to have the harness restart it first. Exit code 0 only if every flow matches.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import subprocess
import sys
import time
from pathlib import Path
from typing import Any

import requests

sys.path.insert(0, str(Path(__file__).resolve().parent))
import parity_common as pc  # noqa: E402

BASELINE = Path(__file__).resolve().parent.parent / "baseline"
APPS = ("mvc", "webforms")


def load_flows(baseline: Path) -> dict[str, Any]:
    return json.loads((baseline / "flows.json").read_text())


def wait_ready(base: str, app: str, timeout: float = 300) -> None:
    deadline = time.time() + timeout
    url = base.rstrip("/") + pc.DATA_STATE_PATHS[app]
    while time.time() < deadline:
        try:
            if requests.get(url, timeout=120).status_code == 200:
                return
        except requests.RequestException:
            pass
        time.sleep(1)
    raise RuntimeError(f"{app} at {base} not ready after {timeout}s")


def run_app(app: str, base: str, flows: list[dict[str, Any]], restart_cmd: str | None = None,
            only: set[str] | None = None) -> dict[str, Any]:
    """Executes the app's flows in order; returns actual normalised outputs + data-state snapshots."""
    if restart_cmd:
        subprocess.run(restart_cmd, shell=True, check=True)
    wait_ready(base, app)
    session = requests.Session()
    norm = pc.Normaliser(base)
    result: dict[str, Any] = {"initial_state": pc.fetch_data_state(session, base, app), "flows": []}
    for flow in sorted((f for f in flows if f["app"] == app), key=lambda f: f["order"]):
        if only and flow["id"] not in only and not flow.get("mutates"):
            continue
        entry: dict[str, Any] = {"flow": flow}
        try:
            resp, record = pc.execute(session, base, flow)
            entry["request"] = record
            entry["response"] = pc.normalise_response(norm, resp, resp.url)
        except Exception as exc:  # recorded as a failure, replay continues
            entry["error"] = f"{type(exc).__name__}: {exc}"
        if flow.get("snapshot"):
            entry["data_state"] = pc.fetch_data_state(session, base, app)
        result["flows"].append(entry)
    return result


def compare_app(app: str, actual: dict[str, Any], baseline: Path) -> list[dict[str, Any]]:
    results = []
    initial = json.loads((baseline / "data-state" / f"{app}-initial.json").read_text())
    initial.pop("app", None)
    d = pc.diff(initial, actual["initial_state"], "$.initial_state")
    results.append({"id": f"{app}-initial-state", "app": app, "passed": not d, "diffs": d})
    for entry in actual["flows"]:
        flow = entry["flow"]
        tpath = baseline / "transcripts" / f"{flow['id']}.json"
        res: dict[str, Any] = {"id": flow["id"], "app": app, "title": flow["title"], "diffs": []}
        if not tpath.exists():
            res["diffs"].append("no recorded transcript")
        elif "error" in entry:
            res["diffs"].append(f"replay error: {entry['error']}")
        else:
            transcript = json.loads(tpath.read_text())
            res["diffs"] += pc.diff(pc.projection(flow, transcript["response"]), pc.projection(flow, entry["response"]),
                                    "$.response")
            if flow.get("snapshot"):
                expected_state = json.loads((baseline / "data-state" / f"{flow['id']}.json").read_text())
                expected_state.pop("app", None)
                res["diffs"] += pc.diff(expected_state, entry.get("data_state"), "$.data_state")
            res["status"] = {"expected": transcript["response"]["status"], "actual": entry["response"]["status"]}
        res["passed"] = not res["diffs"]
        results.append(res)
    return results


def write_report(results: list[dict[str, Any]], targets: dict[str, str], report_dir: Path) -> dict[str, Any]:
    report_dir.mkdir(parents=True, exist_ok=True)
    summary: dict[str, Any] = {"generated_at": dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds"),
                               "targets": targets, "apps": {}}
    for app in targets:
        rs = [r for r in results if r["app"] == app]
        summary["apps"][app] = {"total": len(rs), "passed": sum(r["passed"] for r in rs),
                                "failed": sum(not r["passed"] for r in rs)}
    summary["total"] = len(results)
    summary["passed"] = sum(r["passed"] for r in results)
    summary["failed"] = summary["total"] - summary["passed"]
    (report_dir / "parity-report.json").write_text(json.dumps({"summary": summary, "results": results}, indent=2))
    lines = [f"Parity replay {summary['generated_at']}"]
    for app, s in summary["apps"].items():
        lines.append(f"  {app:9s} {targets[app]:28s} {s['passed']}/{s['total']} passed")
    lines.append(f"  TOTAL     {summary['passed']}/{summary['total']} passed, {summary['failed']} failed")
    lines.append("")
    for r in results:
        lines.append(f"{'PASS' if r['passed'] else 'FAIL'}  {r['id']}")
        for d in r["diffs"][:15]:
            lines.append(f"        {d}")
    (report_dir / "parity-report.txt").write_text("\n".join(lines) + "\n")
    return summary


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--mvc-url")
    ap.add_argument("--webforms-url")
    ap.add_argument("--restart-cmd-mvc", help="shell command run before the MVC flows (resets the mock catalog)")
    ap.add_argument("--restart-cmd-webforms")
    ap.add_argument("--baseline", type=Path, default=BASELINE)
    ap.add_argument("--report-dir", type=Path, default=Path(__file__).resolve().parent / "out")
    ap.add_argument("--only", help="comma-separated flow ids (mutating flows always run to keep state ordering)")
    args = ap.parse_args(argv)
    targets = {a: u.rstrip("/") for a, u in (("mvc", args.mvc_url), ("webforms", args.webforms_url)) if u}
    if not targets:
        ap.error("pass --mvc-url and/or --webforms-url")
    flows = load_flows(args.baseline)["flows"]
    only = set(args.only.split(",")) if args.only else None
    results: list[dict[str, Any]] = []
    for app, base in targets.items():
        actual = run_app(app, base, flows, getattr(args, f"restart_cmd_{app}"), only)
        results += compare_app(app, actual, args.baseline)
    summary = write_report(results, targets, args.report_dir)
    print((args.report_dir / "parity-report.txt").read_text().split("\n\n")[0])
    return 0 if summary["failed"] == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
