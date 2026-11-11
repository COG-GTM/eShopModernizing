using eShop.Catalog.Data.DependencyInjection;
using eShop.Catalog.Mvc.Hosting;

// Each line is owned by one slice (see modern/README.md); slices edit their own Hosting/*.cs file, not this one.
var builder = WebApplication.CreateBuilder(args);

builder.AddCatalogObservability();                          // S9  Hosting/ObservabilityExtensions.cs
builder.Services.AddCatalogData(builder.Configuration);     // S1  eShop.Catalog.Data (mock until S1 lands)
builder.Services.AddCatalogMvcUi();                         // S2  Hosting/MvcUiExtensions.cs (S3/S4 controllers + views are discovered)
builder.Services.AddCatalogWebApi();                        // S5  Hosting/WebApiExtensions.cs

var app = builder.Build();

app.UseCatalogForwardedHeaders();                           // S2  Hosting/MvcUiExtensions.cs
app.UseCatalogSession();                                    // S2  Hosting/MvcUiExtensions.cs
app.UseCatalogErrorHandling();                              // S2  Hosting/MvcUiExtensions.cs
app.UseCatalogLocalization();                               // S2  Hosting/MvcUiExtensions.cs
app.UseCatalogStaticFiles();                                // S2  Hosting/MvcUiExtensions.cs
app.UseRouting();
app.MapCatalogHealthChecks();                               // S9  Hosting/ObservabilityExtensions.cs (/healthz)
app.MapCatalogWebApi();                                     // S5  Hosting/WebApiExtensions.cs
app.MapCatalogMvcRoutes();                                  // S3  Hosting/MvcRoutingExtensions.cs

app.Run();

public partial class Program
{
}
