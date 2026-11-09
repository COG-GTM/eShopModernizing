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
