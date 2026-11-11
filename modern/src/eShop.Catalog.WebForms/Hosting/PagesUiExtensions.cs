using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eShop.Catalog.WebForms.Hosting;

/// <summary>Owned by slice S6 (Razor Pages host, layout, static files, session, About/Contact).</summary>
public static class PagesUiExtensions
{
    private static readonly CultureInfo LegacyCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Legacy <c>sessionState mode="InProc"</c> defaults: 20 minute sliding timeout.</summary>
    public static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(20);

    public static IServiceCollection AddCatalogPagesUi(this IServiceCollection services)
    {
        services.AddRazorPages(options => options.Conventions.AddLegacyPageAliases());
        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.IdleTimeout = SessionTimeout;
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    /// <summary>
    /// First S6 middleware: fixed en-US request culture (Web.config <c>globalization culture="en-US"</c>), an HTML
    /// error page for unhandled exceptions outside Development, and an HTML body for body-less 4xx/5xx responses.
    /// </summary>
    public static IApplicationBuilder UseCatalogErrorHandling(this IApplicationBuilder app)
    {
        var localization = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(LegacyCulture),
            SupportedCultures = [LegacyCulture],
            SupportedUICultures = [LegacyCulture],
        };
        localization.RequestCultureProviders.Clear();
        app.UseRequestLocalization(localization);

        var environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            app.UseExceptionHandler(new ExceptionHandlerOptions
            {
                ExceptionHandler = context => WriteErrorPageAsync(context.Response, StatusCodes.Status500InternalServerError),
            });
        }

        app.UseStatusCodePages(context => WriteErrorPageAsync(context.HttpContext.Response, context.HttpContext.Response.StatusCode));
        return app;
    }

    /// <summary>wwwroot served for any casing of the legacy URLs, with the content types legacy used for fonts.</summary>
    public static IApplicationBuilder UseCatalogStaticFiles(this IApplicationBuilder app)
    {
        var environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        UsePublishedWebRootWhenMissing(environment, AppContext.BaseDirectory);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new CaseInsensitiveFileProvider(environment.WebRootFileProvider),
            ContentTypeProvider = CreateContentTypeProvider(),
        });
        return app;
    }

    /// <summary>
    /// The content root defaults to the working directory, so a published app started from elsewhere
    /// (<c>dotnet /app/eShop.Catalog.WebForms.dll</c>) has no wwwroot and every picture and stylesheet 404s.
    /// Falls back to the wwwroot published next to the assembly when the content root has none.
    /// </summary>
    public static void UsePublishedWebRootWhenMissing(IWebHostEnvironment environment, string appBaseDirectory)
    {
        if (environment.WebRootPath is { Length: > 0 } webRoot && Directory.Exists(webRoot))
        {
            return;
        }

        var publishedWebRoot = Path.Combine(appBaseDirectory, "wwwroot");
        if (!Directory.Exists(publishedWebRoot))
        {
            return;
        }

        environment.WebRootPath = publishedWebRoot;
        environment.WebRootFileProvider = new PhysicalFileProvider(publishedWebRoot);
    }

    public static FileExtensionContentTypeProvider CreateContentTypeProvider()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".js"] = "application/javascript";
        provider.Mappings[".ttf"] = "application/x-font-ttf";
        provider.Mappings[".woff"] = "application/font-woff";
        provider.Mappings[".woff2"] = "application/octet-stream"; // legacy (Mono) has no .woff2 type
        provider.Mappings[".eot"] = "application/vnd.ms-fontobject";
        return provider;
    }

    public static IApplicationBuilder UseCatalogSession(this IApplicationBuilder app)
    {
        app.UseSession();
        app.UseMiddleware<SessionInfoMiddleware>();
        return app;
    }

    public static IEndpointRouteBuilder MapCatalogPages(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRazorPages();
        return endpoints;
    }

    /// <summary>Generic ASP.NET error page (legacy <c>customErrors</c> default for remote clients), HTML-encoded.</summary>
    public static Task WriteErrorPageAsync(HttpResponse response, int statusCode)
    {
        var message = statusCode switch
        {
            StatusCodes.Status404NotFound => "The resource cannot be found.",
            StatusCodes.Status500InternalServerError => "Runtime Error",
            _ => ReasonPhrase(statusCode),
        };
        response.StatusCode = statusCode;
        response.ContentType = "text/html; charset=utf-8";
        var encoded = WebUtility.HtmlEncode(message);
        return response.WriteAsync(
            "<!DOCTYPE html>\n<html>\n<head>\n<title>" + encoded + "</title>\n</head>\n<body>\n" +
            "<h1>Server Error in '/' Application.</h1>\n<h2><i>" + encoded + "</i></h2>\n" +
            "<p>HTTP " + statusCode.ToString(CultureInfo.InvariantCulture) + "</p>\n</body>\n</html>\n");
    }

    private static string ReasonPhrase(int statusCode) =>
        Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(statusCode) is { Length: > 0 } phrase
            ? phrase
            : "Error";
}
