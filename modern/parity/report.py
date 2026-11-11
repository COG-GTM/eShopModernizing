#!/usr/bin/env python3
"""Build parity-report.html (one self-contained file, screenshots inlined as base64) and PARITY-RESULTS.md.

  python3 modern/parity/report.py --results ~/parity-work/results --screenshots ~/parity-work/screenshots \
      --html ~/parity-work/parity-report.html --md modern/parity/PARITY-RESULTS.md [--recording-note TEXT]
"""
import argparse
import base64
import datetime
import html
import json
import pathlib
import io
import subprocess
import sys

from PIL import Image

from intentional import RULES, classify

APPS = ("mvc", "webforms")
MODES = ("mock", "sql")
VERDICTS = ("PASS", "INTENTIONAL", "FAIL")
SQL_IMAGE = "mcr.microsoft.com/mssql/server:2022-latest"

COMMANDS = [
    "git -C modern rev-parse HEAD",
    "modern/parity/run-legacy.sh            # legacy scratch builds (mock + sql) under ~/mecca-legacy-run*",
    "docker run -d --name mba3849-mssql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=*** -p 127.0.0.1:1433:1433 " + SQL_IMAGE,
    "python3 -m unittest discover -s modern/parity/tests -v",
    "dotnet test modern/eShop.Catalog.Modern.sln",
    "PARITY_SQL_PASSWORD=*** modern/parity/run-parity.sh all   # publish modern, fresh DBs, replay mock + sql",
    "PARITY_NO_REPLAY=1 modern/parity/run-parity.sh mock --keep-running && "
    "python3 modern/parity/screenshots.py --out ~/parity-work/screenshots && modern/parity/run-parity.sh --stop",
    "python3 modern/parity/report.py --results ~/parity-work/results --screenshots ~/parity-work/screenshots "
    "--html ~/parity-work/parity-report.html --md modern/parity/PARITY-RESULTS.md",
]

NOT_TESTED = [
    ("Legacy on Windows/IIS", "Legacy runs on Mono 6.12 + xsp4 (the only Linux option); Mono-specific behaviour "
     "(host error pages, MIME table) is normalised or listed as intentional."),
    ("modern/deploy compose stack", "Replay targets the published DLLs on the VM; the containers are exercised by "
     "the S9 compose smoke tests, not by this run."),
    ("Picture upload", "Neither legacy app supports uploads ('Uploading images not allowed for this version')."),
    ("Concurrent users / load", "Out of scope for behavioural parity."),
    ("S9 SqlServerReadinessTests", "Skipped in `dotnet test` without a SQL connection string; SQL mode itself is "
     "covered by the replay against SQL Server 2022."),
]


def sh(cmd):
    try:
        return subprocess.run(cmd, shell=True, capture_output=True, text=True, timeout=30).stdout.strip()
    except Exception as e:  # environment probe only
        return "unavailable (%s)" % e


def environment():
    return [
        ("Mono", sh("mono --version | head -1")),
        ("xsp4", sh("xsp4 --version 2>&1 | head -1")),
        (".NET SDK", sh("dotnet --version") + " (installed: " + sh("dotnet --list-sdks | cut -d' ' -f1 | paste -sd,") + ")"),
        ("SQL Server image", "%s (%s)" % (SQL_IMAGE, sh("docker image inspect --format '{{index .RepoDigests 0}}' "
                                                        + SQL_IMAGE))),
        ("Chromium (screenshots)", sh("google-chrome --version")),
        ("Python", sh("python3 --version")),
        ("OS", sh("uname -sr")),
    ]


def load(results, head):
    runs = {}
    for mode in MODES:
        mode_head = results / mode / "HEAD"
        if not mode_head.exists() or mode_head.read_text().strip() != head:
            print("skipping %s results: not replayed on %s" % (mode, head), file=sys.stderr)
            continue
        for app in APPS:
            f = results / mode / app / "results.json"
            if f.exists():
                r = json.loads(f.read_text())
                for s in r["steps"]:  # re-apply the current rules so a classification change needs no replay
                    s["verdict"], s["reason"] = classify(app, mode, s["id"], s["diffs"], s["legacy"], s["modern"])
                r["summary"] = {v: sum(s["verdict"] == v for s in r["steps"]) for v in VERDICTS}
                runs[(mode, app)] = r
    return runs


def img(path, width=900):
    """Downscaled JPEG data URI: keeps the report a few MB instead of ~17 MB of full-page PNGs."""
    im = Image.open(path).convert("RGB")
    if im.width > width:
        im = im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=80, optimize=True)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()


