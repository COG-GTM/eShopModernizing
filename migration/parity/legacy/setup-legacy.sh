#!/usr/bin/env bash
# Builds throwaway copies of the legacy .NET Framework apps for Mono + xsp4 and applies the
# harness-only config edits documented in migration/baseline/BASELINE.md. Never touches the repo copy.
#   usage: setup-legacy.sh mvc|webforms|all   (copies land in $LEGACY_ROOT, default /tmp)
set -euo pipefail
REPO="$(cd "$(dirname "$0")/../../.." && pwd)"
LEGACY_ROOT="${LEGACY_ROOT:-/tmp}"
which="${1:-all}"

# Mono ships its own CA store; without this nuget.exe fails with CERTIFICATE_VERIFY_FAILED.
cert-sync --user /etc/ssl/certs/ca-certificates.crt >/dev/null 2>&1 || true

setup_mvc() {
  local dst="$LEGACY_ROOT/legacy-mvc"
  rm -rf "$dst" && cp -r "$REPO/eShopLegacyMVCSolution" "$dst" && cd "$dst"
  nuget restore src/eShopLegacyMVC/packages.config -PackagesDirectory packages
  msbuild src/eShopLegacyMVC/eShopLegacyMVC.csproj -restore /p:Configuration=Debug -v:m
  cd src/eShopLegacyMVC
  # 1. No LocalDB on Linux: run against the in-memory mock catalog.
  sed -i 's/key="UseMockData" value="false"/key="UseMockData" value="true"/' Web.config
  # 2. Mono does not apply the System.Web.Mvc binding redirect to the Razor host factory.
  sed -i 's/System.Web.Mvc, Version=5.2.3.0/System.Web.Mvc, Version=5.2.7.0/g' Views/Web.config
  # 3. Linux is case-sensitive, IIS is not: the layout links ~/images/* (folder is Images) and the
  #    css bundle includes ~/Content/site.css (file is Site.css).
  ln -sfn Images images
  #    site.css is a copy, not a symlink: xsp4 serves a symlink truncated to its lstat size (8 bytes).
  cp Content/Site.css Content/site.css
}

setup_webforms() {
  local dst="$LEGACY_ROOT/legacy-wf"
  rm -rf "$dst" && cp -r "$REPO/eShopLegacyWebFormsSolution" "$dst" && cd "$dst"
  # UseMockData is already true in the WebForms Web.config.
  # Mono's System.Web.Extensions has no ScriptResourceDefinition / ScriptManager.ScriptResourceMapping
  # (TypeLoadException on every page). Drop the respond.js mapping and replace the ScriptManager
  # block with the plain <script> tags it would have emitted.
  python3 - <<'PY'
import re
p='src/eShopLegacyWebForms/App_Start/BundleConfig.cs'; s=open(p,encoding='utf-8-sig').read()
s=re.sub(r'ScriptManager\.ScriptResourceMapping\.AddDefinition\(.*?\}\);', '', s, flags=re.S); open(p,'w').write(s)
p='src/eShopLegacyWebForms/Site.Master'; s=open(p,encoding='utf-8-sig').read()
s=re.sub(r'<asp:ScriptManager runat="server">.*?</asp:ScriptManager>',
         '<script src="/Scripts/jquery-3.3.1.js"></script>\n        <script src="/Scripts/bootstrap.js"></script>\n        <script src="/Scripts/respond.js"></script>', s, flags=re.S)
open(p,'w').write(s)
# Mono's page parser predates ASP.NET 4.5: no `<%#: %>` (HTML-encoded binding) and no strongly typed
# `Item` in data-bound templates. Rewrite to the equivalent 4.0 forms (same rendered output).
p='src/eShopLegacyWebForms/Default.aspx'; s=open(p,encoding='utf-8-sig').read()
s=re.sub(r'<%#:(.*?)%>', lambda m: '<%# System.Web.HttpUtility.HtmlEncode('+m.group(1).strip()+') %>', s)
s=re.sub(r'\bItem\.', '((eShopLegacyWebForms.Models.CatalogItem)Container.DataItem).', s)
open(p,'w').write(s)
# Mono's DropDownList ignores SelectMethod (4.5 model binding), leaving Create's Brand/Type lists empty.
# Bind the same GetBrands()/GetTypes() methods explicitly on first load (what SelectMethod does).
p='src/eShopLegacyWebForms/Catalog/Create.aspx'; s=open(p,encoding='utf-8-sig').read()
s=re.sub(r'\s*SelectMethod="Get(Brands|Types)"', '', s); open(p,'w').write(s)
p='src/eShopLegacyWebForms/Catalog/Create.aspx.cs'; s=open(p,encoding='utf-8-sig').read()
anchor='_log.Info($"Now loading... /Catalog/Create.aspx");'
s=s.replace(anchor, anchor+'\n            if (!IsPostBack) { Brand.DataSource = GetBrands(); Brand.DataBind(); Type.DataSource = GetTypes(); Type.DataBind(); }', 1)
open(p,'w').write(s)
# Mono's Page has no ModelState (4.5 model binding) -> MissingMethodException on every postback.
# These pages never model-bind, so on .NET Framework `this.ModelState.IsValid` is always true.
for p in ('src/eShopLegacyWebForms/Catalog/Create.aspx.cs', 'src/eShopLegacyWebForms/Catalog/Edit.aspx.cs'):
    s=open(p,encoding='utf-8-sig').read(); open(p,'w').write(s.replace('this.ModelState.IsValid', 'true'))
PY
  nuget restore src/eShopLegacyWebForms/packages.config -PackagesDirectory packages
  msbuild src/eShopLegacyWebForms/eShopLegacyWebForms.csproj -restore /p:Configuration=Debug -v:m
  # The ScriptManager script packages register ScriptResourceDefinitions from PreApplicationStart.
  rm -f src/eShopLegacyWebForms/bin/AspNet.ScriptManager.*.dll src/eShopLegacyWebForms/bin/Microsoft.ScriptManager.*.dll
}

case "$which" in
  mvc) setup_mvc ;;
  webforms) setup_webforms ;;
  all) setup_mvc; setup_webforms ;;
  *) echo "usage: $0 mvc|webforms|all" >&2; exit 2 ;;
esac
