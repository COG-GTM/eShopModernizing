using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Vans.Catalog.Api.Models;
using Vans.Catalog.Api.Services;

var builder = WebApplication.CreateBuilder(args);

const string ApiTitle = "Vans Catalog API";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = ApiTitle,
        Version = "v1",
        Description = "Cross-platform .NET 8 port of the legacy eShop Catalog module (VF / Vans brand stack)."
    });
});

// Catalog data source. Defaults to EF Core InMemory ("UseMock": false).
// Set "Catalog:UseMock" to true to use the in-memory CatalogServiceMock instead.
var useMock = builder.Configuration.GetValue("Catalog:UseMock", false);
if (useMock)
{
    builder.Services.AddSingleton<ICatalogService, CatalogServiceMock>();
}
else
{
    builder.Services.AddDbContext<CatalogDbContext>(options =>
        options.UseInMemoryDatabase("VansCatalog"));
    builder.Services.AddScoped<ICatalogService, CatalogService>();
}

var app = builder.Build();

if (!useMock)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", ApiTitle));

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = ApiTitle }));

app.MapControllers();

app.Run();

public partial class Program { }
