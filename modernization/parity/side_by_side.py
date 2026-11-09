#!/usr/bin/env python3
"""Side-by-side parity: replays every baseline transcript flow against the LEGACY apps (Mono + xsp4) and the MIGRATED
.NET 8 apps (Linux containers) at the same time, then diffs the two live responses.

  side_by_side.py --mode mock|sql [--apps mvc,webforms] [--out results/side-by-side-<mode>.json]

Per app: both sides are reset first (fresh seed: mock = new in-memory catalog, sql = dropped + re-seeded DB), the full
flow list (modernization/tools/flows.py, same order as the recorded baseline) runs against each side, and per case:
  * contract  - transcript.compare(legacy, migrated) with the CONTRACT.md section 3 rules (status, Location, media type,
                semantic view, data-state rows, 4xx JSON); also legacy-vs-baseline and migrated-vs-baseline
  * strict    - full normalised body for every status, plus the response headers that matter (HEADERS_COMPARED) and the
                set of runtime/server headers. Every strict difference must be explained by a rule in
                classifications.json (classify.py), otherwise it is reported as unexplained.
  * data-state- state-after-* snapshots (catalog rows) and, in sql mode, a dump of Catalog/CatalogBrand/CatalogType and the
                column schema from BOTH databases after the app's full replay.
Raw normalised bodies are written to <out-dir>/raw/<mode>/<app>/<case>.{legacy,migrated}.txt for inspection.
"""
import argparse
import concurrent.futures
import difflib
import json
import os
import pathlib
import subprocess
import sys

import requests

PARITY = pathlib.Path(__file__).resolve().parent
REPO = PARITY.parent.parent
sys.path.insert(0, str(REPO / "modernization" / "tools"))
import flows  # noqa: E402
import normalize  # noqa: E402
import transcript  # noqa: E402

HARNESS = REPO / "modernization" / "legacy-harness"
STACK = REPO / "net8" / "docker" / "stack.sh"
BASELINE = REPO / "modernization" / "baseline" / "transcripts"
LEGACY_SQL_ROOT = os.environ.get("LEGACY_SQL_ROOT", str(pathlib.Path.home() / "eshop-legacy-run-sql"))
LEGACY_MOCK_ROOT = os.environ.get("LEGACY_ROOT", str(pathlib.Path.home() / "eshop-legacy-run"))

# Response headers whose value is compared (lower-case). Everything else is a runtime/server header whose presence
# is compared as a set (RUNTIME_IGNORED are transport-level and never compared).
HEADERS_COMPARED = ("content-type", "location", "cache-control", "expires", "pragma", "allow", "content-disposition",
                    "content-encoding", "vary", "set-cookie")
RUNTIME_IGNORED = {"date", "content-length", "transfer-encoding", "connection", "keep-alive"}

SIDES = {
    "mock": {
        "legacy": {"mvc": "http://127.0.0.1:5100", "webforms": "http://127.0.0.1:5101",
                   "reset": f"LEGACY_ROOT='{LEGACY_MOCK_ROOT}' LEGACY_MODE=mock MVC_PORT=5100 WEBFORMS_PORT=5101 "
                            f"'{HARNESS}/reset.sh' {{app}} >/dev/null"},
        "migrated": {"mvc": "http://127.0.0.1:5200", "webforms": "http://127.0.0.1:5201",
                     "reset": f"'{STACK}' mock reset {{app}}"},
    },
    "sql": {
        "legacy": {"mvc": "http://127.0.0.1:5110", "webforms": "http://127.0.0.1:5111",
                   "reset": f"LEGACY_ROOT='{LEGACY_SQL_ROOT}' LEGACY_MODE=sql MVC_PORT=5110 WEBFORMS_PORT=5111 "
                            f"'{HARNESS}/reset.sh' {{app}} >/dev/null"},
        "migrated": {"mvc": "http://127.0.0.1:5210", "webforms": "http://127.0.0.1:5211",
                     "reset": f"'{STACK}' sql reset {{app}}"},
    },
}
SQL_DB = {"legacy": {"container": "eshop-legacy-sql", "db": "eShopLegacyCatalog_{app}"},
          "migrated": {"container": "eshop-storefront-sql-sqlserver-1", "db": "eShopStorefrontCatalog_{app}"}}
SQL_TABLES = {
    "Catalog": "Id, Name, Description, CAST(Price AS varchar(40)), PictureFileName, CatalogTypeId, CatalogBrandId, "
               "AvailableStock, RestockThreshold, MaxStockThreshold, CAST(OnReorder AS int)",
    "CatalogBrand": "Id, Brand",
    "CatalogType": "Id, Type",
}


