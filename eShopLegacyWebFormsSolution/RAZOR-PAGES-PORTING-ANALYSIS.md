# Web Forms → Razor Pages: page lifecycle analysis

Scope: `eShopLegacyWebFormsSolution/src/eShopLegacyWebForms` (.NET Framework 4.7.2, Web Forms).
Deltas for the Azure-enabled sibling `eShopModernizedWebFormsSolution/src/eShopModernizedWebForms`
are called out in [its own section](#deltas-for-eshopmodernizedwebforms).
Target assumed: ASP.NET Core Razor Pages (.NET 8). This is a static code analysis; nothing was built or run.

## TL;DR

| Page / artifact | Lifecycle surface actually used | Verdict |
| --- | --- | --- |
| `About.aspx`, `Contact.aspx` | `Page_Load` (log line only), `Title` directive | **Mechanical** |
| `Catalog/Details.aspx` | `Page_Load` + `Page.DataBind()`; read-only | **Mechanical** |
| `Default.aspx` (catalog list) | `Page_Load` + imperative `ListView.DataBind()`, server-side `HyperLink` mutation; no postbacks | **Mechanical** |
| `Catalog/Delete.aspx` | `Page_Load` re-runs on postback to rehydrate the entity before `Delete_Click` | **Mechanical, one pattern change** (handler must re-fetch by id) |
| `Catalog/Create.aspx` | Postback + `Button.OnClick`, `SelectMethod` model binding, validator controls, reads values off the control tree | **Rework** |
| `Catalog/Edit.aspx` | `!IsPostBack` gating, ViewState-persisted dropdown items and field values, validator controls, control-tree reads | **Rework** |
| `Site.Master` | `<form runat="server">` + `ScriptManager`, `Page_Load` reads `Session` populated by `Session_Start` | **Layout mechanical; session bit needs rework** |
| `Global.asax` | `Application_Start`, `Session_Start`, `Application_BeginRequest`, Autofac property-injection HTTP module | **Rework** (becomes `Program.cs` + middleware) |
| `Site.Mobile.Master`, `ViewSwitcher.ascx` | FriendlyUrls view switching | **Drop** (dead code, see below) |

Rule of thumb applied: a page is *mechanical* if every piece of state it renders is recomputed on each
GET from route values + services, and it never relies on ViewState, postback event ordering, or
server controls to carry data across requests. Anything that relies on those needs its data flow
redesigned around `OnGet`/`OnPost` + `[BindProperty]` and is classed as *rework*.

## Cross-cutting findings

These apply to more than one page and drive most of the effort.

1. **No page uses anything beyond `Page_Load`.** There is no `Page_Init`, `Page_PreRender`,
   `OnInit` override, `Page_Unload`, `UpdatePanel`, `Server.Transfer`, `ClientScript` or explicit
   `ViewState[...]` access anywhere in the app. The only lifecycle-sensitive constructs are
   `Page.IsPostBack` (Edit), postback event handlers (`Create_Click`, `Save_Click`, `Delete_Click`)
   and *implicit* ViewState on `DropDownList`/`TextBox`. That is why four of the six content pages port mechanically.

2. **Server-side validation is effectively not enforced today.** `Create_Click` and `Save_Click`
   gate on `this.ModelState.IsValid`. In Web Forms, `Page.ModelState` is populated by model binding
   (`TryUpdateModel`, `InsertMethod`/`UpdateMethod`), not by validator controls - those set
   `Page.IsValid`. Neither page uses model binding for the form fields, so as far as I can tell
   `ModelState.IsValid` is always `true` and the `RequiredFieldValidator`/`RangeValidator`s only run
   client-side (via `WebUIValidation.js`). With JS disabled, bad input hits `int.Parse`/`decimal.Parse`
   and throws. A Razor Pages port using data annotations + `ModelState.IsValid` will *start* enforcing
   validation on the server; that is a (desirable) behavior change to call out in testing.
   (I believe this is correct per Web Forms semantics but did not run the app to confirm.)

3. **Output encoding changes.** `Details.aspx`, `Delete.aspx` and `Edit.aspx` bind with `<%# ... %>`
   into `asp:Label.Text` / `asp:Image.ImageUrl`. `Label` renders `Text` without HTML-encoding, and
   `Create.aspx`/`Edit.aspx` set `ValidateRequest="false"`, so HTML in `Name`/`Description` is stored
   and rendered raw on Details/Delete. Razor `@Model.X` encodes by default. Port mechanically, but
   expect rendered output to differ for any existing rows containing markup. `Default.aspx` already uses
   the encoding `<%#: %>` form, so it is unaffected.

4. **Routing is centralised and trivially translatable.** `App_Start/RouteConfig.cs` maps six
   `MapPageRoute`s. Each becomes an `@page` template; named-route URL generation
   (`GetRouteUrl("EditProductRoute", new { id })`) becomes `asp-page="/Catalog/Edit" asp-route-id="@item.Id"`.

   | Route name | URL | Razor Pages |
   | --- | --- | --- |
   | *(unnamed)* | `Default` | `Pages/Index.cshtml` + `AddPageRoute("/Index", "Default")` |
   | `ProductsByPageRoute` | `Default/index/{index}/size/{size}` | `AddPageRoute("/Index", "Default/index/{index:int}/size/{size:int}")` |
   | `CreateProductRoute` | `Catalog/Create` | `@page` in `Pages/Catalog/Create.cshtml` |
   | `EditProductRoute` | `Catalog/Edit/{id}` | `@page "{id:int}"` |
   | `ProductDetailsRoute` | `Catalog/Details/{id}` | `@page "{id:int}"` |
   | `DeleteProductRoute` | `Catalog/Delete/{id}` | `@page "{id:int}"` |

   `href="~"` with `runat="server"` (Cancel / Back links) should become `asp-page="/Index"`;
   Razor only resolves `~/`, not a bare `~`.

5. **DI: property injection → constructor injection.** Every page exposes
   `public ICatalogService CatalogService { get; set; }`, filled by Autofac's `PropertyInjectionModule`
   (`Web.config` `<modules>`). In Razor Pages this is a constructor parameter on the `PageModel`; the
   registrations in `Modules/ApplicationModule.cs` move to `builder.Services` (or keep Autofac via
   `Autofac.Extensions.DependencyInjection`). Mechanical, but it touches every page.

6. **The master page's `<form runat="server">` + `ScriptManager` go away.** They exist only to
   support postbacks and the WebForms/MsAjax client scripts. Razor forms are per-page `<form method="post">`
   with antiforgery tokens (on by default for Razor Pages POSTs).

7. **Logging.** Each page has a static log4net logger and a `Page_Load` "Now loading..." line; replace
   with `ILogger<T>` in `OnGet`. `Application_BeginRequest` sets log4net `LogicalThreadContext`
   properties (activity id, raw URL + UA) - replace with a small middleware or logging scopes.

## Per-page analysis

### `About.aspx`, `Contact.aspx` - Mechanical

- `Page_Load` only logs. Markup is static apart from `<%: Title %>`.
- Port: `@page`, `ViewData["Title"] = "About"`, copy markup. Note these are not registered in
  `RouteConfig` and are not linked from `Site.Master`; confirm they are wanted at all before porting.

### `Catalog/Details.aspx` - Mechanical

- `Page_Load` (every request): read `RouteData["id"]`, `FindCatalogItem`, `this.DataBind()` so the
  `<%# product.X %>` expressions evaluate. No postback, no controls that post data.
- Port:
  ```csharp
  public CatalogItem Product { get; private set; }
  public IActionResult OnGet(int id) {
      Product = _catalog.FindCatalogItem(id);
      return Product is null ? NotFound() : Page();
  }
  ```
  `asp:Label Text='<%# product.Name %>'` → `@Model.Product.Name`; `asp:Image` → `<img src="~/Pics/@Model.Product.PictureFileName">`.
- Watch: encoding change (finding 3); legacy code NREs on unknown id, `NotFound()` is an improvement.

### `Default.aspx` (catalog list) - Mechanical

- `Page_Load` (every request, no `IsPostBack` check - there are no postbacks on this page): reads
  optional `size`/`index` route values, calls `GetCatalogItemsPaginated`, assigns `ListView.DataSource`
  and calls `DataBind()`, then mutates the two pager `HyperLink`s' `NavigateUrl`/`CssClass`.
- Port: `OnGet(int index = 0, int size = 10)` populates `Model`; the `ListView`
  `LayoutTemplate`/`ItemTemplate`/`EmptyDataTemplate` becomes a `@foreach` with an `@if (!Model.Data.Any())`
  branch; `ConfigurePagination` becomes inline `asp-route-index`/`asp-route-size` plus a conditional
  `esh-pager-item--hidden` class.
- Watch: the item template has a malformed `<image ...>` tag followed by a stray `</a>` (line ~53) -
  fix to `<img>` while porting. `Model.ItemsPerPage` text says "Showing N of M" using page size, not
  actual count; keep as-is for parity unless product wants it fixed.

### `Catalog/Delete.aspx` - Mechanical, one pattern change

- `Page_Load` runs on **both** GET and the postback, re-fetching `productToDelete` and calling
  `DataBind()`; `Delete_Click` then relies on that field having been populated by `Page_Load`
  earlier in the same lifecycle. This is the one place event ordering matters.
- Port: `OnGet(int id)` loads for display; `OnPost(int id)` must load the entity itself (or call a
  `RemoveCatalogItem(id)` overload) and `RedirectToPage("/Index")`. The `asp:Button` becomes a
  `<button type="submit">` inside a `<form method="post">`.
- Watch: same encoding change as Details.

### `Catalog/Create.aspx` - Rework

Lifecycle/state dependencies:
- `Brand`/`Type` `DropDownList`s use `SelectMethod="GetBrands"`/`"GetTypes"` (Web Forms model binding).
  They bind on the first request and on postback their items are restored from ViewState.
- `Create_Click` is a postback event handler that reads values straight off server controls
  (`Name.Text`, `Brand.SelectedValue`, `int.Parse(Stock.Text)` ...).
- Validation is 1 `RequiredFieldValidator` + 4 `RangeValidator`s (client-side only in practice - finding 2).
  `RangeValidator Type="Currency"` is culture-sensitive, as is `decimal.Parse`.
- `ValidateRequest="false"`.

Port plan:
- Introduce a `CatalogItemInput` view model with `[BindProperty]` and data annotations mirroring the
  validators (`[Required]` Name, `[Range(0, 1_000_000)]` Price with 2-decimal regex, `[Range(0, 10_000_000)]` x3).
- `OnGet` and the invalid branch of `OnPost` both populate `SelectList`s for brands/types - there is
  no ViewState to preserve them, so the list must be rebuilt on every POST that re-renders.
- `OnPost`: `if (!ModelState.IsValid) { LoadLists(); return Page(); }` → map → `CreateCatalogItem` → `RedirectToPage("/Index")`.
- Client validation: `_ValidationScriptsPartial` (jquery-validation-unobtrusive) replaces `WebUIValidation.js`.
- Decide explicitly on culture for price parsing (Web Forms used the thread culture; Core model binding uses
  `CultureInfo.CurrentCulture` for form values, set by request localization).

### `Catalog/Edit.aspx` - Rework

Lifecycle/state dependencies (the heaviest page):
- `Page_Load` does everything inside `if (!Page.IsPostBack)`: loads the product, **imperatively**
  sets `DataSource` + `SelectedValue` on both dropdowns, and calls `DataBind()` to evaluate
  `Text='<%# product.X %>'` on every `TextBox`.
- On postback nothing is reloaded: the dropdown items, the text box values and the read-only
  `PictureFileName` all round-trip via ViewState/form fields, and `product` is `null`.
  `Save_Click` reads the id again from `RouteData` and every field from controls.
- `GetBrands`/`GetTypes` are defined but unused (no `SelectMethod` on this page's dropdowns).
- `Debug="true"` and `ValidateRequest="false"` in the `@Page` directive.

Port plan:
- Same `CatalogItemInput` view model as Create (plus `Id`, `PictureFileName`), shared validation.
- `OnGet(int id)`: load product → map to input → build `SelectList`s with the selected ids.
- `OnPost(int id)`: validate, rebuild `SelectList`s on failure, otherwise `UpdateCatalogItem` + redirect.
- `PictureFileName` is `ReadOnly` in the UI but is posted back and trusted by `Save_Click` today; in the port
  either re-read it from the DB in `OnPost` or bind it from a hidden field knowingly (overposting decision).
- Drop `Debug="true"`.

### `Site.Master` - Layout mechanical, session bit needs rework

- Becomes `Pages/Shared/_Layout.cshtml`; `<%: Page.Title %>` → `@ViewData["Title"]`;
  `Scripts.Render`/`webopt:BundleReference` → static `<link>`/`<script>` (or a bundler);
  `ContentPlaceHolder MainContent` → `@RenderBody()`.
- `<form runat="server">` and the whole `ScriptManager` block are removed (finding 6).
- `Page_Load` sets `SessionInfoLabel.Text` from `Session["MachineName"]`/`Session["SessionStartTime"]`,
  which are written in `Global.Session_Start`. ASP.NET Core has **no `Session_Start` event** and
  session is opt-in (`AddSession`/`UseSession`, string/byte values only). Options: (a) small middleware
  that seeds the keys when absent, (b) drop session and render `Environment.MachineName` + a cookie-based
  timestamp. The footer text is demo-only; I'd take (a) to keep parity for the sticky-session demo.

### `Global.asax` - Rework (→ `Program.cs`)

| Web Forms | ASP.NET Core |
| --- | --- |
| `Application_Start`: `RouteConfig`, `BundleConfig`, `ConfigureContainer`, `ConfigDataBase` | `builder.Services.AddRazorPages()`, DI registrations, DB initialiser at startup |
| `Session_Start` | middleware (see Site.Master) |
| `Application_BeginRequest` log4net context | middleware / `ILogger.BeginScope` |
| `ContainerDisposal` / `PropertyInjection` HTTP modules | built-in DI scopes; constructor injection |
| `SessionStateModuleAsync` (`Web.config`) | `AddSession()` + `AddDistributedMemoryCache()`/Redis |
| `UseMockData` appSetting | `appsettings.json` |

`Database.SetInitializer<CatalogDBContext>` is EF6; moving the data layer (EF6 on .NET 8 vs EF Core)
is a separate decision and out of scope here.

### `Site.Mobile.Master` + `ViewSwitcher.ascx` - Drop

No `.aspx` references `Site.Mobile.Master`, and `RouteConfig` never calls `EnableFriendlyUrls`, so
`ViewSwitcher` always finds no `AspNet.FriendlyUrls.SwitchView` route and hides itself. Both are
template leftovers; do not port. `Microsoft.AspNet.FriendlyUrls*` packages can go with them.

## Deltas for `eShopModernizedWebForms`

Same pages minus About/Contact, same verdicts, plus:

- **Auth in `Page_Load`** (Create, Edit, Delete): `if (!Request.IsAuthenticated) Context.GetOwinContext().Authentication.Challenge(...)`.
  Execution continues after `Challenge` (it only sets a 401 that OWIN turns into a redirect), so the
  page still runs. Replace with `[Authorize]` on the `PageModel` (or `AuthorizeFolder("/Catalog")`)
  and `AddOpenIdConnect` - cleaner and not lifecycle-dependent. Rework, but small.
- **`Site.Master` `LoginView`/`LoginStatus`/`LinkButton`**: postback-driven sign-in/out (`Login_Click`,
  `Unnamed_LoggingOut`). Rework into a `_LoginPartial` with plain links/`<form method="post">` to
  sign-in/sign-out endpoints.
- **`Catalog/PicUploader.asmx`** (ASMX `[ScriptService]`, resolves Autofac manually from
  `HttpContext.Current.ApplicationInstance`, uses `System.Drawing` for validation): rework into a
  minimal-API/controller endpoint taking `IFormFile`; `System.Drawing` is Windows-only on .NET 6+, so
  image validation needs a different library (e.g. ImageSharp) if targeting Linux containers.
- **`UploadButton.Visible = ...` / `HiddenField TempImageName`**: `runat="server"` visibility toggles → `@if`;
  hidden field → `<input type="hidden" asp-for="Input.TempImageName">`. Mechanical once the page is on
  the Create/Edit rework path. Note `Scripts/site.js` posts `itemId` from `$("#Id")`, which does not
  exist in the markup, so the server always receives `0`; worth fixing during the port.
- `Default.aspx` sets `PictureUri` via `ImageService` before binding - still mechanical.
- `ImageMockStorage`/`ImageAzureStorage` use `HttpContext.Current.Server.MapPath` → `IWebHostEnvironment.WebRootPath`.

## Suggested order and effort

1. Scaffold Razor Pages project, `_Layout`, DI, routing conventions, static assets (half a session).
2. Port mechanical pages: Index (Default), Details, Delete, About/Contact - ~1-2 hours combined.
3. Shared `CatalogItemInput` + validation, then Create and Edit - ~half a session including parity tests
   for validation messages and dropdown preselection.
4. Session seeding middleware + logging middleware.
5. (Modernized variant only) OIDC auth, login partial, image upload endpoint.

Parity checks worth automating before/after: list paging links at first/last page, Edit preselects
brand/type, invalid Price/Stock rejected server-side (new behavior - finding 2), Delete redirects to list,
HTML in Name renders encoded (new behavior - finding 3).
