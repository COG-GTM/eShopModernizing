#!/usr/bin/env python3
"""Records the legacy baseline: runs every flow in migration/baseline/flows.json against the legacy
apps (Mono + xsp4, see legacy/setup-legacy.sh) and writes transcripts/<flow-id>.json plus
data-state/<app>-initial.json and data-state/<flow-id>.json for every snapshot flow.

    python migration/parity/capture.py --mvc-url http://localhost:5100 --webforms-url http://localhost:5101 \
        --restart-cmd-mvc "migration/parity/legacy/start-legacy.sh mvc" \
        --restart-cmd-webforms "migration/parity/legacy/start-legacy.sh webforms"
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from replay import BASELINE, load_flows, run_app  # noqa: E402


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--mvc-url")
    ap.add_argument("--webforms-url")
    ap.add_argument("--restart-cmd-mvc")
    ap.add_argument("--restart-cmd-webforms")
    ap.add_argument("--baseline", type=Path, default=BASELINE)
    args = ap.parse_args(argv)
    doc = load_flows(args.baseline)
    tdir, sdir = args.baseline / "transcripts", args.baseline / "data-state"
    captured_at = dt.datetime.now(dt.timezone.utc).isoformat(timespec="seconds")
    counts = {}
    for app, base in (("mvc", args.mvc_url), ("webforms", args.webforms_url)):
        if not base:
            continue
        for d in (tdir, sdir):
            d.mkdir(parents=True, exist_ok=True)
        for old in list(tdir.glob("*.json")) + list(sdir.glob("*.json")):
            if json.loads(old.read_text()).get("app") == app:
                old.unlink()
        actual = run_app(app, base, doc["flows"], getattr(args, f"restart_cmd_{app}"))
        (sdir / f"{app}-initial.json").write_text(json.dumps({"app": app, **actual["initial_state"]}, indent=2))
        for entry in actual["flows"]:
            flow = entry["flow"]
            if "error" in entry:
                raise SystemExit(f"{flow['id']}: {entry['error']}")
            transcript = {"flow_id": flow["id"], "app": app, "title": flow["title"], "captured_at": captured_at,
                          "captured_against": base, "runtime": "legacy .NET Framework 4.7.2 on Mono 6.12 + xsp4",
                          "request": entry["request"], "response": entry["response"]}
            if flow.get("snapshot"):
                transcript["data_state"] = f"data-state/{flow['id']}.json"
                (sdir / f"{flow['id']}.json").write_text(json.dumps({"app": app, **entry["data_state"]}, indent=2))
            (tdir / f"{flow['id']}.json").write_text(json.dumps(transcript, indent=2))
        counts[app] = len(actual["flows"])
        print(f"{app}: {counts[app]} transcripts, statuses:",
              sorted({e['response']['status'] for e in actual['flows']}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
