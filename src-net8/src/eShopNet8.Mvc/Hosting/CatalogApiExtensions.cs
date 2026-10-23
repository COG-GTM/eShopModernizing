using eShopNet8.Mvc.Api;

namespace eShopNet8.Mvc.Hosting
{
    // Owned by ticket T4 (Web API): /api/brands and /api/files as minimal APIs.
    // Legacy Controllers/Api/CatalogController2 ([Route("api")] hello world) is deliberately not ported: MVC 5 never
    // discovered it (type name does not end in "Controller"), so GET /api stays 404.
    public static class CatalogApiExtensions
    {
        public static IServiceCollection AddCatalogApi(this IServiceCollection services, IConfiguration configuration)
        {
            return services;
        }

        public static WebApplication MapCatalogApi(this WebApplication app)
        {
            app.MapBrandsApi();
            app.MapFilesApi();
            return app;
        }
    }
}
