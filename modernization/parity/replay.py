#!/usr/bin/env python3
"""Replay the recorded baseline against any target and compare per flow.

  python3 replay.py --mvc-url http://127.0.0.1:5200 --webforms-url http://127.0.0.1:5201 \
      [--restart-cmd-mvc CMD] [--restart-cmd-webforms CMD] --report-dir modernization/parity/report [--accept-quirks]

Verdicts: identical | equivalent-after-normalisation | different (see modernization/CONTRACT.md).
Exit code 1 when any flow is `different` (or errored), except quirk-tagged flows when --accept-quirks is given and
flows listed in --known-divergences (same file as the xUnit parity tests) whose status matches and whose every difference
reason starts with one of the entry's `accept` prefixes.
"""
import argparse
import collections
import json
import pathlib
import sys
import time

import normalize as N
import runner


def is_known_divergence(res, entry):
    """Same rule as the xUnit TranscriptParityTests: status must match and every reason must start with an accept prefix.
    Python renders a missing content type as None where the C# port renders ''; both spellings are accepted."""
    if not entry or res.get("status") is None or res.get("status") != res.get("expected_status"):
        return False
    prefixes = [p for p in entry.get("accept", []) if not p.startswith("status")]
    return bool(prefixes) and all(any(r.replace("!= None", "!= ''").startswith(p) for p in prefixes) for r in res["reasons"])


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--mvc-url")
    ap.add_argument("--webforms-url")
    ap.add_argument("--restart-cmd-mvc")
    ap.add_argument("--restart-cmd-webforms")
    ap.add_argument("--baseline", default=str(runner.BASELINE))
    ap.add_argument("--report-dir", required=True)
    ap.add_argument("--only", nargs="*", help="sequence ids")
    ap.add_argument("--accept-quirks", action="store_true", help="do not fail on flows tagged with a documented legacy quirk")
    ap.add_argument("--known-divergences", help="JSON of documented divergences (eShopNet8/tests/eShopNet8.IntegrationTests/Parity/known-divergences.json)")
    a = ap.parse_args()
    known = json.loads(pathlib.Path(a.known_divergences).read_text())["flows"] if a.known_divergences else {}
    base = pathlib.Path(a.baseline)
    doc = runner.load_flows(base / "flows.json")
    targets = {k: v for k, v in (("mvc", a.mvc_url), ("webforms", a.webforms_url)) if v}
    if not targets:
        ap.error("give --mvc-url and/or --webforms-url")
    results, started = [], time.time()
    for seq, flow, tr, snap in runner.run_sequences(doc, targets, {"mvc": a.restart_cmd_mvc, "webforms": a.restart_cmd_webforms}, only=a.only):
        exp_path = base / "transcripts" / ("%s.json" % flow["id"])
        res = {"id": flow["id"], "sequence": seq["id"], "app": seq["app"], "quirk": flow.get("quirk")}
        if not exp_path.exists():
            res.update(verdict="different", reasons=["no baseline transcript"])
        elif "error" in tr:
            res.update(verdict="different", reasons=["replay error: " + tr["error"]])
        else:
            exp = json.loads(exp_path.read_text())
            if "error" in exp:
                res.update(verdict="different", reasons=["baseline flow errored: " + exp["error"]])
            else:
                verdict, reasons = N.compare(exp["response"], tr["response"], flow.get("compare", "full"))
                if flow.get("snapshot"):
                    exp_snap = json.loads((base / exp["data_state"]).read_text()) if exp.get("data_state") else None
                    if exp_snap != snap:
                        verdict = "different"
                        reasons.append("data-state differs: %s" % N.first_diff(
                            (exp_snap or {}).get("items", []), (snap or {}).get("items", []))
                            if (exp_snap or {}).get("items") != (snap or {}).get("items") else "data-state lookups/status differ")
                res.update(verdict=verdict, reasons=reasons, status=tr["response"]["status"], expected_status=exp["response"]["status"])
        res["accepted"] = res["verdict"] == "different" and a.accept_quirks and bool(res["quirk"])
        res["known_divergence"] = res["verdict"] == "different" and not res["accepted"] and is_known_divergence(res, known.get(res["id"]))
        results.append(res)
        if res["verdict"] == "different":
            tag = " (accepted quirk %s)" % res["quirk"] if res["accepted"] else " (known divergence)" if res["known_divergence"] else ""
            print("    DIFFERENT%s: %s" % (tag, "; ".join(res["reasons"])[:400]))
    counts = collections.Counter(r["verdict"] for r in results)
    failing = [r for r in results if r["verdict"] == "different" and not r["accepted"] and not r["known_divergence"]]
    summary = {"targets": targets, "flows": len(results), "verdicts": dict(counts), "failing": len(failing),
               "accepted_quirks": sum(r["accepted"] for r in results),
               "known_divergences": sum(r["known_divergence"] for r in results), "duration_s": round(time.time() - started, 1),
               "baseline_flows": sum(len(s["flows"]) for s in doc["sequences"] if s["app"] in targets and (not a.only or s["id"] in a.only))}
    out = pathlib.Path(a.report_dir)
    out.mkdir(parents=True, exist_ok=True)
    (out / "replay-report.json").write_text(json.dumps({"summary": summary, "results": results}, indent=1) + "\n")
    lines = ["eShop parity replay", "targets: %s" % targets, "summary: %s" % json.dumps(summary["verdicts"]),
             "failing: %d  accepted quirks: %d  known divergences: %d  flows: %d/%d" % (
                 len(failing), summary["accepted_quirks"], summary["known_divergences"], len(results), summary["baseline_flows"]), ""]
    for r in results:
        tag = " [quirk %s%s]" % (r["quirk"], ", accepted" if r["accepted"] else "") if r["quirk"] else ""
        tag += " [known divergence]" if r["known_divergence"] else ""
        lines.append("%-34s %-52s %s%s" % (r["sequence"], r["id"], r["verdict"], tag))
        for reason in r.get("reasons", []):
            lines.append("      - " + reason[:300])
    (out / "replay-report.txt").write_text("\n".join(lines) + "\n")
    print("\n".join(lines[:5]))
    return 1 if failing or len(results) < summary["baseline_flows"] else 0


if __name__ == "__main__":
    sys.exit(main())