CSS = """
body{font-family:Segoe UI,Helvetica,Arial,sans-serif;margin:0;color:#1d2433;background:#f5f6f8}
header{background:#00a69c;color:#fff;padding:24px 32px}header h1{margin:0 0 6px;font-weight:600}
main{padding:24px 32px;max-width:1400px}section{background:#fff;border-radius:8px;padding:20px 24px;margin:0 0 20px;
box-shadow:0 1px 3px rgba(0,0,0,.08)}h2{margin-top:0;font-weight:600}
table{border-collapse:collapse;width:100%;font-size:13px}th,td{border-bottom:1px solid #e3e6ea;padding:6px 8px;
text-align:left;vertical-align:top}th{background:#f0f2f5}
.PASS{color:#137333;font-weight:600}.INTENTIONAL{color:#a05a00;font-weight:600}.FAIL{color:#c5221f;font-weight:700}
.cards{display:flex;gap:16px;flex-wrap:wrap}.card{border:1px solid #e3e6ea;border-radius:8px;padding:12px 16px;
min-width:220px}.card h3{margin:0 0 8px;font-size:15px}.num{font-size:22px;margin-right:14px}
pre,code{font-family:Consolas,Menlo,monospace;font-size:12px}pre{background:#f0f2f5;padding:10px;overflow:auto;
white-space:pre-wrap;max-height:420px}
.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin:10px 0 26px}.pair img{width:100%;
border:1px solid #d0d4da}.pair div>b{display:block;margin-bottom:4px}
details summary{cursor:pointer;color:#1a5fb4}.muted{color:#5f6b7a}
"""


def build_html(head, stamp, runs, shots, env, note):
    e = html.escape
    out = ["<!DOCTYPE html><html lang='en'><head><meta charset='utf-8'><title>MBA-3849 parity report</title>"
           "<style>%s</style></head><body>" % CSS,
           "<header><h1>Legacy vs .NET 8 side-by-side parity &mdash; MBA-3849</h1>"
           "<div>Commit <code>%s</code> &middot; generated %s</div></header><main>" % (e(head), e(stamp))]
    # scorecard
    out.append("<section><h2>Scorecard</h2><div class='cards'>")
    for (mode, app), r in runs.items():
        s = r["summary"]
        out.append("<div class='card'><h3>%s &middot; %s mode</h3>%s<div class='muted'>%d requests</div></div>" % (
            "MVC + Web API" if app == "mvc" else "WebForms", mode,
            "".join("<span class='num %s'>%d</span>" % (v, s.get(v, 0)) for v in VERDICTS), sum(s.values())))
    out.append("</div><p class='muted'>PASS = equal after normalisation; INTENTIONAL = matches a documented "
               "hardening (listed below); FAIL = anything else.</p></section>")
    # environment + commands
    out.append("<section><h2>Environment</h2><table>%s</table><h2 style='margin-top:18px'>Commands</h2><pre>%s</pre>"
               "</section>" % ("".join("<tr><th>%s</th><td>%s</td></tr>" % (e(k), e(v)) for k, v in env),
                               e("\n".join(COMMANDS))))
    # intentional
    out.append("<section><h2>Intentional differences</h2><table><tr><th>App</th><th>Steps</th><th>Reason</th></tr>")
    for rule in RULES:
        out.append("<tr><td>%s</td><td><code>%s</code></td><td>%s</td></tr>" % (
            e(", ".join(rule["apps"])), e(", ".join(rule["steps"])), e(rule["reason"])))
    out.append("</table></section>")
    # failures first
    fails = [(m, a, s) for (m, a), r in runs.items() for s in r["steps"] if s["verdict"] == "FAIL"]
    out.append("<section><h2>Open FAILs (%d)</h2>" % len(fails))
    if fails:
        out.append("<table><tr><th>Mode</th><th>App</th><th>#</th><th>Request</th><th>Legacy</th><th>Modern</th>"
                   "<th>Differs in</th><th>Note</th></tr>")
        for m, a, s in fails:
            out.append("<tr><td>%s</td><td>%s</td><td>%d</td><td>%s <code>%s</code></td><td>%s</td><td>%s</td>"
                       "<td>%s</td><td>%s</td></tr>" % (m, a, s["n"], s["method"], e(s["path"]), s["legacy"]["status"],
                                                         s["modern"]["status"], e(", ".join(s["diffs"])), e(s["note"])))
        out.append("</table>")
    else:
        out.append("<p class='PASS'>None.</p>")
    out.append("</section>")
    # request-level table
    out.append("<section><h2>Request-level results</h2>")
    for (mode, app), r in runs.items():
        out.append("<h3>%s &middot; %s mode <span class='muted'>(legacy %s, modern %s)</span></h3><table><tr><th>#</th>"
                   "<th>Step</th><th>Request</th><th>Legacy</th><th>Modern</th><th>Verdict</th><th>Detail</th></tr>"
                   % (app, mode, e(r["legacy"]), e(r["modern"])))
        for s in r["steps"]:
            detail = ""
            if s["verdict"] != "PASS":
                detail = "differs in: %s" % e(", ".join(s["diffs"]))
                if s.get("reason"):
                    detail += "<br>%s" % e(s["reason"])
                if s.get("body-diff"):
                    detail += "<details><summary>normalised body diff</summary><pre>%s</pre></details>" % e(
                        s["body-diff"][:6000])
            out.append("<tr><td>%d</td><td><code>%s</code><div class='muted'>%s</div></td><td>%s <code>%s</code></td>"
                       "<td>%s<div class='muted'>%s</div></td><td>%s<div class='muted'>%s</div></td>"
                       "<td class='%s'>%s</td><td>%s</td></tr>" % (
                           s["n"], e(s["id"]), e(s["note"]), s["method"], e(s["path"]),
                           s["legacy"]["status"] or e(str(s["legacy"].get("error"))), e(s["legacy"]["content-type"] or ""),
                           s["modern"]["status"] or e(str(s["modern"].get("error"))), e(s["modern"]["content-type"] or ""),
                           s["verdict"], s["verdict"], detail))
        out.append("</table>")
    out.append("</section>")
    # screenshots
    out.append("<section><h2>Side-by-side screenshots</h2><p class='muted'>Chromium 1280px viewport, full page, "
               "both hosts on fresh mock data.</p>")
    for s in shots:
        out.append("<h3>%s &middot; %s <span class='muted'><code>%s</code></span></h3><div class='pair'>" % (
            "MVC" if s["app"] == "mvc" else "WebForms", e(s["caption"]), e(s["path"])))
        for side in ("legacy", "modern"):
            out.append("<div><b>%s</b><img alt='%s %s %s' src='%s'></div>" % (
                "Legacy (.NET Framework 4.7.2 on Mono)" if side == "legacy" else "Modern (ASP.NET Core 8)",
                s["app"], s["screen"], side, img(s["file_" + side])))
        out.append("</div>")
    out.append("</section>")
    out.append("<section><h2>Browser recording</h2><p>%s</p></section>" % e(note))
    out.append("<section><h2>Not tested</h2><table>%s</table></section>" % "".join(
        "<tr><th>%s</th><td>%s</td></tr>" % (e(k), e(v)) for k, v in NOT_TESTED))
    out.append("</main></body></html>")
    return "\n".join(out)