class CapturingSession(requests.Session):
    """requests.Session that remembers the last response so raw headers/bytes are available after transcript.execute."""
    last = None

    def request(self, *a, **kw):
        r = super().request(*a, **kw)
        CapturingSession.last = r
        self.last_response = r
        return r


# Mono/xsp4 intermittently fails to parse a keep-alive request line and answers 500 "The format of the HTTP method is
# invalid." before the request reaches the application (nothing executed, so nothing mutated). Such a step is retried
# once on a fresh connection and the retry is recorded in the case ("legacy_transport_retries").
MONO_TRANSPORT_FLAKE = b"The format of the HTTP method is invalid"


def run_side(app, base):
    session = CapturingSession()
    out = []
    for i, step in enumerate(flows.FLOWS[app], 1):
        rec = {"app": app, "seq": i, "case": step["name"], "flow": step.get("flow", ""),
               "kind": "state" if step.get("state") else "request"}
        try:
            rec.update(transcript.execute(session, base, step))
            r = session.last_response
            if r.status_code == 500 and MONO_TRANSPORT_FLAKE in r.content:
                session.close()
                rec["transport_retries"] = 1
                rec.update(transcript.execute(session, base, step))
                r = session.last_response
            ct = r.headers.get("content-type", "")
            body = r.content
            rec["raw"] = {
                "headers": {k.lower(): v for k, v in r.headers.items()},
                "status": r.status_code,
                # full normalised body for every status (transcript.execute keeps it for 2xx comparison only)
                "body": normalize.normalize_body(ct, body) if (body and (normalize.is_text(ct) or b"\x00" not in body[:512]))
                else normalize.binary_digest(body) if body else "",
            }
        except Exception as e:  # recorded so a failing step is visible instead of aborting the run
            rec["error"] = f"{type(e).__name__}: {e}"
        out.append(rec)
    return out


def header_value(name, v):
    if v is None:
        return None
    if name == "set-cookie":  # cookie names + attributes only, never values
        parts = [p.strip() for p in v.split(";")]
        return "; ".join([parts[0].split("=")[0] + "=*"] + sorted(p.lower() for p in parts[1:]))
    if name == "location":
        return normalize.normalize_headers({"location": v})["location"]
    if name == "expires" and v != "-1":
        return "{{HTTP-DATE}}"
    return v


def short_diff(a, b, limit=40):
    if not isinstance(a, str) or not isinstance(b, str):
        return None
    lines = list(difflib.unified_diff(a.splitlines(), b.splitlines(), "legacy", "migrated", n=0, lineterm=""))
    return lines[:limit] + ([f"... {len(lines) - limit} more diff lines"] if len(lines) > limit else [])


def strict_diffs(lg, mg):
    """Every difference between the two live responses beyond the contract view."""
    diffs = []
    if "error" in lg or "error" in mg:
        return diffs
    lr, mr = lg["raw"], mg["raw"]
    if lr["body"] != mg["raw"]["body"]:
        diffs.append({"field": "body", "legacy": _clip(lr["body"]), "migrated": _clip(mr["body"]),
                      "diff": short_diff(lr["body"], mr["body"])})
    for h in HEADERS_COMPARED:
        a, b = header_value(h, lr["headers"].get(h)), header_value(h, mr["headers"].get(h))
        if a != b:
            diffs.append({"field": f"header:{h}", "legacy": a, "migrated": b})
    la = sorted(set(lr["headers"]) - set(HEADERS_COMPARED) - RUNTIME_IGNORED)
    ma = sorted(set(mr["headers"]) - set(HEADERS_COMPARED) - RUNTIME_IGNORED)
    if la != ma:
        diffs.append({"field": "runtime-headers", "legacy": la, "migrated": ma})
    return diffs


def _clip(v, n=600):
    if isinstance(v, str) and len(v) > n:
        return v[:n] + f"... [{len(v)} chars]"
    return v


def sql_dump(side, app):
    c, db = SQL_DB[side]["container"], SQL_DB[side]["db"].format(app=app)
    out = {}
    queries = {t: f"SET NOCOUNT ON; SELECT {cols} FROM dbo.{t} ORDER BY Id" for t, cols in SQL_TABLES.items()}
    queries["schema"] = ("SET NOCOUNT ON; SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, "
                         "NUMERIC_SCALE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME IN "
                         "('Catalog','CatalogBrand','CatalogType') ORDER BY TABLE_NAME, COLUMN_NAME")
    queries["sequences"] = ("SET NOCOUNT ON; SELECT name, CAST(current_value AS bigint), CAST(increment AS bigint) FROM sys.sequences "
                            "ORDER BY name")
    for name, q in queries.items():
        cmd = ["docker", "exec", c, "bash", "-c",
               '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -h -1 -W -s "|" -d "$0" -Q "$1"',
               db, q]
        p = subprocess.run(cmd, capture_output=True, text=True)
        out[name] = [l for l in p.stdout.splitlines() if l.strip()] if p.returncode == 0 else {"error": (p.stdout + p.stderr)[-500:]}
    return out


