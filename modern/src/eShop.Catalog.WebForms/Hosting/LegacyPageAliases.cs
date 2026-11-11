using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace eShop.Catalog.WebForms.Hosting;

/// <summary>
/// Owned by slice S6. Extra URLs under which legacy WebForms served each page: the <c>Default</c> route from
/// <c>RouteConfig</c> and the physical <c>.aspx</c> paths (the targets of every <c>MapPageRoute</c>).
/// The parameterised RouteConfig routes (<c>Default/index/{index}/size/{size}</c>, <c>Catalog/Details/{id}</c>, ...)
/// belong to the owning page slices (S7/S8). An alias for a page that is not in the app is ignored.
/// </summary>
public static class LegacyPageAliases
{
    public static readonly IReadOnlyList<(string Page, string Alias)> Table =
    [
        ("/Index", "Default"),
        ("/Index", "Default.aspx"),
        ("/About", "About.aspx"),
        ("/Contact", "Contact.aspx"),
        ("/Catalog/Create", "Catalog/Create.aspx"),
        ("/Catalog/Edit", "Catalog/Edit.aspx"),
        ("/Catalog/Details", "Catalog/Details.aspx"),
        ("/Catalog/Delete", "Catalog/Delete.aspx"),
    ];

    public static PageConventionCollection AddLegacyPageAliases(this PageConventionCollection conventions)
    {
        foreach (var (page, alias) in Table)
        {
            conventions.AddPageRoute(page, alias);
        }

        return conventions;
    }
}
