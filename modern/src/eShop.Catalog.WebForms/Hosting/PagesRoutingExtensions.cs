namespace eShop.Catalog.WebForms.Hosting;

/// <summary>
/// Owned by slice S7 (list/details/delete). Maps the legacy WebForms page routes
/// (/Default/index/{index}/size/{size}, /Catalog/Details/{id}, /Catalog/Edit/{id}, /Catalog/Delete/{id}, /Catalog/Create)
/// onto Razor Pages via route conventions; S8 adds Create/Edit conventions here too.
/// </summary>
public static class PagesRoutingExtensions
{
    public static IServiceCollection AddCatalogPagesRouting(this IServiceCollection services)
    {
        return services;
    }
}
