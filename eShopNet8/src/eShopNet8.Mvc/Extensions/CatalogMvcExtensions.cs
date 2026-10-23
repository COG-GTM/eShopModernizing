namespace eShopNet8.Mvc.Extensions;

// OWNED BY T3 (MVC catalog UI: controllers, views, pic route, static assets, session footer).
public static class CatalogMvcExtensions
{
    public static IServiceCollection AddCatalogMvc(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public static WebApplication UseCatalogMvc(this WebApplication app)
    {
        return app;
    }

    public static WebApplication MapCatalogMvc(this WebApplication app)
    {
        // Scaffold placeholder so the app answers on :5200. T3 replaces this with the catalog controllers.
        app.MapGet("/", () => Results.Text("eShopNet8.Mvc scaffold - catalog UI pending (T3)"));
        return app;
    }
}
