// Owned by T6. Scaffold smoke tests: both hosts boot with the frozen Program.cs and stub extensions.
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EShop.Storefront.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public async Task MvcHostStarts()
    {
        await using var factory = new WebApplicationFactory<EShop.Storefront.Mvc.Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/__scaffold-probe");
        Assert.True((int)response.StatusCode < 500);
    }

    [Fact]
    public async Task WebFormsHostStarts()
    {
        await using var factory = new WebApplicationFactory<EShop.Storefront.WebForms.Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/__scaffold-probe");
        Assert.True((int)response.StatusCode < 500);
    }
}
