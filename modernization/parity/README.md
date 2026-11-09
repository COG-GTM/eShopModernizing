# Parity (owned by T6)

Replays the recorded legacy baseline against the .NET 8 apps and records the result here.

```bash
pip install -r modernization/tools/requirements.txt
modernization/parity/run-parity.sh            # build + replay + characterization + report
modernization/parity/run-parity.sh --no-build --strict --keep-running
```

What `run-parity.sh` does:

1. `dotnet build net8/EShopStorefront.sln -c Release` (`DOTNET`, `CONFIGURATION` override the defaults).
2. Replays all `baseline/transcripts/mvc` (86) against `127.0.0.1:5200` and all `baseline/transcripts/webforms` (52) against
   `127.0.0.1:5201` with `tools/transcript.py replay`; `--reset-cmd` restarts the app first, so every set runs against a freshly
   seeded in-memory mock catalog (CONTRACT.md §7).
3. Restarts both apps and runs the 77-test characterization suite (`tools/tests`) against them.
4. `report.py` writes `results/{mvc,webforms}.json` (replay reports), `results/characterization.{xml,txt,json}`,
   `results/summary.json` and `PARITY-REPORT.md` (pass/total vs the legacy baseline). Exit code 0 only at full parity.

`app.sh start|stop|restart|status [all|mvc|webforms]` runs each built dll from its project directory with an absolute
`--contentRoot` (a relative one makes static assets 404) in mock mode, logs to `.run/`, and stops by PID file / port only.

Accepted differences go in `accepted-differences.json`; every entry must cite its CONTRACT.md section or `report.py` refuses it:

```json
{"transcripts": [{"app": "mvc", "case": "071-api-files.json", "contract": "CONTRACT.md §5 ..."}],
 "tests": [{"id": "test_mvc::test_api_files_returns_binary_brand_list", "contract": "CONTRACT.md §5 ..."}]}
```

`PARITY_RESULTS_DIR` / `PARITY_REPORT` point `report.py` at another results set (e.g. a control run against legacy :5100/:5101).

## Side-by-side parity (legacy Mono + xsp4 vs .NET 8 containers)

`run-side-by-side.sh [--setup] [--no-sql] [--no-screenshots]` runs legacy and migrated at the same time on identical fresh seeds
(mock and SQL Server 2022) and writes `results/side-by-side/` plus `PARITY-REPORT.html`:

| Script | What it does |
|---|---|
| `side_by_side.py --mode mock\|sql` | resets both sides, replays all 138 baseline flows against each, diffs contract / baseline / strict (full body + headers), data state (and SQL table/schema/sequence dumps) |
| `classify.py` | attributes every strict difference to a rule in `classifications.json` (fixed / accepted framework difference / known limitation / pre-existing legacy defect); anything else is unexplained |
| `screenshots.py` | headless Chromium legacy-vs-migrated pairs into `screenshots/` |
| `side_by_side_report.py` | `results/side-by-side/summary.json` + self-contained `PARITY-REPORT.html` (screenshots inlined as base64); exit 0 only on parity |

Fresh Linux VM (Mono 6.12 + msbuild + xsp4, .NET SDK 8.0.4xx, Docker, Python 3):

```bash
git clone https://github.com/COG-GTM/eShopModernizing && cd eShopModernizing && git checkout devin/net8-storefront-modernization
pip install -r modernization/tools/requirements.txt playwright && python3 -m playwright install --with-deps chromium
modernization/parity/run-side-by-side.sh --setup
```
