using Microsoft.EntityFrameworkCore;
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
        builder.Services.AddLogging();
        var logger = builder.Services.BuildServiceProvider().GetService<ILogger<Program>>();
        logger?.LogWarning(ex, "Failed to configure Azure Key Vault. Continuing without it.");
    }
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ICatalogConfiguration, CatalogConfiguration>();

if (!string.IsNullOrEmpty(builder.Configuration["ApplicationInsights:InstrumentationKey"]))
{
    builder.Services.AddApplicationInsightsTelemetry(builder.Configuration["ApplicationInsights:InstrumentationKey"]);
}

var catalogConfig = new CatalogConfiguration(builder.Configuration);

if (catalogConfig.UseMockData)
{
    builder.Services.AddSingleton<ICatalogService, CatalogServiceMock>();
}
else
{
    builder.Services.AddDbContext<CatalogDbContext>(options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("CatalogDBContext"));
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

builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

if (!catalogConfig.UseMockData)
{
    builder.Services.AddHealthChecks()
        .AddSqlServer(builder.Configuration.GetConnectionString("CatalogDBContext")!);
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

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

if (!catalogConfig.UseMockData)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    context.Database.EnsureCreated();
}

app.Run();
