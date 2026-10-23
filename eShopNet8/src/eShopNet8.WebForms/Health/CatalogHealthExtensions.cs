namespace eShopNet8.WebForms.Health;

// OWNED BY T6 (containers, compose, CI, health endpoints).
public static class CatalogHealthExtensions
{
    public static IServiceCollection AddCatalogHealth(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public static WebApplication MapCatalogHealth(this WebApplication app)
    {
        return app;
    }
}
