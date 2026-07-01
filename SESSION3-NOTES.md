# Session 3 Notes — N-Tier Services (WCF + WinForms)

Scope: `eShopLegacyNTier` and `eShopModernizedNTier` WCF/WinForms projects only (per MODERNIZATION-WORK-BREAKDOWN.md). Legacy projects were left untouched as the reference baseline.

## Changes

### eShopModernizedNTier/src/eShopWCFService → .NET 8 + CoreWCF
- Converted the legacy-style v4.6.1 IIS-hosted WCF project to an SDK-style `net8.0` app hosted with **CoreWCF 1.8.1** (`CoreWCF.Http` + `CoreWCF.Primitives`).
- **WCF contract preserved**: `ICatalogService`, all `[ServiceContract]`/`[OperationContract]`/`[DataContract]` types, the `http://tempuri.org/` namespace, `basicHttpBinding`, and the `/CatalogService.svc` endpoint address are unchanged, so existing WinForms clients (legacy and modernized) keep working without regenerating service references. WSDL metadata GET remains enabled.
- Verified locally: project builds and `GET /CatalogService.svc?wsdl` returns the CatalogService WSDL with the original target namespace.
- EntityFramework 6.1.3 → 6.4.4 (the .NET Standard-compatible EF6 build).
- `CatalogConfiguration` still honors the `ConnectionString` env var; the fallback is now the literal LocalDB connection string (the old `name=EntityModel` Web.config lookup doesn't exist on .NET 8).
- Removed IIS artifacts: `CatalogService.svc`, `Web.config`/transforms, `packages.config`, publish profile; `CatalogService.svc.cs` renamed to `CatalogService.cs`; added `Program.cs` host.
- `CatalogServiceClient.cs` was never compiled by the old csproj (and does not compile — it calls a no-arg `GetCatalogItems()` that isn't on the contract); it stays excluded via `<Compile Remove>`.
- Dockerfile switched from the Windows `dotnet/framework/wcf:4.7.2` image to a Linux multi-stage `sdk:8.0`/`aspnet:8.0` build.

### eShopModernizedNTier/src/eShopWinForms
- `eShopWinForms.csproj`: `net6.0-windows` (EOL) → `net8.0-windows`; `System.ServiceModel.*` 4.8.0 → 4.10.3. Connected Services `Reference.cs` untouched (contract unchanged). Cross-compiles from Linux with `-p:EnableWindowsTargeting=true`; runs on Windows only.
- `eShopWinForms.fx.csproj` (v4.7.1 variant): kept as-is except a regenerated ProjectGuid `{FD55AF77-141C-4E21-83D3-CED31C796724}` (previously duplicated the legacy WinForms GUID `{AE32909C-...}`).

### GUID cleanup
- `eShopWinForms.fx.csproj`: new GUID as above.
- `eShopModernizedNTier.sln`: regenerated project GUIDs for eShopWCFService (`{1A5FE544-...}`, was duplicating legacy `{52E28A9A-...}`) and eShopWinForms (`{C912B42D-...}`, was duplicating legacy `{AE32909C-...}`), and corrected both entries to the SDK-style project-type GUID `{9A19103F-16F7-4668-BE54-9A1E7A4F7556}`.
- Legacy solution/projects keep their original GUIDs.

## Not done / follow-ups
- Legacy `eShopLegacyNTier` projects intentionally unchanged (baseline for the modernization story; require Windows/MSBuild to build).
- `eShopWinForms.fx.csproj` remains .NET Framework 4.7.1 by design (Framework-flavor variant).
- The eShopCoreAPI project and docker-compose wiring belong to Session 4 and were not touched.
- HTTPS/auth hardening for the CoreWCF host and containerized SQL connection strings are deployment concerns left for later sessions.
