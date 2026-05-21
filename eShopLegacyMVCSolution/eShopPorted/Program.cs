using eShopPorted.Models;
using eShopPorted.Models.Infrastructure;
using eShopPorted.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add MVC services
builder.Services.AddControllersWithViews();

// Configure services based on mock data setting
bool useMockData = builder.Configuration.GetValue<bool>("UseMockData");

if (useMockData)
{
    builder.Services.AddSingleton<ICatalogService, CatalogServiceMock>();
}
else
{
    string connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<CatalogDBContext>(options =>
        options.UseSqlServer(connectionString)
    );
    builder.Services.AddScoped<ICatalogService, CatalogService>();
    builder.Services.AddSingleton<CatalogItemHiLoGenerator>();
}

var app = builder.Build();

// Seed database if not using mock data
if (!useMockData)
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<CatalogDBContext>();
        var generator = scope.ServiceProvider.GetRequiredService<CatalogItemHiLoGenerator>();
        bool useCustomizationData = builder.Configuration.GetValue<bool>("UseCustomizationData");
        CatalogDBSeeder.Seed(context, generator, app.Environment.ContentRootPath, useCustomizationData);
    }
}

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Catalog}/{action=Index}/{id?}");

app.Run();
