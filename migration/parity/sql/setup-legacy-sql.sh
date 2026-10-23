#!/usr/bin/env bash
# Builds throwaway legacy copies (via ../legacy/setup-legacy.sh) wired to the SQL Server 2022 container instead
# of the mock catalog. Harness-only edits, never committed to the legacy projects:
#   1. UseMockData=false
#   2. CatalogDBContext -> localhost,1433 with SQL auth and Encrypt=False (System.Data.SqlClient on Mono)
#   3. CatalogDBInitializer runs Path.Combine(BaseDirectory, @"Models\Infrastructure\dbo.*.Sequence.sql"); on Linux
#      the backslashes are literal file-name characters, so the hilo scripts are copied under those names.
#      Without this EF6 creates the tables, the seed throws FileNotFoundException, and the catalog stays empty.
#   usage: MSSQL_SA_PASSWORD=... setup-legacy-sql.sh mvc|webforms|all   (copies land in $LEGACY_ROOT, default /tmp/sql)
set -euo pipefail
: "${MSSQL_SA_PASSWORD:?set MSSQL_SA_PASSWORD (same value as the compose sql profile)}"
export LEGACY_ROOT="${LEGACY_ROOT:-/tmp/sql}"
here="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$LEGACY_ROOT" && chmod 700 "$LEGACY_ROOT"

wire() {
  cd "$1"
  sed -i 's/key="UseMockData" value="true"/key="UseMockData" value="false"/' Web.config
  python3 - <<'PY'
import os, re
cs = ("Data Source=localhost,1433;Initial Catalog=Microsoft.eShopOnContainers.Services.CatalogDb;User Id=sa;"
      f"Password={os.environ['MSSQL_SA_PASSWORD']};Encrypt=False;MultipleActiveResultSets=True;")
s = open("Web.config").read()
s = re.sub(r'(name="CatalogDBContext" connectionString=")[^"]*"', lambda m: m.group(1) + cs + '"', s)
open("Web.config", "w").write(s)
PY
  chmod 600 Web.config  # holds the SA password; throwaway copy, never committed
  for f in Models/Infrastructure/dbo.*.Sequence.sql; do cp "$f" "${f//\//\\}"; done
}

case "${1:-}" in
  mvc|webforms|all) "$here/../legacy/setup-legacy.sh" "$1" ;;
  *) echo "usage: $0 mvc|webforms|all" >&2; exit 2 ;;
esac
[ "$1" = webforms ] || wire "$LEGACY_ROOT/legacy-mvc/src/eShopLegacyMVC"
[ "$1" = mvc ] || wire "$LEGACY_ROOT/legacy-wf/src/eShopLegacyWebForms"
