# Modernization Work-Breakdown: 5 Independent Parallel Sessions

This document splits the projects in this repository into 5 self-contained modernization sessions that can be executed concurrently by separate engineers/agents. All `TargetFramework` values and package lists below have been verified directly against the `.csproj` / `packages.config` files on `main`.

## Key Constraint Driving the Split

The **only compile-time shared library** in the repo is `eShopLegacy.Utilities`:

- Path: `eShopLegacyMVCSolution/eShopLegacy.Utilities/eShopLegacy.Utilities.csproj` (.NET Framework **v4.6.1**, BCL-only class library)
- Referenced (via `<ProjectReference>`) by BOTH:
  - `eShopLegacyMVCSolution/src/eShopLegacyMVC/eShopLegacyMVC.csproj` (lines 567–572)
  - `eShopLegacyMVCSolution/eShopPorted/eShopPorted.csproj` (lines 30–32)

All other cross-project links are **runtime-only** WCF service references (WinForms clients → WCF services via Connected Services), so those projects can be split apart safely.

**Parallelism summary:** Sessions 2, 3, 4, and 5 have NO compile-time `ProjectReference` dependencies and are safe to run fully in parallel. Session 1 is the only one with a real shared-code dependency and must handle `eShopLegacy.Utilities` before/with its consumers.

---

## SESSION 1 — Legacy MVC Track + Shared Library 🚩 (has the coupling; keep together)

| Project | Path | Target Framework | Key Packages |
|---|---|---|---|
| eShopLegacy.Utilities 🚩 SHARED | `eShopLegacyMVCSolution/eShopLegacy.Utilities/eShopLegacy.Utilities.csproj` | v4.6.1 (legacy-style) | BCL only (no NuGet packages) |
| eShopLegacyMVC | `eShopLegacyMVCSolution/src/eShopLegacyMVC/eShopLegacyMVC.csproj` | v4.7.2 (legacy-style) | Autofac 4.9.1 + Autofac.Mvc5, EntityFramework 6.2.0, Microsoft.AspNet.Mvc 5.2.7, ApplicationInsights 2.x, log4net, Newtonsoft.Json |
| eShopPorted | `eShopLegacyMVCSolution/eShopPorted/eShopPorted.csproj` | net461 (SDK-style) | Microsoft.AspNetCore.Mvc 2.2.0, Microsoft.AspNetCore.StaticFiles 2.2.0, Microsoft.EntityFrameworkCore 2.2.6 (+Design/Relational/SqlServer), Autofac 4.9.1, Autofac.Extensions.DependencyInjection 4.4.0 |

**Dependency:** `eShopLegacyMVC` and `eShopPorted` both take a compile-time `ProjectReference` on `eShopLegacy.Utilities`. Modernize/extract the shared library FIRST.

**Deliverable:** Decide whether to extract `eShopLegacy.Utilities` into a shared package (e.g., multi-target netstandard2.0/net8.0 NuGet) or duplicate it into each consumer, then modernize both consumers.

---

## SESSION 2 — WebForms Track (fully self-contained, no ProjectReference)

| Project | Path | Target Framework | Key Packages |
|---|---|---|---|
| eShopLegacyWebForms | `eShopLegacyWebFormsSolution/src/eShopLegacyWebForms/eShopLegacyWebForms.csproj` | v4.7.2 (legacy-style) | Antlr 3.5.0.2, AspNet.ScriptManager.bootstrap/jQuery, Autofac 4.9.1 + Autofac.Web, EntityFramework |
| eShopModernizedWebForms ✅ verified | `eShopModernizedWebFormsSolution/src/eShopModernizedWebForms/eShopModernizedWebForms.csproj` | **v4.7.2** (legacy-style) | Antlr 3.5.0.2, AspNet.ScriptManager.bootstrap 4.3.1 / jQuery 3.4.1, Autofac 4.9.4 + Autofac.Web 4.0.0, EntityFramework 6.3.0, log4net 2.0.10 + Azure appender, ApplicationInsights 2.11.x, Azure KeyVault 3.0.4, ConfigurationBuilders 2.0.0-beta, Owin/OpenIdConnect 4.x, Microsoft.Web.RedisSessionStateProvider 4.0.1, StackExchange.Redis 2.0.601, Newtonsoft.Json 13.0.2 |

**Note:** `eShopLegacyWebForms` and `eShopModernizedWebForms` share ProjectGuid `{4416714A-9BB8-480B-95B3-C2599598E3EC}` — harmless across separate solutions, but do not merge them into one solution without regenerating a GUID.

---

## SESSION 3 — N-Tier Services (WCF + WinForms clients; runtime-only WCF coupling)

