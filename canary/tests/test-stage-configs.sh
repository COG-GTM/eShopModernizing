#!/usr/bin/env bash
# Static checks for the nginx-<N>percent.conf stage files and the cutover log parser.
# Needs bash and awk; also runs `nginx -t` on every stage (listen port rewritten) when nginx is installed.
set -euo pipefail

TESTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$TESTS_DIR/../.." && pwd)"
CUTOVER="$REPO_ROOT/canary/cutover.sh"
STAGES=(0 25 50 75 100)
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
failures=0

pass() { echo "ok   - $*"; }
not_ok() { echo "FAIL - $*"; failures=$((failures + 1)); }

# 1. marker and weights agree with the file name
if out="$(CANARY_ENV_FILE=/dev/null "$CUTOVER" verify-configs 2>&1)"; then
    pass "verify-configs: every stage marker matches its catalog_migration weights"
else
    not_ok "verify-configs"
    echo "$out"
fi

# 2. stage files are identical apart from the stage-specific lines
normalize() {
    awk '
        NR == 1 { next }
        /^[[:space:]]*upstream[[:space:]]+catalog_migration/ { inb = 1; print; next }
        inb && /^[[:space:]]*\}/ { inb = 0 }
        inb && /^[[:space:]]*server[[:space:]]/ { next }
        /^# Gradual migration upstream - / { next }
        /set \$canary_stage/ { next }
        /# Catalog MVC endpoints with gradual migration/ { next }
        { print }' "$1"
}
normalize "$REPO_ROOT/nginx-25percent.conf" >"$TMP/reference"
for s in "${STAGES[@]}"; do
    if diff -u "$TMP/reference" <(normalize "$REPO_ROOT/nginx-${s}percent.conf") >"$TMP/diff"; then
        pass "nginx-${s}percent.conf only differs from stage 25 in stage-specific lines"
    else
        not_ok "nginx-${s}percent.conf drifted from the other stage files"
        cat "$TMP/diff"
    fi
done

# 3. each stage has the canary contract: affinity, stage header, canary access log
for s in "${STAGES[@]}"; do
    f="$REPO_ROOT/nginx-${s}percent.conf"
    # shellcheck disable=SC2016
    for pattern in 'hash \$canary_client_key consistent;' 'add_header X-Canary-Stage \$canary_stage always;' 'access_log /var/log/nginx/eshop-canary.log eshop_canary;' 'location /Catalog/ {'; do
        grep -q "$pattern" "$f" || not_ok "nginx-${s}percent.conf is missing: $pattern"
    done
done
pass "canary directives present in every stage file"

# 4. pre-runbook configs (no $canary_stage marker) are still recognised from their weights
if [[ "$(CANARY_ENV_FILE=/dev/null NGINX_CONF_TARGET="$REPO_ROOT/nginx.conf" CANARY_STATE_DIR="$TMP/state" "$CUTOVER" status 2>/dev/null | sed -n 's/^Installed stage *: //p')" == 25 ]]; then
    pass "legacy nginx.conf (3:1 weights, no marker) detected as stage 25"
else
    not_ok "legacy nginx.conf not detected as stage 25"
fi

# 5. nginx accepts every stage file
if command -v nginx >/dev/null 2>&1; then
    mkdir -p "$TMP/nginx/logs"
    for s in "${STAGES[@]}"; do
        sed -e "s#/var/log/nginx/#$TMP/nginx/logs/#g" -e "s/listen 80;/listen 18080;/" "$REPO_ROOT/nginx-${s}percent.conf" >"$TMP/nginx/eshop.conf"
        cat >"$TMP/nginx/main.conf" <<CONF
pid $TMP/nginx/nginx.pid;
error_log $TMP/nginx/logs/error.log;
events {}
http {
    client_body_temp_path $TMP/nginx/cb;
    proxy_temp_path $TMP/nginx/px;
    fastcgi_temp_path $TMP/nginx/fc;
    uwsgi_temp_path $TMP/nginx/uw;
    scgi_temp_path $TMP/nginx/sc;
    include $TMP/nginx/eshop.conf;
}
CONF
        if nginx -p "$TMP/nginx" -c "$TMP/nginx/main.conf" -t >"$TMP/nginx/t.out" 2>&1; then
            pass "nginx -t accepts nginx-${s}percent.conf"
        else
            not_ok "nginx -t rejects nginx-${s}percent.conf"
            cat "$TMP/nginx/t.out"
        fi
    done
else
    echo "skip - nginx not installed; syntax not checked"
fi

# 6. log parser: split, 5xx, failover and p95 attribution
now="$(date +%s)"
{
    for i in $(seq 1 30); do printf '%s.000\t50\t200\t127.0.0.1:5002\t200\t0.0%02d\t0.010\tGET\t/Catalog/\n' "$now" "$i"; done
    for i in $(seq 1 28); do printf '%s.000\t50\t200\t127.0.0.1:5001\t200\t0.020\t0.020\tGET\t/Catalog/Details/1\n' "$now"; done
    printf '%s.000\t50\t502\t127.0.0.1:5002, 127.0.0.1:5001\t502, 502\t0.100\t0.050, 0.050\tGET\t/Catalog/\n' "$now"
    printf '%s.000\t50\t500\t[::1]:5002\t500\t0.010\t0.010\tPOST\t/Catalog/Create\n' "$now"
    printf '%s.000\t25\t500\t127.0.0.1:5002\t500\t0.010\t0.010\tGET\t/Catalog/\n' "$now"
    printf '%s.000\t50\t500\t127.0.0.1:5002\t500\t0.010\t0.010\tGET\t/api/health\n' "$now"
} >"$TMP/canary.log"
report="$(CANARY_ENV_FILE=/dev/null CANARY_ACCESS_LOG="$TMP/canary.log" CANARY_STATE_DIR="$TMP/state" NGINX_CONF_TARGET="$TMP/none.conf" "$CUTOVER" report --to 50 2>&1)"
expect() {
    if grep -Eq "$1" <<<"$report"; then pass "report: $2"; else not_ok "report: $2 (pattern '$1')"; echo "$report"; fi
}
expect '\.NET Core +31 +51\.67% +1 +3\.23 +15 +29' "core counts, share, 5xx and percentiles (IPv6 upstream parsed)"
expect '\.NET Fwk +29 +48\.33% +1 ' "framework counts include the failover target"
expect 'total 60, failovers 1 \(1\.67%\)' "other stages and non-catalog paths excluded; failover counted"

if ((failures > 0)); then
    echo "$failures check(s) failed"
    exit 1
fi
echo "all stage config checks passed"
