// Owned by T5 (hosting, layout, static content). Scaffold stub.
namespace EShop.Storefront.Mvc.Hosting;

public static class MvcHostingExtensions
{
    public static IServiceCollection AddMvcHosting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllersWithViews();
        return services;
    }

    public static WebApplication UseMvcHosting(this WebApplication app)
    {
        app.UseStaticFiles();
        app.UseRouting();
        return app;
    }
}
