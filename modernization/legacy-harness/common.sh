#!/usr/bin/env bash
# Shared settings for the legacy harness (sourced by the other scripts).
set -euo pipefail
HARNESS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$HARNESS_DIR/../.." && pwd)"
LEGACY_ROOT="${LEGACY_ROOT:-$HOME/eshop-legacy-run}"
MVC_PORT="${MVC_PORT:-5100}"
WEBFORMS_PORT="${WEBFORMS_PORT:-5101}"
LEGACY_MODE="${LEGACY_MODE:-mock}"   # mock | sql
MVC_APP_DIR="$LEGACY_ROOT/mvc/src/eShopLegacyMVC"
WF_APP_DIR="$LEGACY_ROOT/wf/src/eShopLegacyWebForms"
app_dir() { case "$1" in mvc) echo "$MVC_APP_DIR";; webforms) echo "$WF_APP_DIR";; *) echo "unknown app $1" >&2; exit 2;; esac; }
app_port() { case "$1" in mvc) echo "$MVC_PORT";; webforms) echo "$WEBFORMS_PORT";; *) echo "unknown app $1" >&2; exit 2;; esac; }
