#!/usr/bin/env python3
"""Full-page Playwright screenshots of the main catalog pages of both apps.

  python3 screenshots.py --prefix legacy --mvc-url http://127.0.0.1:5100 --webforms-url http://127.0.0.1:5101
  python3 screenshots.py --prefix net8   --mvc-url http://127.0.0.1:5200 --webforms-url http://127.0.0.1:5201

Writes <out>/<prefix>-<app>-<page>.png (default out: modernization/baseline/screenshots). Pages are visited in
list-first order so that mock-mode navigation properties are populated (INVENTORY quirk Q4).
"""
import argparse
import os
import shutil
import pathlib
import sys

from playwright.sync_api import sync_playwright

PAGES = {
    "mvc": [("index", "/"), ("index-page2", "/?pageSize=10&pageIndex=1"), ("details", "/Catalog/Details/1"),
            ("create", "/Catalog/Create"), ("edit", "/Catalog/Edit/1"), ("delete", "/Catalog/Delete/1")],
    "webforms": [("index", "/"), ("index-page2", "/Default/index/1/size/10"), ("details", "/Catalog/Details/1"),
                 ("create", "/Catalog/Create"), ("edit", "/Catalog/Edit/1"), ("delete", "/Catalog/Delete/1"),
                 ("about", "/About.aspx"), ("contact", "/Contact.aspx")],
}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--prefix", required=True)
    ap.add_argument("--mvc-url")
    ap.add_argument("--webforms-url")
    ap.add_argument("--out", default=str(pathlib.Path(__file__).resolve().parent.parent / "baseline" / "screenshots"))
    ap.add_argument("--width", type=int, default=1280)
    a = ap.parse_args()
    out = pathlib.Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    targets = {k: v for k, v in (("mvc", a.mvc_url), ("webforms", a.webforms_url)) if v}
    if not targets:
        ap.error("give --mvc-url and/or --webforms-url")
    failures = 0
    with sync_playwright() as p:
        try:
            browser = p.chromium.launch()
        except Exception:  # bundled browser not downloaded: fall back to an installed Chrome
            exe = os.environ.get("CHROME_PATH") or shutil.which("google-chrome") or shutil.which("chromium")
            browser = p.chromium.launch(executable_path=exe) if exe else p.chromium.launch(channel="chrome")
        for app, base in targets.items():
            page = browser.new_page(viewport={"width": a.width, "height": 900})
            for name, path in PAGES[app]:
                resp = page.goto(base.rstrip("/") + path, wait_until="networkidle", timeout=180000)
                status = resp.status if resp else None
                file = out / ("%s-%s-%s.png" % (a.prefix, app, name))
                page.screenshot(path=str(file), full_page=True)
                failures += status != 200
                print("%s %s -> %s %s" % (app, path, status, file.name))
            page.close()
        browser.close()
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
