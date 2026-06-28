using Catalog.Api.Infrastructure;
using Catalog.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Mock-data mode (UseMockData=true) mirrors the legacy app's mock toggle and is
// the default for the demo. Otherwise an EF Core InMemory catalog is used; swap
// UseInMemoryDatabase for UseSqlServer to target a real Azure SQL database.
var useMockData = builder.Configuration.GetValue("UseMockData", true);

if (useMockData)
{
    builder.Services.AddSingleton<ICatalogService, CatalogServiceMock>();
}
else
{
    builder.Services.AddDbContext<CatalogDbContext>(o => o.UseInMemoryDatabase("CatalogDb"));
    builder.Services.AddScoped<ICatalogService, CatalogService>();
}

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

if (!useMockData)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    db.Database.EnsureCreated();
    db.EnsureSeeded();
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Catalog API (.NET 8)" }));
app.MapControllers();

app.Run();

// Exposed so the WebApplicationFactory integration tests can reference the entry point.
public partial class Program { }
