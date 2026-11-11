#!/usr/bin/env python3
"""Side-by-side HTTP replay: sends the same request list (scenarios.py) to a legacy and a modern app, normalises
both responses identically (normalise.py) and classifies every step as PASS / INTENTIONAL / FAIL (intentional.py).

Usage: replay.py --app mvc|webforms --mode mock|sql --legacy http://127.0.0.1:5100 --modern http://127.0.0.1:5200
                 --out results/mock/mvc
Writes <out>/results.json, <out>/legacy/NN-<id>.txt and <out>/modern/NN-<id>.txt (normalised captures), and prints
one line per step. Exit code 1 if any step is FAIL. Both apps must be freshly started (fresh seed) before a run.
Stdlib only.
"""
import argparse
import difflib
import html.parser
import http.cookiejar
import json
import pathlib
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from intentional import classify  # noqa: E402
from normalise import host_error_detail, json_shape, normalise_body, normalise_content_type, normalise_location  # noqa: E402
from scenarios import FLOWS  # noqa: E402

COMPARED_HEADERS = ("content-disposition",)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class FormParser(html.parser.HTMLParser):
    """Collects the fields a browser would submit from the first POST form of a page."""

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.forms, self._form, self._select, self._textarea = [], None, None, None

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == "form":
            self._form = {"action": a.get("action"), "method": (a.get("method") or "get").lower(), "fields": [],
                          "submits": []}
            self.forms.append(self._form)
        if self._form is None:
            return
        name = a.get("name")
        if tag == "input" and name:
            t = (a.get("type") or "text").lower()
            if t in ("submit", "image", "button"):
                if t != "button":
                    self._form["submits"].append((name, a.get("value", "")))
            elif t in ("checkbox", "radio"):
                if "checked" in a:
                    self._form["fields"].append([name, a.get("value", "on")])
            elif t != "file":
                self._form["fields"].append([name, a.get("value", "")])
        elif tag == "select" and name:
            self._select = [name, None, None]
        elif tag == "option" and self._select is not None:
            value = a.get("value", "")
            if self._select[2] is None:
                self._select[2] = value
            if "selected" in a:
                self._select[1] = value
        elif tag == "textarea" and name:
            self._textarea = [name, ""]

    def handle_data(self, data):
        if self._textarea is not None:
            self._textarea[1] += data

    def handle_endtag(self, tag):
        if tag == "form":
            self._form = None
        elif tag == "select" and self._select is not None and self._form is not None:
            name, selected, first = self._select
            self._form["fields"].append([name, selected if selected is not None else (first or "")])
            self._select = None
        elif tag == "textarea" and self._textarea is not None and self._form is not None:
            self._form["fields"].append(self._textarea)
            self._textarea = None


def parse_post_form(page_html):
    p = FormParser()
    p.feed(page_html)
    forms = [f for f in p.forms if f["method"] == "post"]
    return forms[-1] if forms else None  # MVC Delete has the post form after the layout's forms


class Side:
    def __init__(self, name, base_url):
        self.name, self.base = name, base_url.rstrip("/")
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(NoRedirect, urllib.request.HTTPCookieProcessor(self.jar))

    def request(self, method, path, data=None):
        url = path if path.startswith("http") else self.base + path
        body = urllib.parse.urlencode(data).encode() if data is not None else None
        req = urllib.request.Request(url, data=body, method=method,
                                     headers={"Accept": "text/html,application/json;q=0.9,*/*;q=0.8"})
        if body is not None:
            req.add_header("Content-Type", "application/x-www-form-urlencoded")
        try:
            resp = self.opener.open(req, timeout=180)
        except urllib.error.HTTPError as e:
            resp = e
        raw = resp.read()
        return url, resp.status if hasattr(resp, "status") else resp.code, \
            {k.lower(): v for k, v in resp.headers.items()}, raw


def build_post(side, step):
    form = step.get("form") or {}
    source = form.get("from", step["path"])
    page_url, status, headers, raw = side.request("GET", source)
    parsed = parse_post_form(raw.decode("utf-8", errors="replace")) if status == 200 else None
    if parsed is None:
        return side.base + step["path"], [], "no POST form on GET %s (status %d)" % (source, status)
    fields = [f for f in parsed["fields"] if f[0] not in set(form.get("drop", []))]
    present = {f[0] for f in fields}
    for k, v in form.get("set", {}).items():
        if k in present:
            for f in fields:
                if f[0] == k:
                    f[1] = v
        else:
            fields.append([k, v])
    for k, v in form.get("extra", {}).items():
        fields.append([k, v])
    if form.get("submit"):
        match = [s for s in parsed["submits"] if s[1] == form["submit"]]
        if not match:
            return side.base + step["path"], [], "submit %r not found on %s" % (form["submit"], source)
        fields.append(list(match[0]))
    action = parsed["action"]
    url = urllib.parse.urljoin(page_url, action) if action else side.base + step["path"]
    return url, [tuple(f) for f in fields], None


LIST_ALL = {"mvc": "/Catalog/Index?pageSize=100&pageIndex=0", "webforms": "/Default/index/0/size/100"}
DETAILS_ID = re.compile(r'href="[^"]*/Catalog/Details/(\d+)"')


def resolve(side, app, step):
    if "{last_id}" not in step["path"]:
        return step
    _, status, _, raw = side.request("GET", LIST_ALL[app])
    ids = [int(i) for i in DETAILS_ID.findall(raw.decode("utf-8", errors="replace"))] if status == 200 else []
    return dict(step, path=step["path"].replace("{last_id}", str(max(ids) if ids else 0)))


