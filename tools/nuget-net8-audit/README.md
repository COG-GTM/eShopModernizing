# nuget-net8-audit

Tooling behind `Docs/NET8-NuGet-Audit.md`.

| File | Purpose |
|---|---|
| `audit.py` | Scans every `packages.config` / `<PackageReference>` and queries nuget.org for the TFMs of the pinned, latest, and newest net8-compatible versions. Writes `results.json`. Python 3 stdlib only. HTTP responses are cached in `.cache/`. |
| `results.json` | Output of the last `audit.py` run (committed so the report can be diffed over time). |
| `check_blockers.sh` | Adds each blocker to a throwaway `net8.0` project and records the NuGet diagnostic (`NU1701` = only net4x assets). |
| `Net8ReplacementsSmoke/` | A `net8.0` web app referencing every proposed replacement, with `NU1701` as an error. `GET /smoke` lists the loaded assemblies. |

```bash
python3 tools/nuget-net8-audit/audit.py
tools/nuget-net8-audit/check_blockers.sh
dotnet run --project tools/nuget-net8-audit/Net8ReplacementsSmoke
```
