// Owned by T5 (hosting, layout, static content).
namespace EShop.Storefront.WebForms.Hosting;

public static class WebFormsHostingExtensions
{
    // Mono/xsp serves none of the legacy BundleConfig URLs (/Content/css, /bundles/WebFormsJs, ...): they are 404 in the baseline.
    private static readonly IReadOnlyDictionary<string, string> NoBundles = new Dictionary<string, string>();

    public static IServiceCollection AddWebFormsHosting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRazorPages();
        services.AddLegacyHostingServices();
        return services;
    }

    /// <summary>
    /// Pipeline equivalent of the legacy Global.asax + Web.config: request log scope (Application_BeginRequest),
    /// error statuses, en-US globalization, static files, then routing and session (Session_Start).
    /// </summary>
    public static WebApplication UseWebFormsHosting(this WebApplication app)
    {
        app.UseLegacyHostingPipeline("Application_BeginRequest", NoBundles);
        return app;
    }
}
