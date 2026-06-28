# Security Remediation — eShopModernizedMVC

**Beat 2 (SECURE) of the VF Corporation demo.** Real findings from the
**Snyk** scanner (Snyk Code SAST + Snyk Open Source SCA) against
`eShopModernizedMVCSolution/src/eShopModernizedMVC` (.NET Framework 4.7.2,
ASP.NET MVC 5). SonarQube MCP was also queried — no prior server-side analysis
exists for this private fork, so Snyk is the source of findings.

## Findings (before)

### Snyk Code (SAST)
| Severity | Rule | CWE | Location |
|---|---|---|---|
| Low | Anti-forgery token validation disabled | CWE-352 (CSRF) | `Controllers/CatalogController.cs:71` (Create POST) |
| Low | Anti-forgery token validation disabled | CWE-352 (CSRF) | `Controllers/CatalogController.cs:124` (Edit POST) |
| Low | Anti-forgery token validation disabled | CWE-352 (CSRF) | `Controllers/CatalogController.cs:167` (DeleteConfirmed POST) |
| Low | Anti-forgery token validation disabled | CWE-352 (CSRF) | `Controllers/PicController.cs:27` (UploadImage POST) |

### Snyk Open Source (SCA)
| Severity | Package | Version | CVE | Fixed in |
|---|---|---|---|---|
| Medium | log4net | 2.0.12 | CVE-2026-40021 | 3.3.0 |
| Medium | Microsoft.IdentityModel.JsonWebTokens | 5.6.0 | CVE-2024-21319 | 5.7.0 / 6.34.0 / 7.1.2 |
| Medium | System.IdentityModel.Tokens.Jwt | 5.6.0 | CVE-2024-21319 | 5.7.0 / 6.34.0 / 7.1.2 |

### Configuration issue (found while remediating)
- `packages.config` declares **Newtonsoft.Json 13.0.2**, but `eShopModernizedMVC.csproj`
  referenced **`Newtonsoft.Json 12.0.3` (assembly v12.0.0.0)** and `Web.config` pinned the
  binding redirect to **`newVersion="12.0.0.0"`** — so the app bound to the **vulnerable
  12.x** assembly (CVE-2024-21907, High) at runtime despite the manifest claiming 13.0.2.

## Fixed in this PR
1. **CSRF / CWE-352 (3 findings):** added `[ValidateAntiForgeryToken]` to the
   `Create`, `Edit`, and `DeleteConfirmed` POST actions in `CatalogController`, and
   `@Html.AntiForgeryToken()` to the matching `Create`/`Edit`/`Delete` Razor forms so
   the token is rendered and validated.
2. **Newtonsoft.Json binding (High, CVE-2024-21907):** aligned the csproj reference
   (`13.0.0.0`, `packages\Newtonsoft.Json.13.0.2`) and the `Web.config` binding redirect
   (`0.0.0.0-13.0.0.0` → `13.0.0.0`) with the 13.0.2 already in `packages.config`,
   eliminating the runtime bind to the vulnerable 12.x assembly.

## Before / after (counts by severity)
| | Before | After (this PR) |
|---|---|---|
| High (Newtonsoft 12.x runtime bind) | 1 | **0** |
| Medium (SCA: log4net, JWT x2) | 3 | 3 *(see follow-up)* |
| Low (CSRF) | 4 | **1** *(PicController, see follow-up)* |

## Deferred to a follow-up PR (documented, not silently dropped)
- **log4net 2.0.12 → 3.3.0** (CVE-2026-40021): the fix is a **major** version bump
  (log4net 3.x changes namespaces/config) — needs its own change + Windows/MSBuild
  build verification. Upgrade: `Update-Package log4net -Version 3.3.0`.
- **Microsoft.IdentityModel.* / System.IdentityModel.Tokens.Jwt 5.6.0 → 5.7.0**
  (CVE-2024-21319): co-versioned family upgrade; verify against the OWIN/OpenIdConnect
  auth pipeline on Windows. Upgrade: `Update-Package System.IdentityModel.Tokens.Jwt -Version 5.7.0`.
- **PicController.UploadImage CSRF**: the upload is a JS/AJAX endpoint, so remediation
  requires sending the anti-forgery token via a request header in the client script —
  handled separately to avoid breaking the image-upload flow.

## Build constraint
These are .NET Framework 4.7.2 projects requiring **Windows + MSBuild**; they do not
build on the Linux demo VM. The changes above are idiomatic ASP.NET MVC 5 remediations
(attributes, Razor helpers, assembly-binding config) and are reviewed as code; a full
`msbuild` + Snyk re-scan should run in Windows CI to confirm the after-counts.
