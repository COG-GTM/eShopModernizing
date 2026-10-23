using eShop.Catalog.Core.Services;
using eShop.Parity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace eShopNet8.Mvc.IntegrationTests
{
    /// <summary>The migrated app in-process, in mock mode (the mode the legacy baseline was recorded in), Production environment like the container.</summary>
    public class MvcAppFactory : WebApplicationFactory<eShopNet8.Mvc.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("UseMockData", "true");
        }

        /// <summary>Cookie session without auto-redirect (python-requests semantics), or a live server when ESHOP_PARITY_MVC_URL is set.</summary>
        public HttpClient CreateParityClient()
        {
            var external = ParityApp.ExternalUrl("mvc");
            return external != null
                ? ParityApp.ExternalClient(external)
                : CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        }

        public ICatalogService CatalogService => Services.GetRequiredService<ICatalogService>();
    }
}
