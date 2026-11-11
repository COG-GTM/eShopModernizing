#!/usr/bin/env python3
"""Records a compact, normalised HTTP baseline (status, key headers, normalised body) for a list of routes.

Stdlib only. The same script is meant to be pointed at the modern apps later, so legacy and modern
captures can be diffed file by file (`diff -r baseline/legacy-mvc <modern-capture>`).

Usage: capture_baseline.py --app legacy-mvc --base-url http://127.0.0.1:5100 --out baseline/legacy-mvc
                           [--routes routes.json] [--routes-key legacy-mvc]
"""
import argparse
import hashlib
import json
import pathlib
import re
import sys
import urllib.error
import urllib.request

KEY_HEADERS = ("content-type", "location", "content-disposition", "cache-control")
TOKEN_FIELDS = ("__RequestVerificationToken", "__VIEWSTATE", "__VIEWSTATEGENERATOR", "__EVENTVALIDATION")
TEXT_TYPES = ("text/html", "application/json", "text/json", "text/plain", "application/xml", "text/xml")
MASK = "{masked}"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


OPENER = urllib.request.build_opener(NoRedirect)


def fetch(url):
    req = urllib.request.Request(url, headers={"Accept": "text/html,application/json;q=0.9,*/*;q=0.8"})
    try:
        resp = OPENER.open(req, timeout=180)
    except urllib.error.HTTPError as e:
        resp = e
    body = resp.read()
    return resp.status if hasattr(resp, "status") else resp.code, {k.lower(): v for k, v in resp.headers.items()}, body


def normalise_origin(text, base_url):
    origin = base_url.rstrip("/")
    host_port = re.sub(r"^https?://", "", origin)
    port = host_port.rsplit(":", 1)[-1]
    text = text.replace(origin, "{origin}")
    return re.sub(r"https?://(127\.0\.0\.1|localhost):%s" % re.escape(port), "{origin}", text)


def normalise_html(text, base_url):
    text = re.sub(r"<!--.*?-->", "", text, flags=re.S)
    text = re.sub(r"(<script\b[^>]*>).*?(</script>)", r"\1\2", text, flags=re.S | re.I)
    text = re.sub(r"(<style\b[^>]*>).*?(</style>)", r"\1\2", text, flags=re.S | re.I)
    for field in TOKEN_FIELDS:
        text = re.sub(r'(name="%s"[^>]*?value=")[^"]*(")' % re.escape(field), r"\1%s\2" % MASK, text)
        text = re.sub(r'(value=")[^"]*("[^>]*?name="%s")' % re.escape(field), r"\1%s\2" % MASK, text)
    # Session footer "{MachineName}, {SessionStartTime}" rendered by both legacy layouts.
    text = re.sub(r"(<small[^>]*>(?:<span[^>]*>)?)\s*[^<>,]+,\s*\d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}:\d{2}(?:\s*[AP]M)?\s*",
                  r"\1" + MASK, text)
    text = normalise_origin(text, base_url)
    text = re.sub(r"\s+", " ", text)
    lines = re.sub(r"\s*(<)", r"\n\1", text).split("\n")
    return "\n".join(l.strip() for l in lines if l.strip())


def condense_host_error_page(text):
    """Mono/xsp4 (and IIS YSOD) error pages carry stack traces, paths and runtime versions: keep only the
    status title, exception type and message, which is what parity can meaningfully compare."""
    def first(pattern):
        m = re.search(pattern, text, flags=re.S | re.I)
        return re.sub(r"\s+", " ", m.group(1)).strip() if m else ""
    return "(host error page) title=%s | h1=%s | message=%s" % (
        first(r"<title>(.*?)</title>"), first(r"<h1>(.*?)</h1>"), first(r'class="exceptionMessage">(.*?)</h2>'))


def normalise_body(ctype, body, base_url):
    mime = (ctype or "").split(";")[0].strip().lower()
    if not body:
        return "(empty body)"
    text = body.decode("utf-8-sig", errors="replace") if mime in TEXT_TYPES else None
    if text is not None and ("\x00" in text or "\ufffd" in text):
        text = None  # e.g. legacy /api/files: BinaryFormatter payload labelled text/html
    if text is not None:
        if mime == "text/html" and ("Version Information:" in text or "<!-- [" in text and "Exception" in text):
            return condense_host_error_page(text)
        if mime == "text/html":
            return normalise_html(text, base_url)
        if "json" in mime:
            try:
                return normalise_origin(json.dumps(json.loads(text), indent=2, ensure_ascii=False), base_url)
            except ValueError:
                pass
        return normalise_origin(text.strip(), base_url)
    return "(binary %s) bytes=%d sha256=%s" % (mime or "unknown", len(body), hashlib.sha256(body).hexdigest())


def slug(i, route):
    s = re.sub(r"[^A-Za-z0-9]+", "-", route).strip("-") or "root"
    return "%02d-%s.txt" % (i, s)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--app", required=True)
    ap.add_argument("--base-url", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--routes", default=str(pathlib.Path(__file__).with_name("routes.json")))
    ap.add_argument("--routes-key")
    a = ap.parse_args()

    routes = json.loads(pathlib.Path(a.routes).read_text())[a.routes_key or a.app]
    out = pathlib.Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    for old in out.glob("*.txt"):
        old.unlink()
    summary = ["route\tstatus\tcontent-type\tlocation\tnormalised-sha256"]
    for i, route in enumerate(routes, 1):
        status, headers, body = fetch(a.base_url.rstrip("/") + route)
        norm = normalise_body(headers.get("content-type"), body, a.base_url)
        head = ["GET %s" % route, "status: %d" % status]
        head += ["%s: %s" % (h, normalise_origin(headers[h], a.base_url)) for h in KEY_HEADERS if h in headers]
        (out / slug(i, route)).write_text("\n".join(head) + "\n---\n" + norm + "\n", encoding="utf-8")
        digest = hashlib.sha256(norm.encode("utf-8")).hexdigest()[:16]
        summary.append("\t".join([route, str(status), headers.get("content-type", ""),
                                  normalise_origin(headers.get("location", ""), a.base_url), digest]))
        print("  %-42s %d %s" % (route, status, headers.get("content-type", "")))
    (out / "SUMMARY.tsv").write_text("\n".join(summary) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
