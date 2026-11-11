#!/usr/bin/env bash
# Compose smoke test for slice S9 (MBA-3848). Usage: deploy/compose-smoke.sh [sql|mock|all]   (KEEP_UP=1 keeps containers)
# sql : SQL Server 2022 + both apps healthy, GET / 200, non-root, JSON request log line with trace id,
#       SQL Server stopped -> /healthz/ready 503 while /healthz stays 200, then recovers.
# mock: both apps in UseMockData=true mode (no SQL Server), same HTTP / non-root / log checks.
set -uo pipefail
cd "$(dirname "$0")/.."

MODE="${1:-all}"
# Own Compose project so cleanup (down -v) never touches a regular deployment's sqldata volume.
export COMPOSE_PROJECT_NAME="${SMOKE_PROJECT_NAME:-eshop-catalog-smoke}"
FAILURES=0
PASSES=0

# Mirror Compose's precedence: shell env wins, then .env, then the defaults below.
if [[ -f .env ]]; then
  while IFS='=' read -r key value || [[ -n "$key" ]]; do
    [[ "$key" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || continue
    [[ -n "${!key+x}" ]] && continue
    value="${value%$'\r'}"; value="${value#[\"\']}"; value="${value%[\"\']}"
    export "$key=$value"
  done < .env
fi

if [[ -z "${MSSQL_SA_PASSWORD:-}" ]]; then
  MSSQL_SA_PASSWORD="Smoke!$(openssl rand -hex 12)Aa1"
  export MSSQL_SA_PASSWORD
  echo "info: MSSQL_SA_PASSWORD not set; using a random throwaway password for this run"
fi

port_of() {
  case "$1" in
    catalog-mvc) echo "${CATALOG_MVC_PORT:-5300}" ;;
    catalog-webforms) echo "${CATALOG_WEBFORMS_PORT:-5301}" ;;
    catalog-mvc-mock) echo "${CATALOG_MVC_MOCK_PORT:-5310}" ;;
    catalog-webforms-mock) echo "${CATALOG_WEBFORMS_MOCK_PORT:-5311}" ;;
  esac
}

pass() { PASSES=$((PASSES + 1)); echo "PASS  $*"; }
fail() { FAILURES=$((FAILURES + 1)); echo "FAIL  $*"; }
status_of() { curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$@"; }

expect_status() { # name url expected
  local got
  got="$(status_of "$2")"
  if [[ "$got" == "$3" ]]; then pass "$1 -> $got"; else fail "$1 -> $got (expected $3)"; fi
}

wait_for_status() { # name url expected timeout_seconds
  local got="" deadline=$((SECONDS + $4))
  while ((SECONDS < deadline)); do
    got="$(status_of "$2")"
    [[ "$got" == "$3" ]] && break
    sleep 2
  done
  if [[ "$got" == "$3" ]]; then pass "$1 -> $got"; else fail "$1 -> $got (expected $3 within $4s)"; fi
}

check_http() { # service
  local base="http://localhost:$(port_of "$1")"
  expect_status "$1 GET /" "$base/" 200
  expect_status "$1 GET /healthz" "$base/healthz" 200
  expect_status "$1 GET /healthz/ready" "$base/healthz/ready" 200
}

check_non_root() { # service
  local cid user uid
  cid="$(docker compose --profile mock ps -q "$1")"
  user="$(docker inspect -f '{{.Config.User}}' "$cid")"
  uid="$(docker compose --profile mock exec -T "$1" id -u | tr -d '\r')"
  if [[ -n "$user" && "$user" != "0" && "$user" != "root" && "$uid" != "0" ]]; then
    pass "$1 runs as non-root (docker inspect User=$user, id -u=$uid)"
  else
    fail "$1 runs as root (docker inspect User='$user', id -u=$uid)"
  fi
}

check_log_line() { # service
  local trace base out
  trace="$(openssl rand -hex 16)"
  base="http://localhost:$(port_of "$1")"
  curl -s -o /dev/null --max-time 10 -H "traceparent: 00-${trace}-$(openssl rand -hex 8)-01" "$base/"
  sleep 1
  out="$(docker compose --profile mock logs --no-log-prefix --no-color "$1" 2>/dev/null | python3 -c '
import json, sys
trace = sys.argv[1]
hits = []
for raw in sys.stdin:
    raw = raw.strip()
    if not raw.startswith("{"):
        continue
    try:
        entry = json.loads(raw)
    except ValueError:
        continue
    state = entry.get("State") or {}
    if state.get("TraceId") == trace and entry.get("Category", "").endswith("RequestLoggingMiddleware"):
        hits.append((entry, raw))
if len(hits) != 1:
    print("expected exactly 1 request log line for trace %s, found %d" % (trace, len(hits)))
    sys.exit(1)
entry, raw = hits[0]
state = entry["State"]
ok = state.get("Method") == "GET" and state.get("Path") == "/" and isinstance(state.get("StatusCode"), int)
print(raw)
sys.exit(0 if ok else 1)
' "$trace")"
  if [[ $? -eq 0 ]]; then pass "$1 JSON request log line: $out"; else fail "$1 JSON request log line: $out"; fi
}

dump_diagnostics() {
  docker compose --profile mock ps -a
  for svc in "$@"; do echo "--- logs: $svc (tail)"; docker compose --profile mock logs --no-color --tail 30 "$svc"; done
}

run_sql() {
  echo "== SQL mode: docker compose up --build --wait"
  if docker compose up -d --build --wait --wait-timeout 300; then
    pass "docker compose up --wait (sqldata, catalog-mvc, catalog-webforms healthy)"
  else
    fail "docker compose up --wait"
    dump_diagnostics sqldata catalog-mvc catalog-webforms
    return
  fi
  for svc in catalog-mvc catalog-webforms; do
    check_http "$svc"; check_non_root "$svc"; check_log_line "$svc"
  done

  echo "== SQL mode: database down"
  docker compose stop sqldata > /dev/null 2>&1
  for svc in catalog-mvc catalog-webforms; do
    local base="http://localhost:$(port_of "$svc")"
    wait_for_status "$svc GET /healthz/ready (sqldata stopped)" "$base/healthz/ready" 503 30
    expect_status "$svc GET /healthz (sqldata stopped)" "$base/healthz" 200
  done
  docker compose start sqldata > /dev/null 2>&1
  for svc in catalog-mvc catalog-webforms; do
    wait_for_status "$svc GET /healthz/ready (sqldata restarted)" "http://localhost:$(port_of "$svc")/healthz/ready" 200 120
  done
}

run_mock() {
  echo "== mock profile: docker compose --profile mock up --build --wait"
  if docker compose --profile mock up -d --build --wait --wait-timeout 300 catalog-mvc-mock catalog-webforms-mock; then
    pass "docker compose --profile mock up --wait (catalog-mvc-mock, catalog-webforms-mock healthy)"
  else
    fail "docker compose --profile mock up --wait"
    dump_diagnostics catalog-mvc-mock catalog-webforms-mock
    return
  fi
  for svc in catalog-mvc-mock catalog-webforms-mock; do
    check_http "$svc"; check_non_root "$svc"; check_log_line "$svc"
  done
}

case "$MODE" in
  sql) run_sql ;;
  mock) run_mock ;;
  all) run_sql; run_mock ;;
  *) echo "usage: $0 [sql|mock|all]"; exit 2 ;;
esac

if [[ "${KEEP_UP:-0}" != "1" ]]; then
  docker compose --profile mock down -v --remove-orphans > /dev/null 2>&1
fi

echo "== compose smoke: ${PASSES} passed, ${FAILURES} failed"
[[ $FAILURES -eq 0 ]]
