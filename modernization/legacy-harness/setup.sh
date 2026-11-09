#!/usr/bin/env bash
# Copies the legacy solutions into $LEGACY_ROOT (never edits the repo sources), restores NuGet
# packages, applies harness-only patches, and builds both apps with Mono MSBuild.
# Usage: setup.sh [all|mvc|webforms]
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
WHICH="${1:-all}"
command -v mono >/dev/null && command -v msbuild >/dev/null && command -v xsp4 >/dev/null && command -v nuget >/dev/null \
  || { echo "need mono, msbuild, xsp4, nuget (Mono 6.12)"; exit 1; }
cert-sync --user /etc/ssl/certs/ca-certificates.crt >/dev/null 2>&1 || true
mkdir -p "$LEGACY_ROOT"

sql_conn() { [ "$LEGACY_MODE" = sql ] && "$HARNESS_DIR/sql.sh" conn "$1" || echo ""; }

setup_mvc() {
  rm -rf "$LEGACY_ROOT/mvc"
  cp -r "$REPO_ROOT/eShopLegacyMVCSolution" "$LEGACY_ROOT/mvc"
  cd "$LEGACY_ROOT/mvc"
  rm -rf eShopPorted   # an unrelated ASP.NET Core port that lives in the same folder; not part of the legacy app
  # The csproj mixes PackageReference with <Reference HintPath=..\..\packages>. Without the packages/
  # folder the HintPaths miss and Mono's GAC System.Web.Mvc 3.0 collides with 5.2.7, so restore both.
  nuget restore src/eShopLegacyMVC/eShopLegacyMVC.csproj -NonInteractive >/dev/null
  nuget install src/eShopLegacyMVC/packages.config -OutputDirectory packages -NonInteractive >/dev/null
  LEGACY_SQL_CONNECTION="$(sql_conn mvc)" python3 "$HARNESS_DIR/patch_config.py" src/eShopLegacyMVC/Web.config "$LEGACY_MODE"
  python3 "$HARNESS_DIR/patch_mvc.py" src/eShopLegacyMVC
  cd src/eShopLegacyMVC
  msbuild eShopLegacyMVC.csproj /p:Configuration=Debug /v:m /nologo
  fix_case "$PWD"
}

setup_webforms() {
  rm -rf "$LEGACY_ROOT/wf"
  cp -r "$REPO_ROOT/eShopLegacyWebFormsSolution" "$LEGACY_ROOT/wf"
  cd "$LEGACY_ROOT/wf"
  nuget restore eShopLegacyWebForms.sln -NonInteractive >/dev/null
  LEGACY_SQL_CONNECTION="$(sql_conn webforms)" python3 "$HARNESS_DIR/patch_config.py" src/eShopLegacyWebForms/Web.config "$LEGACY_MODE"
  python3 "$HARNESS_DIR/patch_webforms.py" src/eShopLegacyWebForms
  cd src/eShopLegacyWebForms
  msbuild eShopLegacyWebForms.csproj /p:Configuration=Debug /v:m /nologo
  fix_case "$PWD"
}

# Linux is case-sensitive; the apps reference images/Images and Content/site.css/Site.css.
# Real copies, not symlinks: xsp4 serves a symlink truncated to the link's own lstat size.
fix_case() {
  local d="$1"
  [ -d "$d/Images" ] && [ ! -e "$d/images" ] && cp -r "$d/Images" "$d/images"
  [ -d "$d/images" ] && [ ! -e "$d/Images" ] && cp -r "$d/images" "$d/Images"
  [ -f "$d/Content/Site.css" ] && [ ! -e "$d/Content/site.css" ] && cp "$d/Content/Site.css" "$d/Content/site.css"
  [ -f "$d/Content/site.css" ] && [ ! -e "$d/Content/Site.css" ] && cp "$d/Content/site.css" "$d/Content/Site.css"
  mkdir -p "$d/logFiles"
  # SQL mode: CatalogDBInitializer reads Path.Combine(BaseDirectory, @"Models\Infrastructure\x.sql").
  # On Linux that is ONE file name containing backslashes (Mono 6 ignores MONO_IOMAP), so provide it.
  # The scripts also start with `USE [Microsoft.eShopOnContainers.Services.CatalogDb]`; drop that line so
  # each app seeds the database named in ITS connection string (one DB per app, no initializer race).
  # (MONO_IOMAP=all maps the backslashes back to Models/Infrastructure/, so strip it there too.)
  for f in "$d"/Models/Infrastructure/*.sql; do
    grep -v '^USE \[' "$f" > "$f.tmp" && mv "$f.tmp" "$f"
    cp "$f" "$d/Models\\Infrastructure\\$(basename "$f")"
  done
}

case "$WHICH" in
  all) setup_mvc; setup_webforms;;
  mvc) setup_mvc;;
  webforms) setup_webforms;;
  *) echo "usage: $0 [all|mvc|webforms]"; exit 2;;
esac
echo "legacy setup done in $LEGACY_ROOT (mode=$LEGACY_MODE)"
