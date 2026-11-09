// FROZEN: this file only wires per-ticket extension methods. Tickets implement them in their own folders
// (T1 EShop.Catalog, T2 Catalog/, T3 Api/, T5 Hosting/) and must never edit this file.
using EShop.Catalog;
using EShop.Storefront.Mvc.Api;
using EShop.Storefront.Mvc.Catalog;
using EShop.Storefront.Mvc.Hosting;

namespace EShop.Storefront.Mvc;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddCatalogData(builder.Configuration); // T1
        builder.Services.AddMvcHosting(builder.Configuration);  // T5
        builder.Services.AddCatalogMvc();                       // T2
        builder.Services.AddCatalogApi();                       // T3

        var app = builder.Build();

        app.UseMvcHosting();                    // T5: error pages, static files, bundles, session, routing
        await app.InitializeCatalogDataAsync(); // T1
        app.MapCatalogMvc();                    // T2: /, /Catalog/{action}/{id?}
        app.MapCatalogApi();                    // T3: /api/brands, /api/files, /items/{id}/pic

        await app.RunAsync();
    }
}
