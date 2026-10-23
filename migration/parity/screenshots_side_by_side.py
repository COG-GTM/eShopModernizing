#!/usr/bin/env python3
"""Captures legacy-vs-migrated screenshots of the catalog flows into results/screenshots/<flow>-<side>.png.

    python3 migration/parity/screenshots.py --legacy-mvc http://localhost:5100 --legacy-webforms http://localhost:5101 \
        --migrated-mvc http://localhost:5200 --migrated-webforms http://localhost:5201 [--chrome /path/to/chrome]

Read-only flows only (create-validation submits an empty form, which never reaches the catalog), so the
mock catalogs stay on their seed state. Run against freshly restarted apps.
"""
from __future__ import annotations

import argparse
from pathlib import Path

from playwright.sync_api import Page, sync_playwright

OUT = Path(__file__).resolve().parent / "results" / "screenshots"


def create_validation(page: Page, base: str) -> None:
    page.goto(f"{base}/Catalog/Create")
    page.fill("input[name=Name]", "")
    page.fill("input[name=Price]", "abc")
    page.click("input[type=submit], button[type=submit]")
    page.wait_for_selector(".field-validation-error")


FLOWS = [
    ("index", "mvc", lambda p, b: p.goto(f"{b}/")),
    ("details", "mvc", lambda p, b: p.goto(f"{b}/Catalog/Details/1")),
    ("create", "mvc", lambda p, b: p.goto(f"{b}/Catalog/Create")),
    ("create-validation", "mvc", create_validation),
    ("edit", "mvc", lambda p, b: p.goto(f"{b}/Catalog/Edit/1")),
    ("delete-confirm", "mvc", lambda p, b: p.goto(f"{b}/Catalog/Delete/1")),
    ("webforms-list", "webforms", lambda p, b: p.goto(f"{b}/")),
]


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--legacy-mvc", required=True)
    ap.add_argument("--legacy-webforms", required=True)
    ap.add_argument("--migrated-mvc", required=True)
    ap.add_argument("--migrated-webforms", required=True)
    ap.add_argument("--chrome", default=None, help="Chrome/Chromium executable (default: Playwright's bundled one)")
    a = ap.parse_args()
    bases = {("legacy", "mvc"): a.legacy_mvc, ("legacy", "webforms"): a.legacy_webforms,
             ("migrated", "mvc"): a.migrated_mvc, ("migrated", "webforms"): a.migrated_webforms}
    OUT.mkdir(parents=True, exist_ok=True)
    with sync_playwright() as pw:
        browser = pw.chromium.launch(executable_path=a.chrome, headless=True)
        for side in ("legacy", "migrated"):
            ctx = browser.new_context(viewport={"width": 1280, "height": 900})
            page = ctx.new_page()
            for flow, app, go in FLOWS:
                go(page, bases[(side, app)].rstrip("/"))
                page.wait_for_load_state("networkidle")
                path = OUT / f"{flow}-{side}.png"
                page.screenshot(path=str(path), full_page=True)
                print(path.relative_to(OUT.parent.parent.parent.parent))
            ctx.close()
        browser.close()


if __name__ == "__main__":
    main()
