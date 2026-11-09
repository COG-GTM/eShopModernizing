#!/usr/bin/env python3
"""Transcript recorder / replayer for the eShop storefront behaviour contract.

  record:  transcript.py record --app mvc --base-url http://127.0.0.1:5100 [--out DIR]
  replay:  transcript.py replay --app mvc --base-url http://127.0.0.1:5100 [--strict] [--report FILE]
  list:    transcript.py list --app mvc

The app MUST be freshly started (mock catalog is an in-memory singleton) before record/replay:
use --reset-cmd "modernization/legacy-harness/reset.sh mvc" or reset it yourself.
Default comparison = status + key headers + semantic view (see normalize.py / CONTRACT.md).
--strict additionally compares the full normalised body.
"""
import argparse
import json
import os
import pathlib
import subprocess
import sys
from urllib.parse import urljoin, urlsplit

import requests
from bs4 import BeautifulSoup

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import flows  # noqa: E402
import normalize  # noqa: E402

DEFAULT_DIR = pathlib.Path(__file__).resolve().parent.parent / "baseline" / "transcripts"
TIMEOUT = 120


def _error_semantic(status, sem):
    """5xx pages are runtime error pages: compare title + exception headline only, never stack traces."""
    if status >= 500 and isinstance(sem, dict) and "text" in sem:
        sem = dict(sem, text=sem["text"].split(" Description:")[0].split(" Exception stack trace")[0])
    return sem


def _path_of(url: str) -> str:
    s = urlsplit(url)
    return s.path + (("?" + s.query) if s.query else "")


def build_form(session, base, step):
    spec = step["form"]
    page_url = base + spec["page"]
    r = session.get(page_url, timeout=TIMEOUT, allow_redirects=False)
    if r.status_code != 200:
        raise RuntimeError(f"{step['name']}: form page {spec['page']} returned {r.status_code}")
    soup = BeautifulSoup(r.text, "html.parser")
    form = next((f for f in soup.find_all("form") if (f.get("method") or "").lower() == "post"), None)
    if form is None:
        raise RuntimeError(f"{step['name']}: no POST form on {spec['page']}")
    data = {}
    for el in form.find_all(["input", "select", "textarea"]):
        name = el.get("name")
        if not name:
            continue
        typ = (el.get("type") or "").lower()
        if el.name == "input" and typ in ("submit", "button", "image", "reset"):
            continue
        if el.name == "select":
            opt = el.find("option", selected=True) or el.find("option")
            data[name] = opt.get("value") if opt else ""
        elif el.name == "textarea":
            data[name] = el.get_text()
        elif typ in ("checkbox", "radio"):
            if el.has_attr("checked"):
                data[name] = el.get("value", "on")
        else:
            data[name] = el.get("value", "")

    def resolve(key):
        if key in data:
            return key
        for n in data:
            if n.endswith("$" + key):
                return n
        return key

    for k, v in spec.get("fields", {}).items():
        data[resolve(k)] = v
    for k in spec.get("drop", []):
        data.pop(resolve(k), None)
    submit = spec.get("submit")
    if submit:
        btn = form.find("input", attrs={"type": "submit", "value": submit})
        if btn is None:
            raise RuntimeError(f"{step['name']}: submit button {submit!r} not found")
        data[btn["name"]] = btn["value"]
    action = urljoin(page_url, form.get("action") or page_url)
    return action, data


def execute(session, base, step):
    if "form" in step:
        url, data = build_form(session, base, step)
        method = "POST"
        r = session.post(url, data=data, timeout=TIMEOUT, allow_redirects=False)
        req = {"method": method, "path": _path_of(url), "form_page": step["form"]["page"],
               "fields": step["form"].get("fields", {}), "drop": step["form"].get("drop", [])}
    else:
        method = step.get("method", "GET")
        url = base + step["path"]
        r = session.request(method, url, headers=step.get("headers", {}), data=step.get("data"),
                            timeout=TIMEOUT, allow_redirects=False)
        req = {"method": method, "path": step["path"], "headers": step.get("headers", {})}
        if step.get("data") is not None:
            req["data"] = step["data"]
    ct = r.headers.get("content-type", "")
    resp = {
        "status": r.status_code,
        "headers": normalize.normalize_headers(r.headers),
        "semantic": _error_semantic(r.status_code, normalize.semantic(ct, r.content)),
        "body": normalize.normalize_body(ct, r.content),
    }
    if step.get("state"):
        resp = {"status": r.status_code, "state_rows": resp["semantic"].get("rows", resp["semantic"])}
    return {"request": req, "response": resp}


