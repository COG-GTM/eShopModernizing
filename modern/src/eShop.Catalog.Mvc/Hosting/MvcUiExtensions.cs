namespace eShop.Catalog.Mvc.Hosting;

/// <summary>Owned by slice S2 (MVC host, layout, static files, error handling).</summary>
public static class MvcUiExtensions
{
    public static IServiceCollection AddCatalogMvcUi(this IServiceCollection services)
    {
        services.AddControllersWithViews();
        return services;
    }

    public static IApplicationBuilder UseCatalogErrorHandling(this IApplicationBuilder app)
    {
        return app;
    }

    public static IApplicationBuilder UseCatalogStaticFiles(this IApplicationBuilder app)
    {
        return app;
    }
}
