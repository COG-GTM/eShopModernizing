using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Logging.AddAzureWebAppDiagnostics();
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
builder.Services.AddDbContext<SmokeDbContext>(o => o.UseSqlServer("Server=localhost;Database=Smoke;TrustServerCertificate=True"));
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = "localhost:6379");
builder.Services.AddSession();
builder.Services.AddSystemWebAdapters();
builder.Services.AddWebOptimizer();
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddAuthentication().AddCookie().AddOpenIdConnect(o =>
{
    o.Authority = "https://login.microsoftonline.com/common/v2.0";
    o.ClientId = "00000000-0000-0000-0000-000000000000";
});

var app = builder.Build();
app.UseWebOptimizer();
app.UseSession();
app.UseSystemWebAdapters();

// One representative public type per replacement package, reported with the assembly it loaded from.
Type[] replacementTypes =
[
    typeof(AutofacServiceProviderFactory),
    typeof(Azure.Extensions.AspNetCore.Configuration.Secrets.AzureKeyVaultConfigurationOptions),
    typeof(Azure.Identity.DefaultAzureCredential),
    typeof(Azure.Messaging.ServiceBus.ServiceBusClient),
    typeof(Azure.Monitor.OpenTelemetry.AspNetCore.AzureMonitorOptions),
    typeof(Azure.Security.KeyVault.Secrets.SecretClient),
    typeof(Azure.Storage.Blobs.BlobServiceClient),
    typeof(System.Data.Entity.DbContext),
    typeof(System.Data.Entity.SqlServer.MicrosoftSqlProviderServices),
    typeof(WebOptimizer.IAssetPipeline),
    typeof(log4net.LogManager),
    typeof(Microsoft.ApplicationInsights.AspNetCore.Extensions.ApplicationInsightsServiceOptions),
    typeof(System.Net.Http.Formatting.JsonMediaTypeFormatter),
    typeof(Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions),
    typeof(Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache),
    typeof(Microsoft.Extensions.Logging.Log4NetProvider),
    typeof(Microsoft.Identity.Client.PublicClientApplicationBuilder),
    typeof(Microsoft.Identity.Web.MicrosoftIdentityOptions),
    typeof(Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration),
    typeof(Newtonsoft.Json.JsonConvert),
    typeof(System.ServiceModel.BasicHttpBinding),
    typeof(System.ServiceModel.NetTcpBinding),
    typeof(System.ServiceModel.ChannelFactory<>),
    typeof(SmokeDbContext),
];

app.MapGet("/smoke", () => Results.Json(new
{
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    loaded = replacementTypes.Select(t => new { type = t.FullName, assembly = t.Assembly.GetName().Name, version = t.Assembly.GetName().Version?.ToString() }),
}));

app.Run();

public class SmokeDbContext(DbContextOptions<SmokeDbContext> options) : Microsoft.EntityFrameworkCore.DbContext(options);
