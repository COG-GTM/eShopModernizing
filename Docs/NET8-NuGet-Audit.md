# .NET 8 NuGet dependency audit

Audit of every NuGet package referenced from a `packages.config` or `<PackageReference>` in this repo, checked against nuget.org on 2026-09-29.

- **475 references, 182 unique packages, 16 projects/package files** (inventory: `tools/nuget-net8-audit/results.json`).
- **38 packages have no .NET 8-compatible version on nuget.org at all.** Each one gets a proposed replacement in [section 1](#1-blockers-no-net-8-compatible-version-exists).
- A further set restores on `net8.0` but is deprecated, unsupported, or unnecessary. These are listed in [section 2](#2-net-8-loadable-but-should-still-be-replaced-or-removed) so they can be dealt with in the same pass.
- Every proposed replacement was restored, built and loaded together in one `net8.0` app (`tools/nuget-net8-audit/Net8ReplacementsSmoke`). The build had 0 warnings, with `NU1701` treated as an error. See [Verification](#verification).

## How "compatible" was decided

The audit script (`tools/nuget-net8-audit/audit.py`) reads the nuget.org registration and catalog APIs. For each package it finds:

1. the pinned version,
2. the latest stable version,
3. the **newest stable version that ships a `lib/` or `ref/` asset loadable by `net8.0`**. That means `netstandard1.x/2.x`, `netcoreapp*`, `net5.0`–`net8.0`, or `net8.0-windows` for WinForms. `net9.0+`-only builds do not count.

A package is a **blocker** if (3) doesn't exist, meaning it only ships `net4x` assemblies. By default, NuGet still *restores* these on `net8.0` through `AssetTargetFallback` and only emits warning `NU1701`. They then fail at runtime, usually on `System.Web`. `tools/nuget-net8-audit/check_blockers.sh` confirms this: all 38 blockers restore only with `NU1701`. Content-only packages (JS/CSS, MSBuild tooling, metapackages) are listed separately in [section 3](#3-content--tooling-only-packages-no-assemblies).

## 1. Blockers: no .NET 8-compatible version exists

"Used in" abbreviations: **LMVC** = `eShopLegacyMVCSolution` (incl. `eShopPorted`), **LWF** = `eShopLegacyWebFormsSolution`, **MMVC** = `eShopModernizedMVCSolution`, **MWF** = `eShopModernizedWebFormsSolution`.

Replacement versions are the ones verified in the smoke project. "Shared framework" means the type ships in `Microsoft.AspNetCore.App` / `Microsoft.NETCore.App` (use `Microsoft.NET.Sdk.Web`), so no package is needed.

### ASP.NET (System.Web) stack

| Package (pinned → latest) | Latest TFMs | Used in | Proposed replacement |
|---|---|---|---|
| Microsoft.AspNet.Mvc 5.2.7 → 5.3.0 | net45 | LMVC, MMVC | ASP.NET Core MVC (shared framework). |
| Microsoft.AspNet.Razor 3.2.7 → 3.3.0 | net45 | LMVC, MMVC | Razor in ASP.NET Core (shared framework). Add `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` 8.0.31 only if views must be edited without a rebuild. |
| Microsoft.AspNet.WebPages 3.2.7 → 3.3.0 | net45 | LMVC, MMVC | Shared framework (Razor views / Tag Helpers replace `System.Web.Helpers`). |
| Microsoft.AspNet.WebApi.Core 5.2.7 → 5.3.0 | net45 | LMVC | ASP.NET Core controllers with `[ApiController]` (shared framework). Don't use `Microsoft.AspNetCore.Mvc.WebApiCompatShim`: it is 2.x-only. |
| Microsoft.AspNet.WebApi.WebHost 5.2.7 → 5.3.0 | net45 | LMVC | Kestrel/IIS hosting in ASP.NET Core (shared framework). |
| Microsoft.Web.Infrastructure 1.0.0.0 → 2.0.0 | net40 | all 4 | Remove. It is only a transitive dependency of the System.Web stack. |
| Microsoft.CodeDom.Providers.DotNetCompilerPlatform 2.0.1 → 4.1.0 | net472 | all 4 | Remove. Views are compiled at build time by the Razor SDK. For runtime compilation see RuntimeCompilation above. |
| Microsoft.AspNet.SessionState.SessionStateModule 1.1.0 → 2.0.0 | net462 | all 4 | ASP.NET Core session middleware, `AddSession()` (shared framework). During strangler-fig coexistence, use **Microsoft.AspNetCore.SystemWebAdapters.CoreServices 2.3.0** remote session so the legacy and new apps share session state. |
| Microsoft.Web.RedisSessionStateProvider 4.0.1 → 5.0.4 | net462, net472 | MMVC, MWF | **Microsoft.Extensions.Caching.StackExchangeRedis 8.0.31** (`AddStackExchangeRedisCache`) as the `IDistributedCache` behind `AddSession()`. |
| Microsoft.AspNet.TelemetryCorrelation 1.0.5/1.0.7 → 1.0.8 | net45 | all 4 | Remove. W3C trace-context correlation is built into ASP.NET Core / `System.Diagnostics.Activity`. |

### Web Forms-specific

These have no drop-in replacement because Web Forms itself doesn't exist on .NET 8. The replacement is the target UI stack: **Razor Pages** (recommended here because the pages are simple CRUD) or Blazor.

| Package (pinned → latest) | Latest TFMs | Used in | Proposed replacement |
|---|---|---|---|
| Microsoft.AspNet.ScriptManager.MSAjax 5.0.0 | net45 | LWF, MWF | Remove. Use plain `<script>` tags / `fetch` in Razor Pages. |
| Microsoft.AspNet.ScriptManager.WebForms 5.0.0 | net45 | LWF, MWF | Remove (same as above). |
| AspNet.ScriptManager.bootstrap 4.3.1 → 5.2.3 | net40, net45 | LWF, MWF | Remove. Serve bootstrap as a static asset via LibMan or npm (see section 3). |
| AspNet.ScriptManager.jQuery 3.4.1 → 3.7.1 | net40, net45 | LWF, MWF | Remove. Serve jQuery as a static asset. |
| Microsoft.AspNet.FriendlyUrls.Core 1.0.2 | net40, net45 | LWF, MWF | Razor Pages routing (shared framework), which gives extension-less URLs by default. |
| Microsoft.AspNet.Web.Optimization.WebForms 1.1.3 | net45 | LWF, MWF | Remove, along with `Web.Optimization` below. |

### Bundling / minification

| Package (pinned → latest) | Latest TFMs | Used in | Proposed replacement |
|---|---|---|---|
| Microsoft.AspNet.Web.Optimization 1.1.3 | net40 | all 4 | **LigerShark.WebOptimizer.Core 3.0.477**, which is the alternate nuget.org itself lists in the deprecation metadata. Build-time bundling (npm, esbuild) also works. |
| WebGrease 1.6.0 | lib root (net) | all 4 | Remove. It is a dependency of `Web.Optimization`, and WebOptimizer covers minification. |
| Antlr 3.5.0.2 | lib root (net) | all 4 | Remove. It is only pulled in by WebGrease. |

### Dependency injection (Autofac integrations)

| Package (pinned → latest) | Latest TFMs | Used in | Proposed replacement |
|---|---|---|---|
| Autofac.Mvc5 4.0.2 → 7.0.0 | net481 | LMVC, MMVC | **Autofac.Extensions.DependencyInjection 11.0.2** (`UseServiceProviderFactory(new AutofacServiceProviderFactory())`). The existing `ApplicationModule` Autofac modules keep working. Alternatively drop Autofac for built-in DI, as `eShopModernized` already does. |
| autofac.webapi2 6.0.1 → 7.0.0 | net481 | LMVC | Same as above: one container serves MVC and API controllers in ASP.NET Core. |
| Autofac.Web 4.0.0 → 8.0.0 | net481 | LWF, MWF | Same as above. Razor Pages use constructor injection instead of `[InjectProperties]`/page injection. |

### Authentication / OWIN

| Package (pinned → latest) | Latest TFMs | Used in | Proposed replacement |
|---|---|---|---|
| Owin 1.0 | net40 | MMVC, MWF | Remove. ASP.NET Core middleware pipeline (shared framework). |
| Microsoft.Owin 4.2.2 → 4.2.3 | net45 | MMVC, MWF | Remove (same as above). |
| Microsoft.Owin.Host.SystemWeb 4.0.1 → 4.2.3 | net45 | MMVC, MWF | Remove. Kestrel/IIS in-process hosting. |
| Microsoft.Owin.Security 4.0.1 → 4.2.3 | net45 | MMVC, MWF | `Microsoft.AspNetCore.Authentication` (shared framework). |
| Microsoft.Owin.Security.Cookies 4.2.2 → 4.2.3 | net45 | MMVC, MWF | `AddCookie()` (shared framework). |
| Microsoft.Owin.Security.OpenIdConnect 4.0.1 → 4.2.3 | net45 | MMVC, MWF | **Microsoft.Identity.Web 3.15.1** (`AddMicrosoftIdentityWebApp`), already used by `eShopModernized`. For a non-Entra IdP, use **Microsoft.AspNetCore.Authentication.OpenIdConnect 8.0.31**. |
| Microsoft.IdentityModel.Protocol.Extensions 1.0.4.403061554 | net45 | MMVC, MWF | **Microsoft.IdentityModel.Protocols.OpenIdConnect 8.23.0**, the alternate listed in the deprecation metadata. It usually comes in transitively via Identity.Web, so remove the direct reference. |

### Configuration

| Package (pinned → latest) | Latest TFMs | Used in | Proposed replacement |
|---|---|---|---|
| Microsoft.Configuration.ConfigurationBuilders.Base 2.0.0-beta → 3.0.0 | net471 | MMVC, MWF | `Microsoft.Extensions.Configuration` (shared framework, `WebApplication.CreateBuilder`). |
| Microsoft.Configuration.ConfigurationBuilders.Environment 2.0.0-beta → 3.0.0 | net471 | MMVC, MWF | Built-in environment-variables provider (on by default). |
| Microsoft.Configuration.ConfigurationBuilders.UserSecrets 2.0.0-beta → 3.0.0 | net471 | MMVC, MWF | Built-in user-secrets provider (on by default in Development). |
| Microsoft.Configuration.ConfigurationBuilders.Azure 2.0.0-beta → 3.0.0 | net471 | MMVC, MWF | **Azure.Extensions.AspNetCore.Configuration.Secrets 1.5.2** + **Azure.Identity 1.21.0** (`AddAzureKeyVault(uri, new DefaultAzureCredential())`). This replaces `OptionalKeyVaultConfigurationBuilder.cs`. |
| Microsoft.WindowsAzure.ConfigurationManager 3.2.3 | net40 | MMVC, MWF | Remove. Read settings through `IConfiguration` / `IOptions<T>`. |

### Azure SDKs, logging, telemetry

| Package (pinned → latest) | Latest TFMs | Used in | Proposed replacement |
|---|---|---|---|
| WindowsAzure.ServiceBus 6.0.0 → 7.0.1 | net462 | MMVC, MWF | **Azure.Messaging.ServiceBus 7.20.2**, the alternate listed in the deprecation metadata. No `.cs` file in the repo uses Service Bus, so the likely action is to delete the reference outright. |
| log4net.Appender.Azure 1.4.3.0 | net45 | MMVC, MWF | Preferred: `ILogger` + **Microsoft.Extensions.Logging.AzureAppServices 8.0.31** (blob/file sinks), or App Insights / Azure Monitor below. To keep log4net: **log4net 3.4.0** + **Microsoft.Extensions.Logging.Log4Net.AspNetCore 10.0.0**, with a blob appender from Azure Monitor / a custom `AppenderSkeleton` over **Azure.Storage.Blobs 12.29.2**. |
| Microsoft.ApplicationInsights.Web 2.9.1/2.11.2 → 3.1.2 | net462 | all 4 | **Microsoft.ApplicationInsights.AspNetCore 2.23.0**, the 2.x line already used by `eShopModernized`/`eShopCoreAPI`. Forward path: **Azure.Monitor.OpenTelemetry.AspNetCore 1.6.0**. |
| Microsoft.ApplicationInsights.Agent.Intercept 2.4.0 | net40, net45 | all 4 | Remove. The ASP.NET Core SDK above collects dependencies without the profiler intercept. |

## 2. .NET 8-loadable but should still be replaced or removed

These restore on `net8.0` but are deprecated, target an unsupported runtime line, or are redundant. The cheapest time to fix them is during the migration.

| Package(s) | Issue | Proposed action |
|---|---|---|
| WindowsAzure.Storage 9.3.3 | Deprecated (→ Azure.Storage.*) | **Azure.Storage.Blobs 12.29.2** (already used by `eShopModernized`) |
| Microsoft.Azure.KeyVault, .Core, .WebKey 3.0.4 | Deprecated | **Azure.Security.KeyVault.Secrets 4.11.1** / Azure.Extensions.AspNetCore.Configuration.Secrets |
| Microsoft.Azure.Services.AppAuthentication 1.3.1 | Deprecated and all versions unlisted | **Azure.Identity 1.21.0** (`DefaultAzureCredential`) |
| Microsoft.IdentityModel.Clients.ActiveDirectory 5.2.4 (ADAL) | Deprecated, and ADAL endpoints are retired | **Microsoft.Identity.Client 4.90.0** (MSAL) / Microsoft.Identity.Web |
| Microsoft.Rest.ClientRuntime 2.3.24, .Azure 3.3.19 | Deprecated (→ Azure.Core) | Remove. These are transitive dependencies of the old KeyVault SDK. |
| Microsoft.Data.Edm / OData / Services.Client 5.8.4, System.Spatial 5.8.4 | OData v3, deprecated | Remove. These are transitive dependencies of WindowsAzure.Storage. |
| EntityFramework 6.1.3 / 6.2.0 (LNTier, LMVC) | Pinned versions are net4x-only | Target **Microsoft.EntityFrameworkCore.SqlServer 8.0.31**, matching `eShopModernized`/`eShopCoreAPI`. Interim option: **EntityFramework 6.5.2** + **Microsoft.EntityFramework.SqlServer 6.5.2** runs on net8 (netstandard2.1), but has no EDMX designer or `Enable-Migrations` tooling on SDK projects. |
| EntityFramework 6.3.0 / 6.4.4 (MWF, MNTier WinForms) | Loads on net8 but outdated | Same as above |
| Microsoft.EntityFrameworkCore* 2.2.6, Microsoft.AspNetCore / .Mvc / .StaticFiles 2.2.0 (`eShopPorted`, net461) | netstandard2.0 packages from an out-of-support line. ASP.NET Core 2.x packages must not be mixed with the 8.0 shared framework. | Retarget `eShopPorted` to `net8.0` with `Microsoft.NET.Sdk.Web` and delete the three AspNetCore package refs. Use **Microsoft.EntityFrameworkCore.SqlServer/Design 8.0.31**. |
| Microsoft.ApplicationInsights.WindowsServer / .TelemetryChannel / DependencyCollector / PerfCounterCollector / TraceListener / Log4NetAppender / ServiceFabric | net8-loadable, but the classic-ASP.NET module set | Collapse to **Microsoft.ApplicationInsights.AspNetCore 2.23.0**, which brings the collectors transitively. |
| Newtonsoft.Json 6.0.4 (LNTier WinForms) | Pinned version has no netstandard asset | **Newtonsoft.Json 13.0.4**, or `System.Text.Json` (shared framework) |
| Microsoft.AspNet.WebApi.Client 5.2.3 / 5.2.7 | Pinned 5.2.3 is net45-only | **Microsoft.AspNet.WebApi.Client 6.0.0** (netstandard2.0), or `System.Net.Http.Json` (shared framework) |
| System.ServiceModel.Http / NetTcp 4.8.0; Duplex / Security 4.8.0 (MNTier WinForms, `net6.0-windows`) | Works, but the 4.x WCF client is old, and Duplex/Security stop at 6.0.0 | **System.ServiceModel.Http / NetTcp / Primitives 8.1.2**, and drop Duplex/Security. `System.ServiceModel.Primitives` 8.1.2 ships the `System.ServiceModel.Duplex.dll` and `System.ServiceModel.Security.dll` assemblies itself (checked in the restored package). Retarget the app to `net8.0-windows`. |
| Autofac 4.9.x / 6.1.0, log4net 2.0.x, StackExchange.Redis 2.0.601, Pipelines.Sockets.Unofficial | Compatible | Bump to current versions when touching the project. These are not blockers. |
| Microsoft.Net.Compilers 2.10.0 / 3.3.1 | Deprecated | Remove. The .NET SDK ships the compiler. |
| ~60 `System.*` facade packages (System.Runtime, System.Net.Http, System.Memory, System.ValueTuple, System.Buffers …), NETStandard.Library, Microsoft.NETCore.Platforms, Microsoft.CSharp, Microsoft.Win32.Primitives, Microsoft.Bcl.AsyncInterfaces, Microsoft.Extensions.* 3.0.0 | Inbox in `Microsoft.NETCore.App` 8.0 | Remove. SDK-style projects get these from the shared framework, and pinning old versions only causes downgrade warnings (NU1605). |

## 3. Content / tooling-only packages (no assemblies)

These don't block .NET 8 because they ship no assemblies, but NuGet `content/` packages don't work in SDK-style projects, so they still need to move.

| Package(s) | Proposed action |
|---|---|
| bootstrap 4.3.1, jQuery 3.5.0, jQuery.Validation 1.19.4, Microsoft.jQuery.Unobtrusive.Validation 3.2.11, popper.js 1.14.3 | LibMan (`libman.json`) or npm into `wwwroot/lib`, the default ASP.NET Core template layout. |
| Modernizr 2.8.3, Respond 1.4.2 | Remove. These are IE8/9 polyfills. |
| Microsoft.AspNet.WebApi 5.2.7, Microsoft.AspNet.FriendlyUrls 1.0.2 (metapackages) | Remove, together with their section 1 dependencies. |
| Microsoft.VisualStudio.Azure.Fabric.MSBuild 1.6.1 (`ServiceFabric/*`) | Keep only if the Service Fabric deployment option survives. The SF SDK supports .NET 8 services, but that is a deployment decision outside this audit. |

## Assumptions

- **Scope:** all 16 package files in the repo, including the legacy samples, `eShopPorted`, the Service Fabric wrappers and the projects already on `net8.0`. The last group was all compatible.
- **Target:** `net8.0` (LTS), or `net8.0-windows` for WinForms. Where a Microsoft.* package has 9.x/10.x versions, the 8.0.x servicing line was picked to match the runtime and the repo's existing EF Core 8 usage.
- **Versions:** only stable versions published at least 7 days before the audit (supply-chain policy). Nothing newer than 2026-09-22 was selected.
- **Compatibility:** judged by `lib/`/`ref/` TFMs, not by API-level analysis. A netstandard2.0 package that calls `System.Web` via reflection would be missed. None was found in this set, but the .NET Upgrade Assistant / API Port would catch that class of problem.
- **Web Forms:** assumed to move to Razor Pages. That is the lowest-friction fit for these CRUD pages, and it is what the existing `webforms-catalog-razor-pages` branches do.

## Verification

```bash
python3 tools/nuget-net8-audit/audit.py            # regenerates results.json and prints the 38 blockers
DOTNET=dotnet tools/nuget-net8-audit/check_blockers.sh   # each blocker restores on net8.0 only via NU1701 fallback
cd tools/nuget-net8-audit/Net8ReplacementsSmoke && dotnet build && dotnet run   # GET /smoke
```

Results on 2026-09-29 with .NET SDK 8.0.425:

- `check_blockers.sh`: all 38 packages restore on `net8.0` only with `NU1701`. `Microsoft.AspNet.Web.Optimization.WebForms` also emits `NU1602`. None has a real .NET 8 asset.
- `Net8ReplacementsSmoke`: every replacement package above restored, and the build finished with **0 warnings / 0 errors**, with `NU1701;NU1202;NU1603;NU1605` treated as errors. On .NET 8.0.31 runtime, `GET /smoke` returned HTTP 200 listing all 24 representative types loaded from their replacement assemblies. The services (Autofac factory, EF Core, Redis cache, session, SystemWebAdapters, WebOptimizer, App Insights, cookie + OIDC auth) were registered in the pipeline.
