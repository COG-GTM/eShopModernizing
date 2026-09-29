# eShopLegacyMVC: System.Web inventory and ASP.NET Core migration plan

Scope: `eShopLegacyMVCSolution/src/eShopLegacyMVC` (ASP.NET MVC 5 + Web API 2 on .NET Framework 4.7.2) plus its one
project dependency, `eShopLegacyMVCSolution/eShopLegacy.Utilities`.

Target: ASP.NET Core on .NET 8 (same TFM as the existing `eShopModernized/src/eShopCoreModernized` project), single
host serving both MVC views and the former Web API endpoints.

## Assumptions

1. "The MVC app" means `eShopLegacyMVC`. The already-ported projects (`eShopPorted`, `eShopModernized`,
   `eShopModernizedMVCSolution`) are treated as prior art, not as the thing being planned.
2. "System.Web dependency" is read broadly: anything that only works under the classic ASP.NET/IIS pipeline —
   `System.Web.*` namespaces and assemblies (`System.Web.Mvc`, `System.Web.Http`, `System.Web.Optimization`,
   `System.Web.Routing`, `System.Web.Hosting`, `System.Web.WebPages`), `HttpContext.Current`, `Global.asax`,
   HTTP modules/handlers, `Web.config`-driven settings read via `ConfigurationManager`, and System.Web-bound
   integration packages (Autofac.Mvc5, Autofac.WebApi2, Microsoft.ApplicationInsights.Web,
   Microsoft.AspNet.SessionState/TelemetryCorrelation).
3. The ORM swap (EF6 -> EF Core) is **out of scope** for this plan. EF 6.4.x runs on .NET 8, so the first cut keeps EF6
   and only removes its dependency on `Web.config`. This isolates System.Web risk from data-access risk; EF Core can
   follow (see `eShopModernized` for a worked example).
4. Autofac is kept (via `Autofac.Extensions.DependencyInjection`) to avoid re-validating lifetimes in the same change.
5. Existing public URLs and JSON shapes must be preserved, because nginx (`nginx*.conf`, strangler-fig rollout) routes
   the same paths to legacy and modernized backends.
6. Nothing was compiled: this box is Linux with no .NET Framework/MSBuild, so the inventory is from static reading and
   `rg`. Line numbers are against `main` at `ad14023`.

## How the inventory was produced

```bash
cd eShopLegacyMVCSolution/src/eShopLegacyMVC
rg -n "System\.Web|HttpContext|HttpApplication|Server\.MapPath|HostingEnvironment|ConfigurationManager|GlobalConfiguration|RouteTable|BundleTable|GlobalFilters|DependencyResolver|ApiController|IHttpActionResult|HttpResponseMessage|HttpStatusCodeResult|HttpNotFound|Scripts\.Render|Styles\.Render|Bind\(Include" \
  --glob '*.cs' --glob '*.cshtml' --glob '*.config' --glob '*.asax'
```

Re-running the same command against the migrated project is the "done" check: it should return nothing except
ASP.NET Core equivalents (`HttpContext` on `ControllerBase`, etc.).

## Inventory summary

