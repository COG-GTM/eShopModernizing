namespace eShopNet8.WebForms.Extensions;

// OWNED BY T5 (WebForms catalog -> Razor Pages: list/paging, details, create, edit, delete, about, contact, static assets).
public static class CatalogPagesExtensions
{
    public static IServiceCollection AddCatalogPages(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public static WebApplication UseCatalogPages(this WebApplication app)
    {
        return app;
    }

    public static WebApplication MapCatalogPages(this WebApplication app)
    {
        // Scaffold placeholder so the app answers on :5201. T5 replaces this with the Razor Pages catalog.
        app.MapGet("/", () => Results.Text("eShopNet8.WebForms scaffold - Razor Pages catalog pending (T5)"));
        return app;
    }
}
