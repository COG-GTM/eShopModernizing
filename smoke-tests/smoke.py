#!/usr/bin/env python3
"""Canary smoke tests for the modernized eShop catalog (eShopCoreModernized).

Exercises the application's critical paths over HTTP against any deployment
stage (direct instance, nginx canary at 25/50/75/100%, or production) and
exits non-zero when a critical check fails, so it can gate traffic shifts.

Only the Python 3.8+ standard library is required.
"""

from __future__ import annotations

import argparse
import http.cookiejar
import json
import os
import re
import ssl
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from html.parser import HTMLParser
from typing import Callable, Dict, List, Optional, Tuple

CRITICAL = "critical"
WARN = "warn"

PASS = "pass"
FAIL = "fail"
SKIP = "skip"

EXIT_OK = 0
EXIT_CRITICAL = 1
EXIT_WARN_STRICT = 2
EXIT_USAGE = 64

DEFAULT_STAGES_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "stages.json")
NOT_FOUND_ID = 2147483647


class CheckFailure(Exception):
    pass


class CheckSkipped(Exception):
    pass


@dataclass
class Response:
    status: int
    headers: Dict[str, str]
    body: bytes
    url: str
    elapsed_ms: float

    @property
    def text(self) -> str:
        return self.body.decode("utf-8", errors="replace")

    def json(self):
        try:
            return json.loads(self.body)
        except ValueError as exc:
            raise CheckFailure(f"{self.url}: response is not valid JSON ({exc})") from exc

    def header(self, name: str) -> str:
        return self.headers.get(name.lower(), "")


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Client:
    def __init__(self, base_url: str, timeout: float, extra_headers: Dict[str, str], insecure: bool):
        self.base_url = base_url.rstrip("/")
        self.timeout = timeout
        self.extra_headers = extra_headers
        self.insecure = insecure
        self.cookies = http.cookiejar.CookieJar()
        context = ssl._create_unverified_context() if insecure else ssl.create_default_context()
        https = urllib.request.HTTPSHandler(context=context)
        cookie_handler = urllib.request.HTTPCookieProcessor(self.cookies)
        self._follow = urllib.request.build_opener(https, cookie_handler)
        self._no_follow = urllib.request.build_opener(https, cookie_handler, _NoRedirect())
        self.latencies_ms: List[float] = []

    def url(self, path: str) -> str:
        if path.startswith("http://") or path.startswith("https://"):
            return path
        return self.base_url + (path if path.startswith("/") else "/" + path)

    def request(
        self,
        method: str,
        path: str,
        form: Optional[Dict[str, str]] = None,
        follow_redirects: bool = True,
        headers: Optional[Dict[str, str]] = None,
    ) -> Response:
        url = self.url(path)
        data = urllib.parse.urlencode(form).encode() if form is not None else None
        req = urllib.request.Request(url, data=data, method=method)
        req.add_header("User-Agent", "eshop-canary-smoke/1.0")
        for name, value in {**self.extra_headers, **(headers or {})}.items():
            req.add_header(name, value)
        if data is not None:
            req.add_header("Content-Type", "application/x-www-form-urlencoded")
        opener = self._follow if follow_redirects else self._no_follow
        started = time.monotonic()
        try:
            with opener.open(req, timeout=self.timeout) as resp:
                body = resp.read()
                status = resp.status
                resp_headers = {k.lower(): v for k, v in resp.headers.items()}
                final_url = resp.geturl()
        except urllib.error.HTTPError as err:
            body = err.read() if err.fp else b""
            status = err.code
            resp_headers = {k.lower(): v for k, v in (err.headers or {}).items()}
            final_url = url
        except (urllib.error.URLError, OSError) as err:
            raise CheckFailure(f"{method} {url} failed: {getattr(err, 'reason', err)}") from err
        elapsed = (time.monotonic() - started) * 1000
        self.latencies_ms.append(elapsed)
        return Response(status, resp_headers, body, final_url, elapsed)

    def get(self, path: str, **kwargs) -> Response:
        return self.request("GET", path, **kwargs)

    def post(self, path: str, form: Dict[str, str], **kwargs) -> Response:
        return self.request("POST", path, form=form, **kwargs)


