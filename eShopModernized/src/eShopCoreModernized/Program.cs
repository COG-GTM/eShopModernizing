using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using eShopCoreModernized.Configuration;
using eShopCoreModernized.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs;
using Azure.Identity;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using eShopCoreModernized.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.Configure(options =>
    options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

if (!builder.Environment.IsDevelopment())
{
    var keyVaultName = builder.Configuration["Azure:KeyVaultName"];
    if (!string.IsNullOrEmpty(keyVaultName))
    {
        var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
        builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
    }
}

var deploymentInfo = DeploymentInfo.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(deploymentInfo);

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["Azure:ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = false;
    options.EnableQuickPulseMetricStream = true;
});

builder.Services.AddSingleton<Microsoft.ApplicationInsights.Extensibility.ITelemetryInitializer, eShopCoreModernized.Infrastructure.MigrationTelemetryInitializer>();

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
else
{
    builder.Services.AddAuthentication("Cookies")
        .AddCookie("Cookies", options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });
}

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddCatalogHealthChecks(catalogConfig);

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        var correlationId = RequestLoggingMiddleware.GetCorrelationId(context.HttpContext);
        if (correlationId != null)
        {
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
        }
    });

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.Logger.LogInformation(
    "Starting {ServiceName} {ServiceVersion} on deployment track {DeploymentTrack} (mock data: {UseMockData}, Azure storage: {UseAzureStorage})",
    deploymentInfo.ServiceName,
    deploymentInfo.Version,
    deploymentInfo.Track,
    catalogConfig.UseMockData,
    catalogConfig.UseAzureStorage);

if (!catalogConfig.UseMockData)
{
    const int maxInitAttempts = 5;
    for (var attempt = 1; ; attempt++)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDBContext>();
        try
        {
            context.Database.EnsureCreated();
            break;
        }
        catch (Exception ex) when (attempt < maxInitAttempts)
        {
            app.Logger.LogWarning(ex, "Catalog database initialization attempt {Attempt}/{MaxAttempts} failed; retrying", attempt, maxInitAttempts);
            Thread.Sleep(TimeSpan.FromSeconds(2 * attempt));
        }
        catch (Exception ex)
        {
            app.Logger.LogCritical(ex, "Catalog database initialization failed after {MaxAttempts} attempts; shutting down", maxInitAttempts);
            throw;
        }
    }
}

app.UseMiddleware<RequestLoggingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapCatalogHealthChecks();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Catalog}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
