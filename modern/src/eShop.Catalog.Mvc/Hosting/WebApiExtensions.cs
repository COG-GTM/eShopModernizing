namespace eShop.Catalog.Mvc.Hosting;

/// <summary>
/// Owned by slice S5 (legacy Web API: /api/brands, /api/files, /api). Controllers live in Controllers/Api and are
/// attribute-routed, so they take precedence over the conventional MVC route.
/// </summary>
public static class WebApiExtensions
{
    public static IServiceCollection AddCatalogWebApi(this IServiceCollection services)
    {
        services.AddControllers();
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogWebApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        return endpoints;
    }
}