def build_md(head, stamp, runs, env, note):
    lines = ["# MBA-3849 side-by-side parity results", "",
             "Commit `%s`, generated %s. Full report (request-level table, body diffs, screenshots): "
             "`parity-report.html` (attached to the PR / Jira, not committed)." % (head, stamp), "",
             "| App | Mode | PASS | INTENTIONAL | FAIL |", "|---|---|---|---|---|"]
    for (mode, app), r in runs.items():
        s = r["summary"]
        lines.append("| %s | %s | %d | %d | %d |" % (app, mode, s.get("PASS", 0), s.get("INTENTIONAL", 0), s.get("FAIL", 0)))
    lines += ["", "## Environment", ""] + ["- %s: %s" % kv for kv in env]
    lines += ["", "## Commands", "", "```"] + COMMANDS + ["```"]
    lines += ["", "## Non-PASS steps", "", "| Mode | App | Step | Request | Legacy | Modern | Verdict | Reason |",
              "|---|---|---|---|---|---|---|---|"]
    for (mode, app), r in runs.items():
        for s in r["steps"]:
            if s["verdict"] != "PASS":
                lines.append("| %s | %s | `%s` | %s `%s` | %s | %s | **%s** | %s |" % (
                    mode, app, s["id"], s["method"], s["path"], s["legacy"]["status"], s["modern"]["status"],
                    s["verdict"], (s.get("reason") or "differs in " + ", ".join(s["diffs"])).replace("|", "\\|")))
    lines += ["", "## Browser recording", "", note, "", "## Not tested", ""] + ["- **%s**: %s" % kv for kv in NOT_TESTED]
    return "\n".join(lines) + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--results", required=True)
    ap.add_argument("--screenshots", required=True)
    ap.add_argument("--html", required=True)
    ap.add_argument("--md", required=True)
    ap.add_argument("--recording-note", default="Testing-agent recording of create -> edit -> delete in legacy and "
                                                "modern is attached to the PR and Jira story.")
    a = ap.parse_args()
    results, shotdir = pathlib.Path(a.results).expanduser(), pathlib.Path(a.screenshots).expanduser()
    head = (results / "HEAD").read_text().strip()
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    runs, env = load(results, head), environment()
    shots = json.loads((shotdir / "screens.json").read_text())
    for s in shots:
        for side in ("legacy", "modern"):
            s["file_" + side] = shotdir / ("%s-%s-%s.png" % (s["app"], s["screen"], side))
    pathlib.Path(a.html).expanduser().write_text(build_html(head, stamp, runs, shots, env, a.recording_note))
    pathlib.Path(a.md).expanduser().write_text(build_md(head, stamp, runs, env, a.recording_note))
    print("wrote %s and %s" % (a.html, a.md))


if __name__ == "__main__":
    main()
