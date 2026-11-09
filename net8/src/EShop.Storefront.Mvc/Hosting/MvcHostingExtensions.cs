// Owned by T5 (hosting, layout, static content).
namespace EShop.Storefront.Mvc.Hosting;

public static class MvcHostingExtensions
{
    public static IServiceCollection AddMvcHosting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllersWithViews();
        services.AddLegacyHostingServices();
        return services;
    }

    /// <summary>
    /// Pipeline equivalent of the legacy Global.asax + Web.config: request log scope (Application_BeginRequest),
    /// error statuses, en-US globalization, static files and the BundleConfig bundles, then routing and session (Session_Start).
    /// </summary>
    public static WebApplication UseMvcHosting(this WebApplication app)
    {
        app.UseLegacyHostingPipeline("WebApplication_BeginRequest", LegacyBundles.Mvc);
        return app;
    }
}
