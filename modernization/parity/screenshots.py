#!/usr/bin/env python3
"""Captures legacy-vs-migrated screenshots (headless Chromium via Playwright) into screenshots/<id>-{legacy,migrated}.png.

  screenshots.py [--legacy-mvc URL] [--legacy-webforms URL] [--migrated-mvc URL] [--migrated-webforms URL] [--out DIR]

Each side/app gets its own browser context (localhost cookies are shared across ports and Mono 500s on a foreign
session cookie). Pages are read-only GETs plus one invalid WebForms create POST, so app state is not changed.
"""
import argparse
import pathlib

from playwright.sync_api import sync_playwright

PARITY = pathlib.Path(__file__).resolve().parent
SHOTS = [
    ("01-mvc-catalog-list", "mvc", "/", None),
    ("02-mvc-details-1", "mvc", "/Catalog/Details/1", None),
    ("03-mvc-edit-1", "mvc", "/Catalog/Edit/1", None),
    ("04-webforms-catalog-list", "webforms", "/", None),
    ("05-webforms-paged-size5-page1", "webforms", "/Default/index/1/size/5", None),
    ("06-webforms-create-invalid-price", "webforms", "/Catalog/Create", "invalid-price"),
]


def invalid_price(page):
    page.fill("input[id$='Name']", "Parity check")
    page.fill("input[id$='Price']", "-5")
    page.click("input[type=submit], button[type=submit]")
    page.wait_for_load_state("networkidle")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--legacy-mvc", default="http://127.0.0.1:5100")
    ap.add_argument("--legacy-webforms", default="http://127.0.0.1:5101")
    ap.add_argument("--migrated-mvc", default="http://127.0.0.1:5200")
    ap.add_argument("--migrated-webforms", default="http://127.0.0.1:5201")
    ap.add_argument("--chrome", default=None, help="Chrome/Chromium executable (default: Playwright's bundled Chromium)")
    ap.add_argument("--out", default=str(PARITY / "screenshots"))
    a = ap.parse_args()
    out = pathlib.Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    bases = {("legacy", "mvc"): a.legacy_mvc, ("legacy", "webforms"): a.legacy_webforms,
             ("migrated", "mvc"): a.migrated_mvc, ("migrated", "webforms"): a.migrated_webforms}
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=a.chrome, args=["--no-sandbox"])
        for (side, app), base in bases.items():
            ctx = browser.new_context(viewport={"width": 1280, "height": 900})
            page = ctx.new_page()
            for shot_id, shot_app, path, action in SHOTS:
                if shot_app != app:
                    continue
                page.goto(base + path, wait_until="networkidle")
                if action == "invalid-price":
                    invalid_price(page)
                page.screenshot(path=str(out / f"{shot_id}-{side}.png"), full_page=True)
                print(f"{shot_id}-{side}.png  {page.url}")
            ctx.close()
        browser.close()


if __name__ == "__main__":
    main()