class _PageParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.assets: List[str] = []
        self.links: List[str] = []
        self.inputs: Dict[str, str] = {}
        self.table_rows = 0
        self._in_tbody = False

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == "link" and a.get("rel") == "stylesheet" and a.get("href"):
            self.assets.append(a["href"])
        elif tag in ("script", "img") and a.get("src"):
            self.assets.append(a["src"])
        elif tag == "a" and a.get("href"):
            self.links.append(a["href"])
        elif tag == "input" and a.get("name"):
            self.inputs.setdefault(a["name"], a.get("value") or "")
        elif tag == "tbody":
            self._in_tbody = True
        elif tag == "tr" and self._in_tbody:
            self.table_rows += 1

    def handle_endtag(self, tag):
        if tag == "tbody":
            self._in_tbody = False


def parse_page(html: str) -> _PageParser:
    parser = _PageParser()
    parser.feed(html)
    return parser


def expect(condition: bool, message: str) -> None:
    if not condition:
        raise CheckFailure(message)


def expect_status(resp: Response, *statuses: int) -> None:
    expect(
        resp.status in statuses,
        f"GET {resp.url}: expected HTTP {'/'.join(map(str, statuses))}, got {resp.status}",
    )


def detail_ids(links: List[str]) -> List[int]:
    ids = []
    for href in links:
        m = re.search(r"/Catalog/Details/(\d+)", href, re.IGNORECASE) or re.search(
            r"/Catalog/Details\?id=(\d+)", href, re.IGNORECASE
        )
        if m:
            ids.append(int(m.group(1)))
    return ids


@dataclass
class Context:
    client: Client
    stage: str
    expect: Dict[str, object]
    allow_writes: bool
    username: str
    password: str
    state: Dict[str, object] = field(default_factory=dict)


@dataclass
class Check:
    name: str
    description: str
    severity: str
    fn: Callable[[Context], Optional[str]]
    sampled: bool = True
    writes: bool = False


@dataclass
class Result:
    name: str
    description: str
    severity: str
    status: str
    message: str = ""
    samples: int = 0
    failures: int = 0
    duration_ms: float = 0.0
    max_latency_ms: float = 0.0


CHECKS: List[Check] = []


def check(name: str, description: str, severity: str = CRITICAL, sampled: bool = True, writes: bool = False):
    def register(fn):
        CHECKS.append(Check(name, description, severity, fn, sampled, writes))
        return fn

    return register


@check("health.basic", "GET /api/health returns Healthy")
def check_health_basic(ctx: Context) -> str:
    resp = ctx.client.get("/api/health")
    expect_status(resp, 200)
    body = resp.json()
    expect(body.get("status") == "Healthy", f"/api/health status is {body.get('status')!r}")
    return "Healthy"


@check("health.detailed", "GET /api/health/detailed reports healthy dependencies and expected feature flags")
def check_health_detailed(ctx: Context) -> str:
    resp = ctx.client.get("/api/health/detailed")
    expect_status(resp, 200)
    body = resp.json()
    expect(body.get("status") == "Healthy", f"overall status is {body.get('status')!r}")
    services = body.get("services") or {}
    expect(isinstance(services, dict), "'services' missing from detailed health payload")
    unhealthy = [
        f"{name}={svc.get('status')}"
        for name, svc in services.items()
        if name != "configuration" and isinstance(svc, dict) and svc.get("status") != "Healthy"
    ]
    expect(not unhealthy, "unhealthy dependencies: " + ", ".join(unhealthy))
    config = services.get("configuration") or {}
    ctx.state["configuration"] = config
    mismatches = [
        f"{flag}: expected {want!r}, got {config.get(flag)!r}"
        for flag, want in (ctx.expect.get("flags") or {}).items()
        if config.get(flag) != want
    ]
    expect(not mismatches, "feature flag drift: " + "; ".join(mismatches))
    deps = ", ".join(n for n in services if n != "configuration") or "none"
    return f"dependencies healthy ({deps})"


@check("catalog.index", "Catalog list page renders items")
def check_catalog_index(ctx: Context) -> str:
    resp = ctx.client.get("/Catalog/Index?pageSize=10&pageIndex=0")
    expect_status(resp, 200)
    html = resp.text
    expect("esh-table" in html, "catalog table markup not found on /Catalog/Index")
    page = parse_page(html)
    min_items = int(ctx.expect.get("min_catalog_items", 1))
    expect(page.table_rows >= min_items, f"expected >= {min_items} catalog rows, found {page.table_rows}")
    ids = detail_ids(page.links)
    expect(ids, "no /Catalog/Details links found on catalog page")
    ctx.state.setdefault("catalog_ids", ids)
    ctx.state.setdefault("catalog_html", html)
    m = re.search(r"of\s+(\d+)\s+products", html)
    total = f", {m.group(1)} total" if m else ""
    return f"{page.table_rows} rows{total}"


