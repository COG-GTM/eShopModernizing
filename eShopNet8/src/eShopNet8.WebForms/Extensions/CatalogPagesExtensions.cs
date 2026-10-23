using System.Globalization;
using System.Text;
using eShop.Catalog.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;

namespace eShopNet8.WebForms.Extensions;

// Razor Pages port of the legacy WebForms catalog (Default.aspx, Catalog/*.aspx, About.aspx, Contact.aspx, Site.Master).
public static class CatalogPagesExtensions
{
    public const string SessionCookieName = "ASP.NET_SessionId";

    // The antiforgery token travels in the hidden field the legacy pages used for event validation, so the
    // rendered form keeps the legacy hidden-field structure and a post without it is treated like a legacy
    // post without __EVENTVALIDATION/__VIEWSTATE (page re-rendered, nothing saved).
    public const string AntiforgeryFieldName = "__EVENTVALIDATION";

    private static readonly (string Path, string ContentType, string[] Files)[] Bundles =
    {
        ("/Content/css", "text/css; charset=utf-8",
            new[] { "Content/bootstrap.css", "Content/custom.css", "Content/base.css", "Content/site.css" }),
        ("/bundles/modernizr", "text/javascript; charset=utf-8",
            new[] { "Scripts/modernizr-2.8.3.js" }),
        ("/bundles/WebFormsJs", "text/javascript; charset=utf-8",
            new[]
            {
                "Scripts/WebForms/WebForms.js", "Scripts/WebForms/WebUIValidation.js", "Scripts/WebForms/MenuStandards.js",
                "Scripts/WebForms/Focus.js", "Scripts/WebForms/GridView.js", "Scripts/WebForms/DetailsView.js",
                "Scripts/WebForms/TreeView.js", "Scripts/WebForms/WebParts.js",
            }),
        ("/bundles/MsAjaxJs", "text/javascript; charset=utf-8",
            new[]
            {
                "Scripts/WebForms/MsAjax/MicrosoftAjax.js", "Scripts/WebForms/MsAjax/MicrosoftAjaxApplicationServices.js",
                "Scripts/WebForms/MsAjax/MicrosoftAjaxTimer.js", "Scripts/WebForms/MsAjax/MicrosoftAjaxWebForms.js",
            }),
    };

    public static IServiceCollection AddCatalogPages(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRazorPages(options =>
        {
            // Legacy RouteConfig.MapPageRoute routes plus the physical .aspx URLs.
            options.Conventions.AddPageRoute("/Default", "");
            options.Conventions.AddPageRoute("/Default", "Default.aspx");
            options.Conventions.AddPageRoute("/Default", "Default/index/{index}/size/{size}");
            options.Conventions.AddPageRoute("/Catalog/Create", "Catalog/Create.aspx");
            options.Conventions.AddPageRoute("/Catalog/Edit", "Catalog/Edit.aspx");
            options.Conventions.AddPageRoute("/Catalog/Details", "Catalog/Details.aspx");
            options.Conventions.AddPageRoute("/Catalog/Delete", "Catalog/Delete.aspx");
        });

        services.AddAntiforgery(options => options.FormFieldName = AntiforgeryFieldName);

        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.Cookie.Name = SessionCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            // Secure whenever the request is HTTPS; the apps also serve plain HTTP (ports 5200/5201, containers).
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        // SQL mode: save-time validation of the legacy WebForms CatalogItem, not the MVC model's (used by AddCatalogData).
        services.Replace(ServiceDescriptor.Singleton<ICatalogItemSaveValidator, WebFormsCatalogItemSaveValidator>());

        return services;
    }

    public static WebApplication UseCatalogPages(this WebApplication app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = WriteServerErrorAsync });
        app.UseStatusCodePages("text/html; charset=utf-8",
            "<!DOCTYPE html><html><head><title>Error {0}</title></head><body><h2>The resource cannot be found.</h2></body></html>");

        app.UseStaticFiles();
        app.UseRouting();
        app.UseSession();
        app.Use(InitializeSessionAsync);
        return app;
    }

    public static WebApplication MapCatalogPages(this WebApplication app)
    {
        foreach (var (path, contentType, files) in Bundles)
        {
            app.MapGet(path, (IWebHostEnvironment env) => Results.Text(Concatenate(env.WebRootFileProvider, files), contentType));
        }

        app.MapRazorPages();
        return app;
    }

    // Global.asax Session_Start: remember the machine name and the session start time (shown in the Site.Master footer).
    private static async Task InitializeSessionAsync(HttpContext context, Func<Task> next)
    {
        if (context.GetEndpoint() is not null && !context.Session.Keys.Contains(LegacyPage.SessionMachineNameKey))
        {
            context.Session.SetString(LegacyPage.SessionMachineNameKey, Environment.MachineName);
            context.Session.SetString(LegacyPage.SessionStartTimeKey,
                DateTime.Now.ToString("M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture));
        }

        await next();
    }

    // Legacy unhandled exceptions surface as HTTP 500 (quirks Q4/Q5/Q7/Q8); no exception details are rendered.
    private static async Task WriteServerErrorAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(
            "<!DOCTYPE html><html><head><title>Runtime Error</title></head><body><h2>Server Error</h2>" +
            "<p>An error occurred while processing your request.</p></body></html>");
    }

    private static string Concatenate(IFileProvider files, IEnumerable<string> paths)
    {
        var builder = new StringBuilder();
        foreach (var path in paths)
        {
            var file = files.GetFileInfo(path);
            if (!file.Exists)
            {
                continue;
            }

            using var reader = new StreamReader(file.CreateReadStream());
            builder.Append(reader.ReadToEnd()).Append('\n');
        }

        return builder.ToString();
    }
}
