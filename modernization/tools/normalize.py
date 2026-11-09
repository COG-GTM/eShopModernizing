"""Normalisation of volatile response content so legacy and migrated transcripts can be compared.

Two views are produced for every response:
  * body     - full body with volatile values replaced by placeholders (exact/strict comparison)
  * semantic - implementation-neutral facts (title, visible text, form fields, validation
               messages, table rows, JSON) used for the default contract comparison
"""
import hashlib
import json
import re
import socket

from bs4 import BeautifulSoup

KEY_HEADERS = ("content-type", "location")

_HOST = socket.gethostname()

_SUBS = [
    # antiforgery / webforms state (attribute order varies, so match name-then-value and value-then-name)
    (re.compile(r'(name="(?:__RequestVerificationToken|__VIEWSTATE|__VIEWSTATEGENERATOR|__EVENTVALIDATION)"[^>]*?value=")[^"]*(")'), r"\1{{VOLATILE}}\2"),
    (re.compile(r'(value=")[^"]*("[^>]*?name="(?:__RequestVerificationToken|__VIEWSTATE|__VIEWSTATEGENERATOR|__EVENTVALIDATION)")'), r"\1{{VOLATILE}}\2"),
    # WebResource.axd / ScriptResource.axd query strings
    (re.compile(r"((?:WebResource|ScriptResource)\.axd\?)[^\"']*"), r"\1{{AXD}}"),
    # session info shown in the layouts: "<machine>, <session start time>"
    (re.compile(r"\d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}:\d{2}(?: ?[AP]M)?"), "{{DATETIME}}"),
    (re.compile(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?"), "{{DATETIME}}"),
    # Mono stack-trace module ids and bundle version hashes
    (re.compile(r"<[0-9a-f]{32}>"), "<{{MODULE}}>"),
    (re.compile(r"App_Web_[a-z0-9]{8}"), "App_Web_{{DYNAMIC}}"),
    (re.compile(r"(\?v=)[A-Za-z0-9_\-]{20,}"), r"\1{{HASH}}"),
    # absolute base URLs echoed in bodies (e.g. Web API 404 messages) are port independent
    (re.compile(r"https?://(?:127\.0\.0\.1|localhost|\[::1\]):\d+"), "{{BASE}}"),
    (re.compile(r"\b(?:ASP\.NET_SessionId|\.AspNetCore\.Session)=[A-Za-z0-9]+"), "{{SESSIONID}}"),
]


def normalize_text(text: str) -> str:
    for pat, rep in _SUBS:
        text = pat.sub(rep, text)
    if _HOST:
        text = text.replace(_HOST, "{{HOST}}")
    return text


def is_text(content_type: str) -> bool:
    ct = (content_type or "").lower()
    return ct.startswith("text/") or "json" in ct or "xml" in ct or "javascript" in ct


def normalize_headers(headers) -> dict:
    out = {}
    for k in KEY_HEADERS:
        v = headers.get(k)
        if v is None:
            continue
        if k == "location":
            v = re.sub(r"^https?://[^/]+", "", v)  # host/port independent
        out[k] = normalize_text(v)
    return out


def _collapse(s: str) -> str:
    return re.sub(r"\s+", " ", s or "").strip()


def semantic_html(text: str) -> dict:
    soup = BeautifulSoup(text, "html.parser")
    for t in soup(["script", "style"]):
        t.decompose()
    title = _collapse(soup.title.get_text()) if soup.title else ""
    fields = []
    for el in soup.find_all(["input", "select", "textarea"]):
        name = el.get("name")
        if not name or name.startswith("__"):
            continue
        if el.name == "select":
            opts = [(_collapse(o.get_text()), o.get("value"), o.has_attr("selected")) for o in el.find_all("option")]
            fields.append({"name": name, "type": "select", "options": [[t, v] for t, v, _ in opts],
                           "selected": [v for _, v, s in opts if s]})
        else:
            fields.append({"name": name, "type": el.get("type", el.name), "value": el.get("value")})
    errors = []
    for el in soup.select(".field-validation-error, .validation-summary-errors li, .text-danger"):
        msg = _collapse(el.get_text())
        if msg and (el.get("style") or "").replace(" ", "").find("visibility:hidden") < 0 \
                and (el.get("style") or "").replace(" ", "").find("display:none") < 0:
            errors.append(msg)
    rows = []
    for tr in soup.find_all("tr"):
        cells = [_collapse(td.get_text()) for td in tr.find_all("td")]
        if cells:
            rows.append(cells)
    return {
        "title": title,
        "text": normalize_text(_collapse(soup.get_text(" "))),
        "fields": fields,
        "errors": errors,
        "rows": rows,
    }


def semantic(content_type: str, body: bytes) -> dict:
    ct = (content_type or "").lower()
    if not body:
        return {"empty": True}
    if "json" in ct:
        try:
            return {"json": json.loads(normalize_text(body.decode("utf-8-sig")))}
        except ValueError:
            pass
    if b"\x00" in body[:512]:  # binary payload regardless of declared type (legacy /api/files is text/html)
        return binary_digest(body)
    if "html" in ct:
        return semantic_html(body.decode("utf-8", "replace"))
    if is_text(ct):
        return {"text": normalize_text(body.decode("utf-8", "replace"))}
    return binary_digest(body)


def binary_digest(body: bytes) -> dict:
    return {"sha256": hashlib.sha256(body).hexdigest(), "length": len(body)}


def normalize_body(content_type: str, body: bytes):
    if not body:
        return ""
    if is_text(content_type):
        return normalize_text(body.decode("utf-8", "replace"))
    return binary_digest(body)
