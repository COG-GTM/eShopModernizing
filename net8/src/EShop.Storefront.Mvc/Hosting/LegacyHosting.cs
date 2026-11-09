using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace EShop.Storefront.Mvc.Hosting;

public static class LegacyHosting
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    public static IServiceCollection AddLegacyHostingServices(this IServiceCollection services)
    {
        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });
        services.Configure<RequestLocalizationOptions>(options =>
        {
            // <globalization culture="en-US" uiCulture="en-US" /> in the legacy Web.config.
            options.DefaultRequestCulture = new RequestCulture(Culture, Culture);
            options.SupportedCultures = new[] { Culture };
            options.SupportedUICultures = new[] { Culture };
            options.RequestCultureProviders.Clear();
        });
        // Console logs show the request scope (log4net's %property{activityid} / %property{requestinfo}).
        services.AddLogging(logging => logging.AddSimpleConsole(options => options.IncludeScopes = true));
        return services;
    }

    public static WebApplication UseLegacyHostingPipeline(this WebApplication app, string beginRequestMessage,
        IReadOnlyDictionary<string, string> bundles)
    {
        app.UseMiddleware<RequestLoggingScopeMiddleware>(beginRequestMessage);
        app.UseLegacyErrorHandling();
        app.UseRequestLocalization();
        app.UseStaticFiles(LegacyStaticContent.CreateOptions(app.Environment.WebRootPath, bundles));
        app.UseRouting();
        app.UseSession();
        app.UseMiddleware<LegacySessionMiddleware>();
        return app;
    }
}
