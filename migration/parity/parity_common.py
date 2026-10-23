"""Shared flow execution + normalisation for the legacy baseline capture and the parity replay.

A flow (see migration/baseline/flows.json) is executed against a base URL and reduced to a
normalised, host-independent representation so legacy (Mono/xsp4) and migrated (.NET 8) outputs can
be compared field by field. Normalisation rules are documented in migration/CONTRACT.md.
"""
from __future__ import annotations

import base64
import hashlib
import json
import re
from typing import Any
from urllib.parse import urljoin, urlsplit, urlunsplit

import requests
from bs4 import BeautifulSoup, Comment

# Hidden fields that are per-request infrastructure (antiforgery / WebForms state), not app data.
TOKEN_FIELDS = {
    "__RequestVerificationToken", "__VIEWSTATE", "__VIEWSTATEGENERATOR", "__VIEWSTATEENCRYPTED",
    "__EVENTVALIDATION", "__EVENTTARGET", "__EVENTARGUMENT", "__LASTFOCUS", "__PREVIOUSPAGE",
}
# Footer rendered from Session["MachineName"] + Session["SessionStartTime"] (en-US G format).
SESSION_INFO_RE = re.compile(r"[^\s,]+, \d{1,2}/\d{1,2}/\d{4} \d{1,2}:\d{2}:\d{2} [AP]M")
SESSION_INFO = "<machine>, <session-start>"
SELECTED_HEADERS = ["content-type", "location"]
HTML_TYPES = ("text/html", "application/xhtml+xml")
JSON_TYPES = ("application/json", "text/json")
TEXT_STATIC_TYPES = ("text/css", "application/javascript", "text/javascript", "application/x-javascript")
DATA_STATE_PATHS = {
    "mvc": "/Catalog/Index?pageSize=1000&pageIndex=0",
    "webforms": "/Default/index/0/size/1000",
}


def _ws(text: str) -> str:
    return re.sub(r"\s+", " ", text or "").strip()


def mime_of(content_type: str | None) -> str:
    return (content_type or "").split(";")[0].strip().lower()


