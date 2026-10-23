"""Shared response normaliser for the parity harness (capture.py / replay.py).

The rules are the contract documented in modernization/CONTRACT.md; change both together.
"""
import base64
import hashlib
import json
import re
from urllib.parse import urlsplit

from bs4 import BeautifulSoup, Comment

MASK = "<masked>"
TOKEN_FIELDS = {"__RequestVerificationToken", "__VIEWSTATE", "__VIEWSTATEGENERATOR", "__EVENTVALIDATION",
                "__EVENTTARGET", "__EVENTARGUMENT", "__PREVIOUSPAGE", "__LASTFOCUS"}
# Session footer "{MachineName}, {SessionStartTime}" (INVENTORY section 8).
SESSION_RE = re.compile(r"^\s*[^,<>]+,\s*\d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}:\d{2}\s*(AM|PM)?\s*$")
WS_RE = re.compile(r"\s+")
# Attributes that carry meaning for parity. Everything else (style, id, data-*, aria-*, event handlers...) is ignored.
KEEP_ATTRS = {
    "a": ["href"], "img": ["src", "alt"], "image": ["src"], "form": ["action", "method"],
    "input": ["name", "type", "value", "readonly", "checked", "disabled"], "select": ["name"],
    "option": ["value", "selected"], "textarea": ["name"], "button": ["type", "name", "value"], "label": ["for"],
}
STRUCT_TAGS = set(KEEP_ATTRS) | {"title", "h1", "h2", "h3", "h4", "h5", "h6", "table", "th", "td", "tr", "dt", "dd",
                                 "ul", "li", "span", "small", "p"}
# Classes that change behaviour/visibility (pager hidden state, validation state, prices).
KEEP_CLASS_RE = re.compile(r"^(esh-pager-item--hidden|esh-pager-item--navigable|field-validation-error|"
                           r"input-validation-error|validation-summary-errors|text-danger|esh-price)$")
TEXT_TYPES = ("text/css", "application/javascript", "text/javascript", "application/x-javascript")
BINARY_INLINE_LIMIT = 262144


def mime_of(content_type):
    return (content_type or "").split(";")[0].strip().lower()


def norm_ws(s):
    return WS_RE.sub(" ", s or "").strip()


def norm_url(u, base=None):
    """Absolute same-host URLs -> path?query; relative stays relative; lower-cases nothing (paths are case-sensitive)."""
    if u is None:
        return None
    u = u.strip()
    parts = urlsplit(u)
    if parts.scheme in ("http", "https") and parts.hostname in ("127.0.0.1", "localhost", None) or \
            (base and parts.netloc and parts.netloc == urlsplit(base).netloc):
        u = parts.path + (("?" + parts.query) if parts.query else "")
    return u


def logical_name(name):
    """Form field names are compared by their last segment: WebForms `ctl00$MainContent$Name`, Razor Pages
    `Item.Name` and MVC `Name` all normalise to `Name` (CONTRACT.md, rule H5)."""
    if not name:
        return name
    return re.split(r"[$.]", name)[-1]


def _keep_classes(el):
    return sorted(c for c in (el.get("class") or []) if KEEP_CLASS_RE.match(c))


def normalise_html(text, mask_patterns=()):
    soup = BeautifulSoup(text, "lxml")
    for el in soup(["script", "style", "noscript", "template"]):
        if el.name == "script" and el.get("src"):
            el.attrs = {"src": el.get("src")}
            el.string = ""
            continue
        el.decompose()
    for c in soup.find_all(string=lambda s: isinstance(s, Comment)):
        c.extract()

    assets = sorted({norm_url(e.get("href")) for e in soup.find_all("link", href=True)
                     if "stylesheet" in (e.get("rel") or [])} |
                    {norm_url(e.get("src")) for e in soup.find_all("script", src=True)})
    for e in soup.find_all(["link", "script", "meta"]):
        e.decompose()

    def mask_text(s):
        s = norm_ws(s)
        if SESSION_RE.match(s):
            return MASK
        for pat in mask_patterns:
            s = re.sub(pat, MASK, s)
        return s

    structure = []
    for el in soup.find_all(True):
        if el.name not in STRUCT_TAGS:
            continue
        item = {"tag": el.name}
        for a in KEEP_ATTRS.get(el.name, []):
            if el.has_attr(a):
                v = el.get(a)
                if isinstance(v, list):
                    v = " ".join(v)
                if a == "name" or (el.name == "label" and a == "for"):
                    v = logical_name(v.replace("_", "$")) if a == "for" else logical_name(v)
                if a in ("href", "src", "action"):
                    v = norm_url(v)
                if el.name == "input" and a == "value" and el.get("name") in TOKEN_FIELDS:
                    v = MASK
                if a in ("readonly", "checked", "disabled", "selected"):
                    v = True
                item[a] = v
        cls = _keep_classes(el)
        if cls:
            item["class"] = cls
        if el.name in ("title", "h1", "h2", "h3", "h4", "h5", "h6", "th", "td", "dt", "dd", "option", "label",
                       "button", "a", "span", "small", "li", "p", "textarea"):
            item["text"] = mask_text(el.get_text(" "))
        structure.append(item)

    body = soup.body or soup
    lines = [mask_text(t) for t in body.stripped_strings]
    text_lines = [t for t in lines if t]
    title = soup.title.get_text() if soup.title else ""
    return {"kind": "html", "title": mask_text(title), "text": text_lines, "structure": structure, "assets": assets}


def binary_repr(data):
    out = {"kind": "binary", "sha256": hashlib.sha256(data).hexdigest(), "length": len(data)}
    if len(data) <= BINARY_INLINE_LIMIT:
        out["base64"] = base64.b64encode(data).decode("ascii")
    return out


