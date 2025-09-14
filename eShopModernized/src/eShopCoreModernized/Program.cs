using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using eShopCoreModernized.Configuration;
using eShopCoreModernized.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    var keyVaultName = builder.Configuration["Azure:KeyVaultName"];
    if (!string.IsNullOrEmpty(keyVaultName))
    {
        var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
        builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
    }
}

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["Azure:ApplicationInsights:ConnectionString"];
});

builder.Services.AddDbContext<CatalogDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CatalogDBContext")));

builder.Services.AddSingleton<ICatalogConfiguration, CatalogConfiguration>();

var catalogConfig = new CatalogConfiguration(builder.Configuration);

if (catalogConfig.UseManagedIdentity)
{
    builder.Services.AddSingleton<ISqlConnectionFactory, ManagedIdentitySqlConnectionFactory>();
}
else
{
    builder.Services.AddSingleton<ISqlConnectionFactory, AppSettingsSqlConnectionFactory>();
}

if (catalogConfig.UseMockData)
{
    builder.Services.AddSingleton<ICatalogService, CatalogServiceMock>();
}
else
{
    builder.Services.AddScoped<ICatalogService, CatalogService>();
}

if (catalogConfig.UseAzureStorage)
{
    builder.Services.AddSingleton(x =>
    {
        var connectionString = catalogConfig.StorageConnectionString;
        return new BlobServiceClient(connectionString);
    });
    builder.Services.AddScoped<IImageService, ImageAzureStorage>();
}
else
{
    builder.Services.AddScoped<IImageService, ImageMockStorage>();
}

builder.Services.AddSingleton<CatalogItemHiLoGenerator>();

if (catalogConfig.UseAzureActiveDirectory)
{
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("Azure:ActiveDirectory"));
}

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!catalogConfig.UseMockData)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<CatalogDBContext>();
    context.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

if (catalogConfig.UseAzureActiveDirectory)
{
    app.UseAuthentication();
}
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Catalog}/{action=Index}/{id?}");

app.Run();
