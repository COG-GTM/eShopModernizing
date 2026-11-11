using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShop.Catalog.WebForms.Hosting;

/// <summary>
/// Owned by slice S7 (list/details/delete). Maps the legacy WebForms page routes
/// (/Default/index/{index}/size/{size}, /Catalog/Details/{id}, /Catalog/Edit/{id}, /Catalog/Delete/{id}, /Catalog/Create)
/// onto Razor Pages via route conventions; S8 adds Create/Edit conventions here too.
/// Details/Delete use <c>@page "{id}"</c>; the /Default and *.aspx aliases belong to S6.
/// </summary>
public static class PagesRoutingExtensions
{
    public const string IndexPage = "/Index";

    /// <summary>Legacy RouteConfig "ProductsByPageRoute".</summary>
    public const string ProductsByPageTemplate = "Default/index/{index}/size/{size}";

    public static IServiceCollection AddCatalogPagesRouting(this IServiceCollection services)
    {
        services.Configure<RazorPagesOptions>(options =>
            options.Conventions.AddPageRouteModelConvention(IndexPage, ApplyLegacyListRoutes));
        return services;
    }

    /// <summary>Legacy serves the list at "/" and ProductsByPageRoute only; there is no "/Index" URL.</summary>
    public static void ApplyLegacyListRoutes(PageRouteModel model)
    {
        var indexSelectors = model.Selectors
            .Where(s => string.Equals(s.AttributeRouteModel?.Template, "Index", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var selector in indexSelectors)
        {
            model.Selectors.Remove(selector);
        }

        model.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel
            {
                Template = ProductsByPageTemplate,
                SuppressLinkGeneration = true,
            },
        });
    }
}
