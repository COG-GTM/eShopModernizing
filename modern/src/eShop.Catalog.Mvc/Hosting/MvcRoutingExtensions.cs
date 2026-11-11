using System.Globalization;

namespace eShop.Catalog.Mvc.Hosting;

/// <summary>Owned by slice S3 (MVC read flows). Mirrors legacy RouteConfig: attribute routes + {controller=Catalog}/{action=Index}/{id?}.</summary>
public static class MvcRoutingExtensions
{
    public const string LegacyCultureName = "en-US";

    public static IEndpointRouteBuilder MapCatalogMvcRoutes(this IEndpointRouteBuilder endpoints)
    {
        // Legacy Web.config <globalization culture="en-US" uiCulture="en-US" />: prices render as "$19.50" whatever the container locale.
        var legacyCulture = CultureInfo.GetCultureInfo(LegacyCultureName);
        CultureInfo.DefaultThreadCurrentCulture = legacyCulture;
        CultureInfo.DefaultThreadCurrentUICulture = legacyCulture;

        endpoints.MapControllerRoute(
            name: "Default",
            pattern: "{controller=Catalog}/{action=Index}/{id?}");
        return endpoints;
    }
}
