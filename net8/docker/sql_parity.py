#!/usr/bin/env python3
"""Compares the .NET 8 SQL-mode replay with legacy SQL mode (modernization/baseline/sql-mode).

Both replays use the mock-mode transcripts as expectation, so each case is either a pass or a list of diffs. The .NET 8 SQL mode
matches legacy SQL mode when, per case, the outcome is the same and every diff (field, expected, actual) is identical: WebForms then
differs from mock only in the documented price formatting (CONTRACT.md section 6). One case is held to the mock baseline instead:

  mvc 071-api-files  legacy SQL mode serialised a 721-byte List<BrandDTO> (internal capacity differs from mock's 719 bytes).
                     The acceptance (TICKETS.md T7: MVC 86/86) requires the .NET 8 SQL mode to return the mock bytes.

Also checks the seed counts from `stack.sh sql seed` (12 items, 5 brands, 4 types, 3 HiLo sequences per app database).
"""
import argparse
import csv
import json
import pathlib
import sys

REPO = pathlib.Path(__file__).resolve().parents[2]
LEGACY = REPO / "modernization" / "baseline" / "sql-mode"
MUST_MATCH_MOCK = {("mvc", "071-api-files.json")}
EXPECTED_SEED = {"items": 12, "brands": 5, "types": 4, "hilo_sequences": 3}


def load(path):
    rep = json.loads(pathlib.Path(path).read_text())
    return rep, {r["case"]: r for r in rep["results"]}


def compare_app(app, results_dir):
    legacy_rep, legacy = load(LEGACY / f"{app}-mock-transcripts-vs-sql.json")
    net8_rep, net8 = load(pathlib.Path(results_dir) / f"sql-{app}.json")
    cases = []
    for name in sorted(set(legacy) | set(net8)):
        lg, nt = legacy.get(name), net8.get(name)
        if lg is None or nt is None:
            cases.append({"case": name, "match": False, "reason": "missing in " + ("legacy" if lg is None else ".NET 8")})
            continue
        if (app, name) in MUST_MATCH_MOCK:
            ok = nt["result"] == "pass"
            reason = "held to the mock baseline (legacy SQL bytes differ)"
        else:
            ok = nt["result"] == lg["result"] and nt.get("diffs", []) == lg.get("diffs", [])
            reason = "same outcome and diffs" if ok else f"legacy={lg['result']} net8={nt['result']} (diffs differ)"
        cases.append({"case": name, "match": ok, "legacy": lg["result"], "net8": nt["result"], "reason": reason})
    return {
        "app": app,
        "legacy_vs_mock": f"{legacy_rep['passed']}/{legacy_rep['total']}",
        "net8_vs_mock": f"{net8_rep['passed']}/{net8_rep['total']}",
        "matching_legacy_sql": f"{sum(c['match'] for c in cases)}/{len(cases)}",
        "ok": all(c["match"] for c in cases),
        "cases": cases,
    }


def check_seed(path):
    rows, ok = [], True
    for row in csv.reader(l for l in pathlib.Path(path).read_text().splitlines() if l.strip() and not l.startswith("database,")):
        db, counts = row[0], dict(zip(EXPECTED_SEED, map(int, row[1:])))
        good = counts == EXPECTED_SEED
        ok &= good
        rows.append({"database": db, **counts, "ok": good})
    return {"ok": ok and len(rows) == 2, "databases": rows}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--results", required=True, help="directory with sql-mvc.json / sql-webforms.json from transcript.py replay")
    ap.add_argument("--seed", help="CSV printed by `stack.sh sql seed` right after a reset")
    ap.add_argument("--report")
    args = ap.parse_args()

    out = {"apps": [compare_app(app, args.results) for app in ("mvc", "webforms")]}
    if args.seed:
        out["seed"] = check_seed(args.seed)
    out["ok"] = all(a["ok"] for a in out["apps"]) and out.get("seed", {"ok": True})["ok"]
    if args.report:
        pathlib.Path(args.report).write_text(json.dumps(out, indent=2) + "\n")

    if "seed" in out:
        for d in out["seed"]["databases"]:
            print(f"seed {d['database']}: items={d['items']} brands={d['brands']} types={d['types']} "
                  f"hilo_sequences={d['hilo_sequences']} {'OK' if d['ok'] else 'MISMATCH'}")
    for a in out["apps"]:
        for c in a["cases"]:
            if not c["match"]:
                print(f"MISMATCH {a['app']} {c['case']}: {c['reason']}")
        print(f"{a['app']}: .NET 8 SQL vs mock transcripts {a['net8_vs_mock']} (legacy SQL {a['legacy_vs_mock']}); "
              f"matches legacy SQL mode {a['matching_legacy_sql']}")
    print("sql parity: " + ("OK" if out["ok"] else "FAILED"))
    return 0 if out["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
