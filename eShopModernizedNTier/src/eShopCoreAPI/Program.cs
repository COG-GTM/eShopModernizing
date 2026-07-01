using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using eShopCoreAPI.Configuration;
using eShopCoreAPI.Data;
using eShopCoreAPI.Infrastructure;
using eShopCoreAPI.Services;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

if (!string.IsNullOrEmpty(builder.Configuration["Azure:KeyVaultName"]))
{
    try
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri($"https://{builder.Configuration["Azure:KeyVaultName"]}.vault.azure.net/"),
            new DefaultAzureCredential());
    }
    catch (Exception ex)
    {
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
        loggerFactory.CreateLogger<Program>()
            .LogWarning(ex, "Failed to configure Azure Key Vault. Continuing without it.");
    }
}

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "eShop Catalog API",
        Version = "v1",
        Description = "Modernized .NET 8 catalog API for the eShop N-Tier solution."
    });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddSingleton<ICatalogConfiguration, CatalogConfiguration>();

if (!string.IsNullOrEmpty(builder.Configuration["ApplicationInsights:ConnectionString"]))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    });
}
else if (!string.IsNullOrEmpty(builder.Configuration["ApplicationInsights:InstrumentationKey"]))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = $"InstrumentationKey={builder.Configuration["ApplicationInsights:InstrumentationKey"]}";
    });
}

var catalogConfig = new CatalogConfiguration(builder.Configuration);

if (catalogConfig.UseMockData)
{
    builder.Services.AddSingleton<ICatalogService, CatalogServiceMock>();
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("CatalogDBContext");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Connection string 'CatalogDBContext' is required when FeatureFlags:UseMockData is false.");
    }

    builder.Services.AddDbContext<CatalogDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
        });
    });

    builder.Services.AddScoped<ICatalogService, CatalogService>();
}

if (catalogConfig.UseManagedIdentity)
{
    builder.Services.AddSingleton<ISqlConnectionFactory, ManagedIdentitySqlConnectionFactory>();
}
else
{
    builder.Services.AddSingleton<ISqlConnectionFactory, AppSettingsSqlConnectionFactory>();
}

var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "live" });

if (!catalogConfig.UseMockData)
{
    healthChecksBuilder.AddSqlServer(
        builder.Configuration.GetConnectionString("CatalogDBContext")!,
        name: "catalog-db",
        tags: new[] { "ready" });
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

if (!catalogConfig.UseMockData)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        context.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize the catalog database. The /health/ready endpoint will report unhealthy until the database is reachable.");
    }
}

app.Run();

public partial class Program { }
