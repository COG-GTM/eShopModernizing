using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Timberland.Catalog.Api.Infrastructure;
using Timberland.Catalog.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Timberland Catalog API",
        Version = "v1",
        Description = ".NET 8 ASP.NET Core port of the legacy eShop Catalog module (VF Corporation / Timberland brand stack)."
    });
});

// Choose the data backing store. Default is the in-memory mock (faithful port of
// the legacy CatalogServiceMock). Set "Catalog:UseMock" to false to use the EF Core
// InMemory provider instead.
var useMock = builder.Configuration.GetValue("Catalog:UseMock", true);
if (useMock)
{
    builder.Services.AddSingleton<ICatalogService, CatalogServiceMock>();
}
else
{
    builder.Services.AddDbContext<CatalogDbContext>(options =>
        options.UseInMemoryDatabase("TimberlandCatalog"));
    builder.Services.AddScoped<ICatalogService, CatalogService>();
}

var app = builder.Build();

if (!useMock)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    db.EnsureSeeded();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Timberland Catalog API v1");
    options.DocumentTitle = "Timberland Catalog API";
});

app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed so the test project can reference the entry point for WebApplicationFactory.
public partial class Program { }
