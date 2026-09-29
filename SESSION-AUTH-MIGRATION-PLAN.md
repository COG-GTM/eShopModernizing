# Session State and Authentication Migration Plan (System.Web → ASP.NET Core)

This plan covers moving the MVC and WebForms apps off `System.Web` for **session state** and **authentication**. It lists what each app depends on today, the ASP.NET Core replacement for each dependency, and the risks that come up during the strangler-fig cutover that nginx drives (`nginx*.conf`, `migrate-traffic.sh`).

It looks at five apps:

| Id | Project | Runtime | Auth today | Session today |
|----|---------|---------|------------|---------------|
| L-MVC | `eShopLegacyMVCSolution/src/eShopLegacyMVC` | .NET Framework 4.7.2, System.Web MVC 5 | **None** | InProc via `SessionStateModuleAsync` |
| L-WF | `eShopLegacyWebFormsSolution/src/eShopLegacyWebForms` | .NET Framework 4.7.2, WebForms | **None** | InProc via `SessionStateModuleAsync` |
| M-MVC | `eShopModernizedMVCSolution/src/eShopModernizedMVC` | .NET Framework 4.7.2, MVC 5 + OWIN (Katana) | OWIN Cookies + OpenID Connect (Entra ID), or a synthetic "always authenticated" middleware | InProc (Redis provider only in a comment) |
| M-WF | `eShopModernizedWebFormsSolution/src/eShopModernizedWebForms` | .NET Framework 4.7.2, WebForms + OWIN | Same as M-MVC | Same as M-MVC |
| CORE | `eShopModernized/src/eShopCoreModernized` | .NET 8 (`net8.0`), ASP.NET Core MVC | `Microsoft.Identity.Web` 3.0.1 (Entra ID), or a local cookie login | `AddSession()` registered, but nothing uses it |

