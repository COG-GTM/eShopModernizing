using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;

namespace eShop.Catalog.WebForms.Tests.S8_PagesWrite;

/// <summary>
/// Real-browser check of the client-side validators on /Catalog/Create. Uses the Chromium given by
/// PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH, otherwise installs Playwright's own Chromium on first run.
/// </summary>
public sealed class ClientValidationBrowserTests : IClassFixture<KestrelCatalogFactory>
{
    private readonly KestrelCatalogFactory factory;

    public ClientValidationBrowserTests(KestrelCatalogFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Create_shows_legacy_validator_messages_client_side_and_blocks_submit()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchChromiumAsync(playwright);
        var page = await browser.NewPageAsync();

        await page.GotoAsync(factory.ServerAddress + "/Catalog/Create");

        var nameMessage = page.Locator("#ctl00_MainContent_ctl00");
        var priceMessage = page.Locator("#ctl00_MainContent_ctl01");
        var stockMessage = page.Locator("#ctl00_MainContent_ctl02");
        await Assertions.Expect(nameMessage).ToBeHiddenAsync();
        await Assertions.Expect(priceMessage).ToBeHiddenAsync();

        await page.FillAsync("#ctl00_MainContent_Price", "1000000.01");
        await page.FillAsync("#ctl00_MainContent_Stock", "-1");
        await page.Locator("#ctl00_MainContent_Stock").BlurAsync();

        await Assertions.Expect(priceMessage).ToBeVisibleAsync();
        await Assertions.Expect(priceMessage).ToHaveTextAsync("The Price must be a positive number with maximum two decimals between 0 and 1 million.");
        await Assertions.Expect(stockMessage).ToBeVisibleAsync();
        await Assertions.Expect(stockMessage).ToHaveTextAsync("The field Stock must be between 0 and 10 million.");

        await page.ClickAsync("input[value='[ Create ]']");

        await Assertions.Expect(nameMessage).ToBeVisibleAsync();
        await Assertions.Expect(nameMessage).ToHaveTextAsync("The Name field is required.");
        Assert.EndsWith("/Catalog/Create", page.Url);

        await page.FillAsync("#ctl00_MainContent_Name", "Browser Mug");
        await page.FillAsync("#ctl00_MainContent_Price", "12.50");
        await page.FillAsync("#ctl00_MainContent_Stock", "3");
        await page.Locator("#ctl00_MainContent_Stock").BlurAsync();
        await Assertions.Expect(nameMessage).ToBeHiddenAsync();
        await Assertions.Expect(priceMessage).ToBeHiddenAsync();
        await Assertions.Expect(stockMessage).ToBeHiddenAsync();

        var catalog = factory.KestrelServices.GetRequiredService<ICatalogService>();
        Assert.Equal(12L, catalog.GetCatalogItemsPaginated(100, 0).TotalItems);

        await page.ClickAsync("input[value='[ Create ]']");
        await page.WaitForURLAsync(factory.ServerAddress + "/");

        Assert.Equal(13L, catalog.GetCatalogItemsPaginated(100, 0).TotalItems);
        Assert.Equal("Browser Mug", catalog.FindCatalogItem(13)!.Name);
    }

    private static async Task<IBrowser> LaunchChromiumAsync(IPlaywright playwright)
    {
        var executable = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH");
        if (string.IsNullOrEmpty(executable))
        {
            var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
            Assert.True(exitCode == 0, $"Playwright Chromium install failed ({exitCode}); set PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH to a local Chromium/Chrome.");
        }

        return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            // Chrome 132+ removed the old headless mode Playwright 1.48 requests; ask for the new one explicitly.
            Headless = false,
            Args = ["--headless=new"],
            ExecutablePath = string.IsNullOrEmpty(executable) ? null : executable,
        });
    }
}

/// <summary>Hosts the app on a real Kestrel port (TestServer is in-memory and unreachable from a browser).</summary>
public sealed class KestrelCatalogFactory : WebApplicationFactory<Program>
{
    private IHost? kestrelHost;
    private string? serverAddress;

    public string ServerAddress
    {
        get
        {
            EnsureServer();
            return serverAddress!;
        }
    }

    /// <summary>Services of the Kestrel host, i.e. the catalog the browser is talking to.</summary>
    public IServiceProvider KestrelServices
    {
        get
        {
            EnsureServer();
            return kestrelHost!.Services;
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        builder.ConfigureWebHost(webHost => webHost.UseKestrel().UseUrls("http://127.0.0.1:0"));
        kestrelHost = builder.Build();
        kestrelHost.Start();
        serverAddress = kestrelHost.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');

        testHost.Start();
        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            kestrelHost?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void EnsureServer()
    {
        if (serverAddress is null)
        {
            using var client = CreateDefaultClient();
        }
    }
}
