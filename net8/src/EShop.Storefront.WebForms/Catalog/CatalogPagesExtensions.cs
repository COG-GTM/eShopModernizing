// Owned by T4 (WebForms catalog as Razor Pages).
namespace EShop.Storefront.WebForms.Catalog;

public static class CatalogPagesExtensions
{
    /// <summary>Registers the catalog Razor Pages with the legacy WebForms page routes (see <see cref="LegacyPageRouteConvention"/>).</summary>
    public static IServiceCollection AddCatalogPages(this IServiceCollection services)
    {
        services.AddRazorPages(options =>
        {
            options.Conventions.Add(new LegacyPageRouteConvention());
            options.Conventions.ConfigureFilter(new AntiforgeryFailureAs500Filter());
        });
        return services;
    }

    /// <summary>Maps the catalog pages: /, /Default, /Default.aspx, /Default/index/{index}/size/{size}, /Catalog/{Create|Details|Edit|Delete}.</summary>
    public static WebApplication MapCatalogPages(this WebApplication app)
    {
        app.MapRazorPages();
        return app;
    }
}
