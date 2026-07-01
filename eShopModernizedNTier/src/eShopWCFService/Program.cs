using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace eShopWCFService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddServiceModelServices();
            builder.Services.AddServiceModelMetadata();
            builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

            var app = builder.Build();

            app.UseServiceModel(serviceBuilder =>
            {
                serviceBuilder.AddService<CatalogService>(options =>
                {
                    options.DebugBehavior.IncludeExceptionDetailInFaults = false;
                });

                // Same binding and relative address as the original IIS-hosted
                // service (basicHttpBinding at /CatalogService.svc), so existing
                // WinForms clients keep working unchanged.
                serviceBuilder.AddServiceEndpoint<CatalogService, ICatalogService>(
                    new BasicHttpBinding(), "/CatalogService.svc");

                var metadata = app.Services.GetRequiredService<ServiceMetadataBehavior>();
                metadata.HttpGetEnabled = true;
                metadata.HttpsGetEnabled = true;
            });

            app.Run();
        }
    }
}
