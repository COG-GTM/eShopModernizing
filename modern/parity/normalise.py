"""Normalisation used by the side-by-side replay (replay.py) on top of capture_baseline.normalise_body.

Both legacy and modern responses go through exactly the same functions, so any remaining difference is a real one:
- body: capture_baseline.normalise_body (tokens/viewstate masked, session footer masked, origin -> {origin},
  scripts/styles dropped, one tag per line, JSON re-serialised with sorted keys, binaries -> size + sha256)
  plus asset cache-busting hashes (?v=...) masked;
- page-state tokens: the hidden __VIEWSTATE (legacy WebForms) and __RequestVerificationToken (modern, the
  antiforgery hardening) inputs are one canonical line; __EVENTVALIDATION / __VIEWSTATEGENERATOR and the
  <div class="aspNetHidden"> wrappers WebForms puts round them are dropped;
- host error pages (Mono/xsp4 error page, the IIS-style "Server Error in '/' Application." page) -> one marker
  line: the status code is compared separately and the page body is host chrome (stack traces, versions);
- numeric character references are canonical (&#x27; == &#39;): same encoded character, different spelling;
- location: resolved against the request URL and reduced to path+query (absolute vs relative is not a difference);
- content-type: compared as mime type + charset (charset defaults to utf-8 for text types when omitted).
Stdlib only.
"""
import json
import re
import urllib.parse

from capture_baseline import MASK, normalise_body as _baseline_normalise_body, normalise_origin

ASSET_HASH = re.compile(r'((?:src|href)="[^"?]+\.(?:css|js|png|jpg|gif|svg|ico|woff2?|ttf|eot))\?v=[^"]*"', re.I)
TEXT_MIMES = ("text/", "application/json", "application/xml", "application/javascript")


PAGE_STATE = re.compile(r'<input\b[^>]*\bname="(?:__VIEWSTATE|__RequestVerificationToken)"[^>]*>')
DROPPED_STATE = re.compile(r'<input\b[^>]*\bname="(?:__EVENTVALIDATION|__VIEWSTATEGENERATOR)"[^>]*>\n?')
HIDDEN_DIV = re.compile(r'<div class="aspNetHidden">\n((?:<input [^\n]*\n)*)</div>\n?')
HEX_REF = re.compile(r"&#[xX]([0-9a-fA-F]+);")
HOST_ERROR = re.compile(r"\A\(host error page\)|<h1>Server Error in '/' Application\.")
PAGE_STATE_LINE = '<input type="hidden" name="{page-state}" value="%s" />' % MASK
HOST_ERROR_LINE = "(host error page)"


def normalise_body(ctype, body, base_url):
    text = _baseline_normalise_body(ctype, body, base_url)
    if HOST_ERROR.search(text):
        return HOST_ERROR_LINE
    text = ASSET_HASH.sub(r'\1?v=%s"' % MASK, text)
    text = PAGE_STATE.sub(PAGE_STATE_LINE, text)
    text = DROPPED_STATE.sub("", text)
    text = HIDDEN_DIV.sub(r"\1", text)
    return HEX_REF.sub(lambda m: "&#%d;" % int(m.group(1), 16), text)


def host_error_detail(ctype, body, base_url):
    """What the host error page said (for the capture files / report; never compared)."""
    text = _baseline_normalise_body(ctype, body, base_url)
    if not HOST_ERROR.search(text):
        return ""
    if text.startswith("(host error page)"):
        return text[len("(host error page) "):]
    return visible_text(text)[:300]


def normalise_location(location, request_url):
    if not location:
        return ""
    absolute = urllib.parse.urljoin(request_url, location)
    parts = urllib.parse.urlsplit(absolute)
    req = urllib.parse.urlsplit(request_url)
    if parts.netloc and parts.netloc != req.netloc:
        return absolute  # off-site redirect: keep it whole
    return urllib.parse.urlunsplit(("", "", parts.path or "/", parts.query, parts.fragment))


def normalise_content_type(ctype):
    if not ctype:
        return ""
    pieces = [p.strip() for p in ctype.split(";")]
    mime = pieces[0].lower()
    params = dict(p.split("=", 1) for p in pieces[1:] if "=" in p)
    charset = params.get("charset", "").strip('"').lower()
    if not charset and mime.startswith(TEXT_MIMES):
        charset = "utf-8"
    return mime + ("; charset=" + charset if charset else "")


def json_shape(text):
    """Type skeleton of a JSON document (keys + value types), used to describe JSON-shape differences."""
    def shape(v):
        if isinstance(v, dict):
            return {k: shape(v[k]) for k in sorted(v)}
        if isinstance(v, list):
            return [shape(v[0])] if v else []
        return type(v).__name__
    try:
        return json.dumps(shape(json.loads(text)), sort_keys=True)
    except ValueError:
        return None


def visible_text(normalised_html):
    """Visible text of a normalised HTML body (tags removed, whitespace collapsed) - for report summaries."""
    text = re.sub(r"<[^>]+>", " ", normalised_html)
    return re.sub(r"\s+", " ", text).strip()


__all__ = ["normalise_body", "host_error_detail", "normalise_location", "normalise_content_type", "json_shape", "visible_text",
           "normalise_origin", "MASK"]