def observe(side, step):
    if step["method"] == "POST" and "form" in step:
        url, data, error = build_post(side, step)
        if error:
            return {"error": error}
        req_url, status, headers, raw = side.request("POST", url, data)
        sent = sorted({k for k, _ in data})
    elif step["method"] == "POST":
        req_url, status, headers, raw = side.request("POST", step["path"], [])
        sent = []
    else:
        req_url, status, headers, raw = side.request("GET", step["path"])
        sent = None
    ctype = headers.get("content-type", "")
    body = normalise_body(ctype, raw, side.base)
    return {
        "url": req_url.replace(side.base, ""),
        "status": status,
        "location": normalise_location(headers.get("location", ""), req_url),
        "content-type": normalise_content_type(ctype),
        "raw-content-type": ctype,
        "headers": {h: headers[h] for h in COMPARED_HEADERS if h in headers},
        "body": body,
        "body-bytes": len(raw),
        "detail": host_error_detail(ctype, raw, side.base),
        "json-shape": json_shape(body) if "json" in ctype else None,
        "fields-sent": sent,
    }


def diff_fields(legacy, modern):
    if "error" in legacy or "error" in modern:
        return ["harness"]
    redirect = legacy["status"] == modern["status"] and 300 <= legacy["status"] < 400
    # a redirect is status + Location: its body ("Object moved to here" vs empty) is never shown by a browser
    keys = ("status", "location", "headers") if redirect else ("status", "location", "content-type", "headers")
    diffs = [k for k in keys if legacy[k] != modern[k]]
    if not redirect and legacy["body"] != modern["body"]:
        diffs.append("body")
    return diffs


def body_diff(legacy, modern, limit=60):
    if "body" not in legacy or "body" not in modern:
        return ""
    lines = list(difflib.unified_diff(legacy["body"].splitlines(), modern["body"].splitlines(), "legacy", "modern",
                                      n=1, lineterm=""))
    return "\n".join(lines[:limit]) + ("\n... (%d more diff lines)" % (len(lines) - limit) if len(lines) > limit else "")


def write_capture(path, step, obs):
    if "error" in obs:
        text = "%s %s\nharness error: %s\n" % (step["method"], step["path"], obs["error"])
    else:
        head = ["%s %s" % (step["method"], obs["url"]), "status: %s" % obs["status"],
                "content-type: %s" % obs["raw-content-type"]]
        if obs["detail"]:
            head.append("host-error: %s" % obs["detail"])
        if obs["location"]:
            head.append("location: %s" % obs["location"])
        head += ["%s: %s" % kv for kv in obs["headers"].items()]
        if obs["fields-sent"] is not None:
            head.append("fields-sent: %s" % ", ".join(obs["fields-sent"]))
        text = "\n".join(head) + "\n---\n" + obs["body"] + "\n"
    path.write_text(text, encoding="utf-8")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--app", required=True, choices=sorted(FLOWS))
    ap.add_argument("--mode", required=True, choices=["mock", "sql"])
    ap.add_argument("--legacy", required=True)
    ap.add_argument("--modern", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()

    out = pathlib.Path(a.out)
    for sub in ("legacy", "modern"):
        (out / sub).mkdir(parents=True, exist_ok=True)
        for old in (out / sub).glob("*.txt"):
            old.unlink()
    legacy, modern = Side("legacy", a.legacy), Side("modern", a.modern)
    results = []
    for i, step in enumerate(FLOWS[a.app], 1):
        lo, mo = observe(legacy, resolve(legacy, a.app, step)), observe(modern, resolve(modern, a.app, step))
        fname = "%02d-%s.txt" % (i, step["id"])
        write_capture(out / "legacy" / fname, step, lo)
        write_capture(out / "modern" / fname, step, mo)
        diffs = diff_fields(lo, mo)
        verdict, reason = classify(a.app, a.mode, step["id"], diffs, lo, mo)
        row = {"n": i, "id": step["id"], "method": step["method"], "path": step["path"], "note": step.get("note", ""),
               "verdict": verdict, "reason": reason, "diffs": diffs,
               "legacy": {k: lo.get(k) for k in ("status", "location", "content-type", "body-bytes", "error")},
               "modern": {k: mo.get(k) for k in ("status", "location", "content-type", "body-bytes", "error")},
               "body-diff": body_diff(lo, mo) if "body" in diffs else ""}
        results.append(row)
        print("  %-12s %3s %-4s %-44s legacy=%s modern=%s %s" % (
            verdict, i, step["method"], step["path"][:44], lo.get("status", "ERR"), mo.get("status", "ERR"),
            ",".join(diffs)))
    summary = {v: sum(1 for r in results if r["verdict"] == v) for v in ("PASS", "INTENTIONAL", "FAIL")}
    (out / "results.json").write_text(json.dumps({"app": a.app, "mode": a.mode, "legacy": a.legacy,
                                                   "modern": a.modern, "summary": summary, "steps": results},
                                                  indent=2), encoding="utf-8")
    print("  %s/%s: PASS %d, INTENTIONAL %d, FAIL %d" % (a.mode, a.app, summary["PASS"], summary["INTENTIONAL"],
                                                       summary["FAIL"]))
    return 1 if summary["FAIL"] else 0


if __name__ == "__main__":
    sys.exit(main())
