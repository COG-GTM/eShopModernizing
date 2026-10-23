#!/usr/bin/env python3
"""Record the legacy baseline: one normalised transcript per flow + data-state snapshots after mutating flows.

  python3 capture.py --mvc-url http://127.0.0.1:5100 --webforms-url http://127.0.0.1:5101 \
      --restart-cmd-mvc "legacy/start-legacy.sh mvc" --restart-cmd-webforms "legacy/start-legacy.sh webforms"
"""
import argparse
import pathlib
import shutil
import sys

import runner


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--mvc-url")
    ap.add_argument("--webforms-url")
    ap.add_argument("--restart-cmd-mvc")
    ap.add_argument("--restart-cmd-webforms")
    ap.add_argument("--flows", default=str(runner.BASELINE / "flows.json"))
    ap.add_argument("--out", default=str(runner.BASELINE), help="writes <out>/transcripts and <out>/data-state")
    ap.add_argument("--only", nargs="*", help="sequence ids")
    a = ap.parse_args()
    doc = runner.load_flows(a.flows)
    targets = {k: v for k, v in (("mvc", a.mvc_url), ("webforms", a.webforms_url)) if v}
    if not targets:
        ap.error("give --mvc-url and/or --webforms-url")
    out = pathlib.Path(a.out)
    tdir, sdir = out / "transcripts", out / "data-state"
    if not a.only:
        for d in (tdir, sdir):
            for app in targets:
                for f in d.glob("%s-*.json" % app):
                    f.unlink()
    errors = 0
    for seq, flow, tr, snap in runner.run_sequences(doc, targets, {"mvc": a.restart_cmd_mvc, "webforms": a.restart_cmd_webforms}, only=a.only):
        tr["sequence"] = seq["id"]
        for k in ("description", "quirk", "compare"):
            if k in flow:
                tr[k] = flow[k]
        tr.pop("elapsed_s", None)
        if snap is not None:
            tr["data_state"] = "data-state/%s.json" % flow["id"]
            runner.write_json(sdir / ("%s.json" % flow["id"]), snap)
        errors += "error" in tr
        runner.write_json(tdir / ("%s.json" % flow["id"]), tr)
    print("capture finished, %d flow errors" % errors)
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
