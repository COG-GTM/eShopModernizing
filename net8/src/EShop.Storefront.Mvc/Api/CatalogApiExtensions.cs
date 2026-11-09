// Owned by T3 (legacy Web API + pictures).
namespace EShop.Storefront.Mvc.Api;

public static class CatalogApiExtensions
{
    public static IServiceCollection AddCatalogApi(this IServiceCollection services) => services;

    /// <summary>
    /// Legacy routes: Web API 2 convention route <c>api/{controller}/{id}</c> (BrandsController, FilesController)
    /// and the MVC attribute route <c>items/{catalogItemId:int}/pic</c> (PicController).
    /// </summary>
    public static WebApplication MapCatalogApi(this WebApplication app)
    {
        app.Map("/api/{controller}/{id?}", LegacyWebApi.DispatchAsync);
        app.Map("/items/{catalogItemId:int}/pic", PicEndpoint.HandleAsync);
        return app;
    }
}
