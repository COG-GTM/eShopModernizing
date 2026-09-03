using eShopPortedWebForms.Models;
using eShopPortedWebForms.Models.Infrastructure;
using eShopPortedWebForms.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var useMockData = builder.Configuration.GetValue("UseMockData", true);

if (useMockData)
{
    builder.Services.AddSingleton<ICatalogService, CatalogServiceMock>();
}
else
{
    builder.Services.AddDbContext<CatalogDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("CatalogDBContext")));
    builder.Services.AddScoped<ICatalogService, CatalogService>();
}

var app = builder.Build();

if (!useMockData)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await CatalogDbInitializer.SeedAsync(context);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
