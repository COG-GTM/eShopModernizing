using Autofac;
using Autofac.Extensions.DependencyInjection;
using eShopModernizedCore.Configuration;
using eShopModernizedCore.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    var useMockData = builder.Configuration.GetValue<bool>("UseMockData");
    containerBuilder.RegisterModule(new ApplicationModule(useMockData));
});

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<CatalogDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("CatalogDBContext");
    options.UseSqlServer(connectionString);
});

builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Catalog}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var useMockData = app.Configuration.GetValue<bool>("UseMockData");
    
    if (!useMockData)
    {
        context.Database.EnsureCreated();
    }
}

app.Run();