| Project | Path | Target Framework | Key Packages |
|---|---|---|---|
| eShopWCFService (legacy) | `eShopLegacyNTier/src/eShopWCFService/eShopWCFService.csproj` | v4.6.1 (legacy-style) | EntityFramework 6.1.3 |
| eShopWinForms (legacy) | `eShopLegacyNTier/src/eShopWinForms/eShopWinForms.csproj` | v4.7 (legacy-style) | EntityFramework 6.1.3, Newtonsoft.Json, Microsoft.AspNet.WebApi.Client; consumes WCF via Connected Services |
| eShopWCFService (modernized) | `eShopModernizedNTier/src/eShopWCFService/eShopWCFService.csproj` | v4.6.1 (legacy-style) | EntityFramework 6.1.3 |
| eShopWinForms (modernized) | `eShopModernizedNTier/src/eShopWinForms/eShopWinForms.csproj` | net6.0-windows (SDK-style) | EntityFramework 6.4.4, Microsoft.AspNet.WebApi.Client 5.2.7, System.ServiceModel.* 4.8.0 (Duplex/Http/NetTcp/Security) |
| eShopWinForms.fx (variant) | `eShopModernizedNTier/src/eShopWinForms/eShopWinForms.fx.csproj` | v4.7.1 (legacy-style) | Same codebase, .NET Framework flavor |

**ProjectGuid cleanup (resolve in this session):** `eShopWinForms.fx.csproj` carries ProjectGuid `{AE32909C-9EE6-4ECE-B407-D23A15A1FEED}`, which duplicates the legacy `eShopLegacyNTier/src/eShopWinForms/eShopWinForms.csproj` GUID (the net6.0-windows SDK-style `eShopWinForms.csproj` has no GUID). Similarly, the two `eShopWCFService` projects share GUID `{52E28A9A-68ED-4F7C-BC67-E2AB4396CB53}`. Regenerate GUIDs if these ever coexist in one solution.

**Constraint:** Preserve the WCF contract so clients keep working during modernization. All WinForms→WCF coupling is runtime-only (service references), not compile-time.

---

## SESSION 4 — Modernized .NET Core API Backend

| Project | Path | Target Framework | Key Packages |
|---|---|---|---|
| eShopCoreAPI | `eShopModernizedNTier/src/eShopCoreAPI/eShopCoreAPI.csproj` | net8.0 (SDK-style) | Microsoft.EntityFrameworkCore.SqlServer/Tools 8.0.8, Azure.Identity 1.12.0, Azure.Extensions.AspNetCore.Configuration.Secrets 1.3.2, Microsoft.Data.SqlClient 5.2.0, Microsoft.ApplicationInsights.AspNetCore 2.22.0, AspNetCore.HealthChecks.SqlServer 8.0.1, Swashbuckle.AspNetCore 6.7.3 |

Self-contained, already modernized — focus on hardening/completing (health checks, auth, error handling, tests, deployment).

---

## SESSION 5 — Modernized MVC + Strangler-Fig Core

| Project | Path | Target Framework | Key Packages |
|---|---|---|---|
| eShopModernizedMVC ✅ verified | `eShopModernizedMVCSolution/src/eShopModernizedMVC/eShopModernizedMVC.csproj` | **v4.7.2** (legacy-style) | Autofac 4.9.4 + Autofac.Mvc5 4.0.2, EntityFramework 6.3.0, Microsoft.AspNet.Mvc 5.2.7, log4net 2.0.12 + Azure appender, ApplicationInsights 2.11.x, Azure KeyVault 3.0.4, ConfigurationBuilders 2.0.0-beta, Owin/OpenIdConnect 4.x, Microsoft.Web.RedisSessionStateProvider 4.0.1, StackExchange.Redis 2.0.601, Newtonsoft.Json 13.0.2 |
| eShopModernized (assembly `eShopCoreModernized`) | `eShopModernized/src/eShopCoreModernized/eShopModernized.csproj` | net8.0 (SDK-style) | Microsoft.EntityFrameworkCore 8.0.8 (+SqlServer/Tools/Design), Azure.Storage.Blobs 12.19.1, Azure.Identity 1.12.0, Azure.Extensions.AspNetCore.Configuration.Secrets 1.3.0, Microsoft.Identity.Web 3.0.1, Microsoft.ApplicationInsights.AspNetCore 2.22.0 |

`eShopCoreModernized` is the new core that nginx routes `/Catalog/*` to (see `nginx.conf` and the weighted `nginx-25/50/75/100percent.conf` strangler-fig configs).

**Note:** `eShopModernizedMVC` shares ProjectGuid `{C4E50A92-30E2-4592-9A9A-A9D2B1B13A7E}` with `eShopLegacyMVC` (Session 1) — harmless across separate solutions, but regenerate if merged.

---

## Verification Notes (previously unverified items, now confirmed)

- `eShopModernizedMVC.csproj`: TargetFrameworkVersion **v4.7.2**; full package list in `eShopModernizedMVCSolution/src/eShopModernizedMVC/packages.config`.
- `eShopModernizedWebForms.csproj`: TargetFrameworkVersion **v4.7.2**; full package list in `eShopModernizedWebFormsSolution/src/eShopModernizedWebForms/packages.config`.
- Duplicate-GUID correction: the duplicate is between `eShopWinForms.fx.csproj` (v4.7.1) and the **legacy** `eShopLegacyNTier` WinForms project — the net6.0-windows `eShopWinForms.csproj` is SDK-style and declares no ProjectGuid.
- Confirmed only two `<ProjectReference>` entries exist in the entire repo, both pointing at `eShopLegacy.Utilities`.
