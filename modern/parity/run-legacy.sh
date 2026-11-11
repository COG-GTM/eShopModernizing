#!/usr/bin/env bash
# Runs both legacy .NET Framework apps on Linux (Mono 6.12 + xsp4) from a SCRATCH copy and records the
# normalised HTTP baseline of the main routes under modern/parity/baseline/. The repo checkout is never modified.
# Full recipe and the reason for every patch: modern/parity/LEGACY-RUN.md
#
# Usage: modern/parity/run-legacy.sh [--skip-build] [--keep-running] [--sql] | --stop
#   --skip-build    reuse the scratch copy built by a previous run
#   --keep-running  leave both xsp4 servers up after the capture (stop later with: run-legacy.sh --stop)
#   --stop          stop servers started by an earlier --keep-running run (by PID) and exit
#   --sql           EF6 + SQL Server mode instead of the in-memory mock (needs LEGACY_SQL_PASSWORD, see LEGACY-RUN.md)
# Env: LEGACY_RUN_DIR (default ~/mecca-legacy-run), LEGACY_MVC_PORT (5100), LEGACY_WEBFORMS_PORT (5101),
#      LEGACY_SQL_SERVER (127.0.0.1,1433), LEGACY_SQL_PASSWORD (sa password, --sql only)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
RUN="${LEGACY_RUN_DIR:-$HOME/mecca-legacy-run}"
MVC_PORT="${LEGACY_MVC_PORT:-5100}"
WF_PORT="${LEGACY_WEBFORMS_PORT:-5101}"
SQL_SERVER="${LEGACY_SQL_SERVER:-127.0.0.1,1433}"
SKIP_BUILD=0; KEEP=0; MODE=mock; STOP_ONLY=0
for arg in "$@"; do
  case "$arg" in
    --skip-build) SKIP_BUILD=1;;
    --keep-running) KEEP=1;;
    --stop) STOP_ONLY=1;;
    --sql) MODE=sql;;
    *) echo "unknown option $arg" >&2; exit 2;;
  esac
done
OUT="$HERE/baseline/$MODE"
MVC="$RUN/$MODE/eShopLegacyMVCSolution/src/eShopLegacyMVC"
WF="$RUN/$MODE/eShopLegacyWebFormsSolution/src/eShopLegacyWebForms"