def run(app, base):
    session = requests.Session()
    out = []
    for i, step in enumerate(flows.FLOWS[app], 1):
        rec = {"app": app, "seq": i, "case": step["name"], "flow": step.get("flow", ""),
               "kind": "state" if step.get("state") else "request"}
        try:
            rec.update(execute(session, base.rstrip("/"), step))
        except Exception as e:  # recorded so a failing step is visible instead of aborting the run
            rec["error"] = f"{type(e).__name__}: {e}"
        out.append(rec)
    return out


def file_name(rec):
    return f"{rec['seq']:03d}-{rec['case']}.json"


def _media_type(headers):
    return (headers or {}).get("content-type", "").split(";")[0].strip().lower()


def compare(expected, actual, strict):
    """Comparison rules (CONTRACT.md section 3/4):
    always status (+ data-state rows); 3xx: Location path only; 4xx/5xx: JSON bodies semantically, HTML bodies not at all;
    2xx: media type + semantic view; --strict adds the full normalised body for 2xx."""
    diffs = []
    if "error" in expected or "error" in actual:
        if expected.get("error") != actual.get("error"):
            diffs.append(("error", expected.get("error"), actual.get("error")))
        return diffs
    e, a = expected["response"], actual["response"]

    def check(name, ev, av):
        if ev != av:
            diffs.append((name, ev, av))

    status = int(e.get("status", 0))
    check("status", e.get("status"), a.get("status"))
    check("state_rows", e.get("state_rows"), a.get("state_rows"))
    if 300 <= status < 400:
        check("location", (e.get("headers") or {}).get("location"), (a.get("headers") or {}).get("location"))
    elif status >= 400:
        if isinstance(e.get("semantic"), dict) and "json" in e["semantic"]:
            check("semantic", e.get("semantic"), a.get("semantic"))
    else:
        check("content-type", _media_type(e.get("headers")), _media_type(a.get("headers")))
        check("semantic", e.get("semantic"), a.get("semantic"))
        if strict:
            check("body", e.get("body"), a.get("body"))
    return diffs


def maybe_reset(cmd):
    if cmd:
        subprocess.run(cmd, shell=True, check=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("command", choices=["record", "replay", "list"])
    ap.add_argument("--app", required=True, choices=sorted(flows.FLOWS))
    ap.add_argument("--base-url")
    ap.add_argument("--dir", default=str(DEFAULT_DIR))
    ap.add_argument("--strict", action="store_true")
    ap.add_argument("--reset-cmd")
    ap.add_argument("--report")
    args = ap.parse_args()
    d = pathlib.Path(args.dir) / args.app

    if args.command == "list":
        for i, s in enumerate(flows.FLOWS[args.app], 1):
            print(f"{i:03d} {s.get('flow', ''):14s} {s['name']}")
        return 0
    if not args.base_url:
        ap.error("--base-url required")
    maybe_reset(args.reset_cmd)
    recs = run(args.app, args.base_url)

    if args.command == "record":
        d.mkdir(parents=True, exist_ok=True)
        for old in d.glob("*.json"):
            old.unlink()
        for rec in recs:
            (d / file_name(rec)).write_text(json.dumps(rec, indent=2, sort_keys=True, ensure_ascii=False) + "\n")
        errs = [r for r in recs if "error" in r]
        print(f"recorded {len(recs)} transcripts for {args.app} into {d} ({len(errs)} step errors)")
        return 1 if errs else 0

    results, failed = [], 0
    expected = {p.name: json.loads(p.read_text()) for p in sorted(d.glob("*.json"))}
    for rec in recs:
        name = file_name(rec)
        exp = expected.pop(name, None)
        if exp is None:
            results.append({"case": name, "result": "missing-baseline"}); failed += 1; continue
        diffs = compare(exp, rec, args.strict)
        if diffs:
            failed += 1
        results.append({"case": name, "result": "fail" if diffs else "pass",
                        "diffs": [{"field": f, "expected": x, "actual": y} for f, x, y in diffs]})
    for name in expected:
        results.append({"case": name, "result": "not-replayed"}); failed += 1
    summary = {"app": args.app, "base_url": args.base_url, "strict": args.strict,
               "total": len(results), "passed": len(results) - failed, "failed": failed, "results": results}
    if args.report:
        pathlib.Path(args.report).parent.mkdir(parents=True, exist_ok=True)
        pathlib.Path(args.report).write_text(json.dumps(summary, indent=2, ensure_ascii=False) + "\n")
    for r in results:
        if r["result"] != "pass":
            print(f"FAIL {r['case']}: " + ", ".join(x["field"] for x in r.get("diffs", [])) if r.get("diffs") else f"FAIL {r['case']}: {r['result']}")
    print(f"{args.app}: {summary['passed']}/{summary['total']} transcripts match (strict={args.strict})")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