@check("catalog.pagination", "Catalog pagination serves a second page")
def check_catalog_pagination(ctx: Context) -> str:
    resp = ctx.client.get("/Catalog/Index?pageSize=2&pageIndex=1")
    expect_status(resp, 200)
    expect("esh-pager" in resp.text, "pager markup missing on paginated catalog page")
    return "page 2 rendered"


@check("catalog.details", "Catalog item details page renders for a listed item")
def check_catalog_details(ctx: Context) -> str:
    ids = ctx.state.get("catalog_ids") or []
    if not ids:
        raise CheckSkipped("no catalog item ids discovered (catalog.index failed)")
    item_id = ids[0]
    resp = ctx.client.get(f"/Catalog/Details/{item_id}")
    expect_status(resp, 200)
    expect("Details" in resp.text and "esh-picture" in resp.text, f"details markup missing for item {item_id}")
    return f"item {item_id}"


@check("catalog.not_found", "Unknown catalog item returns 404, not 5xx")
def check_catalog_not_found(ctx: Context) -> str:
    resp = ctx.client.get(f"/Catalog/Details/{NOT_FOUND_ID}")
    expect_status(resp, 404)
    return "404 as expected"


@check("api.brands", "GET /api/Brands and /api/Brands/{id} return catalog brands")
def check_api_brands(ctx: Context) -> str:
    resp = ctx.client.get("/api/Brands")
    expect_status(resp, 200)
    brands = resp.json()
    expect(isinstance(brands, list) and brands, "/api/Brands returned an empty or non-list payload")
    first = brands[0]
    expect(isinstance(first, dict) and "id" in first and "brand" in first, f"unexpected brand shape: {first!r}")
    one = ctx.client.get(f"/api/Brands/{first['id']}")
    expect_status(one, 200)
    expect(one.json().get("id") == first["id"], "/api/Brands/{id} returned a different brand")
    ctx.state["brands"] = brands
    return f"{len(brands)} brands"


@check("api.files", "GET /api/Files returns the brand export")
def check_api_files(ctx: Context) -> str:
    resp = ctx.client.get("/api/Files")
    expect_status(resp, 200)
    exported = resp.json()
    expect(isinstance(exported, list) and exported, "/api/Files returned an empty or non-list payload")
    brands = ctx.state.get("brands")
    if isinstance(brands, list):
        expect(len(exported) == len(brands), f"/api/Files has {len(exported)} brands, /api/Brands has {len(brands)}")
    return f"{len(exported)} brands exported"


@check("api.default_image", "GET /api/ImageUpload/default returns an image URL")
def check_default_image(ctx: Context) -> str:
    resp = ctx.client.get("/api/ImageUpload/default")
    expect_status(resp, 200)
    url = resp.json().get("imageUrl")
    expect(bool(url), "imageUrl missing")
    return str(url)


@check("auth.guard", "Protected page (/Catalog/Create) redirects anonymous users to sign-in")
def check_auth_guard(ctx: Context) -> str:
    anon = Client(ctx.client.base_url, ctx.client.timeout, ctx.client.extra_headers, ctx.client.insecure)
    resp = anon.get("/Catalog/Create", follow_redirects=False)
    ctx.client.latencies_ms.append(resp.elapsed_ms)
    expect_status(resp, 302, 401)
    if resp.status == 302:
        location = resp.header("location")
        expect(bool(location), "302 without Location header")
        expect("/Catalog/Create" not in location.split("?")[0], f"redirect loops back to protected page: {location}")
        return f"redirects to {location.split('?')[0]}"
    return "401 challenge"


@check("auth.login_page", "Sign-in page renders", sampled=False)
def check_login_page(ctx: Context) -> str:
    if (ctx.state.get("configuration") or {}).get("useAzureActiveDirectory"):
        raise CheckSkipped("Azure AD sign-in is enabled; local login page is not used")
    resp = ctx.client.get("/Account/Login")
    expect_status(resp, 200)
    page = parse_page(resp.text)
    expect("username" in page.inputs and "password" in page.inputs, "login form fields missing")
    return "login form present"


