// FROZEN: this file only wires per-ticket extension methods. Tickets implement them in their own folders
// (T1 EShop.Catalog, T4 Catalog/ + Pages/Catalog/, T5 Hosting/) and must never edit this file.
using EShop.Catalog;
using EShop.Storefront.WebForms.Catalog;
using EShop.Storefront.WebForms.Hosting;

namespace EShop.Storefront.WebForms;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddCatalogData(builder.Configuration);     // T1
        builder.Services.AddWebFormsHosting(builder.Configuration); // T5
        builder.Services.AddCatalogPages();                         // T4

        var app = builder.Build();

        app.UseWebFormsHosting();               // T5: error pages, static files, session, routing
        await app.InitializeCatalogDataAsync(); // T1
        app.MapCatalogPages();                  // T4: /, /Default/index/{i}/size/{s}, /Catalog/{Create|Details|Edit|Delete}

        await app.RunAsync();
    }
}