| System.Web surface | Where used | ASP.NET Core equivalent |
|---|---|---|
| `HttpApplication` / `Global.asax` lifecycle (`Application_Start`, `Session_Start`, `Application_BeginRequest`) | `Global.asax`, `Global.asax.cs` | `Program.cs` (`WebApplication.CreateBuilder`) + custom middleware |
| `HttpContext.Current` (static) | `Global.asax.cs:43-44,112`, `Views/Shared/_Layout.cshtml:35` | Injected `HttpContext` / `IHttpContextAccessor`; `Context` in Razor |
| Session state (`Session[...]`, `SessionStateModuleAsync`, `<sessionState mode="InProc">`) | `Global.asax.cs:41-45`, `_Layout.cshtml:35`, `Web.config:33,91-92` | `AddDistributedMemoryCache()` + `AddSession()` + `UseSession()`; `ISession.GetString/SetString` |
| `System.Web.Mvc` (Controller, ActionResult, filters, `SelectList`, `[Bind]`, `HttpStatusCodeResult`, `HttpNotFound`, `Server.MapPath`, `Request.Url`) | `Controllers/CatalogController.cs`, `Controllers/PicController.cs`, `Controllers/Api/CatalogController.cs`, `App_Start/FilterConfig.cs` | `Microsoft.AspNetCore.Mvc` (`Controller`, `IActionResult`, `BadRequest()`, `NotFound()`, `IWebHostEnvironment`, `Request.Scheme`) |
| `System.Web.Http` (Web API 2: `ApiController`, `IHttpActionResult`, `HttpResponseMessage`, `HttpConfiguration`) | `Controllers/WebApi/BrandsController.cs`, `Controllers/WebApi/FilesController.cs`, `App_Start/WebApiConfig.cs` | `ControllerBase` + `[ApiController]` + attribute routes; `ActionResult<T>`, `FileStreamResult` |
| `System.Web.Routing` / `RouteTable` / `MapMvcAttributeRoutes` / `UrlParameter.Optional` | `App_Start/RouteConfig.cs`, `Global.asax.cs:33` | `app.MapControllerRoute(...)`, `app.MapControllers()` |
| `System.Web.Optimization` bundling (`BundleTable`, `Scripts.Render`, `Styles.Render`) | `App_Start/BundleConfig.cs`, `_Layout.cshtml:7-8,43-44`, `Create.cshtml:103`, `Edit.cshtml:104` | Static files in `wwwroot` + `<link>/<script asp-append-version="true">` (optionally `LigerShark.WebOptimizer.Core`) |
| `System.Web.Hosting.HostingEnvironment` | `Models/Infrastructure/CatalogDBInitializer.cs:11,345` | `IWebHostEnvironment.ContentRootPath` |
| `Web.config` settings via `ConfigurationManager` | `Global.asax.cs:68,85`, `CatalogDBInitializer.cs:29`, `CatalogDBContext.cs:10` (`name=CatalogDBContext`) | `appsettings.json` + `IConfiguration` / `IOptions<T>` |
| System.Web Razor host (`Views/Web.config`, `System.Web.Mvc.WebViewPage`, `System.Web.Mvc.Html` helpers, `System.Web.HtmlString`) | `Views/Web.config`, all `.cshtml` | `Views/_ViewImports.cshtml`, `RazorPage<T>`, `IHtmlHelper`, `Microsoft.AspNetCore.Html.HtmlString` |
| HTTP modules/handlers (TelemetryCorrelation, ApplicationInsightsWebTracking, ExtensionlessUrlHandler, BlockViewHandler) | `Web.config:34-37,84-101`, `Views/Web.config:29-34`, `ApplicationInsights.config:104-110` | Middleware pipeline; `Microsoft.ApplicationInsights.AspNetCore`; W3C trace context is built in |
| DI integration bound to System.Web (`Autofac.Integration.Mvc`, `Autofac.Integration.WebApi`, `DependencyResolver`, `GlobalConfiguration.Configuration.DependencyResolver`) | `Global.asax.cs:2-3,65-66,74-78` | `builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())` |
| Dead `using System.Web;` (no symbols used) | `Models/CatalogBrand.cs:4`, `Models/CatalogType.cs:4`, `Models/CatalogItemHiLoGenerator.cs:5`, `Models/Infrastructure/PreconfiguredData.cs:4`, `CatalogDBInitializer.cs:10`, `App_Start/FilterConfig.cs:1` | Delete |

Adjacent non-System.Web blockers that surface in the same files and must move at the same time:

- `System.Runtime.Remoting.Messaging` (`BrandsController.cs:7`, unused) — namespace does not exist on .NET Core.
- `BinaryFormatter` (`eShopLegacy.Utilities/Serializing.cs`, used by `FilesController`) — throws
  `NotSupportedException` by default in ASP.NET Core on .NET 8 and is removed in .NET 9.
- Windows-only path literals (`CatalogDBInitializer.cs:19-21` use `@"Models\Infrastructure\..."`) and a case mismatch
  (`BundleConfig.cs:29` references `~/Content/site.css`; the file on disk is `Content/Site.css`). Both break on Linux
  containers, which is where the rest of this repo already runs.

## Risk rubric

- **High** — changes app lifecycle, request pipeline, wire format, or data initialization; failures are silent or only
  visible at runtime/under load; hard to cover with unit tests.
- **Medium** — API shape changes with a clear Core equivalent, but behavior differences (routing, model binding,
  status codes, URL generation) need targeted tests.
- **Low** — near source-compatible; mostly namespace/helper renames, caught by the compiler.
- **Trivial** — delete or rename only.

## Per-file plan (ordered by risk, highest first)

### 1. `Global.asax.cs` + `Global.asax` — **High**

