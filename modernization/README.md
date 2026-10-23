# .NET Framework 4.7.2 -> .NET 8 migration (retail storefront)

Integration branch `devin/hcl-net8-eshop` for a dynamic-workflow migration of the legacy eShop storefront.
Ticket PRs target this branch; the umbrella PR targets `main` and stays a draft. Nothing merges into `main`.

## Scope
- Port `eShopLegacyMVCSolution/src/eShopLegacyMVC` (ASP.NET MVC 5 + Web API 2, EF6, Autofac, log4net) to ASP.NET Core on .NET 8 with EF Core 8 and built-in DI.
- Port the `eShopLegacyWebFormsSolution` catalog pages (list/detail/create/edit/delete) to .NET 8 if it fits.
- Preserve the catalog list/detail/create/edit/delete flows and the `UseMockData` toggle.
- Containerise on Linux (`mcr.microsoft.com/dotnet/aspnet:8.0`).

## Workflow stages
1. Plan: inventory, build and run the legacy apps, capture a baseline (tests + recorded transcripts of every user/API flow), cut tickets with disjoint owned paths.
2. Parallel: one agent per ticket, one PR per ticket into this branch.
3. Integration: merge ticket PRs in waves, build green.
4. Parity: legacy and migrated side by side on identical seed data; replay every transcript and test suite; diff outputs and data state.

The .NET 8 solution lives in `eShopNet8/`; plan, baseline, harness and parity report live in `modernization/`.
