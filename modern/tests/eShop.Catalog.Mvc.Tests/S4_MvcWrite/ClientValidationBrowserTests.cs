using Microsoft.Playwright;

namespace eShop.Catalog.Mvc.Tests.S4_MvcWrite;

/// <summary>
/// Browser check that unobtrusive client validation (driven by the rendered data-val-* attributes) blocks an
/// empty-Name Create submit before any request reaches the server.
/// </summary>
public sealed class ClientValidationBrowserTests : IAsyncLifetime
{
    private readonly KestrelWebApplicationFactory factory = new();
    private IPlaywright? playwright;
    private IBrowser? browser;

    public async Task InitializeAsync()
    {
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            // Chrome 132+ removed the old headless mode Playwright 1.48 requests; ask for the new one explicitly.
            Headless = false,
            Args = new[] { "--headless=new" },
            ExecutablePath = BrowserLocator.ChromeExecutable(),
        });
    }

    public async Task DisposeAsync()
    {
        if (browser != null)
        {
            await browser.DisposeAsync();
        }

        playwright?.Dispose();
        factory.Dispose();
    }

    [Fact]
    public async Task Client_validation_blocks_empty_name_submit()
    {
        var context = await browser!.NewContextAsync();
        var page = await context.NewPageAsync();
        var posts = 0;
        page.Request += (_, request) =>
        {
            if (request.Method == "POST")
            {
                posts++;
            }
        };

        await page.GotoAsync(new Uri(factory.ServerAddress, "/Catalog/Create").ToString());
        await EnsureValidationScriptsAsync(page);

        await page.FillAsync("#Name", string.Empty);
        await page.FillAsync("#Price", "10.00");
        await page.FillAsync("#AvailableStock", "1");
        await page.FillAsync("#RestockThreshold", "1");
        await page.FillAsync("#MaxStockThreshold", "1");
        await page.ClickAsync("input[type=submit][value='[ Create ]']");

        var message = page.Locator("span[data-valmsg-for=Name]");
        await Assertions.Expect(message).ToHaveTextAsync("The Name field is required.");
        await Assertions.Expect(message).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("field-validation-error"));
        Assert.EndsWith("/Catalog/Create", page.Url);
        Assert.Equal(0, posts);
        Assert.Equal(12, factory.KestrelCatalog.GetCatalogItemsPaginated(100, 0).TotalItems);
    }

    /// <summary>
    /// Create.cshtml declares jquery.validate + unobtrusive in its Scripts section; jQuery itself and the section are
    /// rendered by the S2 layout. Until that layout exists the page has no jQuery, so inject the same legacy scripts.
    /// When jQuery is present (layout rendered) the validator must already be loaded by the page itself.
    /// </summary>
    private static async Task EnsureValidationScriptsAsync(IPage page)
    {
        var hasJquery = await page.EvaluateAsync<bool>("() => typeof window.jQuery !== 'undefined'");
        if (hasJquery)
        {
            Assert.True(await page.EvaluateAsync<bool>("() => !!(window.jQuery.validator && window.jQuery.validator.unobtrusive)"),
                "layout loaded jQuery but the Create page did not load jquery.validate / unobtrusive");
            return;
        }

        foreach (var script in new[] { "jquery-3.3.1.js", "jquery.validate.js", "jquery.validate.unobtrusive.js" })
        {
            await page.AddScriptTagAsync(new PageAddScriptTagOptions { Path = BrowserLocator.LegacyScript(script) });
        }

        await page.EvaluateAsync("() => window.jQuery.validator.unobtrusive.parse(document)");
    }
}

internal static class BrowserLocator
{
    public static string? ChromeExecutable()
    {
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH"),
        };
        var devinChrome = "/opt/.devin/chrome/chrome";
        if (Directory.Exists(devinChrome))
        {
            candidates.AddRange(Directory.GetFiles(devinChrome, "chrome", SearchOption.AllDirectories));
        }

        candidates.AddRange(new[] { "/usr/bin/google-chrome-stable", "/usr/bin/google-chrome", "/usr/bin/chromium" });
        var found = candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c) && File.Exists(c));
        if (found == null)
        {
            // No system Chrome: fall back to Playwright's own Chromium build.
            Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        }

        return found;
    }

    public static string LegacyScript(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            foreach (var relative in new[] { "modern/src/eShop.Catalog.Mvc/wwwroot/Scripts", "eShopLegacyMVCSolution/src/eShopLegacyMVC/Scripts" })
            {
                var path = Path.Combine(dir.FullName, relative, name);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        throw new FileNotFoundException($"{name} not found in wwwroot/Scripts or the legacy MVC Scripts folder");
    }
}
