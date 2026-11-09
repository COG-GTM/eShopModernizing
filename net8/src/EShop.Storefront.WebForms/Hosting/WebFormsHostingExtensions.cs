// Owned by T5 (hosting, layout, static content). Scaffold stub.
namespace EShop.Storefront.WebForms.Hosting;

public static class WebFormsHostingExtensions
{
    public static IServiceCollection AddWebFormsHosting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRazorPages();
        return services;
    }

    public static WebApplication UseWebFormsHosting(this WebApplication app)
    {
        app.UseStaticFiles();
        app.UseRouting();
        return app;
    }
}
