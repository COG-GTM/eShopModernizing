#!/usr/bin/env bash
# Negative check: add each "no .NET 8 version" package (from results.json) to a
# throwaway net8.0 project and record how NuGet resolves it. Expected outcome is
# NU1701 (restored only via the .NET Framework AssetTargetFallback, i.e. it will
# compile against net4x assemblies and typically fail at runtime on System.Web)
# or NU1202 (no compatible assets at all).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
DOTNET="${DOTNET:-dotnet}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

mapfile -t BLOCKERS < <(python3 - "$HERE/results.json" <<'PY'
import json, sys
for pkg in json.load(open(sys.argv[1])):
    for v in pkg["versions"]:
        latest = v.get("latest") or {}
        if v.get("newest_net8") is None and latest.get("has_assemblies"):
            print(f'{pkg["id"]} {latest["version"]}')
            break
PY
)

printf '%-55s %-16s %s\n' PACKAGE VERSION RESULT
for line in "${BLOCKERS[@]}"; do
  read -r id version <<<"$line"
  proj="$WORK/p"
  rm -rf "$proj" && mkdir -p "$proj"
  cat > "$proj/p.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="$id" Version="$version" /></ItemGroup>
</Project>
XML
  out="$("$DOTNET" restore "$proj/p.csproj" 2>&1 || true)"
  codes="$(grep -oE 'NU1[0-9]{3}' <<<"$out" | sort -u | tr '\n' ' ')"
  printf '%-55s %-16s %s\n' "$id" "$version" "${codes:-clean restore}"
done
