using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace eShopNet8.WebForms.IntegrationTests
{
    public class HealthEndpointTests : IClassFixture<WebApplicationFactory<eShopNet8.WebForms.Program>>
    {
        private readonly WebApplicationFactory<eShopNet8.WebForms.Program> factory;

        public HealthEndpointTests(WebApplicationFactory<eShopNet8.WebForms.Program> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task Health_endpoint_returns_200()
        {
            var response = await factory.CreateClient().GetAsync("/health");

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }
    }
}
