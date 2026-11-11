namespace eShop.Catalog.Mvc.Hosting;

/// <summary>Owned by slice S3 (MVC read flows). Mirrors legacy RouteConfig: {controller=Catalog}/{action=Index}/{id?}.</summary>
public static class MvcRoutingExtensions
{
    public static IEndpointRouteBuilder MapCatalogMvcRoutes(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllerRoute(
            name: "Default",
            pattern: "{controller=Catalog}/{action=Index}/{id?}");
        return endpoints;
    }
}
