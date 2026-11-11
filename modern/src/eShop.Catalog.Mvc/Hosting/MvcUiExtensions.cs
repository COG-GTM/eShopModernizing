using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace eShop.Catalog.Mvc.Hosting;

/// <summary>Owned by slice S2 (MVC host, layout, static files, error handling, session).</summary>
public static class MvcUiExtensions
{
    public const string ErrorViewPath = "~/Views/Shared/Error.cshtml";

    // Legacy <globalization uiCulture="en-US" culture="en-US" /> pins the culture regardless of the browser.
    public static readonly CultureInfo LegacyCulture = CultureInfo.GetCultureInfo("en-US");

    public static IServiceCollection AddCatalogMvcUi(this IServiceCollection services)
    {
        services.AddControllersWithViews(options => options.Filters.Add<CatalogSessionStartFilter>());

        services.TryAddSingletonTimeProvider();
        services.AddSingleton<CatalogSessionInitializer>();
        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            // Legacy <sessionState mode="InProc" /> with the default 20 minute timeout. The cookie name differs from
            // legacy ASP.NET_SessionId on purpose: both apps can share a host behind the strangler gateway.
            options.Cookie.Name = ".eShop.Catalog.Mvc.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.IdleTimeout = TimeSpan.FromMinutes(20);
        });

        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(LegacyCulture, LegacyCulture);
            options.SupportedCultures = new[] { LegacyCulture };
            options.SupportedUICultures = new[] { LegacyCulture };
            options.RequestCultureProviders.Clear();
        });

        services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>(ConfigureForwardedHeaders);

        return services;
    }

    /// <summary>X-Forwarded-For/Proto from loopback plus any "ForwardedHeaders:KnownProxies" / "KnownNetworks" (CIDR) configured.</summary>
    public static IApplicationBuilder UseCatalogForwardedHeaders(this IApplicationBuilder app)
    {
        return app.UseForwardedHeaders(app.ApplicationServices.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value);
    }

    /// <summary>
    /// Legacy FilterConfig HandleErrorAttribute: unhandled exceptions render Views/Shared/Error.cshtml (through the
    /// layout, as MVC 5 applied _ViewStart) with 500. Requests that match no endpoint get the ASP.NET 404 page; 404s
    /// produced by actions (HttpNotFound) keep their empty body.
    /// </summary>
    public static IApplicationBuilder UseCatalogErrorHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = RenderErrorViewAsync });
        app.UseStatusCodePages(context => WriteNotFoundPageAsync(context.HttpContext));
        return app;
    }

    public static IApplicationBuilder UseCatalogLocalization(this IApplicationBuilder app)
    {
        return app.UseRequestLocalization();
    }

    /// <summary>Content, Scripts, Images, Pics, fonts and favicon.ico from wwwroot, matched case-insensitively like IIS.</summary>
    public static IApplicationBuilder UseCatalogStaticFiles(this IApplicationBuilder app)
    {
        var environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        return app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new CaseInsensitiveFileProvider(environment.WebRootFileProvider),
            ContentTypeProvider = CreateContentTypeProvider(),
        });
    }

    public static FileExtensionContentTypeProvider CreateContentTypeProvider()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".woff2"] = "application/octet-stream"; // legacy (Mono) has no .woff2 type
        return provider;
    }

    public static IApplicationBuilder UseCatalogSession(this IApplicationBuilder app)
    {
        return app.UseSession();
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (!services.Any(d => d.ServiceType == typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }

    private static void ConfigureForwardedHeaders(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
        {
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        }

        foreach (var network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
        {
            options.KnownNetworks.Add(Microsoft.AspNetCore.HttpOverrides.IPNetwork.Parse(network));
        }
    }

    private static async Task RenderErrorViewAsync(HttpContext context)
    {
        var result = new ViewResult { ViewName = ErrorViewPath, StatusCode = StatusCodes.Status500InternalServerError };
        await result.ExecuteResultAsync(new ActionContext(context, context.GetRouteData(), new ActionDescriptor()));
    }

    private static Task WriteNotFoundPageAsync(HttpContext context)
    {
        if (context.Response.StatusCode != StatusCodes.Status404NotFound || context.GetEndpoint() is not null)
        {
            return Task.CompletedTask;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        return context.Response.WriteAsync(NotFoundPage);
    }

    internal const string NotFoundPage = """
        <!DOCTYPE html>
        <html>
            <head>
                <title>The resource cannot be found.</title>
                <meta name="viewport" content="width=device-width" />
            </head>
            <body bgcolor="white">
                <span><h1>Server Error in '/' Application.<hr width=100% size=1 color=silver></h1>
                <h2> <i>The resource cannot be found.</i> </h2></span>
                <b> Description: </b>HTTP 404. The resource you are looking for (or one of its dependencies) could have been removed, had its name changed, or is temporarily unavailable. &nbsp;Please review the following URL and make sure that it is spelled correctly.
            </body>
        </html>
        """;
}
