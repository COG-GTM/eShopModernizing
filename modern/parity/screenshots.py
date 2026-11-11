#!/usr/bin/env python3
"""Paired legacy/modern screenshots of the key screens (Playwright + Chromium, 1280x900 viewport, full page).

Run against hosts on fresh data: PARITY_NO_REPLAY=1 modern/parity/run-parity.sh mock --keep-running
  python3 modern/parity/screenshots.py --out ~/parity-work/screenshots
Writes <app>-<screen>-<side>.png plus screens.json (screen order and captions, for report.py).
"""
import argparse
import json
import os
import pathlib
import shutil

from playwright.sync_api import sync_playwright

MVC_NAME, MVC_PRICE = "#Name", "#Price"
WF = "#ctl00_MainContent_"


def mvc_create_validation(page):
    page.fill(MVC_NAME, "")
    page.fill(MVC_PRICE, "abc")
    page.click("input[type=submit]")
    page.wait_for_timeout(500)


def wf_create_validation(page):
    page.fill(WF + "Name", "")
    page.fill(WF + "Price", "abc")
    page.click("input[type=submit][value*=Create]")
    page.wait_for_timeout(500)


SCREENS = {
    "mvc": [
        ("list", "/", "Catalog list (page 1)", None),
        ("details", "/Catalog/Details/1", "Details of item 1", None),
        ("create-validation", "/Catalog/Create", "Create with validation errors (empty Name, Price 'abc')",
         mvc_create_validation),
        ("edit", "/Catalog/Edit/1", "Edit item 1", None),
        ("delete", "/Catalog/Delete/1", "Delete confirmation for item 1", None),
    ],
    "webforms": [
        ("list", "/", "Catalog list (page 1)", None),
        ("details", "/Catalog/Details/1", "Details of item 1", None),
        ("create", "/Catalog/Create", "Create form", None),
        ("create-validation", "/Catalog/Create", "Create with validation errors (empty Name, Price 'abc')",
         wf_create_validation),
        ("edit", "/Catalog/Edit/1", "Edit item 1", None),
        ("delete", "/Catalog/Delete/1", "Delete confirmation for item 1", None),
        ("about", "/About.aspx", "About page", None),
    ],
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--mvc-legacy", default="http://127.0.0.1:5100")
    ap.add_argument("--mvc-modern", default="http://127.0.0.1:5200")
    ap.add_argument("--webforms-legacy", default="http://127.0.0.1:5101")
    ap.add_argument("--webforms-modern", default="http://127.0.0.1:5201")
    a = ap.parse_args()
    out = pathlib.Path(a.out).expanduser()
    out.mkdir(parents=True, exist_ok=True)
    index = []
    with sync_playwright() as p:
        # PARITY_CHROMIUM, else a system Chrome, else Playwright's own download
        exe = os.environ.get("PARITY_CHROMIUM") or shutil.which("google-chrome") or None
        browser = p.chromium.launch(executable_path=exe)
        for app, screens in SCREENS.items():
            for side in ("legacy", "modern"):
                base = getattr(a, "%s_%s" % (app, side))
                context = browser.new_context(viewport={"width": 1280, "height": 900})
                page = context.new_page()
                for name, path, caption, action in screens:
                    resp = page.goto(base + path, wait_until="networkidle")
                    if action:
                        action(page)
                    file = "%s-%s-%s.png" % (app, name, side)
                    page.screenshot(path=str(out / file), full_page=True)
                    print("%-9s %-18s %-6s %s %s" % (app, name, side, resp.status if resp else "-", file))
                    if side == "legacy":
                        index.append({"app": app, "screen": name, "path": path, "caption": caption})
                context.close()
        browser.close()
    (out / "screens.json").write_text(json.dumps(index, indent=2))


if __name__ == "__main__":
    main()
