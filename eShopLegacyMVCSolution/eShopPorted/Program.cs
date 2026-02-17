using eShopPorted.Models;
using eShopPorted.Models.Infrastructure;
using eShopPorted.Services;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var appStartTime = DateTime.UtcNow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

bool useMockData = builder.Configuration.GetValue<bool>("UseMockData");
if (!useMockData)
{
    string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    builder.Services.AddDbContext<CatalogDBContext>(options =>
        options.UseSqlServer(connectionString));

    builder.Services.AddScoped<ICatalogService, CatalogService>();
}
else
{
    builder.Services.AddSingleton<ICatalogService, CatalogServiceMock>();
}

builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddCheck("ready", () =>
    {
        return DateTime.UtcNow - appStartTime > TimeSpan.FromSeconds(5)
            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Warm-up complete.")
            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded("Still warming up.");
    }, tags: new[] { "ready" });

if (!useMockData)
{
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<CatalogDBContext>("database", tags: new[] { "ready" });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Name == "self",
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapControllerRoute("default", "{controller=Catalog}/{action=Index}/{id?}");

app.Run();