System.Web usage:
- `MvcApplication : HttpApplication` (L21), `Application_Start` wiring `GlobalConfiguration`, `AreaRegistration`,
  `GlobalFilters`, `RouteTable`, `BundleTable` (L27-36).
- `Session_Start` writing `HttpContext.Current.Session["MachineName"/"SessionStartTime"]` (L41-45).
- `Application_BeginRequest` pushing log4net `LogicalThreadContext` properties (L47-55); `WebRequestInfo.ToString()`
  reads `HttpContext.Current.Request.RawUrl/UserAgent` lazily at log time (L108-114).
- Autofac MVC/WebApi resolvers (L60-81); `ConfigurationManager.AppSettings["UseMockData"]` (L68, L85).
- EF6 `Database.SetInitializer` resolved from the container (L83-91).

Migration:
- Replace with `Program.cs`:
  ```csharp
  var builder = WebApplication.CreateBuilder(args);
  builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
  builder.Host.ConfigureContainer<ContainerBuilder>(b =>
      b.RegisterModule(new ApplicationModule(builder.Configuration.GetValue<bool>("UseMockData"))));
  builder.Services.AddControllersWithViews();
  builder.Services.AddDistributedMemoryCache();
  builder.Services.AddSession();
  builder.Services.AddHttpContextAccessor();
  builder.Services.AddApplicationInsightsTelemetry();

  var app = builder.Build();
  app.UseForwardedHeaders();          // behind nginx; configure ForwardedHeadersOptions (XForwardedProto, KnownProxies)
  app.UseExceptionHandler("/Catalog/Error");
  app.UseStaticFiles();
  app.UseRouting();
  app.UseSession();
  app.UseMiddleware<SessionStartMiddleware>();   // replaces Session_Start
  app.UseMiddleware<RequestLogContextMiddleware>(); // replaces Application_BeginRequest
  app.MapControllers();
  app.MapControllerRoute("default", "{controller=Catalog}/{action=Index}/{id?}");
  if (!app.Configuration.GetValue<bool>("UseMockData"))
      Database.SetInitializer(app.Services.GetRequiredService<CatalogDBInitializer>());
  app.Run();
  ```
- `Session_Start` has no Core equivalent. `SessionStartMiddleware`: if `!session.Keys.Contains("MachineName")`, call
  `SetString("MachineName", Environment.MachineName)` and `SetString("SessionStartTime", DateTime.Now.ToString("O"))`.
  Core sessions only store `byte[]`/strings, so the `DateTime` must be serialized explicitly.
- `ActivityIdHelper`/`WebRequestInfo`: `HttpContext.Current` is gone. Use a middleware that sets
  `LogicalThreadContext.Properties["activityid"] = Activity.Current?.Id ?? context.TraceIdentifier` and
  `["requestinfo"] = $"{context.Request.Path}{context.Request.QueryString}, {context.Request.Headers.UserAgent}"`
  eagerly per request. Verify that log4net's `LogicalThreadContext` flows across `await` on .NET 8 (it should use
  `AsyncLocal` on netstandard builds); if not, use `ILogger.BeginScope`.
  Preferred long-term: `ILogger` scopes and drop log4net.
- log4net config: `Properties/AssemblyInfo.cs:37` `[assembly: XmlConfigurator(ConfigFile = "log4net.xml")]` keeps
  working on .NET 8 but the path resolves against the working directory; switch to
  `builder.Logging.AddLog4Net("log4Net.xml")` (`Microsoft.Extensions.Logging.Log4Net.AspNetCore`) and note the file
  name casing (`log4Net.xml` on disk vs `log4net.xml` in the attribute).
- Delete `Global.asax`.

Risks / tests:
- Lifetime parity: `InstancePerLifetimeScope` in `ApplicationModule` becomes per-request under
  `AutofacServiceProviderFactory` (same as legacy). `CatalogItemHiLoGenerator` must stay `SingleInstance` or HiLo IDs
  collide.
- Existing bug surfaced, not introduced: `log4Net.xml` pattern uses `%property{activity}` but code sets
  `activityid`. Decide whether to fix while here.
- Test: first request creates session, footer shows machine name + start time, second request reuses it; log lines
  contain request URL and a stable activity id.

### 2. `Web.config`, `Web.Debug.config`, `Web.Release.config`, `Views/Web.config`, `ApplicationInsights.config`, `packages.config`, `eShopLegacyMVC.csproj` — **High**