class Normaliser:
    """Turns raw responses into comparable structures. `base` is the app's base URL (no trailing /)."""

    def __init__(self, base: str):
        self.base = base.rstrip("/")
        parts = urlsplit(self.base)
        self.origins = {f"{parts.scheme}://{parts.netloc}"}
        if parts.hostname in ("localhost", "127.0.0.1"):
            self.origins |= {f"{parts.scheme}://localhost:{parts.port}", f"{parts.scheme}://127.0.0.1:{parts.port}"}

    # -- urls -------------------------------------------------------------------------------
    def url(self, value: str | None, page_url: str | None = None) -> str | None:
        """Resolve relative to the page, then replace this app's origin with `{base}`."""
        if value is None:
            return None
        value = value.strip()
        if page_url is not None and not value.startswith(("http://", "https://", "#", "javascript:", "mailto:")):
            value = urljoin(page_url, value)
        for origin in sorted(self.origins, key=len, reverse=True):
            if value.startswith(origin):
                value = "{base}" + value[len(origin):]
        return value

    def text(self, value: str) -> str:
        value = _ws(value)
        for origin in self.origins:
            value = value.replace(origin, "{base}")
        return SESSION_INFO_RE.sub(SESSION_INFO, value)

    # -- headers ----------------------------------------------------------------------------
    def headers(self, resp: requests.Response, page_url: str) -> dict[str, Any]:
        out: dict[str, Any] = {}
        for name in SELECTED_HEADERS:
            v = resp.headers.get(name)
            if v is None:
                continue
            if name == "content-type":
                out["content-type"] = v
                out["mime"] = mime_of(v)
            elif name == "location":
                out["location"] = self.url(v, page_url)
        return out

    # -- bodies -----------------------------------------------------------------------------
    def body(self, resp: requests.Response, page_url: str) -> dict[str, Any]:
        mime = mime_of(resp.headers.get("content-type"))
        raw = resp.content
        if len(raw) == 0:
            return {"kind": "empty"}
        if mime in HTML_TYPES and resp.status_code < 400 and raw.lstrip()[:1] == b"<":
            return {"kind": "html", "html": self.html(raw.decode(resp.encoding or "utf-8", "replace"), page_url)}
        if mime in JSON_TYPES:
            try:
                return {"kind": "json", "json": json.loads(raw.decode("utf-8-sig"))}
            except ValueError:
                pass
        out = {"kind": "binary", "sha256": hashlib.sha256(raw).hexdigest(), "length": len(raw)}
        if mime not in TEXT_STATIC_TYPES and not mime.startswith("image/") and len(raw) <= 4096:
            out["base64"] = base64.b64encode(raw).decode()
        return out

    def html(self, markup: str, page_url: str) -> dict[str, Any]:
        soup = BeautifulSoup(markup, "html.parser")
        for c in soup.find_all(string=lambda s: isinstance(s, Comment)):
            c.extract()
        assets = {
            "stylesheets": [self.url(l.get("href"), page_url) for l in soup.find_all("link")
                            if "stylesheet" in (l.get("rel") or [])],
            "scripts": [self.url(s.get("src"), page_url) for s in soup.find_all("script") if s.get("src")],
        }
        for el in soup.find_all(["script", "style", "noscript", "template"]):
            el.decompose()
        # Invisible elements (e.g. WebForms validators with display:none) are not visible text.
        for el in soup.find_all(style=re.compile(r"display\s*:\s*none|visibility\s*:\s*hidden", re.I)):
            el.decompose()
        title = self.text(soup.title.get_text()) if soup.title else None
        body = soup.body or soup
        return {
            "title": title,
            "headings": [[h.name, self.text(h.get_text(" "))] for h in body.find_all(["h1", "h2", "h3", "h4"])],
            "text": self.text(body.get_text(" ")),
            "links": [{"text": self.text(a.get_text(" ")), "href": self.url(a.get("href"), page_url),
                       "class": sorted(a.get("class") or [])} for a in body.find_all("a")],
            "images": [self.url(i.get("src"), page_url) for i in body.find_all(["img", "image"])],
            "tables": [self._table(t) for t in body.find_all("table")],
            "definitions": [[self.text(dt.get_text(" ")), self.text(dt.find_next_sibling("dd").get_text(" "))
                             if dt.find_next_sibling("dd") else None] for dt in body.find_all("dt")],
            "forms": [self._form(f, page_url) for f in body.find_all("form")],
            "validation": [self.text(e.get_text(" ")) for e in body.select(
                ".field-validation-error, .validation-summary-errors li, .text-danger") if _ws(e.get_text())],
            "assets": assets,
        }

    def _table(self, table) -> dict[str, Any]:
        rows = []
        for tr in table.find_all("tr"):
            cells = [self.text(c.get_text(" ")) for c in tr.find_all(["th", "td"], recursive=False)]
            rows.append(cells)
        return {"rows": rows}

    def _form(self, form, page_url: str) -> dict[str, Any]:
        fields = []
        for el in form.find_all(["input", "select", "textarea", "button"]):
            name = el.get("name")
            if name in TOKEN_FIELDS:
                continue
            f: dict[str, Any] = {"tag": el.name, "name": name}
            if el.name == "input":
                f["type"] = (el.get("type") or "text").lower()
                f["value"] = el.get("value")
                if f["type"] in ("checkbox", "radio"):
                    f["checked"] = el.has_attr("checked")
            elif el.name == "select":
                f["options"] = [[o.get("value"), self.text(o.get_text()), o.has_attr("selected")]
                                for o in el.find_all("option")]
            elif el.name == "textarea":
                f["value"] = el.get_text()
            else:
                f["type"] = (el.get("type") or "submit").lower()
                f["text"] = self.text(el.get_text(" "))
            if el.has_attr("readonly"):
                f["readonly"] = True
            if el.has_attr("disabled"):
                f["disabled"] = True
            dv = {k: v for k, v in sorted(el.attrs.items()) if k.startswith("data-val")}
            if dv:
                f["data-val"] = dv
            fields.append(f)
        return {"method": (form.get("method") or "get").upper(),
                "action": self.url(form.get("action") or page_url, page_url), "fields": fields}

    # -- data state -------------------------------------------------------------------------
    def data_state(self, resp: requests.Response, page_url: str) -> dict[str, Any]:
        """Full catalog snapshot: every row of the list page in row order (cells + per-row links)."""
        soup = BeautifulSoup(resp.text, "html.parser")
        table = soup.find("table", class_="table")
        rows = []
        if table is not None:
            for tr in table.find_all("tr"):
                if tr.find("th"):
                    continue
                tds = tr.find_all("td", recursive=False)
                if not tds:
                    continue
                img = tr.find(["img", "image"])
                rows.append({
                    "image": self.url(img.get("src"), page_url) if img else None,
                    "cells": [self.text(td.get_text(" ")) for td in tds],
                    "links": [self.url(a.get("href"), page_url) for a in tr.find_all("a") if a.get("href")],
                })
        pager = soup.select_one(".esh-pager-item:not(a)") or soup.find("span", class_="esh-pager-item")
        return {"status": resp.status_code, "pager": self.text(pager.get_text(" ")) if pager else None,
                "row_count": len(rows), "rows": rows}


# -- flow execution -----------------------------------------------------------------------------

