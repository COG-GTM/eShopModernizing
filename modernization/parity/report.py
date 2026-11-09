#!/usr/bin/env python3
"""Builds results/characterization.json, results/summary.json and PARITY-REPORT.md from a parity run.

Inputs: results/{mvc,webforms}.json (transcript.py replay reports), results/characterization.xml (pytest junit),
legacy evidence in ../baseline (determinism replays + legacy-test-results.xml) and accepted-differences.json.
Exit code 0 only when every transcript and test passes or is an accepted difference listed with its CONTRACT.md reference."""
import datetime
import json
import os
import pathlib
import subprocess
import sys
import xml.etree.ElementTree as ET

PARITY = pathlib.Path(__file__).resolve().parent
REPO = PARITY.parent.parent
RESULTS = pathlib.Path(os.environ.get("PARITY_RESULTS_DIR", PARITY / "results"))
BASELINE = REPO / "modernization" / "baseline"
APPS = ("mvc", "webforms")


def load_json(path):
    return json.loads(path.read_text()) if path.exists() else None


def junit(path):
    """[{id, suite, outcome, message}] from a pytest junit xml file."""
    if not path.exists():
        return None
    tests = []
    for tc in ET.parse(path).getroot().iter("testcase"):
        outcome, message = "passed", ""
        for tag in ("failure", "error", "skipped"):
            el = tc.find(tag)
            if el is not None:
                outcome, message = {"failure": "failed", "error": "error", "skipped": "skipped"}[tag], el.get("message", "")
                break
        module = tc.get("classname", "").rsplit(".", 1)[-1]
        tests.append({"id": f"{module}::{tc.get('name')}", "suite": module.removeprefix("test_"),
                      "outcome": outcome, "message": message[:500]})
    return tests


def accepted():
    data = load_json(PARITY / "accepted-differences.json") or {"transcripts": [], "tests": []}
    for entry in data.get("transcripts", []) + data.get("tests", []):
        if "CONTRACT.md" not in entry.get("contract", ""):
            sys.exit(f"accepted difference without a CONTRACT.md reference: {entry}")
    return ({(e["app"], e["case"]): e for e in data.get("transcripts", [])},
            {e["id"]: e for e in data.get("tests", [])})