Nothing in this document changes runtime code. Every claim is backed by a file reference or by the runtime spike in [Appendix A](#appendix-a--runtime-spike-evidence-core). Anything I inferred without testing is marked **(inference; verify)**.

---

## 1. Summary

1. **Session state is almost unused, so it's the easy part.** All four System.Web apps store two values, `MachineName` and `SessionStartTime`, in `Session_Start` and only read them to show a diagnostic string in the footer. No business data lives in session. The main job is to *not* build a general-purpose session-compatibility layer (such as System.Web adapters remote session), and to replace the footer with a small ASP.NET Core equivalent.
2. **Authentication is the hard part, and it's the actual cutover risk.** In the current `/Catalog/` weighted split, OWIN cookies (`.AspNet.Cookies`, protected with `machineKey`) and ASP.NET Core cookies (`.AspNetCore.Cookies`, protected with Data Protection) can't read each other. A user who signs in on M-MVC and then gets routed to CORE is anonymous there. CORE also enforces antiforgery on POSTs while M-MVC neither emits nor validates tokens, so form GET/POST pairs that are split across backends will fail with HTTP 400.
3. **CORE isn't ready to be the authentication authority yet.** The spike ([Appendix A](#appendix-a--runtime-spike-evidence-core)) confirmed these gaps:
   - The flat environment variables that `docker-compose.override.yml` passes to CORE (for example `UseAzureActiveDirectory`) are **ignored**, because CORE reads `AppSettings:*` and `Azure:ActiveDirectory:*`.
   - Without Entra ID, `/Account/Login` accepts **any** non-empty username and password, and there's no antiforgery check on that POST.
   - `DELETE /api/brands/{id}` and `POST /api/imageupload/upload` allow anonymous requests.
   - There's no forwarded-headers handling, so behind nginx the OIDC `redirect_uri` comes out as `http://…/signin-oidc`, and the correlation and nonce cookies are `Secure; SameSite=None`, which browsers drop over plain HTTP.
   - In Entra ID mode, `/Account/Logout` only clears the local cookie and never signs out of Entra ID. `Microsoft.Identity.Web.UI` isn't wired up either (`/MicrosoftIdentity/Account/SignOut` returns 404).
   - `_LoginPartial` links to actions that don't exist (`Account/LogOff`, `Manage/Index`, `Account/Register`), and the layout only renders it when `ViewBag.UseAzureActiveDirectory == true`, which nothing ever sets.
   - The CORE `Dockerfile` builds a `net8.0` project with `sdk:6.0` / `aspnet:6.0` images.
4. **Recommended strategy:** don't try to share cookies between OWIN and ASP.NET Core. Make **Entra ID (OIDC) the shared identity authority**, and route by path so that a whole *auth-bearing user journey* lives on a single backend. Put persistent Data Protection keys behind CORE, and harden CORE's auth surface before any authenticated traffic moves. Keep the WebForms port (Razor Pages) behind the same approach. There are sections below on the cookie-interop option for anyone who needs it.

---

## 2. Current state: session state

### 2.1 Configuration

| App | Config | Evidence |
|-----|--------|----------|
| L-MVC | `<sessionState mode="InProc" />`; the default `Session` module is replaced with `Microsoft.AspNet.SessionState.SessionStateModuleAsync` 1.1.0 | `eShopLegacyMVCSolution/src/eShopLegacyMVC/Web.config` lines 33, 91-92 |
| L-WF | Same | `eShopLegacyWebFormsSolution/src/eShopLegacyWebForms/Web.config` lines 29, 79-80 |
| M-MVC | Redis `<sessionState mode="Custom" customProvider="MySessionStateStore">` is **commented out**, so the effective mode is the default **InProc**. Same async module swap. | `eShopModernizedMVCSolution/src/eShopModernizedMVC/Web.config` lines 65-73, 226-227 |
| M-WF | Same as M-MVC | `eShopModernizedWebFormsSolution/src/eShopModernizedWebForms/Web.config` lines 70-78, 213-214 |
| CORE | `AddSession(IdleTimeout = 30 min, HttpOnly, IsEssential)` and `UseSession()` after `UseRouting()`. No `IDistributedCache` is registered explicitly. | `eShopModernized/src/eShopCoreModernized/Program.cs` lines 89-91, 116-117 |

Package references in M-MVC and M-WF (`packages.config`): `Microsoft.AspNet.SessionState.SessionStateModule` 1.1.0, `Microsoft.Web.RedisSessionStateProvider` 4.0.1, `StackExchange.Redis` 2.0.601. None of the `Web.config` files contain `timeout=`, `cookieless`, `requireSSL`, `<httpCookies>` or `<machineKey>`, so all of those use System.Web defaults: 20-minute session timeout, `ASP.NET_SessionId` cookie without `Secure`, and an auto-generated machine key per host or container.

### 2.2 Session reads and writes

| Key | Written | Read | Purpose |
|-----|---------|------|---------|
| `MachineName` (string) | `Global.asax.cs` → `Session_Start` (all four apps) | MVC: `Views/Shared/_Layout.cshtml` (`HttpContext.Current.Session[...]`). WebForms: `Site.Master.cs` → `SessionInfoLabel.Text` | Footer diagnostic showing which node served the session |
| `SessionStartTime` (DateTime) | Same | Same | Footer diagnostic |

These are the only session reads and writes in the four System.Web apps. I found no `Session[...]` usage in controllers, pages, services, or TempData customisation. `rg "Session" --glob '*.cs' --glob '*.cshtml' --glob '*.aspx' --glob '*.Master'` finds only the lines above plus `AccountController.EndSession`, which is an OIDC front-channel logout action and not session state. CORE never reads or writes `ISession`, and its layout already swapped the footer to `Environment.MachineName` plus `DateTime.Now`, which is **render time, not session start time** (`Views/Shared/_Layout.cshtml` line 42).

### 2.3 System.Web behaviours the apps implicitly depend on

- **`Session_Start` event.** Fires once per new session ID. ASP.NET Core has no equivalent.
- **Session locking.** `SessionStateModuleAsync` takes an exclusive per-session lock for pages and handlers with read/write session, so concurrent requests in one session run one at a time. ASP.NET Core session **doesn't lock** and is last-write-wins.
- **Serialization.** InProc keeps live object references. Out-of-proc (Redis provider) uses `BinaryFormatter`. ASP.NET Core `ISession` stores only `byte[]`, with `string`/`int` helpers, so objects need explicit (for example JSON) serialization.
- **Session ID lifetime.** System.Web sets `ASP.NET_SessionId` on first access and keeps using the same ID. ASP.NET Core sets `.AspNetCore.Session` **only once something writes to the session** (the spike confirmed CORE never sends that cookie), and the cookie value is protected with Data Protection.
- **Node affinity.** InProc plus a per-container auto `machineKey` means session and auth cookies only work on the node that issued them. Kubernetes manifests currently use `replicas: 1`. Service Fabric manifests use `InstanceCount = -1` (one instance per node), which means **multi-instance InProc today** unless the load balancer gives affinity. (inference; verify SF LB config)

---

## 3. Current state: authentication

### 3.1 Legacy apps (L-MVC, L-WF)

- There's no authentication middleware or `[Authorize]`, and no `<authentication>` / `<authorization>` in `Web.config`. Every page is anonymous.
- L-MVC uses MVC 5 antiforgery (`@Html.AntiForgeryToken()` + `[ValidateAntiForgeryToken]` on Create, Edit and Delete POSTs in `Controllers/CatalogController.cs` lines 61, 99, 133). Tokens are protected with `machineKey`.
- L-WF has no CSRF protection beyond ViewState MAC and event validation (defaults). `ViewStateUserKey` isn't set.

### 3.2 Modernized System.Web apps (M-MVC, M-WF)

The pipeline is selected in `Startup.cs` (line 13) by `CatalogConfiguration.UseAzureActiveDirectory`, which comes from the `UseAzureActiveDirectory` app setting or environment variable.

**Entra ID on (`ConfigureAuth` in `App_Start/Startup.Auth.cs`):**

```csharp
app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType); // "Cookies"
app.UseCookieAuthentication(new CookieAuthenticationOptions());                           // cookie ".AspNet.Cookies"
app.UseOpenIdConnectAuthentication(new OpenIdConnectAuthenticationOptions {
    ClientId = clientId,                                   // AzureActiveDirectoryClientId
    Authority = string.Format(aadInstance, tenant),        // AzureActiveDirectoryInstance + AzureActiveDirectoryTenant
    PostLogoutRedirectUri = postLogoutRedirectUri,         // PostLogoutRedirectUri
    RedirectUri = postLogoutRedirectUri,                   // reply URL == site root, not /signin-oidc
    Notifications = { AuthenticationFailed = ctx => { ctx.HandleResponse();
        ctx.Response.Redirect("/Error?message=" + ctx.Exception.Message); ... } } });
```

- The OWIN cookie ticket is protected with `MachineKeyDataProtector` (Katana on System.Web). With no `<machineKey>` in `Web.config`, the key is auto-generated per host, so cookies are invalidated when a container is recreated and aren't valid on other instances.
- The reply URL is the **site root** (`RedirectUri = PostLogoutRedirectUri`). The Entra ID app registration therefore has the root URLs (`http://<host>:5115/` and `:5114/` in `docker-compose.override.yml`) registered as reply URLs, and **not** `/signin-oidc`.
- `AuthenticationFailed` reflects `Exception.Message` into a query string (`/Error?message=...`). That's information disclosure and potentially reflected content. Neither app has an `/Error` route: M-MVC only has a shared `Error.cshtml` view for `HandleErrorAttribute` and no `ErrorController`, and M-WF has no error page. Both set `<customErrors mode="Off"/>`, so unhandled errors show full stack traces. (routing result inferred; verify at runtime)
- **Package drift:** `packages.config` pins `Microsoft.Owin` and `Microsoft.Owin.Security.Cookies` at 4.2.2 and `Host.SystemWeb`, `Security` and `Security.OpenIdConnect` at 4.0.1, while every `.csproj` `HintPath` points at the 4.0.1 folders. So what actually gets compiled and deployed is ambiguous. Katana before 4.1 doesn't have the SameSite handling that modern browsers need for `form_post`. (inference; verify built bin/)

**Entra ID off (`Middleware/AuthenticationMiddleware.cs`):**

```csharp
var identity = new ClaimsIdentity("cookies");   // non-empty authenticationType => IsAuthenticated == true
identity.AddClaim(new Claim("iat", "1234"));
context.Authentication.User = new ClaimsPrincipal(); context.Authentication.User.AddIdentity(identity);
```

Every request gets an authenticated principal with **no name claim**. `[Authorize]` always passes and `User.Identity.Name` is `null`. This is the default in `docker-compose.override.yml` (`UseAzureActiveDirectory=false`).

**Consumers of auth state:**

| Consumer | M-MVC | M-WF |
|----------|-------|------|
| Authorization gate | `[Authorize]` on Create, Edit and Delete (GET + POST) in `Controllers/CatalogController.cs` | Imperative `if (!Request.IsAuthenticated) Challenge(...)` in `Page_Load` of `Catalog/Create.aspx.cs`, `Edit.aspx.cs`, `Delete.aspx.cs` |
| Sign-in | `AccountController.SignIn` → OWIN `Challenge(OIDC)` | `Site.Master.cs` → `Challenge(OIDC)` |
| Sign-out | `AccountController.SignOut` → `SignOut(OIDC, Cookies)`. `EndSession` → `SignOut(Cookies)` (front-channel logout target) | `<asp:LoginStatus OnLoggingOut=...>` → `SignOut(OIDC, Cookies)` |
| UI | `_LoginPartial.cshtml`: `Request.IsAuthenticated`, `User.Identity.Name` | `Site.Master`: `<asp:LoginView ViewStateMode="Disabled">`, `Context.User.Identity.Name`, `<asp:LoginStatus>` |
| CSRF | **None.** Modernized `CatalogController` has no `[ValidateAntiForgeryToken]` and views have no `@Html.AntiForgeryToken()` (a regression from L-MVC) | ViewState MAC only |
| Unauthenticated endpoints | `PicController.UploadImage` (`POST /uploadimage`, reads `HttpContext.Current.Request.Files`) | `Catalog/PicUploader.asmx` (`[ScriptService]`) |

**WebForms authorization gaps (code reading):**
- `Edit.aspx.cs` only checks `Request.IsAuthenticated` inside `if (!Page.IsPostBack)`, so the `Save_Click` postback isn't gated.
- OWIN `Challenge(...)` only sets a 401 and a challenge. It doesn't end the page lifecycle, so control event handlers such as `Delete_Click` and `Create_Click` still run on an unauthenticated postback. In Entra ID mode an anonymous crafted postback can probably modify data. **(inference; verify on Windows/IIS before cutover, and fix regardless)**

### 3.3 ASP.NET Core app (CORE)

```csharp
if (catalogConfig.UseAzureActiveDirectory)            // reads "AppSettings:UseAzureActiveDirectory"
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("Azure:ActiveDirectory"));
else
    builder.Services.AddAuthentication("Cookies").AddCookie("Cookies", o => {
        o.LoginPath = "/Account/Login"; o.LogoutPath = "/Account/Logout"; o.AccessDeniedPath = "/Account/AccessDenied"; });
...
app.UseHttpsRedirection(); app.UseStaticFiles(); app.UseRouting(); app.UseSession();
app.UseAuthentication(); app.UseAuthorization();
```

(`Program.cs` lines 73-91, 113-120; `Configuration/CatalogConfiguration.cs` line 20.)

- `AccountController.Login` (POST) signs in `ClaimTypes.Name` and `ClaimTypes.NameIdentifier` for **any** non-empty credentials, with no `[ValidateAntiForgeryToken]`. It does check `Url.IsLocalUrl(returnUrl)`, which blocks open redirects (confirmed in the spike). `Logout` is a GET that only calls `SignOutAsync("Cookies")`.
- `/Account/Login` stays reachable in Entra ID mode. The resulting local cookie **isn't** accepted for `[Authorize]` there (spike: 302 challenge), so it's not a bypass. It's still a confusing duplicate path and should be removed.
- `[Authorize]` and `[ValidateAntiForgeryToken]` are on the Catalog Create, Edit and Delete actions. API controllers have no authorization at all (`BrandsController` includes `HttpDelete`; `ImageUploadController` accepts POSTs).
- Data Protection uses defaults. The key ring is written to the user profile or container filesystem with no XML encryptor (the spike logs `No XML encryptor configured`), so it's lost when a container is recreated and isn't shared between replicas.
- There's no `UseForwardedHeaders`, even though nginx sends `X-Forwarded-Proto`/`X-Forwarded-For`.

---

## 4. Dependency map: System.Web → ASP.NET Core

### 4.1 Session

| System.Web dependency | ASP.NET Core equivalent | Notes |
|-----------------------|-------------------------|-------|
| `<sessionState mode="InProc">` | `AddDistributedMemoryCache()` + `AddSession()` | Dev and single instance only |
| `mode="Custom"` + `RedisSessionStateProvider` | `AddStackExchangeRedisCache(o => o.Configuration = ...)` + `AddSession()` | Key format and serialization are **not** compatible with the System.Web Redis provider, so the two can't share entries |
| `mode="SQLServer"` | `AddDistributedSqlServerCache()` | Not used today |
| `SessionStateModuleAsync` (locking, async provider) | `SessionMiddleware` (**no locking**) | Code that relied on serialised requests needs an explicit concurrency strategy |
| `Session_Start` in `Global.asax` | No direct equivalent. Use middleware that initialises keys when `!session.Keys.Contains(...)`, or compute lazily | See §6.1 |
| `HttpContext.Current.Session["k"]` (object) | `HttpContext.Session.GetString/SetString`, or JSON helpers over `byte[]`; inject `IHttpContextAccessor` outside controllers | `DateTime` must be round-tripped as ISO-8601 |
| `ASP.NET_SessionId` cookie (20 min, not `Secure`) | `.AspNetCore.Session` (Data Protection-encrypted; `IdleTimeout` 30 min in CORE; `SecurePolicy` defaults to SameAsRequest) | Timeout already differs (20 → 30 min) |
| Session available to WebForms pages via `EnableSessionState` | Razor Pages / MVC: `HttpContext.Session` after `await session.LoadAsync()` (recommended to avoid sync I/O) | |
| Incremental bridge: System.Web adapters **remote app session** (`Microsoft.AspNetCore.SystemWebAdapters`) | Lets CORE read System.Web session over HTTP during migration | **Not recommended here.** Two diagnostic keys don't justify the extra dependency and latency |

### 4.2 Authentication and authorization

| System.Web / OWIN dependency | ASP.NET Core equivalent | Notes |
|------------------------------|-------------------------|-------|
| `Microsoft.Owin.Security.OpenIdConnect` 4.0.1 | `Microsoft.Identity.Web` (`AddMicrosoftIdentityWebApp`) on top of `Microsoft.AspNetCore.Authentication.OpenIdConnect` | CORE already references `Microsoft.Identity.Web` 3.0.1 |
| `Microsoft.Owin.Security.Cookies` (`.AspNet.Cookies`, MachineKey protector) | `AddCookie` (`.AspNetCore.Cookies`, Data Protection) | Formats differ. Can only be shared via `Microsoft.Owin.Security.Interop` (§5.3) |
| `app.SetDefaultSignInAsAuthenticationType("Cookies")` | `AuthenticationOptions.DefaultSignInScheme` (Identity.Web sets it to `Cookies`) | Same scheme name `"Cookies"` on both sides |
| OWIN `RedirectUri = "/"` (reply to site root) | `CallbackPath = "/signin-oidc"` (default) | New reply URL must be registered in Entra ID (§7 R-A4) |
| `PostLogoutRedirectUri` | `SignedOutRedirectUri`; `/signout-callback-oidc` must be registered | |
| `AccountController.EndSession` (front-channel logout) | `RemoteSignOutPath = "/signout-oidc"` (OIDC handler) | Update the front-channel logout URL in Entra ID |
| `Challenge(OIDC)` in `AccountController.SignIn` / `Site.Master` / `Page_Load` | `[Authorize]` / `RequireAuthorization()` with automatic challenge, or `Challenge(OpenIdConnectDefaults.AuthenticationScheme)` | Replace the WebForms imperative checks with declarative Razor Pages conventions (`AuthorizeFolder("/Catalog")`) |
| `SignOut(OIDC, Cookies)` | `SignOut(new AuthenticationProperties{RedirectUri="/"}, CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme)` or `Microsoft.Identity.Web.UI` `/MicrosoftIdentity/Account/SignOut` | CORE currently only clears the local cookie |
| `Request.IsAuthenticated`, `Context.User.Identity.Name` | `User.Identity.IsAuthenticated`, `User.Identity.Name` (`ClaimsPrincipal`) | Name claim: Identity.Web maps `name`; OWIN used `ClaimTypes.Name` from `name` / `unique_name`. Compare the claim sets (§7 R-A6) |
| `AuthenticationMiddleware` (synthetic identity) | Development-only authentication handler that is **only** registered when `IHostEnvironment.IsDevelopment()`, or the local cookie login | Must not be reachable in Production |
| `<asp:LoginView>`, `<asp:LoginStatus>` | `_LoginPartial` partial / view component checking `User.Identity.IsAuthenticated` | CORE's `_LoginPartial` must be fixed first |
| `AuthenticationFailed` → `/Error?message=` | `OpenIdConnectEvents.OnRemoteFailure` → log + redirect to a static error page with a correlation ID | Don't reflect exception text |
| `ConfigurationManager.AppSettings[...]` / flat environment variables | `IConfiguration` sections `AppSettings:*`, `Azure:ActiveDirectory:*` (environment: `AppSettings__UseAzureActiveDirectory`, `Azure__ActiveDirectory__ClientId`, ...) | Compose file currently uses the flat names (§7 R-C1) |
| `machineKey` (auth cookie, antiforgery, ViewState) | Data Protection: `PersistKeysToAzureBlobStorage` + `ProtectKeysWithAzureKeyVault` + `SetApplicationName("eShop")` | Needed before running more than one CORE replica, and to survive restarts |
| MVC 5 `[ValidateAntiForgeryToken]` / `@Html.AntiForgeryToken()` | ASP.NET Core antiforgery (automatic for `<form method="post">` tag helpers; `[ValidateAntiForgeryToken]` or `AutoValidateAntiforgeryTokenAttribute` globally) | Tokens not interchangeable (§7 R-X1) |
| ViewState MAC / EventValidation (WebForms) | None. Razor Pages validate antiforgery automatically | |
| IIS/HTTP.sys scheme/host | `UseForwardedHeaders` (`XForwardedFor` + `XForwardedProto`, `KnownProxies`/`KnownNetworks` for nginx) placed **first** in the pipeline | Needed for correct `redirect_uri` and `Secure` cookies |
| `OutputCacheAttribute` global filter (M-MVC `FilterConfig.cs`) | `[ResponseCache(NoStore = true)]` or response headers on authenticated pages | Keeps personalised pages from being cached by nginx/CDN |

---

## 5. Target design

### 5.1 Identity contract

- **Authority:** Entra ID via OIDC, same tenant, one app registration per *audience-facing host*. Recommended: a **single** registration for the public nginx host so that all backends share one reply-URL set.
- **Canonical claims:** `oid` (stable user key), `tid`, `name`, `preferred_username`. Code must use `oid`, not `Name`, as the user identifier. Today no code persists a user ID, so this is forward-looking.
- **Authorization:** One policy `CatalogWriter = RequireAuthenticatedUser()` initially (parity with today). Apply it to MVC catalog write actions, Razor Pages `/Catalog/Create|Edit|Delete`, and the write APIs (`DELETE /api/brands/{id}`, `POST /api/imageupload/upload`). Consider a `FallbackPolicy` of authenticated user with explicit `[AllowAnonymous]` on read pages, so new endpoints are secure by default.
- **Non-production:** a `Development`-only auth path. The local "any password" cookie login is removed, or compiled or registered only in Development.

### 5.2 Session

- Delete `Session_Start` and the session-backed footer from the port. Replace it in CORE with a request-scoped value:
  - `MachineName` → `Environment.MachineName` (already done).
  - `SessionStartTime` → either drop it, or keep the diagnostic by setting it lazily in middleware (`if (!session.Keys.Contains("SessionStartTime")) session.SetString(..., DateTime.UtcNow.ToString("O"))`).
- If session is kept at all, register a real `IDistributedCache` (Azure Cache for Redis via `AddStackExchangeRedisCache`, instance name `eshop-core:`) before scaling CORE past one replica. Otherwise remove `AddSession`/`UseSession` entirely. **Recommended: remove**, because nothing reads it and it adds a cookie and data-protection dependency for no value.
- Don't try to share session between System.Web and CORE. The only shared value is diagnostic.

### 5.3 Cookie strategy during coexistence (decision)

| Option | What it is | Pros | Cons | Recommendation |
|--------|------------|------|------|----------------|
| **A. Path-affine journeys, independent cookies** | Each backend has its own cookie. nginx routes entire auth-bearing paths (`/Catalog/Create`, `/Catalog/Edit/*`, `/Catalog/Delete/*`, `/Account/*`, `/signin-oidc`, `/signout-*`) to one backend at a time. Entra ID SSO makes a second sign-in silent. | No cross-stack crypto, simple rollback | Needs route-based (not random weighted) splitting for write paths. One extra round trip to Entra ID on first hop | **Recommended** |
| B. Shared cookie via `Microsoft.Owin.Security.Interop` | System.Web apps use `DataProtectorShim` + `ChunkingCookieManager` over a shared ASP.NET Core Data Protection key ring (blob + Key Vault). Same cookie name, `SetApplicationName`, `AuthenticationType="Cookies"` | True single sign-on across stacks, random weighting works | Code changes in both .NET Framework apps (Katana 4.x, `Microsoft.AspNetCore.DataProtection.Extensions` on net472). Claim-shape parity required. Rollback must keep the shared ring | Only if random per-request weighting of *authenticated* routes is a hard requirement |
| C. System.Web adapters remote authentication | CORE asks System.Web to authenticate the request | Incremental | Adds hop latency, couples CORE availability to M-MVC, doesn't help WebForms postbacks | Not recommended |

With **Option A**, sticky routing isn't required for correctness, but nginx must not split a single form's GET and POST across backends. That's the main reason to move from `upstream catalog_migration` weighting to route-level `location` blocks for write paths (or `hash $cookie_… consistent` / `ip_hash` affinity as an interim step).

### 5.4 WebForms-specific approach

WebForms can't run on ASP.NET Core, so M-WF is **rewritten** as Razor Pages rather than ported. A scaffold exists on branch `devin/1788399644-webforms-port-1-scaffold` (`eShopPortedWebForms`). The mapping for session and auth:

| WebForms construct | Razor Pages replacement |
|--------------------|------------------------|
| `Page_Load` `if (!Request.IsAuthenticated) Challenge(...)` | `options.Conventions.AuthorizeFolder("/Catalog", "CatalogWriter")` + `AllowAnonymousToPage("/Catalog/Details")` |
| Postback event handlers (`Save_Click`, `Delete_Click`) | `OnPostAsync` handlers, authorized by the page convention (closes the §3.2 gap) and antiforgery-validated automatically |
| `<asp:LoginView>` / `<asp:LoginStatus>` | Shared `_LoginPartial` |
| `Site.Master` session label | `_Layout` diagnostic (§5.2) |
| `PicUploader.asmx` (`HttpContext.Current.Request.Files`) | Minimal API / controller with `IFormFile`, `RequireAuthorization("CatalogWriter")`, antiforgery for form posts |
| Route table (`Catalog/Edit/{id}` etc.) | Page routes `@page "{id:int}"`, **keeping the same URLs** so nginx can switch per path |

---

## 6. Phased plan

Each phase has exit criteria. Don't start the next phase until they're met.

### Phase 0: Fix CORE readiness (no traffic change)

1. Configuration: make the CORE environment names match the configuration schema. In `docker-compose.override.yml` and K8s/ACI, use `AppSettings__UseAzureActiveDirectory`, `AppSettings__UseMockData`, `Azure__ActiveDirectory__ClientId`, `Azure__ActiveDirectory__TenantId`, `ConnectionStrings__CatalogDBContext`. Or add flat-name aliases in `CatalogConfiguration`. Add a startup log line with the *effective* auth mode.
2. Add `UseForwardedHeaders` (first middleware), with nginx as a known proxy. Terminate TLS at nginx, or serve HTTPS end to end. **OIDC over plain HTTP won't work in browsers** outside `localhost` (see Appendix A).
3. Persist Data Protection keys (Azure Blob + Key Vault; `SetApplicationName`).
4. Auth surface:
   - Restrict the local cookie login to Development only (or remove it).
   - Add `[ValidateAntiForgeryToken]` to `Login`.
   - Make `Logout` a POST that signs out of both Cookies and OIDC.
   - Add `AddMicrosoftIdentityUI()` (optional).
   - Put `CatalogWriter` on the write APIs.
   - Add `OnRemoteFailure` handling.
5. UI: fix `_LoginPartial` (actions `Logout`/`Login`, remove `Manage` and `Register`), and render it regardless of `ViewBag.UseAzureActiveDirectory`.
6. Session: remove `AddSession`/`UseSession`, or add a distributed cache (§5.2).
7. Fix the CORE `Dockerfile` base images to `8.0`.

**Exit:** the auth test matrix (§8) passes against CORE alone, in Entra ID mode, behind nginx over HTTPS.

### Phase 1: Entra ID app registration and configuration

- Add reply URLs `https://<public-host>/signin-oidc`, front-channel logout `https://<public-host>/signout-oidc`, and post-logout `https://<public-host>/signout-callback-oidc`. **Keep** the existing root reply URLs used by the OWIN apps until Phase 5.
- Put the client ID and tenant in Key Vault (`Azure:KeyVaultName`, which CORE already supports). Keep the ID-token-only flow (no client secret needed) unless downstream APIs are added.

**Exit:** a test user can complete sign-in and sign-out on CORE through the public host.

### Phase 2: Fix the M-MVC and M-WF baseline (optional but recommended)

- Fix WebForms postback authorization (§3.2) and add antiforgery to M-MVC. This is a security fix in its own right. It also means that during coexistence neither backend accepts writes that the other would reject.
- Align OWIN package versions (`packages.config` vs `HintPath`).
- Add an explicit `<machineKey>` (shared via config or secret) if M-MVC or M-WF run with more than one instance (Service Fabric `InstanceCount=-1`).

### Phase 3: Route-based canary for read-only paths

- Move `/Catalog/Index` and `/Catalog/Details/*` (anonymous) with the existing 25/50/75/100 weights. No auth state is involved, so these can be split randomly.
- Keep `/Catalog/Create|Edit|Delete`, `/Account/*`, `/signin-oidc`, `/signout-*`, `/uploadimage`, `/Catalog/PicUploader.asmx` and `/api/*` write verbs pinned to **one** backend.

**Exit:** error rate and latency are at parity for 24h at 100% read traffic.

### Phase 4: Cut over auth-bearing journeys as a unit

- Switch all auth-bearing locations listed above to CORE in **one** nginx change. Users with an OWIN cookie get a silent Entra ID round trip to establish a CORE cookie. Plan for a one-time re-login if the Entra ID session has expired.
- For WebForms, do the same once the Razor Pages port is feature-complete, keeping URLs identical.

**Exit:** no increase in `/signin-oidc` failures, 400s from antiforgery, or 302 loops for 48h.

### Phase 5: Decommission

- Remove the OWIN reply URLs from Entra ID once logs show no `/` callback traffic.
- Remove Redis/session packages and the `SessionStateModuleAsync` registration. Retire the System.Web containers.

---

## 7. Cutover risk register

Severity: **H** = blocks users or is a security exposure; **M** = user-visible degradation; **L** = operational or cosmetic.

### Authentication (A)

| Id | Risk | Sev | Evidence | Mitigation |
|----|------|-----|----------|------------|
| R-A1 | OWIN `.AspNet.Cookies` isn't understood by CORE (and vice versa). Under weighted `/Catalog/` routing a signed-in user randomly becomes anonymous | H | nginx `upstream catalog_migration` weights 5001/5002; different cookie names and protectors | Option A path-affine routing (§5.3) or Option B interop |
| R-A2 | CORE challenge in cookie mode redirects to `/Account/Login`. nginx sends `/Account/*` to M-MVC, which has no `Login` action → 404 | H | `Program.cs` `LoginPath`; `nginx.conf` `location /`; M-MVC `AccountController` has only `SignIn`, `SignOut`, `EndSession` | Route `/Account/*` with the auth-bearing group; use Entra ID mode |
| R-A3 | Flat environment variables ignored by CORE → CORE silently runs in **local any-password cookie mode** and with mock data even when `ESHOP_USE_AZURE_AD=True` | H | Spike run 1 (Appendix A); `CatalogConfiguration.cs` line 20; `docker-compose.override.yml` lines 53-62 | Phase 0 step 1; fail fast in Production when Entra ID config is missing |
| R-A4 | Reply URL mismatch: OWIN replies to `/`, CORE replies to `/signin-oidc`; behind nginx without forwarded headers CORE emits `http://…/signin-oidc` → `AADSTS50011` | H | Spike run 2 | Register new URLs (Phase 1); `UseForwardedHeaders` |
| R-A5 | Correlation and nonce cookies are `Secure; SameSite=None`. Over plain HTTP (nginx `listen 80`, compose `http://…:5002`) browsers drop them → "Correlation failed" | H | Spike run 2 `Set-Cookie` headers | HTTPS at the edge + forwarded headers |
| R-A6 | Claim shape differs (OWIN vs Identity.Web claim mapping; synthetic identity has no name) → `User.Identity.Name` blank or different in UI | L | `AuthenticationMiddleware.cs`; Identity.Web defaults | Snapshot claims from both stacks for the same test user; align `NameClaimType` |
| R-A7 | Local "any credentials" login or synthetic auth reaches Production | H | `AccountController.Login` line 19; `Login.cshtml` "Use any username and password" | Development-only registration; startup guard |
| R-A8 | Sign-out doesn't end the Entra ID session in CORE → user appears signed out, then is silently signed back in. Leaving M-MVC's front-channel `EndSession` in place clears only one stack | M | `AccountController.Logout` line 47; `/MicrosoftIdentity/Account/SignOut` 404 | Sign out of both schemes; register `/signout-oidc` front-channel; keep both until Phase 5 |
| R-A9 | Anonymous write APIs on CORE (`DELETE /api/brands/{id}`, `POST /api/imageupload/upload`) — nginx `location /api/` already routes 100% to CORE | H | Spike: `DELETE /api/brands/1` → 200 anonymous | Authorization policy on write verbs before Phase 3 |
| R-A10 | WebForms postbacks not authorization-gated (`Edit` only checks on first load; `Challenge` doesn't stop event handlers) | H | `Catalog/Edit.aspx.cs` line 28-33, `Delete.aspx.cs`, `Create.aspx.cs` | Phase 2 fix; Razor Pages conventions in the port |
| R-A11 | `AuthenticationFailed` reflects exception text in the URL; no `/Error` route; `customErrors mode="Off"` exposes stack traces | M | `Startup.Auth.cs` (MVC line 60, WF line 43); `Web.config` (MVC line 56, WF line 50) | `OnRemoteFailure` + static error page in CORE; don't port the pattern |
| R-A12 | Auto-generated `machineKey` per container: OWIN cookies are invalidated on redeploy and not valid across Service Fabric instances | M | No `<machineKey>` in any `Web.config`; SF `InstanceCount=-1` | Explicit `machineKey` or affinity until decommission |
| R-A13 | CORE Data Protection keys are ephemeral → every CORE redeploy or scale-out logs everyone out and invalidates antiforgery tokens | H | Spike log `No XML encryptor configured`; no `AddDataProtection` | Phase 0 step 3 |
| R-A14 | OWIN package-version drift (4.2.2 vs 4.0.1) → unknown SameSite behaviour on the System.Web side during coexistence | M | `packages.config` vs `.csproj` `HintPath` | Phase 2 alignment; verify deployed DLL versions |
| R-A15 | Login CSRF: CORE `Login` POST has no antiforgery. `Logout` is a GET | M | Spike: POST without token → 302 + cookie | `[ValidateAntiForgeryToken]`; POST logout |

### Cross-stack request integrity (X)

| Id | Risk | Sev | Evidence | Mitigation |
|----|------|-----|----------|------------|
| R-X1 | Antiforgery mismatch: form GET served by M-MVC (no token) and POST routed to CORE (validates) → HTTP 400. Tokens from L-MVC (`machineKey`) are never valid in CORE | H | CORE `CatalogController` lines 70, 120, 162; M-MVC has none | Never split a GET/POST pair; route write paths as a unit |
| R-X2 | Form field and route shape differ (`[Bind(Include=...)]`, `TempImageName`) between MVC 5 and CORE for the same URL | M | M-MVC `CatalogController` `[Bind]`; CORE models | Contract test per write route before Phase 4 |
| R-X3 | WebForms ViewState/postback posts to `/Catalog/Edit/{id}` land on a Razor Page → 400 (antiforgery) or model-binding failure | H | WebForms postbacks post back to the same URL | Switch WebForms write paths as a unit; drain in-flight forms (short window) |

### Session (S)

| Id | Risk | Sev | Evidence | Mitigation |
|----|------|-----|----------|------------|
| R-S1 | InProc session lost on recycle, redeploy, or node switch → footer diagnostic resets (cosmetic today, but a trap if anyone adds business data to session before cutover) | L | `Web.config` InProc; SF multi-instance | Freeze new session usage; code-review rule |
| R-S2 | Enabling the commented Redis block with `SessionStateModuleAsync` may fail: the async module needs an async provider (`SessionStateStoreProviderAsyncBase`), and `RedisSessionStateProvider` 4.0.1 is a classic provider **(inference; verify)** | M | `Web.config` lines 65-73 + 226-227 | Don't enable Redis on System.Web; not needed with this plan |
| R-S3 | ASP.NET Core session has no locking → concurrent requests overwrite each other | L | Framework behaviour | Only matters if session is reintroduced; prefer DB or claims |
| R-S4 | `AddSession` without a real distributed cache → per-instance memory cache; scaling CORE out loses session | M | `Program.cs` line 89; no `AddStackExchangeRedisCache` | Remove session or add Redis (§5.2) |
| R-S5 | Timeout semantics change (20 → 30 min idle; auth cookie sliding 14 days default in both stacks) | L | `Program.cs` line 91; System.Web default | Document; set explicitly |
| R-S6 | `SessionStartTime` meaning changed silently in CORE (now render time) | L | CORE `_Layout.cshtml` line 42 | Relabel or implement lazily |

### Operations and config (C)

| Id | Risk | Sev | Evidence | Mitigation |
|----|------|-----|----------|------------|
| R-C1 | Environment variable naming mismatch across compose, K8s and ACI for CORE | H | See R-A3 | One config schema; smoke test that logs effective settings |
| R-C2 | CORE `Dockerfile` uses `sdk:6.0`/`aspnet:6.0` for a `net8.0` project → build or run failure in the container path | H | `eShopModernized/src/eShopCoreModernized/Dockerfile` lines 1, 6 | Bump to 8.0 in Phase 0 |
| R-C3 | `/api/health` doesn't check auth dependencies (Entra ID metadata, Key Vault, key ring), so `migrate-traffic.sh` can promote a CORE that can't sign anyone in | M | `migrate-traffic.sh` polls `/api/health` only | Add a synthetic sign-in probe to the promotion gate |
| R-C4 | Rollback after Phase 4: users now hold only `.AspNetCore.Cookies`; returning to OWIN forces re-login. Entra ID reply URLs must still include the OWIN ones | M | `ROLLBACK-PROCEDURES.md` treats auth failure as a rollback trigger | Keep OWIN reply URLs until Phase 5; communicate a possible one-time re-login |
| R-C5 | Docs are inconsistent (.NET 6 vs `net8.0`; "migration complete" while auth routes are still split) | L | `MIGRATION-COMPLETE.md`, `README-Migration.md` | Update after Phase 4 |

---

## 8. Test matrix (run per backend and through nginx)

| # | Scenario | Expected on CORE |
|---|----------|------------------|
| 1 | Anonymous GET `/Catalog/Index`, `/Catalog/Details/1` | 200, no auth cookie issued |
| 2 | Anonymous GET `/Catalog/Create` | 302 to `login.microsoftonline.com` with `redirect_uri=https://<public-host>/signin-oidc` |
| 3 | Complete OIDC sign-in | 302 back to original URL; `.AspNetCore.Cookies` `Secure; HttpOnly; SameSite=Lax` |
| 4 | Authenticated Create, Edit and Delete round trip | 200/302; antiforgery token present and validated |
| 5 | POST without antiforgery token | 400 |
| 6 | Anonymous `DELETE /api/brands/1`, `POST /api/imageupload/upload` | 401/302 (currently 200 / accepted) |
| 7 | Sign out | Local cookie cleared **and** redirect to Entra ID end-session; subsequent `/Catalog/Create` prompts again |
| 8 | Front-channel logout from another app | `/signout-oidc` clears the CORE cookie |
| 9 | Restart CORE container / scale to 2 replicas | Existing auth cookie and antiforgery tokens still valid (persisted key ring) |
| 10 | Expired Entra ID session with valid local cookie | Local cookie valid until expiry; then silent or interactive re-auth |
| 11 | `returnUrl=https://evil.example/` on login | Redirects to `/` only (already passes) |
| 12 | Production environment with Entra ID config missing | App fails to start (fail fast), doesn't fall back to local login |
| 13 | Mixed routing: sign in on M-MVC, open `/Catalog/Edit/1` | Lands on the same backend as sign-in (per §5.3), no 404 or 400 |
| 14 | WebForms (port): anonymous POST to `/Catalog/Edit/1` | 302/401, no data change |

---

## 9. Observability and rollback triggers

- **Metrics:**
  - `/signin-oidc` 4xx/5xx rate
  - `OnRemoteFailure` count
  - antiforgery validation failures (`Microsoft.AspNetCore.Antiforgery` logs)
  - 302 loops (same client hitting `/Catalog/Create` → Entra ID → back more than 3 times in a minute)
  - `Data Protection key not found` errors
  - 404 on `/Account/*`
- **Rollback triggers** (extends `ROLLBACK-PROCEDURES.md`): sign-in failure rate above 2% over 5 min, antiforgery 400s above 1% of POSTs, or any `key not found` spike after deploy.
- **Rollback mechanics:** revert the nginx config for the auth-bearing location group only. OWIN reply URLs are still registered, so users re-authenticate silently via Entra ID SSO.

---

## 10. Assumptions and open decisions

**Assumptions**
1. The target for both apps is the existing `eShopCoreModernized` (MVC) plus a Razor Pages port for WebForms, not Blazor or SPA.
2. Entra ID stays the identity provider. There are no local user accounts (the "any password" login is demo scaffolding).
3. The two session keys are diagnostic only, and nobody needs session continuity across the cutover.
4. The public entry point is nginx, and TLS will be terminated there (or end to end) before authenticated traffic moves to CORE.
5. nginx `localhost:5001` is M-MVC (`MIGRATION-COMPLETE.md` calls it "Modernized .NET Framework"). WebForms isn't behind nginx yet. The checked-in `nginx.conf` is at the **25%** phase, while `MIGRATION-COMPLETE.md` says `/Catalog/*` is 100% CORE. Either way the risks apply: at 100%, R-A2 (`/Account/Login` → M-MVC 404) and R-A9 become the dominant ones.
6. Brief forced re-authentication at the Phase 4 switch is acceptable. With Entra ID SSO it's usually silent.

**Open decisions**
1. Option A vs B (§5.3). Is random per-request weighting of authenticated routes actually required?
2. Remove session from CORE entirely, or keep it with Redis?
3. Single Entra ID app registration for all backends vs one per app?
4. Authorization beyond "authenticated user" (for example an app role `Catalog.Write`)? This is a good moment to introduce it, since the policy is being rewritten anyway.

---

## Appendix A: Runtime spike evidence (CORE)

I built CORE with .NET SDK 8.0.425 (`dotnet build`, 0 errors) and ran it with `ASPNETCORE_ENVIRONMENT=Production` on `http://localhost:5602`, then probed it with `curl`.

**Run 1: compose-style flat environment variables** (`UseAzureActiveDirectory=True UseMockData=False`)

```
GET  /Catalog/Create (anon)          -> 302 Location: /Account/Login?ReturnUrl=%2FCatalog%2FCreate   (cookie mode => env var ignored)
GET  /                               -> 200, catalog rendered from mock data                            (UseMockData=False ignored)
GET  /Account/Login                  -> 200, Set-Cookie: .AspNetCore.Antiforgery.*; samesite=strict; httponly
POST /Account/Login (no token, any creds, returnUrl=https://evil.example/)
                                     -> 302 Location: /  Set-Cookie: .AspNetCore.Cookies=...; samesite=lax; httponly (no Secure)
GET  /Catalog/Create (with cookie)   -> 200
POST /Account/LogOff                 -> 404   (target of _LoginPartial logout form)
GET  /Manage/Index, /Account/Register -> 404, 404
DELETE /api/brands/1 (anon)          -> 200
(no .AspNetCore.Session cookie observed on any response)
log: "No XML encryptor configured. Key {...} may be persisted to storage in unencrypted form."
log: "Failed to determine the https port for redirect."
```

**Run 2: hierarchical keys** (`AppSettings__UseAzureActiveDirectory=true Azure__ActiveDirectory__ClientId=<dummy> Azure__ActiveDirectory__TenantId=common`)

```
GET /Catalog/Create (anon) -> 302 https://login.microsoftonline.com/common/oauth2/v2.0/authorize?...
      &redirect_uri=http%3A%2F%2Flocalhost%3A5602%2Fsignin-oidc&response_type=id_token&response_mode=form_post
      Set-Cookie: .AspNetCore.OpenIdConnect.Nonce.*; path=/signin-oidc; secure; samesite=none; httponly
      Set-Cookie: .AspNetCore.Correlation.*;         path=/signin-oidc; secure; samesite=none; httponly
GET /Catalog/Create with X-Forwarded-Proto: https, Host: shop.example.com
      -> redirect_uri=http%3A%2F%2Fshop.example.com%2Fsignin-oidc   (forwarded proto ignored)
GET /Account/Login                      -> 200 (local login still reachable)
POST /Account/Login then GET /Catalog/Create -> 302 (local cookie not accepted in Entra ID mode)
GET /Account/Logout                     -> 302 /, clears .AspNetCore.Cookies only (no Entra ID end-session)
GET /MicrosoftIdentity/Account/SignOut  -> 404
```

I didn't run the .NET Framework apps (M-MVC, M-WF, L-*). They need Windows/IIS. Their findings come from reading the code and config, and the ones marked *(inference; verify)* should be confirmed on a Windows host before Phase 2.
