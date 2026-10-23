# .NET Framework 4.7.2 -> .NET 8 migration (eShopLegacyMVC + eShopLegacyWebForms)

Integration branch for the dynamic-workflow migration. Ticket PRs target `devin/net8-eshop-migration`; the umbrella PR targets `main` and stays a draft. Nothing merges into `main`.

## Scope
- Port `eShopLegacyMVCSolution/src/eShopLegacyMVC` (ASP.NET MVC 5 + Web API 2, EF6, Autofac, log4net) to ASP.NET Core on .NET 8 with EF Core 8 and built-in DI.
- Port the `eShopLegacyWebFormsSolution` catalog pages (list/detail/create/edit/delete) to .NET 8 Razor Pages.
- Preserve the catalog list/detail/create/edit/delete flows, the pic route, the Web API endpoints and the `UseMockData` toggle.
- Containerise on Linux (`mcr.microsoft.com/dotnet/aspnet:8.0`).

## Map
| | |
|---|---|
| [`INVENTORY.md`](INVENTORY.md) | what the legacy apps contain (routes, model, HiLo, config, DI, quirks) |
| [`CONTRACT.md`](CONTRACT.md) | shared contracts, config keys, ports, culture decision, parity rules, file ownership |
| [`TICKETS.md`](TICKETS.md) / [`tickets.json`](tickets.json) | the parallel tickets with disjoint owned paths and waves |
| [`baseline/`](baseline/BASELINE.md) | legacy runtime, `flows.json`, 126 transcripts, data-state snapshots, screenshots, self-replay report |
| [`parity/`](parity/) | `replay.py` (diff any MVC/WebForms URL against the baseline), `capture.py`, `screenshots.py`, `legacy/` Mono setup + start scripts |
| `../src-net8/` | .NET 8 scaffold: `eShopNet8.sln` (Core, Infrastructure, Mvc, WebForms, tests) |

## Legacy runtime on this Linux estate
.NET Framework needs Windows; the Devin VM is Ubuntu, so the legacy apps run on **Mono 6.12 + xsp4**: `migration/parity/legacy/setup-legacy.sh all` then `migration/parity/legacy/start-legacy.sh mvc` (port 5100) / `webforms` (port 5101). The harness-only edits it applies to throwaway copies are listed in [`baseline/BASELINE.md`](baseline/BASELINE.md).

## Replay against the migrated apps
```bash
pip install -r migration/parity/requirements.txt
python3 migration/parity/replay.py --mvc-url http://localhost:5200 --webforms-url http://localhost:5201 \
  --restart-cmd-mvc "<restart the MVC app>" --restart-cmd-webforms "<restart the WebForms app>"
# -> migration/parity/out/parity-report.{txt,json}; exit code 0 only when every check passes
```