@check("assets.static", "CSS/JS/images referenced by the catalog page resolve", severity=WARN, sampled=False)
def check_static_assets(ctx: Context) -> str:
    html = ctx.state.get("catalog_html")
    if not isinstance(html, str):
        raise CheckSkipped("catalog page not available (catalog.index failed)")
    assets = sorted({a for a in parse_page(html).assets if not a.startswith(("http://", "https://", "//", "data:"))})
    broken = []
    for asset in assets:
        resp = ctx.client.get(asset)
        if resp.status != 200:
            broken.append(f"{asset} ({resp.status})")
    expect(not broken, f"{len(broken)}/{len(assets)} assets broken: " + ", ".join(broken))
    return f"{len(assets)} assets OK"


@check(
    "catalog.crud_roundtrip",
    "Sign in, create a catalog item, verify it, then delete it",
    sampled=False,
    writes=True,
)
def check_crud_roundtrip(ctx: Context) -> str:
    if not ctx.allow_writes:
        raise CheckSkipped("writes disabled for this stage (use --allow-writes)")
    if (ctx.state.get("configuration") or {}).get("useAzureActiveDirectory"):
        raise CheckSkipped("Azure AD sign-in is enabled; scripted login not supported")
    client = ctx.client
    resp = client.post(
        "/Account/Login?ReturnUrl=%2FCatalog%2FCreate",
        {"username": ctx.username, "password": ctx.password, "returnUrl": "/Catalog/Create"},
    )
    expect_status(resp, 200)
    form = parse_page(resp.text).inputs
    token = form.get("__RequestVerificationToken")
    expect(bool(token), "sign-in did not reach the Create form (no antiforgery token)")
    brand_id, type_id = _first_option(resp.text, "CatalogBrandId"), _first_option(resp.text, "CatalogTypeId")
    expect(brand_id is not None and type_id is not None, "brand/type dropdowns missing on Create form")

    name = f"smoke-{ctx.stage}-{uuid.uuid4().hex[:8]}"
    created = client.post(
        "/Catalog/Create",
        {
            "__RequestVerificationToken": token or "",
            "Name": name,
            "Description": "Created by canary smoke test; safe to delete",
            "CatalogBrandId": str(brand_id),
            "CatalogTypeId": str(type_id),
            "Price": "1.00",
            "PictureFileName": "dummy.png",
            "AvailableStock": "1",
            "RestockThreshold": "0",
            "MaxStockThreshold": "1",
        },
        follow_redirects=False,
    )
    expect(created.status == 302, f"create returned {created.status}, expected 302 redirect to Index")

    item_id = _find_item_id(client, name)
    expect(item_id is not None, f"created item {name!r} not found in catalog")
    try:
        details = client.get(f"/Catalog/Details/{item_id}")
        expect_status(details, 200)
        expect(name in details.text, "details page does not show the created item")
    finally:
        _delete_item(client, item_id)
    expect(_find_item_id(client, name) is None, f"item {item_id} still listed after delete")
    return f"created and deleted item {item_id}"


def _first_option(html: str, select_name: str) -> Optional[int]:
    m = re.search(rf'<select[^>]*name="{select_name}"[^>]*>(.*?)</select>', html, re.S | re.I)
    if not m:
        return None
    opt = re.search(r'<option[^>]*value="(\d+)"', m.group(1))
    return int(opt.group(1)) if opt else None


def _find_item_id(client: Client, name: str) -> Optional[int]:
    resp = client.get("/Catalog/Index?pageSize=10000&pageIndex=0")
    expect_status(resp, 200)
    for row in re.findall(r"<tr>(.*?)</tr>", resp.text, re.S):
        if re.search(rf">\s*{re.escape(name)}\s*<", row):
            ids = detail_ids(re.findall(r'href="([^"]+)"', row))
            if ids:
                return ids[0]
    return None


def _delete_item(client: Client, item_id: int) -> None:
    page = client.get(f"/Catalog/Delete/{item_id}")
    expect_status(page, 200)
    token = parse_page(page.text).inputs.get("__RequestVerificationToken", "")
    resp = client.post(
        f"/Catalog/Delete/{item_id}",
        {"__RequestVerificationToken": token, "id": str(item_id)},
        follow_redirects=False,
    )
    expect(resp.status == 302, f"delete of item {item_id} returned {resp.status}")


