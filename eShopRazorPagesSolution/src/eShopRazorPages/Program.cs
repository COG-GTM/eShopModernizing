using System.Globalization;
using eShopRazorPages.Infrastructure;
using eShopRazorPages.Models;
using eShopRazorPages.Models.Infrastructure;
using eShopRazorPages.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var catalogSettings = builder.Configuration.Get<CatalogSettings>() ?? new CatalogSettings();
builder.Services.Configure<CatalogSettings>(builder.Configuration);

builder.Services.AddRazorPages(options => options.Conventions.Add(new LegacyRouteConvention()));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

if (catalogSettings.UseMockData)
{
    builder.Services.AddSingleton<ICatalogService, CatalogServiceMock>();
}
else
{
    var connectionString = builder.Configuration["CatalogDBContext"]
        ?? builder.Configuration.GetConnectionString("CatalogDBContext")
        ?? throw new InvalidOperationException("A 'CatalogDBContext' connection string is required when UseMockData is false.");

    builder.Services.AddDbContext<CatalogDBContext>(options => options.UseSqlServer(connectionString));
    builder.Services.AddScoped<ICatalogService, CatalogService>();
    builder.Services.AddScoped<CatalogDBInitializer>();
}

var app = builder.Build();

if (!catalogSettings.UseMockData)
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<CatalogDBInitializer>().Initialize();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

var legacyCulture = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(legacyCulture),
    SupportedCultures = new[] { legacyCulture },
    SupportedUICultures = new[] { legacyCulture },
});

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseLegacySessionInfo();

app.MapRazorPages();

app.Run();

public partial class Program
{
}
