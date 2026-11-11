namespace eShop.Catalog.Mvc.Hosting;

/// <summary>Owned by slice S5 (legacy Web API: /api/brands, /api/files, /api). Controllers go in Controllers/Api.</summary>
public static class WebApiExtensions
{
    public static IServiceCollection AddCatalogWebApi(this IServiceCollection services)
    {
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogWebApi(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}
