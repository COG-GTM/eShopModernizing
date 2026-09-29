using eShopCatalogApi.Data;
using eShopCatalogApi.Endpoints;
using eShopCatalogApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// The legacy service read an optional "ConnectionString" environment variable before
// falling back to the "EntityModel" connection string; keep the same precedence.
var connectionString = builder.Configuration["ConnectionString"]
    ?? builder.Configuration.GetConnectionString("EntityModel")
    ?? throw new InvalidOperationException("Connection string 'EntityModel' is not configured.");

builder.Services.AddDbContext<CatalogDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<ICatalogService, CatalogService>();

// Serialize using the exact DataMember names of the WCF contract (PascalCase).
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = null);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI();
}

app.MapCatalogEndpoints();
app.MapHealthChecks("/health");

if (app.Configuration.GetValue("Catalog:InitializeDatabase", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await CatalogDbInitializer.InitializeAsync(db);
}

app.Run();

public partial class Program;
