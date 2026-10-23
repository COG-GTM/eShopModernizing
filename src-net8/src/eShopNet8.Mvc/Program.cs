using eShop.Catalog.Infrastructure.Data;
using eShop.Catalog.Infrastructure.DependencyInjection;
using eShopNet8.Mvc.Hosting;

namespace eShopNet8.Mvc
{
    // Shared scaffold file: tickets plug in through the extension methods, never by editing this file.
    public partial class Program
    {
        public static async Task Main(string[] args)
        {
            var app = BuildApp(args);
            await app.InitializeCatalogDataAsync();
            await app.RunAsync();
        }

        public static WebApplication BuildApp(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddCatalogData(builder.Configuration);       // T1 data layer (SQL mode)
            builder.Services.AddCatalogServices(builder.Configuration);   // T2 services + UseMockData toggle
            builder.Services.AddCatalogMvc(builder.Configuration);        // T3 MVC catalog UI
            builder.Services.AddCatalogApi(builder.Configuration);        // T4 Web API
            builder.Services.AddCatalogHealthChecks(builder.Configuration); // T6 container health

            var app = builder.Build();

            app.UseCatalogMvc();            // T3: error handling, en-US culture, static files, session, routing
            app.MapCatalogHealthChecks();   // T6: GET /health
            app.MapCatalogApi();            // T4: /api/brands, /api/files
            app.MapCatalogMvc();            // T3: catalog + pic routes

            return app;
        }
    }
}
