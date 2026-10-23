using eShop.Catalog.Infrastructure.Data;
using eShop.Catalog.Infrastructure.DependencyInjection;
using eShopNet8.WebForms.Hosting;

namespace eShopNet8.WebForms
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

            builder.Services.AddCatalogData(builder.Configuration);         // T1 data layer (SQL mode)
            builder.Services.AddCatalogServices(builder.Configuration);     // T2 services + UseMockData toggle
            builder.Services.AddCatalogPages(builder.Configuration);        // T5 Razor Pages port of the WebForms catalog
            builder.Services.AddCatalogHealthChecks(builder.Configuration); // T6 container health

            var app = builder.Build();

            app.UseCatalogPages();          // T5: error handling, en-US culture, static files, session, routing
            app.MapCatalogHealthChecks();   // T6: GET /health
            app.MapCatalogPages();          // T5: /, /Default, /Default/index/{index}/size/{size}, /Catalog/*

            return app;
        }
    }
}
