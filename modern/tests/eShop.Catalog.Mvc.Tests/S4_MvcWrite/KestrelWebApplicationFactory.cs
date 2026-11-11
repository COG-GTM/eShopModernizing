using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace eShop.Catalog.Mvc.Tests.S4_MvcWrite;

/// <summary>Runs the MVC host on a real loopback Kestrel port so a browser can reach it.</summary>
public sealed class KestrelWebApplicationFactory : WebApplicationFactory<Program>
{
    private IHost? kestrelHost;

    public Uri ServerAddress
    {
        get
        {
            EnsureStarted();
            return ClientOptions.BaseAddress;
        }
    }

    public ICatalogService KestrelCatalog
    {
        get
        {
            EnsureStarted();
            return kestrelHost!.Services.GetRequiredService<ICatalogService>();
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        builder.ConfigureWebHost(web => web.UseKestrel().UseUrls("http://127.0.0.1:0"));
        kestrelHost = builder.Build();
        kestrelHost.Start();

        var addresses = kestrelHost.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        ClientOptions.BaseAddress = new Uri(addresses.Addresses.Last());

        testHost.Start();
        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        kestrelHost?.Dispose();
        base.Dispose(disposing);
    }

    private void EnsureStarted()
    {
        using var _ = CreateDefaultClient();
    }
}
