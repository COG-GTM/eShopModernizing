#!/usr/bin/env bash
# Builds THROWAWAY copies of the two legacy apps for Mono/xsp4 on Linux and applies the
# harness-only shims documented in modernization/baseline/BASELINE.md (section 3).
# Nothing in the repository checkout is modified.
#
#   LEGACY_RUN_DIR (default ~/legacy-run)  where the copies are built
#   LEGACY_SQL_PASSWORD (required for mvc-sql / wf-sql at start time)  sa password of the local SQL Server container
#   LEGACY_SQL_SERVER (default 127.0.0.1,1433)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
RUN="${LEGACY_RUN_DIR:-$HOME/legacy-run}"
SQL_PW="__SET_BY_START_LEGACY__"  # real password injected by start-legacy.sh mvc-sql from $LEGACY_SQL_PASSWORD
SQL_SERVER="${LEGACY_SQL_SERVER:-127.0.0.1,1433}"

case "$RUN" in "$REPO"|"$REPO"/*) echo "LEGACY_RUN_DIR must be outside the repository" >&2; exit 1;; esac
for t in mono xsp4 msbuild nuget python3; do command -v "$t" >/dev/null || { echo "missing tool: $t" >&2; exit 1; }; done

echo "== copying legacy sources to $RUN"
rm -rf "$RUN"; mkdir -p "$RUN"
cp -r "$REPO/eShopLegacyMVCSolution" "$REPO/eShopLegacyWebFormsSolution" "$RUN/"

# The repo's nuget.exe (4.6) fails TLS under Mono (crypt32.dll); the system nuget 6.x works.
cert-sync --user /etc/ssl/certs/ca-certificates.crt >/dev/null 2>&1 || true

echo "== building eShopLegacyMVC (mixed: packages.config HintPaths + PackageReference -> nuget restore AND msbuild /restore)"
MVC="$RUN/eShopLegacyMVCSolution/src/eShopLegacyMVC"
# Without ../../packages the HintPath references (e.g. System.Web.Mvc 5.2.7) silently fall back to Mono's GAC
# System.Web.Mvc 3.0.0.0 and the build fails with CS0433.
nuget restore "$MVC/packages.config" -PackagesDirectory "$RUN/eShopLegacyMVCSolution/packages" -NonInteractive > "$RUN/restore-mvc.log" 2>&1 \
  || { tail -30 "$RUN/restore-mvc.log"; exit 1; }
msbuild "$MVC/eShopLegacyMVC.csproj" /restore /p:Configuration=Debug /v:minimal /nologo > "$RUN/build-mvc.log" 2>&1 \
  || { tail -30 "$RUN/build-mvc.log"; exit 1; }

echo "== MVC harness shims"
# M1: primary mode is the in-memory mock (LocalDB does not exist on Linux).
sed -i 's/key="UseMockData" value="false"/key="UseMockData" value="true"/' "$MVC/Web.config"
# M2: Views/Web.config Razor host factory references System.Web.Mvc 5.2.3.0; the package is 5.2.7.0 and Mono ignores the binding redirect there.
sed -i 's/System.Web.Mvc, Version=5.2.3.0/System.Web.Mvc, Version=5.2.7.0/g' "$MVC/Views/Web.config"
# M3: case-sensitive file system: layout links /images/* (folder Images), css bundle includes site.css (file Site.css),
#     assembly attribute loads log4net.xml (file log4Net.xml). Real copies: xsp4 serves symlinks truncated.
cp -r "$MVC/Images" "$MVC/images"; cp "$MVC/Content/Site.css" "$MVC/Content/site.css"; cp "$MVC/log4Net.xml" "$MVC/log4net.xml"

echo "== MVC SQL-mode copy ($RUN/mvc-sql)"
rm -rf "$RUN/mvc-sql"; cp -r "$MVC" "$RUN/mvc-sql"
# S1: EF mode against the SQL Server container (DB name must stay Microsoft.eShopOnContainers.Services.CatalogDb:
#     the HiLo sequence scripts hard-code it in a USE statement).
sed -i 's/key="UseMockData" value="true"/key="UseMockData" value="false"/' "$RUN/mvc-sql/Web.config"
sed -i "s#connectionString=\"Data Source=(localdb)[^\"]*\"#connectionString=\"Data Source=$SQL_SERVER; Initial Catalog=Microsoft.eShopOnContainers.Services.CatalogDb; User ID=sa; Password=$SQL_PW; MultipleActiveResultSets=True;\"#" "$RUN/mvc-sql/Web.config"
# S2: CatalogDBInitializer opens @"Models\Infrastructure\dbo.*.Sequence.sql" relative to the app base; on Linux the
#     backslashes are literal file-name characters, so provide files with exactly those names (no code change).
for f in catalog_hilo catalog_brand_hilo catalog_type_hilo; do
  cp "$RUN/mvc-sql/Models/Infrastructure/dbo.$f.Sequence.sql" "$RUN/mvc-sql/Models\\Infrastructure\\dbo.$f.Sequence.sql"
done

echo "== building eShopLegacyWebForms (packages.config -> nuget restore)"
WF="$RUN/eShopLegacyWebFormsSolution/src/eShopLegacyWebForms"
nuget restore "$WF/packages.config" -PackagesDirectory "$RUN/eShopLegacyWebFormsSolution/packages" -NonInteractive > "$RUN/restore-wf.log" 2>&1 \
  || { tail -30 "$RUN/restore-wf.log"; exit 1; }
echo "== WebForms harness shims"
# W1-W5: source shims (ScriptResourceDefinition, ScriptManager, strongly-typed binding, SelectMethod, Page.ModelState).
python3 "$HERE/shim_webforms.py" "$WF"
msbuild "$WF/eShopLegacyWebForms.csproj" /p:Configuration=Debug /v:minimal /nologo > "$RUN/build-wf.log" 2>&1 \
  || { tail -30 "$RUN/build-wf.log"; exit 1; }
# W6: the AspNet.ScriptManager.* / Microsoft.ScriptManager.* assemblies run PreApplicationStart code that needs
#     ScriptResourceDefinition (missing in Mono) and abort app start-up; they are only needed by the ScriptManager.
rm -f "$WF"/bin/AspNet.ScriptManager.*.dll "$WF"/bin/Microsoft.ScriptManager.*.dll
# W7: WebForms Bundle.config references Site.css but the file is site.css; log4net.xml vs log4Net.xml.
cp "$WF/Content/site.css" "$WF/Content/Site.css"; cp "$WF/log4Net.xml" "$WF/log4net.xml"

echo "== WebForms SQL-mode copy ($RUN/wf-sql)"
# S3: same EF6 SQL mode for WebForms (its repo default is UseMockData=true + LocalDB). Copied after the Mono shims and
#     W6/W7 so it runs the same build as the mock copy; shares the legacy catalog DB with mvc-sql (same hard-coded name, Q12).
rm -rf "$RUN/wf-sql"; cp -r "$WF" "$RUN/wf-sql"
sed -i 's/key="UseMockData" value="true"/key="UseMockData" value="false"/' "$RUN/wf-sql/Web.config"
sed -i "s#connectionString=\"Data Source=(localdb)[^\"]*\"#connectionString=\"Data Source=$SQL_SERVER; Initial Catalog=Microsoft.eShopOnContainers.Services.CatalogDb; User ID=sa; Password=$SQL_PW; MultipleActiveResultSets=True;\"#" "$RUN/wf-sql/Web.config"
for f in catalog_hilo catalog_brand_hilo catalog_type_hilo; do
  cp "$RUN/wf-sql/Models/Infrastructure/dbo.$f.Sequence.sql" "$RUN/wf-sql/Models\\Infrastructure\\dbo.$f.Sequence.sql"
done

ln -sfn "$MVC" "$RUN/mvc"; ln -sfn "$WF" "$RUN/webforms"
echo "== done. Start with: $HERE/start-legacy.sh mvc|webforms|mvc-sql|wf-sql"