def run_check(chk: Check, ctx: Context, samples: int) -> Result:
    result = Result(chk.name, chk.description, chk.severity, PASS)
    started = time.monotonic()
    last_ok = ""
    errors: List[str] = []
    for _ in range(samples if chk.sampled else 1):
        before = len(ctx.client.latencies_ms)
        try:
            last_ok = chk.fn(ctx) or ""
        except CheckSkipped as skip:
            result.status, result.message = SKIP, str(skip)
            break
        except CheckFailure as failure:
            errors.append(str(failure))
        except Exception as exc:  # noqa: BLE001 - a crashing check is a failed check
            errors.append(f"unexpected error: {exc.__class__.__name__}: {exc}")
        result.samples += 1
        latest = ctx.client.latencies_ms[before:]
        if latest:
            result.max_latency_ms = max(result.max_latency_ms, max(latest))
    result.duration_ms = (time.monotonic() - started) * 1000
    result.failures = len(errors)
    if result.status == SKIP:
        return result
    budget = ctx.expect.get("max_latency_ms")
    if errors:
        result.status = FAIL
        prefix = f"{len(errors)}/{result.samples} samples failed: " if result.samples > 1 else ""
        result.message = prefix + errors[0]
    elif budget and result.max_latency_ms > float(budget):
        result.status, result.severity = FAIL, WARN
        result.message = f"slowest request {result.max_latency_ms:.0f} ms exceeds budget {budget} ms"
    else:
        result.message = last_ok
    return result


def load_stages(path: str) -> Dict[str, dict]:
    with open(path, encoding="utf-8") as fh:
        return json.load(fh).get("stages", {})


def parse_headers(values: List[str]) -> Dict[str, str]:
    headers = {}
    for raw in values:
        name, sep, value = raw.partition(":")
        if not sep or not name.strip():
            raise ValueError(f"invalid --header {raw!r}; expected 'Name: value'")
        headers[name.strip()] = value.strip()
    return headers


def write_junit(path: str, stage: str, base_url: str, results: List[Result]) -> None:
    suite = ET.Element(
        "testsuite",
        name=f"eshop-canary-smoke.{stage}",
        tests=str(len(results)),
        failures=str(sum(r.status == FAIL for r in results)),
        skipped=str(sum(r.status == SKIP for r in results)),
        time=f"{sum(r.duration_ms for r in results) / 1000:.3f}",
    )
    ET.SubElement(ET.SubElement(suite, "properties"), "property", name="base_url", value=base_url)
    for r in results:
        case = ET.SubElement(suite, "testcase", classname=f"smoke.{stage}", name=r.name, time=f"{r.duration_ms / 1000:.3f}")
        if r.status == FAIL:
            ET.SubElement(case, "failure", message=r.message, type=r.severity).text = r.description
        elif r.status == SKIP:
            ET.SubElement(case, "skipped", message=r.message)
    ET.ElementTree(suite).write(path, encoding="utf-8", xml_declaration=True)


def summarize(results: List[Result]) -> Tuple[int, int, int, int]:
    critical = sum(r.status == FAIL and r.severity == CRITICAL for r in results)
    warnings = sum(r.status == FAIL and r.severity == WARN for r in results)
    passed = sum(r.status == PASS for r in results)
    skipped = sum(r.status == SKIP for r in results)
    return passed, critical, warnings, skipped


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        description="Run canary smoke tests against a deployed eShopCoreModernized stage.",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    p.add_argument("--stage", default=os.environ.get("SMOKE_STAGE", "local"), help="stage profile from the stages file")
    p.add_argument("--stages-file", default=DEFAULT_STAGES_FILE, help="JSON file with stage profiles")
    p.add_argument("--base-url", default=os.environ.get("SMOKE_BASE_URL"), help="override the stage's base URL")
    p.add_argument("--samples", type=int, help="repetitions per read-only check (override stage profile)")
    p.add_argument("--timeout", type=float, default=10.0, help="per-request timeout in seconds")
    p.add_argument("--header", action="append", default=[], help="extra request header 'Name: value' (repeatable)")
    p.add_argument("--insecure", action="store_true", help="skip TLS certificate verification")
    writes = p.add_mutually_exclusive_group()
    writes.add_argument("--allow-writes", dest="allow_writes", action="store_true", default=None,
                        help="run the create/delete round trip even if the stage disables it")
    writes.add_argument("--no-writes", dest="allow_writes", action="store_false", help="never run write checks")
    p.add_argument("--only", action="append", default=[], help="run only checks whose name starts with this (repeatable)")
    p.add_argument("--skip", action="append", default=[], help="skip checks whose name starts with this (repeatable)")
    p.add_argument("--strict", action="store_true", help="treat warnings as failures (exit 2)")
    p.add_argument("--junit", help="write a JUnit XML report to this path")
    p.add_argument("--json", dest="json_out", help="write a JSON report to this path")
    p.add_argument("--list", action="store_true", help="list checks and stages, then exit")
    return p


