"""Self-tests for smoke.py against an in-process fake of the catalog app.

Run with: python3 -m unittest discover -s smoke-tests
"""

import contextlib
import io
import itertools
import json
import os
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import smoke

CATALOG_HTML = """<html><head><link rel="stylesheet" href="/css/site.css"></head><body>
<div class="esh-table"><table><tbody>
<tr><td><img src="/pics/1.png"></td><td>Mug</td><td><a href="/Catalog/Details/1">Details</a></td></tr>
<tr><td><img src="/pics/2.png"></td><td>Shirt</td><td><a href="/Catalog/Details/2">Details</a></td></tr>
</tbody></table><div class="esh-pager">Showing 2 of 2 products</div></div></body></html>"""


class FakeApp:
    def __init__(self):
        self.flags = {"useMockData": True, "useAzureStorage": False,
                      "useManagedIdentity": False, "useAzureActiveDirectory": False}
        self.db_status = "Healthy"
        self.catalog_fail_every = 0
        self.protect_create = True
        self.missing_assets = set()
        self._counter = itertools.count(1)

    def handle(self, method, path):
        route = path.split("?")[0]
        if route == "/api/health":
            return 200, "application/json", json.dumps({"status": "Healthy"})
        if route == "/api/health/detailed":
            return 200, "application/json", json.dumps({"status": "Healthy", "services": {
                "database": {"status": self.db_status}, "configuration": self.flags}})
        if route == "/Catalog/Index":
            if self.catalog_fail_every and next(self._counter) % self.catalog_fail_every == 0:
                return 502, "text/plain", "Bad Gateway"
            return 200, "text/html", CATALOG_HTML
        if route in ("/Catalog/Details/1", "/Catalog/Details/2"):
            return 200, "text/html", '<h2>Details</h2><img class="esh-picture" src="/pics/1.png">'
        if route.startswith("/Catalog/Details/"):
            return 404, "text/plain", ""
        if route == "/Catalog/Create":
            if self.protect_create:
                return 302, "text/plain", "", {"Location": "/Account/Login?ReturnUrl=%2FCatalog%2FCreate"}
            return 200, "text/html", "<form></form>"
        if route == "/Account/Login":
            return 200, "text/html", '<input name="username"><input name="password">'
        if route in ("/api/Brands", "/api/Files"):
            return 200, "application/json", json.dumps([{"id": 1, "brand": "Azure"}] if route == "/api/Brands"
                                                       else [{"Id": 1, "Brand": "Azure"}])
        if route == "/api/Brands/1":
            return 200, "application/json", json.dumps({"id": 1, "brand": "Azure"})
        if route == "/api/ImageUpload/default":
            return 200, "application/json", json.dumps({"imageUrl": "/pics/default.png"})
        if route in ("/css/site.css", "/pics/1.png", "/pics/2.png") and route not in self.missing_assets:
            return 200, "text/plain", "ok"
        return 404, "text/plain", ""


class SmokeSuiteTest(unittest.TestCase):
    def setUp(self):
        self.app = FakeApp()
        app = self.app

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                status, ctype, body, *extra = app.handle("GET", self.path)
                self.send_response(status)
                self.send_header("Content-Type", ctype)
                for k, v in (extra[0] if extra else {}).items():
                    self.send_header(k, v)
                data = body.encode()
                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)

            def log_message(self, *args):
                pass

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.base_url = f"http://127.0.0.1:{self.server.server_address[1]}"
        self.server_running = True
        self.tmp = tempfile.TemporaryDirectory()
        self.report = os.path.join(self.tmp.name, "report.json")

    def tearDown(self):
        self.stop_server()
        self.tmp.cleanup()

    def stop_server(self):
        if self.server_running:
            self.server.shutdown()
            self.server.server_close()
            self.server_running = False

    def run_suite(self, *extra):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = smoke.main(["--stage", "local", "--base-url", self.base_url, "--no-writes",
                               "--json", self.report, *extra])
        with open(self.report) as fh:
            report = json.load(fh)
        return code, {r["name"]: r for r in report["results"]}, out.getvalue()

    def test_healthy_stage_passes(self):
        code, results, _ = self.run_suite()
        self.assertEqual(code, smoke.EXIT_OK)
        failed = [n for n, r in results.items() if r["status"] == smoke.FAIL]
        self.assertEqual(failed, [])
        self.assertEqual(results["catalog.crud_roundtrip"]["status"], smoke.SKIP)

    def test_unhealthy_dependency_is_critical(self):
        self.app.db_status = "Unhealthy"
        code, results, out = self.run_suite()
        self.assertEqual(code, smoke.EXIT_CRITICAL)
        self.assertIn("database=Unhealthy", results["health.detailed"]["message"])
        self.assertIn("roll back", out)

    def test_feature_flag_drift_is_critical(self):
        self.app.flags["useMockData"] = False
        code, results, _ = self.run_suite()
        self.assertEqual(code, smoke.EXIT_CRITICAL)
        self.assertIn("useMockData", results["health.detailed"]["message"])

    def test_sampling_catches_one_bad_upstream_in_four(self):
        self.app.catalog_fail_every = 4
        code, results, _ = self.run_suite("--samples", "8", "--only", "catalog.index")
        self.assertEqual(code, smoke.EXIT_CRITICAL)
        self.assertEqual(results["catalog.index"]["failures"], 2)
        self.assertIn("2/8 samples failed", results["catalog.index"]["message"])

    def test_open_protected_page_fails_auth_guard(self):
        self.app.protect_create = False
        code, results, _ = self.run_suite("--only", "auth.guard")
        self.assertEqual(code, smoke.EXIT_CRITICAL)
        self.assertEqual(results["auth.guard"]["status"], smoke.FAIL)

    def test_broken_asset_is_warning_unless_strict(self):
        self.app.missing_assets.add("/css/site.css")
        code, results, _ = self.run_suite()
        self.assertEqual(code, smoke.EXIT_OK)
        self.assertEqual(results["assets.static"]["severity"], smoke.WARN)
        strict_code, _, _ = self.run_suite("--strict")
        self.assertEqual(strict_code, smoke.EXIT_WARN_STRICT)

    def test_unreachable_stage_fails(self):
        self.stop_server()
        code, results, _ = self.run_suite("--only", "health.basic", "--timeout", "2")
        self.assertEqual(code, smoke.EXIT_CRITICAL)
        self.assertIn("failed", results["health.basic"]["message"])

    def test_junit_report_written(self):
        junit = os.path.join(self.tmp.name, "junit.xml")
        self.run_suite("--junit", junit)
        with open(junit) as fh:
            xml = fh.read()
        self.assertIn('testsuite name="eshop-canary-smoke.local"', xml)
        self.assertIn('name="catalog.index"', xml)

    def test_unknown_stage_is_usage_error(self):
        with contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(smoke.main(["--stage", "nope"]), smoke.EXIT_USAGE)


if __name__ == "__main__":
    unittest.main()
