#!/usr/bin/env bash
# Full side-by-side parity: legacy (Mono + xsp4, copied run dirs) vs migrated (.NET 8 Linux containers), mock and SQL mode.
#   run-side-by-side.sh [--setup] [--no-sql] [--no-screenshots]
# --setup builds/starts everything first: legacy mock :5100/:5101, legacy SQL :5110/:5111 (SQL Server 2022 on :14330),
# migrated mock :5200/:5201 and migrated SQL :5210/:5211 (net8/docker/stack.sh). Without it the eight endpoints must be up.
# Writes results/side-by-side/{side-by-side-mock.json,side-by-side-sql.json,dotnet-test.trx,characterization-*.xml,env.json,
# summary.json}, screenshots/*.png and PARITY-REPORT.html. Exit 0 only on PARITY (see side_by_side_report.py).
set -euo pipefail
PARITY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$PARITY_DIR/../.." && pwd)"
RES="$PARITY_DIR/results/side-by-side"
HARNESS="$REPO/modernization/legacy-harness"
STACK="$REPO/net8/docker/stack.sh"
PYTHON="${PYTHON:-python3}"
DOTNET="${DOTNET:-$([ -x "$HOME/.dotnet/dotnet" ] && echo "$HOME/.dotnet/dotnet" || echo dotnet)}"
LEGACY_ROOT="${LEGACY_ROOT:-$HOME/eshop-legacy-run}"; LEGACY_SQL_ROOT="${LEGACY_SQL_ROOT:-$HOME/eshop-legacy-run-sql}"
export LEGACY_ROOT LEGACY_SQL_ROOT
SETUP=0; SQL=1; SHOTS=1
for a in "$@"; do case "$a" in --setup) SETUP=1;; --no-sql) SQL=0;; --no-screenshots) SHOTS=0;; *) echo "unknown option $a" >&2; exit 2;; esac; done
cd "$REPO"; mkdir -p "$RES"
legacy() { env LEGACY_ROOT="$1" LEGACY_MODE="$2" MVC_PORT="$3" WEBFORMS_PORT="$4" "${@:5}"; }

if [ "$SETUP" = 1 ]; then
  legacy "$LEGACY_ROOT" mock 5100 5101 "$HARNESS/setup.sh"
  legacy "$LEGACY_ROOT" mock 5100 5101 "$HARNESS/start.sh"
  "$STACK" mock up
  if [ "$SQL" = 1 ]; then
    "$HARNESS/sql.sh" up
    legacy "$LEGACY_SQL_ROOT" sql 5110 5111 "$HARNESS/setup.sh"
    legacy "$LEGACY_SQL_ROOT" sql 5110 5111 "$HARNESS/start.sh"
    "$STACK" sql up
  fi
fi

echo "== dotnet test"
rm -f "$RES/dotnet-test.trx"
ESHOP_TESTS_REQUIRE_ALL=1 "$DOTNET" test net8/EShopStorefront.sln -nologo --logger "trx;LogFileName=dotnet-test.trx" --results-directory "$RES" || true

echo "== characterization suite against both sides (fresh mock state each)"
rm -f "$RES/characterization-legacy.xml" "$RES/characterization-migrated.xml"
legacy "$LEGACY_ROOT" mock 5100 5101 "$HARNESS/reset.sh" all >/dev/null
MVC_BASE_URL=http://127.0.0.1:5100 WEBFORMS_BASE_URL=http://127.0.0.1:5101 "$PYTHON" -m pytest -q -p no:cacheprovider \
  modernization/tools/tests --junitxml "$RES/characterization-legacy.xml" | tail -n 1 || true
"$STACK" mock reset mvc >/dev/null; "$STACK" mock reset webforms >/dev/null
MVC_BASE_URL=http://127.0.0.1:5200 WEBFORMS_BASE_URL=http://127.0.0.1:5201 "$PYTHON" -m pytest -q -p no:cacheprovider \
  modernization/tools/tests --junitxml "$RES/characterization-migrated.xml" | tail -n 1 || true

echo "== side-by-side transcript replay"
"$PYTHON" "$PARITY_DIR/side_by_side.py" --mode mock --out "$RES/side-by-side-mock.json"
if [ "$SQL" = 1 ]; then "$PYTHON" "$PARITY_DIR/side_by_side.py" --mode sql --out "$RES/side-by-side-sql.json"; else rm -f "$RES/side-by-side-sql.json"; fi
"$PYTHON" "$PARITY_DIR/classify.py" "$RES"/side-by-side-*.json || true

if [ "$SHOTS" = 1 ]; then
  echo "== screenshots"
  legacy "$LEGACY_ROOT" mock 5100 5101 "$HARNESS/reset.sh" all >/dev/null
  "$STACK" mock reset mvc >/dev/null; "$STACK" mock reset webforms >/dev/null
  CHROME="${CHROME:-$(command -v google-chrome || true)}"   # empty = Playwright's bundled Chromium
  "$PYTHON" "$PARITY_DIR/screenshots.py" --out "$PARITY_DIR/screenshots" ${CHROME:+--chrome "$CHROME"}
fi

DOTNET="$DOTNET" "$PYTHON" - "$RES/env.json" <<'PY'
import json, os, subprocess, sys
def sh(c):
    try: return subprocess.run(c, shell=True, capture_output=True, text=True, timeout=60).stdout.strip()
    except Exception as e: return f"unavailable: {e}"
dn = os.environ["DOTNET"]
env = {
  "git_head": sh("git rev-parse HEAD"), "git_branch": sh("git rev-parse --abbrev-ref HEAD"),
  "os": sh(". /etc/os-release && echo $PRETTY_NAME") + " / " + sh("uname -r"),
  "legacy_runtime": sh("mono --version | head -1") + "; " + sh("msbuild -version 2>/dev/null | tail -1 | sed 's/^/msbuild /'") + "; xsp4 " + sh("xsp4 --version 2>&1 | head -1"),
  "legacy_endpoints": "mock MVC :5100 WebForms :5101; sql MVC :5110 WebForms :5111 (SQL Server 2022 container eshop-legacy-sql :14330)",
  "migrated_runtime": ".NET SDK " + sh(f"cd net8 && {dn} --version") + "; containers: " + sh("docker ps --format '{{.Names}} ({{.Image}})' | grep eshop-storefront | sort | tr '\\n' ' '"),
  "migrated_endpoints": "mock MVC :5200 WebForms :5201; sql MVC :5210 WebForms :5211 (SQL Server 2022 :14331)",
  "docker": sh("docker version --format '{{.Server.Version}}'"),
  "chrome_for_screenshots": "Playwright Chromium " + sh("python3 -c 'import playwright,importlib.metadata as m;print(m.version(\"playwright\"))'"),
  "how_to_rerun": "git clone https://github.com/COG-GTM/eShopModernizing && cd eShopModernizing && git checkout devin/net8-storefront-modernization\n"
                  "pip install -r modernization/tools/requirements.txt playwright && python3 -m playwright install --with-deps chromium\n"
                  "modernization/parity/run-side-by-side.sh --setup",
}
json.dump(env, open(sys.argv[1], "w"), indent=2)
PY
"$PYTHON" "$PARITY_DIR/side_by_side_report.py" --results "$RES"