def reset(mode, side, app):
    subprocess.run(SIDES[mode][side]["reset"].format(app=app), shell=True, check=True)


def write_raw(raw_dir, rec_l, rec_m):
    raw_dir.mkdir(parents=True, exist_ok=True)
    for tag, rec in (("legacy", rec_l), ("migrated", rec_m)):
        body = rec.get("raw", {}).get("body", rec.get("error", ""))
        (raw_dir / f"{rec['seq']:03d}-{rec['case']}.{tag}.txt").write_text(
            body if isinstance(body, str) else json.dumps(body), encoding="utf-8")


def run_app(mode, app, out_dir):
    cfg = SIDES[mode]
    with concurrent.futures.ThreadPoolExecutor(2) as ex:  # both sides reset + replay concurrently, each in its own session
        futs = {side: ex.submit(lambda s: (reset(mode, s, app), run_side(app, cfg[s][app]))[1], side)
                for side in ("legacy", "migrated")}
        recs = {side: f.result() for side, f in futs.items()}
    data_state_db = None
    if mode == "sql":
        dumps = {side: sql_dump(side, app) for side in ("legacy", "migrated")}
        data_state_db = []
        for key in list(SQL_TABLES) + ["schema", "sequences"]:
            a, b = dumps["legacy"][key], dumps["migrated"][key]
            data_state_db.append({"check": f"db:{key}", "match": a == b and not isinstance(a, dict),
                                  "legacy": a, "migrated": b})
    baseline = {p.name: json.loads(p.read_text()) for p in sorted((BASELINE / app).glob("*.json"))}
    cases = []
    raw_dir = out_dir / "raw" / mode / app
    for lg, mg in zip(recs["legacy"], recs["migrated"]):
        name = transcript.file_name(lg)
        exp = baseline.get(name)
        write_raw(raw_dir, lg, mg)
        contract = transcript.compare(lg, mg, strict=False)
        case = {
            "case": name, "flow": lg["flow"], "kind": lg["kind"],
            "request": (lg.get("request") or {}),
            "legacy_status": lg.get("response", {}).get("status"), "migrated_status": mg.get("response", {}).get("status"),
            "legacy_error": lg.get("error"), "migrated_error": mg.get("error"),
            "legacy_transport_retries": lg.get("transport_retries", 0),
            "contract_match": not contract,
            "contract_diffs": [{"field": f, "legacy": _clip(x), "migrated": _clip(y)} for f, x, y in contract],
            "legacy_vs_baseline": None if exp is None else not transcript.compare(exp, lg, strict=False),
            "migrated_vs_baseline": None if exp is None else not transcript.compare(exp, mg, strict=False),
            "strict_diffs": strict_diffs(lg, mg),
        }
        if lg["kind"] == "state":
            case["data_state"] = {"legacy_rows": lg.get("response", {}).get("state_rows"),
                                  "migrated_rows": mg.get("response", {}).get("state_rows")}
        cases.append(case)
    return {"app": app, "legacy_url": cfg["legacy"][app], "migrated_url": cfg["migrated"][app],
            "baseline_total": len(baseline), "cases": cases, "data_state_db": data_state_db}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--mode", required=True, choices=sorted(SIDES))
    ap.add_argument("--apps", default="mvc,webforms")
    ap.add_argument("--out")
    args = ap.parse_args()
    out = pathlib.Path(args.out or PARITY / "results" / f"side-by-side-{args.mode}.json")
    out.parent.mkdir(parents=True, exist_ok=True)
    apps = [run_app(args.mode, a, out.parent) for a in args.apps.split(",")]
    result = {"mode": args.mode, "apps": apps}
    out.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n")
    for a in apps:
        n = len(a["cases"])
        c = sum(x["contract_match"] for x in a["cases"])
        s = sum(not x["strict_diffs"] for x in a["cases"])
        print(f"{args.mode} {a['app']}: contract {c}/{n}, strict-identical {s}/{n}, "
              f"legacy-vs-baseline {sum(bool(x['legacy_vs_baseline']) for x in a['cases'])}/{n}, "
              f"migrated-vs-baseline {sum(bool(x['migrated_vs_baseline']) for x in a['cases'])}/{n}"
              + (f", db checks {sum(d['match'] for d in a['data_state_db'])}/{len(a['data_state_db'])}" if a["data_state_db"] else ""))
    print(f"wrote {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
