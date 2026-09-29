using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace eShopRazorPages.Infrastructure;

/// <summary>
/// Attaches the legacy WebForms route names and URL patterns from <see cref="LegacyRoutes"/> to the Razor
/// Pages that replace them. A page's own <c>@page</c> template is reused (and named) when it already equals
/// the legacy pattern; otherwise an extra selector is added.
/// </summary>
public sealed class LegacyRouteConvention : IPageRouteModelConvention
{
    public void Apply(PageRouteModel model)
    {
        foreach (var route in LegacyRoutes.All.Where(r => IsForPage(r.RazorPage, model)))
        {
            var existing = model.Selectors.FirstOrDefault(s =>
                string.Equals(s.AttributeRouteModel?.Template, route.Template, StringComparison.OrdinalIgnoreCase));

            if (existing?.AttributeRouteModel is { } attributeRoute)
            {
                attributeRoute.Name = route.Name;
                attributeRoute.SuppressLinkGeneration = !route.GeneratesLinks;
                continue;
            }

            model.Selectors.Add(CreateSelector(route.Template, route.Name, suppressLinkGeneration: !route.GeneratesLinks));
        }

        foreach (var alias in LegacyRoutes.PhysicalPageAliases.Where(a => IsForPage(a.RazorPage, model)))
        {
            model.Selectors.Add(CreateSelector(alias.Template, name: null, suppressLinkGeneration: true));
        }
    }

    private static bool IsForPage(string razorPage, PageRouteModel model) =>
        string.Equals(razorPage, model.ViewEnginePath, StringComparison.OrdinalIgnoreCase);

    private static SelectorModel CreateSelector(string template, string? name, bool suppressLinkGeneration) => new()
    {
        AttributeRouteModel = new AttributeRouteModel
        {
            Template = template,
            Name = name,
            SuppressLinkGeneration = suppressLinkGeneration,
        },
    };
}
