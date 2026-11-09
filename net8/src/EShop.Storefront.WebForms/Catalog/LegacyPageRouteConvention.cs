using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace EShop.Storefront.WebForms.Catalog;

/// <summary>
/// Replaces the folder-based Razor Pages routes with the legacy RouteConfig.MapPageRoute routes plus the physical
/// .aspx paths. Route values are unconstrained on purpose: legacy Convert.ToInt32 on a non-numeric value is a 500.
/// </summary>
public sealed class LegacyPageRouteConvention : IPageRouteModelConvention
{
    private static readonly Dictionary<string, string[]> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/Catalog/Default"] = ["", "Default", "Default.aspx", "Default/index/{index}/size/{size}"],
        ["/Catalog/Create"] = ["Catalog/Create", "Catalog/Create.aspx"],
        ["/Catalog/Details"] = ["Catalog/Details/{id}", "Catalog/Details.aspx"],
        ["/Catalog/Edit"] = ["Catalog/Edit/{id}", "Catalog/Edit.aspx"],
        ["/Catalog/Delete"] = ["Catalog/Delete/{id}", "Catalog/Delete.aspx"],
    };

    public void Apply(PageRouteModel model)
    {
        if (!Routes.TryGetValue(model.ViewEnginePath, out var templates))
        {
            return;
        }

        model.Selectors.Clear();
        foreach (var template in templates)
        {
            model.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel { Template = template, SuppressLinkGeneration = true },
            });
        }
    }
}