def main(argv: Optional[List[str]] = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        stages = load_stages(args.stages_file)
    except (OSError, ValueError) as exc:
        print(f"error: cannot read stages file {args.stages_file}: {exc}", file=sys.stderr)
        return EXIT_USAGE

    if args.list:
        print("Checks:")
        for c in CHECKS:
            tags = [c.severity] + (["writes"] if c.writes else []) + (["sampled"] if c.sampled else [])
            print(f"  {c.name:<24} [{', '.join(tags)}] {c.description}")
        print("Stages:")
        for name, cfg in stages.items():
            print(f"  {name:<14} {cfg.get('base_url', '')}  {cfg.get('description', '')}")
        return EXIT_OK

    if args.stage not in stages:
        print(f"error: unknown stage {args.stage!r}; known: {', '.join(stages)}", file=sys.stderr)
        return EXIT_USAGE
    profile = stages[args.stage]
    env_url = os.environ.get("SMOKE_BASE_URL_" + re.sub(r"\W", "_", args.stage).upper())
    base_url = args.base_url or env_url or profile.get("base_url")
    if not base_url:
        print(f"error: no base URL for stage {args.stage!r}", file=sys.stderr)
        return EXIT_USAGE
    try:
        headers = {**profile.get("headers", {}), **parse_headers(args.header)}
    except ValueError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return EXIT_USAGE
    samples = max(1, args.samples or int(profile.get("samples", 1)))
    allow_writes = bool(profile.get("allow_writes", False)) if args.allow_writes is None else args.allow_writes

    ctx = Context(
        client=Client(base_url, args.timeout, headers, args.insecure),
        stage=args.stage,
        expect=profile.get("expect", {}),
        allow_writes=allow_writes,
        username=os.environ.get("SMOKE_USERNAME", "smoke-test"),
        password=os.environ.get("SMOKE_PASSWORD", "smoke-test"),
    )
    selected = [
        c for c in CHECKS
        if (not args.only or any(c.name.startswith(o) for o in args.only))
        and not any(c.name.startswith(s) for s in args.skip)
    ]

    print(f"eShop canary smoke tests: stage={args.stage} base_url={base_url} samples={samples} writes={allow_writes}")
    results = []
    for chk in selected:
        r = run_check(chk, ctx, samples)
        results.append(r)
        label = {PASS: "PASS", SKIP: "SKIP", FAIL: "FAIL" if r.severity == CRITICAL else "WARN"}[r.status]
        print(f"  [{label}] {r.name:<24} {r.max_latency_ms:7.0f} ms  {r.message}")

    passed, critical, warnings, skipped = summarize(results)
    lat = sorted(ctx.client.latencies_ms)
    p95 = lat[min(len(lat) - 1, int(len(lat) * 0.95))] if lat else 0.0
    print(f"Result: {passed} passed, {critical} critical failures, {warnings} warnings, {skipped} skipped; "
          f"{len(lat)} requests, p95 {p95:.0f} ms")

    if args.junit:
        write_junit(args.junit, args.stage, base_url, results)
    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8") as fh:
            json.dump(
                {
                    "stage": args.stage,
                    "base_url": base_url,
                    "samples": samples,
                    "summary": {"passed": passed, "critical": critical, "warnings": warnings, "skipped": skipped,
                                "requests": len(lat), "p95_ms": round(p95, 1)},
                    "results": [r.__dict__ for r in results],
                },
                fh,
                indent=2,
            )

    if critical:
        print("VERDICT: FAIL - do not promote; roll back this stage (see ROLLBACK-PROCEDURES.md)")
        return EXIT_CRITICAL
    if warnings and args.strict:
        print("VERDICT: FAIL (strict) - warnings present")
        return EXIT_WARN_STRICT
    print("VERDICT: PASS" + (" with warnings" if warnings else ""))
    return EXIT_OK


if __name__ == "__main__":
    sys.exit(main())