System.Web usage:
- `connectionStrings/CatalogDBContext` consumed implicitly by EF6 via `base("name=CatalogDBContext")`
  (`Models/CatalogDBContext.cs:10`), `<entityFramework>` provider registration (L103-112).
- `appSettings`: `UseMockData`, `UseCustomizationData`, plus MVC-only `webpages:*`, `ClientValidationEnabled`,
  `UnobtrusiveJavaScriptEnabled` (L14-21).
- `<system.web>`: `sessionState InProc`, `httpModules`, `globalization en-US` (L30-39).
- `<system.webServer>`: modules (TelemetryCorrelation, AI, async Session module), extensionless URL handlers.
- `Views/Web.config`: Razor host factory, `WebViewPage`, namespace imports incl. `System.Web.Optimization`,
  `BlockViewHandler`.
- `ApplicationInsights.config`: handler exclusion list names System.Web handlers.
- Assembly binding redirects for System.Web.Mvc/Http/WebPages/Helpers/Optimization.
- Old-style csproj with `packages.config`, `Microsoft.WebApplication.targets`, IIS Express settings, CodeDom compiler.

Migration:
- New SDK-style `eShopLegacyMVC.csproj` (`Microsoft.NET.Sdk.Web`, `net8.0`). Package map:

  | Remove | Replace with |
  |---|---|
  | Microsoft.AspNet.Mvc, Microsoft.AspNet.Razor, Microsoft.AspNet.WebPages, Microsoft.AspNet.WebApi(.Core/.WebHost/.Client) | shared framework `Microsoft.AspNetCore.App` (no package) |
  | Microsoft.AspNet.Web.Optimization, WebGrease, Antlr | none (static files) or `LigerShark.WebOptimizer.Core` |
  | Autofac.Mvc5, Autofac.WebApi2 | `Autofac.Extensions.DependencyInjection` |
  | Microsoft.ApplicationInsights.Web / .WindowsServer / .PerfCounterCollector / .DependencyCollector / .Agent.Intercept, Microsoft.AspNet.TelemetryCorrelation | `Microsoft.ApplicationInsights.AspNetCore` |
  | Microsoft.AspNet.SessionState.SessionStateModule | built-in session middleware |
  | Microsoft.CodeDom.Providers.DotNetCompilerPlatform, Microsoft.Net.Compilers, Microsoft.Web.Infrastructure | none |
  | EntityFramework 6.2.0 | EntityFramework 6.4.4 (netstandard2.1, runs on .NET 8) — EF Core later |
  | log4net 2.0.10 | log4net 2.0.15+ (+ `Microsoft.Extensions.Logging.Log4Net.AspNetCore`) or drop |
  | System.* polyfill packages (Buffers, Memory, IO.Compression, etc.) | none (in-box) |

- `appsettings.json`:
  ```json
  {
    "ConnectionStrings": { "CatalogDBContext": "Data Source=(localdb)\\MSSQLLocalDB; Initial Catalog=Microsoft.eShopOnContainers.Services.CatalogDb; Integrated Security=True; MultipleActiveResultSets=True;" },
    "UseMockData": false,
    "UseCustomizationData": false
  }
  ```
  Environment-specific values move from Web.*.config transforms to `appsettings.{Environment}.json` / env vars
  (`ConnectionStrings__CatalogDBContext`), which is what the docker-compose files already pass to other services.
- EF6 without `Web.config`: add a `CatalogDBContext(string connectionString)` ctor, register it in Autofac with the
  connection string from `IConfiguration`, and add a code-based `DbConfiguration` that sets
  `SqlProviderServices.Instance` (replaces the `<entityFramework>` section).
- `Views/Web.config` -> `Views/_ViewImports.cshtml`:
  ```cshtml
  @using eShopLegacyMVC
  @using eShopLegacyMVC.Models
  @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
  ```
  `BlockViewHandler` is unnecessary: Core never serves `.cshtml` as static content.
- Globalization: `app.UseRequestLocalization(new RequestLocalizationOptions().SetDefaultCulture("en-US"))` —
  required for identical price formatting/parsing in `Edit`/`Create` model binding.
- Client validation flags: on by default in Core; no setting needed.
- Delete all `*.config` files above and `packages.config` once the new project builds.

