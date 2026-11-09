# .NET 8 storefront parity report

Generated 2026-11-09T18:48:14+00:00 at commit `426a3c7` by `modernization/parity/run-parity.sh`. Each transcript set is replayed against a freshly started .NET 8 app in mock mode (`UseMockData=true`; MVC `127.0.0.1:5200`, Razor Pages `127.0.0.1:5201`), compared per CONTRACT.md §3.

**Overall: PARITY** — transcripts 138/138 (legacy 138/138), characterization tests 77/77 (legacy 77/77).

## Transcripts (`modernization/tools/transcript.py replay`)

| App | .NET 8 pass/total | Legacy pass/total | Accepted differences | Report |
|---|---|---|---|---|
| mvc | 86/86 | 86/86 | 0 | `results/mvc.json` |
| webforms | 52/52 | 52/52 | 0 | `results/webforms.json` |

## Characterization suite (`modernization/tools/tests`, pytest)

| Suite | .NET 8 pass/total | Legacy pass/total |
|---|---|---|
| mvc | 48/48 | 48/48 |
| webforms | 29/29 | 29/29 |
| **total** | **77/77** | **77/77** |

## Accepted differences

None.

## Failures

None.

Re-run: `modernization/parity/run-parity.sh` (see `modernization/parity/README.md`).
