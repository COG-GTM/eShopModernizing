using EShop.Storefront.Tests.Infrastructure;
using Xunit;

namespace EShop.Storefront.Tests;

/// <summary>Both hosts boot with the frozen Program.cs (whatever slices are merged) and answer without a server error.</summary>
public class HostStartupTests
{
    [Fact]
    public async Task Mvc_host_starts()
    {
        await using var app = new MvcAppFixture();
        Assert.True((await app.Client.GetAsync("/__host-probe")).Status < 500);
    }

    [Fact]
    public async Task WebForms_host_starts()
    {
        await using var app = new WebFormsAppFixture();
        Assert.True((await app.Client.GetAsync("/__host-probe")).Status < 500);
    }
}