def nrbf_strings(data):
    """Very small .NET BinaryFormatter (MS-NRBF) reader: returns the BinaryObjectString values in stream order.
    Enough to recover the brand names in the legacy /api/files payload without deserialising anything."""
    out, i = [], 0
    while i < len(data):
        if data[i] == 0x06 and i + 5 < len(data):
            j = i + 5
            length, shift = 0, 0
            while j < len(data):
                b = data[j]
                length |= (b & 0x7F) << shift
                j += 1
                if not b & 0x80:
                    break
                shift += 7
            chunk = data[j:j + length]
            try:
                s = chunk.decode("utf-8")
                if length and len(chunk) == length and s.isprintable():
                    out.append(s)
                    i = j + length
                    continue
            except UnicodeDecodeError:
                pass
        i += 1
    return out


def semantic_brand_list(resp_bytes, content_type):
    """/api/files contract: the list of brand names, whatever the wire format (BinaryFormatter legacy, JSON target)."""
    mime = mime_of(content_type)
    if "json" in mime:
        try:
            doc = json.loads(resp_bytes.decode("utf-8-sig"))
            return [d.get("Brand", d.get("brand")) for d in doc]
        except Exception:
            return None
    strings = nrbf_strings(resp_bytes)
    return [s for s in strings if not s.startswith("eShopLegacy")]


def normalise_body(resp_bytes, content_type, status, compare="full", mask_patterns=()):
    """Return the normalised body dict for a response."""
    mime = mime_of(content_type)
    if not resp_bytes:
        return {"kind": "empty"}
    if compare == "brands-semantic":
        return {"kind": "semantic", "brands": semantic_brand_list(resp_bytes, content_type), **binary_repr(resp_bytes)}
    if status >= 500 or compare == "status":
        # Error pages differ by host (IIS YSOD / Mono / ASP.NET Core); only status + content type family are contract.
        return {"kind": "opaque", "length": len(resp_bytes)}
    if "html" in mime:
        return normalise_html(resp_bytes.decode("utf-8", "replace"), mask_patterns)
    if "json" in mime:
        try:
            return {"kind": "json", "json": json.loads(resp_bytes.decode("utf-8-sig"))}
        except ValueError:
            pass
    if mime.startswith("text/") or mime in TEXT_TYPES or "xml" in mime:
        text = resp_bytes.decode("utf-8-sig", "replace").replace("\r\n", "\n")
        data = text.encode("utf-8")
        return {"kind": "text", "sha256": hashlib.sha256(data).hexdigest(), "length": len(data)}
    return binary_repr(resp_bytes)


def key_headers(headers, base_url):
    h = {k.lower(): v for k, v in headers.items()}
    out = {"content-type": mime_of(h.get("content-type")) or None}
    if "location" in h:
        out["location"] = norm_url(h["location"], base_url)
    return out


def raw_sha(data):
    return hashlib.sha256(data or b"").hexdigest()


# ---- comparison --------------------------------------------------------------------------------------------------

def _content_type_family(ct):
    if not ct:
        return None
    if "html" in ct:
        return "html"
    if "json" in ct:
        return "json"
    if ct.startswith("image/"):
        return ct
    if "javascript" in ct:
        return "javascript"
    return ct


def compare(expected, actual, compare_mode="full"):
    """Return (verdict, reasons). verdict in identical | equivalent-after-normalisation | different."""
    reasons = []
    if expected["status"] != actual["status"]:
        reasons.append("status %s != %s" % (expected["status"], actual["status"]))
    eh, ah = expected["headers"], actual["headers"]
    if eh.get("location") != ah.get("location"):
        reasons.append("location %r != %r" % (eh.get("location"), ah.get("location")))
    ect, act = eh.get("content-type"), ah.get("content-type")
    if expected["body"]["kind"] not in ("empty",) and compare_mode != "brands-semantic":
        if _content_type_family(ect) != _content_type_family(act):
            reasons.append("content-type %r != %r" % (ect, act))
    eb, ab = expected["body"], actual["body"]
    if compare_mode == "brands-semantic":
        if eb.get("brands") != ab.get("brands"):
            reasons.append("brands %r != %r" % (eb.get("brands"), ab.get("brands")))
    elif eb["kind"] == "opaque" or compare_mode == "status":
        pass
    elif eb["kind"] != ab["kind"]:
        reasons.append("body kind %s != %s" % (eb["kind"], ab["kind"]))
    elif eb["kind"] == "html":
        for part in ("title", "text", "structure"):
            if eb[part] != ab[part]:
                reasons.append("html %s differs: %s" % (part, first_diff(eb[part], ab[part])))
    elif eb["kind"] == "json":
        if eb["json"] != ab["json"]:
            reasons.append("json differs: %s" % first_diff(eb["json"], ab["json"]))
    elif eb["kind"] in ("text", "binary"):
        if eb["sha256"] != ab["sha256"]:
            reasons.append("%s sha256 differs (len %s vs %s)" % (eb["kind"], eb["length"], ab["length"]))
    if reasons:
        return "different", reasons
    if expected.get("raw_sha256") == actual.get("raw_sha256") and ect == act:
        return "identical", []
    return "equivalent-after-normalisation", []


def first_diff(a, b):
    if isinstance(a, list) and isinstance(b, list):
        for i, (x, y) in enumerate(zip(a, b)):
            if x != y:
                return "[%d] %s != %s" % (i, json.dumps(x)[:200], json.dumps(y)[:200])
        return "length %d != %d" % (len(a), len(b))
    return "%s != %s" % (json.dumps(a)[:200], json.dumps(b)[:200])
