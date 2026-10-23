#!/usr/bin/env python3
"""Full-page Playwright (chromium) screenshots of the main catalog pages of an MVC and/or WebForms app.

    python migration/parity/screenshots.py --mvc-url http://localhost:5100 --webforms-url http://localhost:5101 \
        --out migration/baseline/screenshots --prefix legacy
Read-only: only GET pages plus a client-side validation attempt that never submits.
"""
from __future__ import annotations

import argparse
from pathlib import Path

from playwright.sync_api import sync_playwright

PAGES = {
    "mvc": [("index", "/"), ("index-page2", "/?pageSize=10&pageIndex=1"), ("details-1", "/Catalog/Details/1"),
            ("create", "/Catalog/Create"), ("edit-1", "/Catalog/Edit/1"), ("delete-1", "/Catalog/Delete/1")],
    "webforms": [("list", "/"), ("list-page2", "/Default/index/1/size/10"), ("details-1", "/Catalog/Details/1"),
                 ("create", "/Catalog/Create"), ("edit-1", "/Catalog/Edit/1"), ("delete-1", "/Catalog/Delete/1")],
}
# Client-side validation: clear the name, then click submit; jquery.validate / WebUIValidation block the post.
CLIENT_VALIDATION = {
    "mvc": ("/Catalog/Create", "#Name", "input[type=submit]"),
    "webforms": ("/Catalog/Create", "#ctl00_MainContent_Name", "input[type=submit]"),
}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--mvc-url")
    ap.add_argument("--webforms-url")
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--prefix", default="legacy")
    args = ap.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    with sync_playwright() as p:
        browser = p.chromium.launch()
        page = browser.new_page(viewport={"width": 1366, "height": 900})
        for app, base in (("mvc", args.mvc_url), ("webforms", args.webforms_url)):
            if not base:
                continue
            for name, path in PAGES[app]:
                page.goto(base.rstrip("/") + path, wait_until="networkidle", timeout=180_000)
                target = args.out / f"{args.prefix}-{app}-{name}.png"
                page.screenshot(path=str(target), full_page=True)
                print(target)
            path, name_sel, submit_sel = CLIENT_VALIDATION[app]
            page.goto(base.rstrip("/") + path, wait_until="networkidle", timeout=180_000)
            page.fill(name_sel, "")
            page.click(submit_sel, no_wait_after=True)
            page.wait_for_timeout(1500)
            target = args.out / f"{args.prefix}-{app}-create-client-validation.png"
            page.screenshot(path=str(target), full_page=True)
            print(target)
        browser.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
