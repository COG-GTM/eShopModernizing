namespace eShop.Catalog.WebForms.Hosting;

/// <summary>Owned by slice S6 (Razor Pages host, layout, static files, session, About/Contact).</summary>
public static class PagesUiExtensions
{
    public static IServiceCollection AddCatalogPagesUi(this IServiceCollection services)
    {
        services.AddRazorPages();
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

    public static IApplicationBuilder UseCatalogSession(this IApplicationBuilder app)
    {
        return app;
    }

    public static IEndpointRouteBuilder MapCatalogPages(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRazorPages();
        return endpoints;
    }
}
