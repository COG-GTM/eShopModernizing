using System.Globalization;
using eShopNet8.Mvc.Controllers;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace eShopNet8.Mvc.Extensions;

// OWNED BY T3 (MVC catalog UI: controllers, views, pic route, static assets, session footer).
public static class CatalogMvcExtensions
{
    // Legacy Web.config <globalization culture="en-US" uiCulture="en-US" />: prices render as $19.50 on any host.
    private static readonly CultureInfo LegacyCulture = CultureInfo.GetCultureInfo("en-US");

    public static IServiceCollection AddCatalogMvc(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllersWithViews(options =>
        {
            // Legacy MVC 5 only enforced the explicit DataAnnotations; no implicit [Required] for non-nullable references.
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            options.Filters.Add<AntiforgeryFailureAs500Filter>();
        });

        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.Cookie.Name = "ASP.NET_SessionId";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.IdleTimeout = TimeSpan.FromMinutes(20);
        });
        return services;
    }

    public static WebApplication UseCatalogMvc(this WebApplication app)
    {
        // Legacy HandleErrorAttribute rendered the generic Shared/Error view with status 500; never expose stack traces.
        app.UseExceptionHandler(errorApp => errorApp.Run(LegacyErrorPage.WriteAsync));

        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(LegacyCulture),
            SupportedCultures = new[] { LegacyCulture },
            SupportedUICultures = new[] { LegacyCulture },
            RequestCultureProviders = new List<IRequestCultureProvider>(),
        });

        app.UseLegacyStaticFiles();
        app.UseSession();
        app.UseLegacySessionStart();
        app.UseRouting();
        return app;
    }

    public static WebApplication MapCatalogMvc(this WebApplication app)
    {
        app.MapLegacyBundles();
        app.MapControllerRoute(
            name: "Default",
            pattern: "{controller=Catalog}/{action=Index}/{id?}");
        return app;
    }

    // IIS serves static files case-insensitively (layout links /images/*, folder is Images; bundle uses site.css,
    // file is Site.css). Wrapping the web root provider keeps those legacy URLs working on Linux.
    private static void UseLegacyStaticFiles(this WebApplication app)
    {
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".woff"] = "application/font-woff";
        contentTypes.Mappings[".woff2"] = "application/font-woff2";

        var fileProvider = new CaseInsensitiveFileProvider(app.Environment.WebRootFileProvider);
        app.Environment.WebRootFileProvider = fileProvider;
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            ContentTypeProvider = contentTypes,
        });
    }

    // Global.asax Session_Start: store MachineName and SessionStartTime once per session (rendered in the footer).
    private static void UseLegacySessionStart(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Session.GetString(SessionKeys.MachineName) is null)
            {
                context.Session.SetString(SessionKeys.MachineName, Environment.MachineName);
                context.Session.SetString(SessionKeys.SessionStartTime, DateTime.Now.ToString(LegacyCulture));
            }
            await next();
        });
    }
}

public static class SessionKeys
{
    public const string MachineName = "MachineName";
    public const string SessionStartTime = "SessionStartTime";
}