Risks / tests:
- Connection string resolution is the most likely "works on my machine" failure (EF6 silently falls back to
  `LocalDbConnectionFactory` if it can't find the name). Test with mock data off against a real SQL Server.
- Culture: without explicit `en-US`, a container with invariant/other culture changes decimal parsing.

### 3. `Controllers/WebApi/FilesController.cs` (+ `eShopLegacy.Utilities/Serializing.cs`) — **High**

System.Web usage: `System.Web.Http.ApiController` (L11), `HttpResponseMessage` return with `StreamContent` (L21-35),
convention-based `api/{controller}` routing + verb-by-method-name (`Get`).

Migration:
```csharp
[ApiController, Route("api/files")]
public class FilesController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => File(serializer.SerializeBinary(brands), "application/octet-stream");
}
```

Risks / tests:
- `BinaryFormatter` is disabled in ASP.NET Core on .NET 8 (throws `NotSupportedException` unless
  `EnableUnsafeBinaryFormatterSerialization` is set) and removed in .NET 9, and it is a known RCE vector. The wire
  format is a .NET-Framework-specific binary blob, so any consumer must be identified before changing it. Recommended:
  switch to JSON (or a documented format) behind a new route and keep the old route on the legacy backend in nginx
  until consumers move. Setting the unsafe flag is a stop-gap only.
- Legacy response has no `Content-Type`; Core's `File(...)` will set one — confirm consumers don't care.
- Retarget `eShopLegacy.Utilities` from `net461` to `netstandard2.0`/`net8.0`.

### 4. `Controllers/WebApi/BrandsController.cs` — **High**

System.Web usage: `ApiController` (L12), `IHttpActionResult` + `NotFound()`/`Ok()` (L29-36),
`ResponseMessage(new HttpResponseMessage(...))` (L45, L49), unused `System.Runtime.Remoting.Messaging` (L7).
Relies on Web API's verb-from-method-name convention (`Get`, `Get(int id)`) and `api/{controller}/{id}` route from
`WebApiConfig.cs`.

Migration:
```csharp
[ApiController, Route("api/brands")]
public class BrandsController : ControllerBase
{
    [HttpGet]            public IEnumerable<CatalogBrand> Get() => ...;
    [HttpGet("{id:int}")] public ActionResult<CatalogBrand> Get(int id) => brand is null ? NotFound() : brand;
    [HttpDelete("{id:int}")] public IActionResult Delete(int id) => brandToDelete is null ? NotFound() : Ok();
}
```

Risks / tests:
- Core has no verb-by-name convention: every action needs an explicit `[HttpGet]`/`[HttpDelete]` or it becomes
  ambiguous/unreachable.
- JSON shape: Web API 2 used Json.NET with PascalCase; Core's System.Text.Json defaults to camelCase. Preserve with
  `AddControllersWithViews().AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null)` (or
  `AddNewtonsoftJson()`), because the modernized backend behind nginx must return byte-compatible payloads.
- `[ApiController]` turns a bare 404 into an RFC 7807 `ProblemDetails` body; legacy returned an empty body. Either
  accept it or set `ApiBehaviorOptions.SuppressMapClientErrors = true`.
- Test: `GET /api/brands`, `GET /api/brands/1`, `GET /api/brands/999` (404), `DELETE /api/brands/1` (200),
  `DELETE /api/brands/999` (404); diff JSON against legacy.

### 5. `Models/Infrastructure/CatalogDBInitializer.cs` — **High**

System.Web usage: `System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath` (L11, L345), dead
`using System.Web` (L10), `ConfigurationManager.AppSettings["UseCustomizationData"]` (L29). Also
`AppDomain.CurrentDomain.BaseDirectory` for SQL scripts (L335) and backslash-literal paths (L19-21).

Migration:
- Constructor-inject `IWebHostEnvironment env` and `IConfiguration` (or an `IOptions<CatalogSettings>`); replace
  `HostingEnvironment.ApplicationPhysicalPath` with `env.ContentRootPath`.
- Replace `@"Models\Infrastructure\dbo.catalog_hilo.Sequence.sql"` with
  `Path.Combine("Models", "Infrastructure", "dbo.catalog_hilo.Sequence.sql")`.
- `AppDomain.CurrentDomain.BaseDirectory`: under IIS this was the site root; under Core it is `bin/...`. The `.sql`
  files already have `CopyToOutputDirectory=PreserveNewest` in the csproj, so this keeps working — keep that item
  metadata in the new csproj. `Pics/` and `Setup/` are resolved from `ContentRootPath`, so they must be published as
  content (`<Content Include="Pics/**;Setup/**" CopyToPublishDirectory="PreserveNewest" />`).
- `Autofac` must be able to build it with the new ctor args (it can; they come from the Core container).

Risks / tests:
- Seeding runs only on first DB creation, so bugs here appear only on a fresh database. Test against an empty SQL
  Server container with `UseCustomizationData` both `false` and `true` (the `true` path deletes and re-extracts
  `Pics/`).

### 6. `Views/Shared/_Layout.cshtml` — **High**

System.Web usage: `HttpContext.Current.Session[...]` + `System.Web.HtmlString` (L35); `Styles.Render`/`Scripts.Render`
(L7-8, L43-44); `~/` URLs in `<img src>` (L14, L30, L33) — these keep working in Core.

Migration:
```cshtml
@using Microsoft.AspNetCore.Http
<link rel="stylesheet" href="~/css/bootstrap.css" asp-append-version="true" />
... (one tag per former bundle entry, see BundleConfig)
<small>@Context.Session.GetString("MachineName"), @Context.Session.GetString("SessionStartTime")</small>
<script src="~/js/jquery-3.3.1.js" asp-append-version="true"></script>
```
Drop the `HtmlString` wrapper: values are server-generated but encoding them is free and removes a raw-HTML sink.

Risks / tests: this is the only place a request-time null session would crash every page (legacy
`HttpContext.Current.Session` is never null after `Session_Start`; in Core it throws if `UseSession` isn't registered
before MVC). Covered by any page render test.

### 7. `Controllers/CatalogController.cs` — **Medium**

System.Web usage: `System.Web.Mvc` (L3); `ActionResult` returns; `new HttpStatusCodeResult(HttpStatusCode.BadRequest)`
(L36, L82, L119); `HttpNotFound()` (L41, L87, L124); `SelectList` into `ViewBag` (L52-53, L71-72, L90-91, L108-109);
`[Bind(Include = "...")]` (L62, L100); `[ValidateAntiForgeryToken]`; `[HttpPost, ActionName("Delete")]` (L132);
`Url.RouteUrl(name, values, this.Request.Url.Scheme)` (L162); `Dispose(bool)` override disposing the injected service
(L142-150).

Migration:
- `using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering;` (for `SelectList`).
- `ActionResult` -> `IActionResult`; `new HttpStatusCodeResult(BadRequest)` -> `BadRequest()`;
  `HttpNotFound()` -> `NotFound()`.
- `[Bind(Include = "A,B,C")]` -> `[Bind("A,B,C")]`.
- `Request.Url.Scheme` -> `Request.Scheme`. `Url.RouteUrl(routeName, values, protocol)` exists in Core with the same
  signature.
- Remove the `Dispose` override: the container owns `ICatalogService` (scoped) and disposes it at request end; keeping
  the override double-disposes it.

Risks / tests:
- Behind nginx, `Request.Scheme` is `http` unless `UseForwardedHeaders` is configured with `X-Forwarded-Proto`, which
  produces mixed-content `PictureUri`s under HTTPS. Legacy has the same exposure via IIS, but Core makes it explicit.
- `Bind` + `ModelState` validation messages differ slightly between MVC5 and Core (e.g. the implicit "The value '' is
  invalid" text for non-nullable ints). Compare Create/Edit validation output with invalid Price/stock values.
- Test all 7 actions: Index (paging), Details/Edit/Delete with missing id (400), unknown id (404), Create/Edit POST
  valid + invalid, Delete POST, antiforgery rejection without token.

### 8. `Controllers/PicController.cs` — **Medium**

System.Web usage: `System.Web.Mvc` (L5); `[Route("items/{catalogItemId:int}/pic", Name = ...)]` (L24, MVC5 attribute
routing enabled by `MapMvcAttributeRoutes`); `HttpStatusCodeResult` (L31); `Server.MapPath("~/Pics")` (L38);
`HttpNotFound()` (L49); `File(byte[], string)`.

Migration:
- Inject `IWebHostEnvironment`; `Server.MapPath("~/Pics")` -> `Path.Combine(env.ContentRootPath, "Pics")`
  (`Pics/` is not under `wwwroot`, and must not be, since it's served through this controller).
- `[Route]` attribute is source-compatible (`Microsoft.AspNetCore.Mvc.RouteAttribute`, `Name` supported); add
  `[HttpGet]` from the Core namespace.
- Optional: `PhysicalFile(path, mimetype)` instead of `ReadAllBytes`, and `FileExtensionContentTypeProvider` instead of
  the hand-written switch.

Risks / tests:
- `PictureFileName` is user-bindable via `CatalogController.Create/Edit` and is `Path.Combine`d unvalidated — a
  pre-existing path traversal. Not a migration regression, but the rewrite is a cheap moment to add
  `Path.GetFileName(...)` and a root-prefix check.
- Linux is case-sensitive: DB values must match file names in `Pics/` exactly.
- Test `GET /items/1/pic` (200 + correct content type), `/items/0/pic` (400), `/items/99999/pic` (404).

### 9. `App_Start/BundleConfig.cs` + `Views/Catalog/Create.cshtml` / `Edit.cshtml` (`@Scripts.Render("~/bundles/jqueryval")`) — **Medium**

System.Web usage: `System.Web.Optimization` `ScriptBundle`/`StyleBundle` with `{version}` and `*` wildcards;
`Scripts.Render`/`Styles.Render` in views (`Create.cshtml:103`, `Edit.cshtml:104`, `_Layout.cshtml`).

Migration:
- Move `Content/`, `Scripts/`, `Images/`, `fonts/`, `favicon.ico` into `wwwroot/` (e.g. `wwwroot/css`, `wwwroot/js`,
  `wwwroot/images`). Keep the `~/images/...` casing used by `_Layout.cshtml` (legacy folder is `Images/`).
- Resolve wildcards to concrete files: `jquery-{version}.js` -> `jquery-3.3.1.js`;
  `jquery.validate*` -> `jquery.validate.js` + `jquery.validate.unobtrusive.js`; `modernizr-*` matched both
  `modernizr-2.6.2.js` and `modernizr-2.8.3.js` — keep only 2.8.3 (or drop Modernizr/Respond; they target IE8/9).
- Replace `@Scripts.Render("~/bundles/jqueryval")` with a `_ValidationScriptsPartial.cshtml` and
  `<partial name="_ValidationScriptsPartial" />` in both views.
- Delete `BundleConfig.cs`.

Risks / tests: bundling currently hides ordering and casing issues — `BundleConfig.cs:29` includes
`~/Content/site.css` but the file is `Content/Site.css`; IIS is case-insensitive, Linux is not. Verify every page for
404s on static assets and that client-side validation still fires on Create/Edit.

### 10. `App_Start/RouteConfig.cs` + `App_Start/WebApiConfig.cs` — **Medium**

System.Web usage: `RouteCollection`, `MapMvcAttributeRoutes`, `IgnoreRoute("{resource}.axd/{*pathInfo}")`,
`UrlParameter.Optional`; Web API `HttpConfiguration`, `MapHttpAttributeRoutes`, `api/{controller}/{id}` with
`RouteParameter.Optional`.

Migration: fold into `Program.cs` as shown in section 1 (`MapControllers()` for attribute routes +
`MapControllerRoute("default", "{controller=Catalog}/{action=Index}/{id?}")`). `.axd` ignore is obsolete. Web API
convention route is replaced by attribute routes on `BrandsController`/`FilesController` (sections 3-4). Delete both
files.

Risks / tests: route precedence changes — in Core, attribute-routed controllers (`PicController.Index`,
`CatalogController2`, the two API controllers) are excluded from conventional routing, and Web API's
`api/{controller}` fallback no longer exists, so any API action without an attribute route becomes unreachable. Run a route table dump (`EndpointDataSource`) and compare every legacy
URL in `nginx*.conf` plus `/`, `/Catalog/*`, `/items/{id}/pic`, `/api`, `/api/brands`, `/api/files`.

### 11. `Controllers/Api/CatalogController.cs` (`CatalogController2`) — **Low**

System.Web usage: `System.Web.Mvc` (L1), `[Route("api")]`, `Json(...)` (L11).

Migration: `using Microsoft.AspNetCore.Mvc;`, `IActionResult`, keep `[Route("api")]` and `[HttpGet]`.

Behavior note: MVC5 `Json(object)` defaults to `JsonRequestBehavior.DenyGet`, so today `GET /api` throws
`InvalidOperationException` (500). Core `Json(...)` returns 200. Flag this to consumers/tests rather than
"preserving" the 500.

### 12. `App_Start/FilterConfig.cs` + `Views/Shared/Error.cshtml` — **Low**

System.Web usage: `GlobalFilterCollection`, `HandleErrorAttribute`, dead `using System.Web`. `<customErrors>` is not
set, so the default `RemoteOnly` applies: remote users get `Error.cshtml`, local requests get the YSOD.

Migration: delete; use `app.UseExceptionHandler("/Catalog/Error")` in non-Development and
`UseDeveloperExceptionPage` in Development. Add an `Error` action returning `Views/Shared/Error.cshtml` (markup is
already framework-neutral).

### 13. `Views/Catalog/Index.cshtml`, `Details.cshtml`, `Delete.cshtml`, `CatalogTable.cshtml`, `_ViewStart.cshtml` (and the non-bundle parts of `Create.cshtml` / `Edit.cshtml`) — **Low**

System.Web usage: implicit, via `System.Web.Mvc.WebViewPage` and `System.Web.Mvc.Html` helpers
(`Html.ActionLink`, `DisplayFor`, `DisplayNameFor`, `EditorFor`, `LabelFor`, `ValidationMessageFor`,
`ValidationSummary`, `DropDownList("CatalogBrandId", null, ...)`, `HiddenFor`, `BeginForm`, `AntiForgeryToken`,
`Html.Partial`).

Migration: all of these exist on Core's `IHtmlHelper` with the same signatures. Changes needed:
- `@Html.Partial("CatalogTable", Model.Data)` (`Index.cshtml:12`) -> `<partial name="CatalogTable" model="Model.Data" />`
  (sync `Html.Partial` triggers analyzer MVC1000 and can deadlock under load).
- `Html.BeginForm()` for POST in Core already emits an antiforgery token, so the explicit `@Html.AntiForgeryToken()`
  (`Create.cshtml:12`, `Edit.cshtml:12`, `Delete.cshtml:91`) is redundant. Harmless either way; remove for tidiness.
- `DropDownList(name, null)` reads `ViewData[name]` as `IEnumerable<SelectListItem>` in both frameworks; works as-is.
- `_ViewStart.cshtml` unchanged.

Tests: covered by the CatalogController action tests (section 7) with HTML snapshot diffs against legacy.

### 14. `Models/CatalogBrand.cs`, `Models/CatalogType.cs`, `Models/CatalogItemHiLoGenerator.cs`, `Models/Infrastructure/PreconfiguredData.cs` — **Trivial**

System.Web usage: `using System.Web;` only; no symbols referenced. Delete the using. (`CatalogItem.cs` uses only
`System.ComponentModel.DataAnnotations`, which is identical in Core.) These can land first, on the legacy project,
with zero behavior change.

Files with **no** System.Web dependency (move as-is): `Models/CatalogItem.cs`, `Models/CatalogDBContext.cs` (only the
`name=` ctor changes, section 2), `Modules/ApplicationModule.cs`, `Services/*.cs`, `ViewModel/PaginatedItemsViewModel.cs`.

## Recommended execution order

Risk order is not the same as execution order. To keep every step shippable behind the existing nginx strangler:

1. **On legacy, zero-risk prep (can merge to main today):** section 14 dead usings; fix `site.css` casing and
   backslash paths (sections 5, 9); remove unused `System.Runtime.Remoting.Messaging` using; retarget
   `eShopLegacy.Utilities` to `netstandard2.0`. Legacy keeps building on .NET Framework.
2. **New SDK-style project skeleton** (section 2): csproj, `appsettings.json`, `Program.cs` with DI/session/static
   files/routing (sections 1, 10), `_ViewImports.cshtml`, `wwwroot` (section 9). Boots with `UseMockData=true`.
3. **UI path:** `_Layout.cshtml`, CatalogController, PicController, views, error handling (sections 6, 7, 8, 12, 13).
   Flip a slice of `/Catalog/*` and `/items/*` traffic in nginx.
4. **Data path:** EF6 connection-string/`DbConfiguration`, `CatalogDBInitializer` (sections 2, 5). Test against a
   fresh SQL Server.
5. **API path last:** Brands, Api/Catalog, then Files (sections 3, 4, 11) — highest external-contract risk; keep
   `/api/files` on legacy until the BinaryFormatter consumer question is answered.
6. Delete `Global.asax`, `App_Start/`, `*.config`, `packages.config`; rerun the inventory `rg` and expect zero hits.

## Open questions to confirm with owners

- Who consumes `GET /api/files` (BinaryFormatter payload)? This decides whether section 3 is a port or a contract change.
- Is the camelCase vs PascalCase JSON change acceptable for `/api/brands`? Plan assumes no.
- Should the migrated project replace `eShopLegacyMVC` in place, or should this plan be applied to finish
  `eShopPorted` (currently `net461` + ASP.NET Core 2.2, still referencing `Autofac.Mvc5` and `WebGrease`)?
