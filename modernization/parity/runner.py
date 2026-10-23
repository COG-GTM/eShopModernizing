"""Executes flows.json sequences against a running target and produces normalised transcripts.
Used by capture.py (record the legacy baseline) and replay.py (compare any target with the baseline)."""
import json
import pathlib
import re
import subprocess
import sys
import time
from urllib.parse import urljoin

import requests
from bs4 import BeautifulSoup

import normalize as N

HERE = pathlib.Path(__file__).resolve().parent
BASELINE = HERE.parent / "baseline"
TIMEOUT = 180


def load_flows(path=None):
    return json.loads(pathlib.Path(path or BASELINE / "flows.json").read_text())


def restart(cmd, log):
    if not cmd:
        return
    log("  restart: %s" % cmd)
    t = time.time()
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    if r.returncode != 0:
        raise RuntimeError("restart command failed (%s): %s %s" % (r.returncode, r.stdout[-500:], r.stderr[-500:]))
    log("  restart done in %.1fs" % (time.time() - t))


def _pick_form(soup, submit_label=None):
    forms = soup.find_all("form")
    if submit_label:
        for f in forms:
            if f.find(["input", "button"], attrs={"type": "submit"}, string=None, value=submit_label) or \
                    any(b.get_text(strip=True) == submit_label for b in f.find_all("button")):
                return f
    for f in forms:
        if (f.get("method") or "get").lower() == "post":
            return f
    return forms[0] if forms else None


def harvest_form(session, base, page, submit_label=None):
    """GET `page`, return (action_url, ordered list of [name, value]) with every successful control's default value."""
    r = session.get(base + page, timeout=TIMEOUT, allow_redirects=False)
    soup = BeautifulSoup(r.content, "lxml")
    form = _pick_form(soup, submit_label)
    if form is None:
        raise RuntimeError("no form on %s (status %s)" % (page, r.status_code))
    action = urljoin(base + page, form.get("action") or page)
    fields = []
    for el in form.find_all(["input", "select", "textarea"]):
        name = el.get("name")
        if not name or el.has_attr("disabled"):
            continue
        if el.name == "input":
            t = (el.get("type") or "text").lower()
            if t in ("submit", "button", "image", "reset", "file"):
                continue
            if t in ("checkbox", "radio") and not el.has_attr("checked"):
                continue
            fields.append([name, el.get("value", "on" if t in ("checkbox", "radio") else "")])
        elif el.name == "select":
            opts = el.find_all("option")
            sel = [o for o in opts if o.has_attr("selected")] or opts[:1]
            for o in sel:
                fields.append([name, o.get("value", o.get_text())])
        else:
            fields.append([name, el.get_text()])
    submit = None
    if submit_label:
        for b in form.find_all(["input", "button"]):
            label = b.get("value") if b.name == "input" else b.get_text(strip=True)
            if (b.get("type") or "submit").lower() == "submit" and label == submit_label:
                submit = [b.get("name"), b.get("value", "")] if b.get("name") else None
                break
        else:
            raise RuntimeError("submit %r not found on %s" % (submit_label, page))
    return action, fields, submit


def build_post(session, base, flow):
    spec = flow["form"]
    action, fields, submit = harvest_form(session, base, spec["page"], spec.get("submit"))
    overrides = dict(spec.get("fields", {}))
    out = []
    for name, value in fields:
        logical = N.logical_name(name)
        if name in spec.get("omit", []) or logical in spec.get("omit", []):
            continue
        if logical in overrides:
            value = overrides.pop(logical)
        out.append([name, value])
    for logical, value in overrides.items():   # fields not present on the page are sent as-is
        out.append([logical, value])
    if submit:
        out.append(submit)
    if flow.get("path"):
        action = base + flow["path"]
    return action, out


def snapshot(session, base, app_cfg):
    """Full catalog data state: every list row keyed by column header + brand/type lookups."""
    snap = {}
    r = session.get(base + app_cfg["snapshot_items"], timeout=TIMEOUT)
    soup = BeautifulSoup(r.content, "lxml")
    headers = [N.norm_ws(th.get_text(" ")) for th in soup.select("table th")]
    items = []
    for tr in soup.select("table tr"):
        tds = tr.find_all("td")
        if not tds:
            continue
        row = {}
        for h, td in zip(headers, tds):
            if h:
                row[h] = N.norm_ws(td.get_text(" "))
        m = None
        for a in tr.find_all("a", href=True):
            m = re.search(r"/Catalog/(?:Edit|Details|Delete)/(\d+)", a["href"]) or re.search(r"[?&]id=(\d+)", a["href"])
            if m:
                break
        row["Id"] = int(m.group(1)) if m else None
        items.append(row)
    snap["status"] = r.status_code
    snap["items"] = items
    r = session.get(base + app_cfg["snapshot_lookups"], timeout=TIMEOUT)
    soup = BeautifulSoup(r.content, "lxml")
    snap["lookups"] = {N.logical_name(s.get("name")): [[o.get("value"), N.norm_ws(o.get_text())] for o in s.find_all("option")]
                       for s in soup.find_all("select")}
    return snap


def run_flow(session, base, flow):
    method = flow.get("method", "GET").upper()
    headers = dict(flow.get("headers", {}))
    req = {"method": method, "path": flow.get("path")}
    data = None
    if "form" in flow:
        url, data = build_post(session, base, flow)
        req["path"] = N.norm_url(url)
        req["form"] = [[k, N.MASK if N.logical_name(k) in N.TOKEN_FIELDS or k in N.TOKEN_FIELDS else v] for k, v in data]
    else:
        url = base + flow["path"]
    if headers:
        req["headers"] = headers
    t = time.time()
    r = session.request(method, url, data=data, headers=headers, timeout=TIMEOUT, allow_redirects=False)
    resp = {
        "status": r.status_code,
        "headers": N.key_headers(r.headers, base),
        "raw_sha256": N.raw_sha(r.content),
        "body": N.normalise_body(r.content, r.headers.get("content-type"), r.status_code, flow.get("compare", "full")),
    }
    return {"id": flow["id"], "request": req, "response": resp, "elapsed_s": round(time.time() - t, 3)}


def run_sequences(doc, targets, restart_cmds, log=print, only=None):
    """targets: {"mvc": base_url, "webforms": base_url}. Yields (sequence, flow, transcript, snapshot_or_None)."""
    for seq in doc["sequences"]:
        app = seq["app"]
        if app not in targets or (only and seq["id"] not in only):
            continue
        base = targets[app].rstrip("/")
        log("sequence %s (%s, %d flows)" % (seq["id"], app, len(seq["flows"])))
        if seq.get("restart", True):
            restart(restart_cmds.get(app), log)
        session = requests.Session()
        session.headers["User-Agent"] = "eshop-parity-harness/1"
        for flow in seq["flows"]:
            try:
                tr = run_flow(session, base, flow)
            except Exception as e:  # recorded as a failed flow, never silently skipped
                tr = {"id": flow["id"], "error": "%s: %s" % (type(e).__name__, e)}
            snap = snapshot(requests.Session(), base, doc["apps"][app]) if flow.get("snapshot") and "error" not in tr else None
            log("  %-48s %s" % (flow["id"], tr.get("response", {}).get("status", tr.get("error"))))
            yield seq, flow, tr, snap


def write_json(path, obj):
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, indent=1, ensure_ascii=False, sort_keys=False) + "\n")


if __name__ == "__main__":
    sys.exit("use capture.py or replay.py")
