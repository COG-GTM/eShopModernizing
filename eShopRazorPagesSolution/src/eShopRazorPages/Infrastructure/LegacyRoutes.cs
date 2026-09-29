namespace eShopRazorPages.Infrastructure;

/// <summary>
/// Single source of truth for the URLs registered by the legacy WebForms <c>RouteConfig.RegisterRoutes</c>
/// and the Razor Page that now serves each of them. Consumed by <see cref="LegacyRouteConvention"/> and by
/// the route-table tests.
/// </summary>
public static class LegacyRoutes
{
    public const string ProductsByPageRouteName = "ProductsByPageRoute";
    public const string CreateProductRouteName = "CreateProductRoute";
    public const string EditProductRouteName = "EditProductRoute";
    public const string ProductDetailsRouteName = "ProductDetailsRoute";
    public const string DeleteProductRouteName = "DeleteProductRoute";

    public static readonly IReadOnlyList<LegacyRoute> All = new[]
    {
        new LegacyRoute(null, "Default", "Default", "~/Default.aspx", "/Index", GeneratesLinks: false),
        new LegacyRoute(ProductsByPageRouteName, "Default/index/{index}/size/{size}", "Default/index/{index:int}/size/{size:int:min(1)}", "~/Default.aspx", "/Index", GeneratesLinks: true),
        new LegacyRoute(CreateProductRouteName, "Catalog/Create", "Catalog/Create", "~/Catalog/Create.aspx", "/Catalog/Create", GeneratesLinks: true),
        new LegacyRoute(EditProductRouteName, "Catalog/Edit/{id}", "Catalog/Edit/{id:int}", "~/Catalog/Edit.aspx", "/Catalog/Edit", GeneratesLinks: true),
        new LegacyRoute(ProductDetailsRouteName, "Catalog/Details/{id}", "Catalog/Details/{id:int}", "~/Catalog/Details.aspx", "/Catalog/Details", GeneratesLinks: true),
        new LegacyRoute(DeleteProductRouteName, "Catalog/Delete/{id}", "Catalog/Delete/{id:int}", "~/Catalog/Delete.aspx", "/Catalog/Delete", GeneratesLinks: true),
    };

    /// <summary>
    /// Physical <c>.aspx</c> paths that were reachable in the legacy app without route data. Pages that
    /// read <c>RouteData.Values["id"]</c> (Edit/Details/Delete) failed when requested this way, so they are
    /// intentionally not aliased.
    /// </summary>
    public static readonly IReadOnlyList<PhysicalPageAlias> PhysicalPageAliases = new[]
    {
        new PhysicalPageAlias("Default.aspx", "/Index"),
        new PhysicalPageAlias("Catalog/Create.aspx", "/Catalog/Create"),
    };
}

/// <param name="Name">Route name used by the legacy <c>GetRouteUrl</c> calls; null for unnamed routes.</param>
/// <param name="LegacyUrl">URL pattern exactly as registered in WebForms <c>RouteConfig</c>.</param>
/// <param name="Template">ASP.NET Core route template (legacy pattern plus type constraints).</param>
/// <param name="LegacyPage">WebForms page that handled the route.</param>
/// <param name="RazorPage">Razor Page (view engine path) that now handles the route.</param>
/// <param name="GeneratesLinks">Whether the route participates in outbound link generation.</param>
public sealed record LegacyRoute(
    string? Name,
    string LegacyUrl,
    string Template,
    string LegacyPage,
    string RazorPage,
    bool GeneratesLinks);

public sealed record PhysicalPageAlias(string Template, string RazorPage);