case "$RUN" in "$REPO"|"$REPO"/*) echo "LEGACY_RUN_DIR must be outside the repository" >&2; exit 1;; esac

stop_app() {  # stop only the xsp4 we started, by PID (an unrelated xsp runs on this box)
  local pidfile="$RUN/$1.pid"
  [ -f "$pidfile" ] || return 0
  local pid; pid="$(cat "$pidfile")"
  if kill -0 "$pid" 2>/dev/null; then
    kill -TERM -- "-$pid" 2>/dev/null || kill -TERM "$pid" 2>/dev/null || true
    for _ in $(seq 1 50); do kill -0 "$pid" 2>/dev/null || break; sleep 0.2; done
    kill -KILL -- "-$pid" 2>/dev/null || kill -KILL "$pid" 2>/dev/null || true
  fi
  rm -f "$pidfile"
  echo "  stopped $1 (pid $pid)"
}

if [ "$STOP_ONLY" = 1 ]; then stop_app legacy-mvc; stop_app legacy-webforms; exit 0; fi
for t in mono xsp4 msbuild python3 curl; do command -v "$t" >/dev/null || { echo "missing tool: $t" >&2; exit 1; }; done
if [ "$MODE" = sql ]; then : "${LEGACY_SQL_PASSWORD:?--sql needs LEGACY_SQL_PASSWORD (sa password of the SQL Server instance)}"; fi

nuget_install() {  # $1 packages.config, $2 output dir
  if command -v nuget >/dev/null; then
    nuget install "$1" -OutputDirectory "$2" -NonInteractive
  else  # repo-root nuget.exe; needs Mono's cert store synced first (see LEGACY-RUN.md)
    mono "$REPO/nuget.exe" install "$1" -OutputDirectory "$2" -NonInteractive
  fi
}

patch_sql_mode() {  # $1 app dir: EF6 against SQL Server instead of the mock / LocalDB
  local dir="$1" pw_esc
  pw_esc=$(printf '%s' "$LEGACY_SQL_PASSWORD" | sed 's/[&#\\]/\\&/g')
  sed -i 's/key="UseMockData" value="true"/key="UseMockData" value="false"/' "$dir/Web.config"
  sed -i "s#connectionString=\"Data Source=(localdb)[^\"]*\"#connectionString=\"Data Source=$SQL_SERVER; Initial Catalog=Microsoft.eShopOnContainers.Services.CatalogDb; User ID=sa; Password=$pw_esc; MultipleActiveResultSets=True; Pooling=false;\"#" "$dir/Web.config"
  chmod 600 "$dir/Web.config"
  for f in "$dir"/Models/Infrastructure/*.Sequence.sql; do
    sed -i '/^\s*USE \[/d' "$f"   # hard-coded database name in the HiLo scripts
    # CatalogDBInitializer opens @"Models\Infrastructure\x.sql"; on Linux the backslashes are literal file-name chars
    cp "$f" "$dir/Models\\Infrastructure\\$(basename "$f")"
  done
}

build() {
  echo "== scratch copy -> $RUN/$MODE"
  rm -rf "${RUN:?}/$MODE"; mkdir -p "$RUN/$MODE"
  cp -r "$REPO/eShopLegacyMVCSolution" "$REPO/eShopLegacyWebFormsSolution" "$RUN/$MODE/"
  cert-sync --user /etc/ssl/certs/ca-certificates.crt >/dev/null 2>&1 || true

  echo "== eShopLegacyMVC: nuget install + msbuild"
  nuget_install "$MVC/packages.config" "$RUN/$MODE/eShopLegacyMVCSolution/packages" > "$RUN/$MODE/restore-mvc.log" 2>&1 \
    || { tail -30 "$RUN/$MODE/restore-mvc.log"; exit 1; }
  msbuild "$MVC/eShopLegacyMVC.csproj" /restore /p:Configuration=Debug /v:minimal /nologo > "$RUN/$MODE/build-mvc.log" 2>&1 \
    || { tail -30 "$RUN/$MODE/build-mvc.log"; exit 1; }
  # Razor host factory in Views/Web.config names System.Web.Mvc 5.2.3.0; the package is 5.2.7.0 and Mono ignores the redirect
  sed -i 's/System.Web.Mvc, Version=5.2.3.0/System.Web.Mvc, Version=5.2.7.0/g' "$MVC/Views/Web.config"
  # case-sensitive FS: layout uses /images (folder Images), bundle uses site.css (file Site.css), log4net.xml (file log4Net.xml).
  # Real copies, not symlinks: xsp4 serves a symlink truncated to its lstat size.
  cp -r "$MVC/Images" "$MVC/images"; cp "$MVC/Content/Site.css" "$MVC/Content/site.css"; cp "$MVC/log4Net.xml" "$MVC/log4net.xml"
  if [ "$MODE" = mock ]; then
    sed -i 's/key="UseMockData" value="false"/key="UseMockData" value="true"/' "$MVC/Web.config"   # no LocalDB on Linux
  fi

  echo "== eShopLegacyWebForms: nuget install + shims + msbuild"
  nuget_install "$WF/packages.config" "$RUN/$MODE/eShopLegacyWebFormsSolution/packages" > "$RUN/$MODE/restore-wf.log" 2>&1 \
    || { tail -30 "$RUN/$MODE/restore-wf.log"; exit 1; }
  python3 "$HERE/shim_webforms.py" "$WF"
  msbuild "$WF/eShopLegacyWebForms.csproj" /p:Configuration=Debug /v:minimal /nologo > "$RUN/$MODE/build-wf.log" 2>&1 \
    || { tail -30 "$RUN/$MODE/build-wf.log"; exit 1; }
  # AspNet.ScriptManager.* PreApplicationStart needs ScriptResourceDefinition (absent in Mono) and aborts start-up with 500s
  rm -f "$WF"/bin/AspNet.ScriptManager.*.dll "$WF"/bin/Microsoft.ScriptManager.*.dll
  cp "$WF/Content/site.css" "$WF/Content/Site.css"; cp "$WF/log4Net.xml" "$WF/log4net.xml"

  if [ "$MODE" = sql ]; then patch_sql_mode "$MVC"; patch_sql_mode "$WF"; fi
}

start_app() {  # $1 name, $2 dir, $3 port
  local name="$1" dir="$2" port="$3"
  if (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null; then
    echo "port $port is already in use; stop that process or set LEGACY_*_PORT" >&2; exit 1
  fi
  (cd "$dir" && exec setsid nohup xsp4 --port "$port" --address 127.0.0.1 --root . --nonstop) > "$RUN/$name.log" 2>&1 < /dev/null &
  echo $! > "$RUN/$name.pid"
  for i in $(seq 1 180); do
    code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:$port/About.aspx" || true)
    [ "$name" = legacy-mvc ] && code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:$port/api/brands" || true)
    if [ "$code" = 200 ]; then echo "  $name up on http://127.0.0.1:$port (pid $(cat "$RUN/$name.pid"), ${i}s)"; return 0; fi
    sleep 1
  done
  echo "$name did not answer 200 on :$port; see $RUN/$name.log" >&2; tail -20 "$RUN/$name.log" >&2; exit 1
}

if [ "$SKIP_BUILD" = 0 ] || [ ! -d "$MVC/bin" ] || [ ! -d "$WF/bin" ]; then build; fi
mkdir -p "$RUN"
stop_app legacy-mvc; stop_app legacy-webforms
[ "$KEEP" = 1 ] || trap 'stop_app legacy-mvc; stop_app legacy-webforms' EXIT

echo "== starting legacy apps under xsp4 ($MODE mode)"
start_app legacy-mvc "$MVC" "$MVC_PORT"
start_app legacy-webforms "$WF" "$WF_PORT"

echo "== smoke: GET / on both apps"
for p in "$MVC_PORT" "$WF_PORT"; do
  code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 180 "http://127.0.0.1:$p/")
  echo "  GET http://127.0.0.1:$p/ -> $code"
  [ "$code" = 200 ] || { echo "GET / on :$p returned $code" >&2; exit 1; }
done

# Restart so the capture starts from fresh in-memory mock data (the smoke GET / already rendered the list once).
stop_app legacy-mvc; stop_app legacy-webforms
start_app legacy-mvc "$MVC" "$MVC_PORT"
start_app legacy-webforms "$WF" "$WF_PORT"

echo "== capturing baseline -> ${OUT#"$REPO"/}"
echo " legacy-mvc:"; python3 "$HERE/capture_baseline.py" --app legacy-mvc --base-url "http://127.0.0.1:$MVC_PORT" --out "$OUT/legacy-mvc"
echo " legacy-webforms:"; python3 "$HERE/capture_baseline.py" --app legacy-webforms --base-url "http://127.0.0.1:$WF_PORT" --out "$OUT/legacy-webforms"
echo "== done ($(find "$OUT" -type f | wc -l) files, $(du -sh "$OUT" | cut -f1))"
[ "$KEEP" = 1 ] && echo "servers left running; stop with: $0 --stop"
exit 0
