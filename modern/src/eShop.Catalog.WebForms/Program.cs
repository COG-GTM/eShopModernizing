using eShop.Catalog.Data.DependencyInjection;
using eShop.Catalog.WebForms.Hosting;

// Each line is owned by one slice (see modern/README.md); slices edit their own Hosting/*.cs file, not this one.
var builder = WebApplication.CreateBuilder(args);

builder.AddCatalogObservability();                          // S9  Hosting/ObservabilityExtensions.cs
builder.Services.AddCatalogData(builder.Configuration);     // S1  eShop.Catalog.Data (mock until S1 lands)
builder.Services.AddCatalogPagesUi();                       // S6  Hosting/PagesUiExtensions.cs (S7/S8 pages are discovered)
builder.Services.AddCatalogPagesRouting();                  // S7  Hosting/PagesRoutingExtensions.cs (legacy MapPageRoute URLs)

var app = builder.Build();

app.UseCatalogErrorHandling();                              // S6  Hosting/PagesUiExtensions.cs
app.UseCatalogStaticFiles();                                // S6  Hosting/PagesUiExtensions.cs
app.UseCatalogSession();                                    // S6  Hosting/PagesUiExtensions.cs (session footer)
app.UseRouting();
app.MapCatalogHealthChecks();                               // S9  Hosting/ObservabilityExtensions.cs (/healthz)
app.MapCatalogPages();                                      // S6  Hosting/PagesUiExtensions.cs

app.Run();

public partial class Program
{
}