def git_head():
    try:
        return subprocess.run(["git", "-C", str(REPO), "rev-parse", "--short", "HEAD"], capture_output=True, text=True,
                              check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return "unknown"


def main():
    acc_transcripts, acc_tests = accepted()
    summary = {"generated": datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds"),
               "commit": git_head(), "transcripts": {}, "characterization": {}, "ok": True}
    lines = []

    for app in APPS:
        legacy = load_json(BASELINE / "determinism" / f"{app}-replay-1.json")
        legacy_total = len(list((BASELINE / "transcripts" / app).glob("*.json")))
        rep = load_json(RESULTS / f"{app}.json")
        entry = {"legacy_passed": legacy["passed"] if legacy else None, "legacy_total": legacy_total,
                 "passed": 0, "total": 0, "accepted": [], "failures": []}
        if rep is None:
            entry["error"] = f"results/{app}.json missing (replay did not run)"
        else:
            entry.update(passed=rep["passed"], total=rep["total"], base_url=rep["base_url"], strict=rep["strict"])
            for r in rep["results"]:
                if r["result"] == "pass":
                    continue
                item = {"case": r["case"], "result": r["result"], "fields": [d["field"] for d in r.get("diffs", [])]}
                if (app, r["case"]) in acc_transcripts:
                    entry["accepted"].append({**item, "contract": acc_transcripts[(app, r["case"])]["contract"]})
                else:
                    entry["failures"].append(item)
        entry["ok"] = "error" not in entry and not entry["failures"] and entry["total"] == legacy_total
        summary["ok"] &= entry["ok"]
        summary["transcripts"][app] = entry

    legacy_tests = junit(BASELINE / "legacy-test-results.xml") or []
    legacy_by_id = {t["id"]: t["outcome"] for t in legacy_tests}
    tests = junit(RESULTS / "characterization.xml")
    ch = {"legacy_passed": sum(t["outcome"] == "passed" for t in legacy_tests), "legacy_total": len(legacy_tests),
          "passed": 0, "total": 0, "by_suite": {}, "accepted": [], "failures": [], "not_run": []}
    if tests is None:
        ch["error"] = "results/characterization.xml missing (pytest did not run)"
    else:
        (RESULTS / "characterization.json").write_text(json.dumps({"tests": tests}, indent=2) + "\n")
        ch["total"] = len(tests)
        ch["passed"] = sum(t["outcome"] == "passed" for t in tests)
        for t in tests:
            s = ch["by_suite"].setdefault(t["suite"], {"passed": 0, "total": 0,
                                                       "legacy_passed": 0, "legacy_total": 0})
            s["total"] += 1
            s["passed"] += t["outcome"] == "passed"
            if t["outcome"] != "passed":
                item = {"id": t["id"], "outcome": t["outcome"], "message": t["message"].splitlines()[0][:200] if t["message"] else "",
                        "legacy": legacy_by_id.get(t["id"], "absent")}
                if t["id"] in acc_tests:
                    ch["accepted"].append({**item, "contract": acc_tests[t["id"]]["contract"]})
                else:
                    ch["failures"].append(item)
        for t in legacy_tests:
            s = ch["by_suite"].setdefault(t["suite"], {"passed": 0, "total": 0, "legacy_passed": 0, "legacy_total": 0})
            s["legacy_total"] += 1
            s["legacy_passed"] += t["outcome"] == "passed"
        ran = {t["id"] for t in tests}
        ch["not_run"] = sorted(i for i in legacy_by_id if i not in ran)
    ch["ok"] = "error" not in ch and not ch["failures"] and not ch["not_run"] and ch["total"] == ch["legacy_total"]
    summary["ok"] &= ch["ok"]
    summary["characterization"] = ch
    (RESULTS / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")

    t_pass = sum(summary["transcripts"][a]["passed"] for a in APPS)
    t_total = sum(summary["transcripts"][a]["legacy_total"] for a in APPS)
    t_legacy = sum(summary["transcripts"][a]["legacy_passed"] or 0 for a in APPS)
    lines += ["# .NET 8 storefront parity report", "",
              f"Generated {summary['generated']} at commit `{summary['commit']}` by `modernization/parity/run-parity.sh`. "
              "Each transcript set is replayed against a freshly started .NET 8 app in mock mode "
              "(`UseMockData=true`; MVC `127.0.0.1:5200`, Razor Pages `127.0.0.1:5201`), compared per CONTRACT.md §3.", "",
              f"**Overall: {'PARITY' if summary['ok'] else 'NOT AT PARITY'}** — transcripts {t_pass}/{t_total} "
              f"(legacy {t_legacy}/{t_total}), characterization tests {ch['passed']}/{ch['legacy_total']} "
              f"(legacy {ch['legacy_passed']}/{ch['legacy_total']}).", "",
              "## Transcripts (`modernization/tools/transcript.py replay`)", "",
              "| App | .NET 8 pass/total | Legacy pass/total | Accepted differences | Report |", "|---|---|---|---|---|"]
    for app in APPS:
        e = summary["transcripts"][app]
        lp = "?" if e["legacy_passed"] is None else e["legacy_passed"]
        lines.append(f"| {app} | {e['passed']}/{e['legacy_total']} | {lp}/{e['legacy_total']} | {len(e['accepted'])} | "
                     f"`results/{app}.json` |")
    lines += ["", "## Characterization suite (`modernization/tools/tests`, pytest)", "",
              "| Suite | .NET 8 pass/total | Legacy pass/total |", "|---|---|---|"]
    for suite, s in sorted(ch["by_suite"].items()):
        lines.append(f"| {suite} | {s['passed']}/{s['legacy_total']} | {s['legacy_passed']}/{s['legacy_total']} |")
    lines.append(f"| **total** | **{ch['passed']}/{ch['legacy_total']}** | **{ch['legacy_passed']}/{ch['legacy_total']}** |")

    lines += ["", "## Accepted differences", ""]
    acc = [(a, x) for a in APPS for x in summary["transcripts"][a]["accepted"]] + [("test", x) for x in ch["accepted"]]
    if acc:
        lines += ["| Where | Case | Differs in | CONTRACT.md reference |", "|---|---|---|---|"]
        for where, x in acc:
            lines.append(f"| {where} | `{x.get('case', x.get('id'))}` | {', '.join(x.get('fields', [])) or x.get('outcome', '')} | {x['contract']} |")
    else:
        lines.append("None.")

    lines += ["", "## Failures", ""]
    fails = []
    for app in APPS:
        e = summary["transcripts"][app]
        if "error" in e:
            fails.append(f"- {app}: {e['error']}")
        fails += [f"- {app} `{x['case']}`: {x['result']} ({', '.join(x['fields']) or 'no diff detail'})" for x in e["failures"]]
    if "error" in ch:
        fails.append(f"- characterization: {ch['error']}")
    fails += [f"- `{x['id']}`: {x['outcome']} (legacy: {x['legacy']}) {x['message']}" for x in ch["failures"]]
    fails += [f"- `{i}`: passed on legacy, not run against .NET 8" for i in ch["not_run"]]
    lines += fails or ["None."]
    lines += ["", "Re-run: `modernization/parity/run-parity.sh` (see `modernization/parity/README.md`).", ""]
    pathlib.Path(os.environ.get("PARITY_REPORT", PARITY / "PARITY-REPORT.md")).write_text("\n".join(lines))
    print(f"parity: transcripts {t_pass}/{t_total}, characterization {ch['passed']}/{ch['legacy_total']} "
          f"-> {'PARITY' if summary['ok'] else 'NOT AT PARITY'} (PARITY-REPORT.md)")
    return 0 if summary["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