def _collect_form(markup: str, page_url: str, form_index: int = 0) -> tuple[str, dict[str, str]]:
    """Returns (absolute action URL, every successful control's default value) for a form."""
    soup = BeautifulSoup(markup, "html.parser")
    forms = [f for f in soup.find_all("form") if (f.get("method") or "get").lower() == "post"]
    if not forms:
        raise RuntimeError(f"no POST form found on {page_url}")
    form = forms[form_index]
    data: dict[str, str] = {}
    for el in form.find_all(["input", "select", "textarea"]):
        name = el.get("name")
        if not name:
            continue
        if el.name == "input":
            t = (el.get("type") or "text").lower()
            if t in ("submit", "button", "image", "reset", "file"):
                continue
            if t in ("checkbox", "radio") and not el.has_attr("checked"):
                continue
            data[name] = el.get("value") or ""
        elif el.name == "select":
            opt = el.find("option", selected=True) or el.find("option")
            data[name] = opt.get("value", opt.get_text()) if opt else ""
        else:
            data[name] = el.get_text()
    return urljoin(page_url, form.get("action") or page_url), data


def execute(session: requests.Session, base: str, flow: dict[str, Any], timeout: float = 180) -> tuple[requests.Response, dict[str, Any]]:
    """Runs one flow. Returns (response, request record). POST flows first GET `form.from` in the
    same session to obtain antiforgery / viewstate tokens dynamically, then overlay `form.fields`."""
    req = flow["request"]
    method = req["method"].upper()
    url = base.rstrip("/") + req["path"]
    record: dict[str, Any] = {"method": method, "path": req["path"]}
    data = None
    form = flow.get("form")
    if form:
        page_url = base.rstrip("/") + form["from"]
        page = session.get(page_url, timeout=timeout, allow_redirects=False)
        if page.status_code != 200:
            raise RuntimeError(f"form source {form['from']} returned {page.status_code}")
        action, data = _collect_form(page.text, page_url, form.get("index", 0))
        for name in form.get("drop", []):
            data.pop(name, None)
        data.update(form.get("fields", {}))
        if not req.get("path_is_explicit"):
            url = action
        record["form_from"] = form["from"]
        record["form_fields"] = {k: v for k, v in data.items() if k not in TOKEN_FIELDS}
        record["tokens_submitted"] = sorted(k for k in data if k in TOKEN_FIELDS)
        parts = urlsplit(url)
        record["path"] = parts.path + (f"?{parts.query}" if parts.query else "")
    resp = session.request(method, url, data=data, headers=req.get("headers") or {}, timeout=timeout,
                           allow_redirects=False)
    return resp, record


def normalise_response(norm: Normaliser, resp: requests.Response, url: str) -> dict[str, Any]:
    return {"status": resp.status_code, "headers": norm.headers(resp, url), "body": norm.body(resp, url)}


def fetch_data_state(session: requests.Session, base: str, app: str, timeout: float = 180) -> dict[str, Any]:
    url = base.rstrip("/") + DATA_STATE_PATHS[app]
    resp = session.get(url, timeout=timeout, allow_redirects=False)
    return Normaliser(base).data_state(resp, url)


# -- comparison ---------------------------------------------------------------------------------

def projection(flow: dict[str, Any], normalised: dict[str, Any]) -> dict[str, Any]:
    """The subset of a normalised response that the flow's `compare` spec says must match."""
    cmp = flow.get("compare", {})
    out: dict[str, Any] = {"status": normalised["status"]}
    h = normalised["headers"]
    if cmp.get("mime", True) and normalised["status"] < 400:
        out["mime"] = h.get("mime")
    if cmp.get("location", True) and "location" in h:
        out["location"] = h["location"]
    mode = cmp.get("body", "auto")
    body = normalised["body"]
    if mode == "none" or (mode == "auto" and (normalised["status"] >= 400 or 300 <= normalised["status"] < 400)):
        return out
    if mode == "sha256" or body.get("kind") == "binary":
        out["body"] = {k: body.get(k) for k in ("kind", "sha256", "length")}
        return out
    if body.get("kind") == "html":
        html = dict(body["html"])
        if not cmp.get("assets", True):
            html.pop("assets", None)
        for key in cmp.get("ignore_html_keys", []):
            html.pop(key, None)
        out["body"] = {"kind": "html", "html": html}
        return out
    out["body"] = body
    return out


def diff(expected: Any, actual: Any, path: str = "$", out: list[str] | None = None, limit: int = 40) -> list[str]:
    out = [] if out is None else out
    if len(out) >= limit:
        return out
    if isinstance(expected, dict) and isinstance(actual, dict):
        for k in list(expected.keys()) + [k for k in actual if k not in expected]:
            if k not in actual:
                out.append(f"{path}.{k}: missing (expected {json.dumps(expected[k])[:200]})")
            elif k not in expected:
                out.append(f"{path}.{k}: unexpected {json.dumps(actual[k])[:200]}")
            else:
                diff(expected[k], actual[k], f"{path}.{k}", out, limit)
    elif isinstance(expected, list) and isinstance(actual, list):
        if len(expected) != len(actual):
            out.append(f"{path}: length {len(actual)} != expected {len(expected)}")
        for i, (e, a) in enumerate(zip(expected, actual)):
            diff(e, a, f"{path}[{i}]", out, limit)
    elif expected != actual:
        out.append(f"{path}: {json.dumps(actual)[:200]} != expected {json.dumps(expected)[:200]}")
    return out
