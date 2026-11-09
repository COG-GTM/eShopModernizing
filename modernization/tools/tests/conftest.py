"""Characterization suite for the eShop storefront. Runs against ANY implementation:
  MVC_BASE_URL=http://127.0.0.1:5100 WEBFORMS_BASE_URL=http://127.0.0.1:5101 pytest modernization/tools/tests
Apps must be in mock mode (UseMockData=true) and freshly started (12 seeded items, 5 brands, 3 types).
Unset base URL => that app's tests are skipped."""
import os

import pytest
import requests


def _base(var):
    url = os.environ.get(var)
    if not url:
        pytest.skip(f"{var} not set")
    return url.rstrip("/")


class Client:
    def __init__(self, base):
        self.base = base
        self.s = requests.Session()  # one cookie jar per app (shared localhost cookies break Mono)

    def get(self, path, **kw):
        kw.setdefault("allow_redirects", False)
        return self.s.get(self.base + path, timeout=120, **kw)

    def request(self, method, path, **kw):
        kw.setdefault("allow_redirects", False)
        return self.s.request(method, self.base + path, timeout=120, **kw)

    def post(self, url, **kw):
        kw.setdefault("allow_redirects", False)
        return self.s.post(url if url.startswith("http") else self.base + url, timeout=120, **kw)


@pytest.fixture(scope="module")
def mvc():
    return Client(_base("MVC_BASE_URL"))


@pytest.fixture(scope="module")
def wf():
    return Client(_base("WEBFORMS_BASE_URL"))
