using System.Net;
using System.Text.RegularExpressions;
using eShop.Catalog.WebForms.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.FileProviders;

namespace eShop.Catalog.WebForms.Tests.S7_PagesRead;

/// <summary>
/// A published app started outside its publish folder (<c>dotnet /app/eShop.Catalog.WebForms.dll</c>) gets the
/// working directory as content root. The list/details/delete pictures and the layout CSS must still load.
/// </summary>
public sealed partial class PublishedAssetsTests : IDisposable
{
    private readonly string emptyContentRoot = Directory.CreateTempSubdirectory("s7-content-root-").FullName;

    public void Dispose() => Directory.Delete(emptyContentRoot, recursive: true);

    [Theory]
    [InlineData("/")]
    [InlineData("/Default/index/1/size/5")]
    [InlineData("/Catalog/Details/1")]
    [InlineData("/Catalog/Delete/2")]
    public async Task Pictures_and_stylesheets_load_when_content_root_has_no_wwwroot(string page)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseContentRoot(emptyContentRoot);
        });
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync(page);
        var assets = AssetUrls().Matches(html).Select(m => m.Groups["url"].Value).Distinct().ToList();

        Assert.Contains(assets, url => url.StartsWith("/Pics/", StringComparison.Ordinal));
        Assert.Contains("/Content/site.css", assets);
        foreach (var url in assets)
        {
            using var response = await client.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{page}: {url} returned {(int)response.StatusCode}");
            var expected = url.EndsWith(".css", StringComparison.Ordinal) ? "text/css" : "image/png";
            Assert.Equal(expected, response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public void Falls_back_to_wwwroot_beside_the_assembly_when_content_root_has_none()
    {
        var appBase = Directory.CreateTempSubdirectory("s7-app-base-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(appBase, "wwwroot", "Pics"));
            File.WriteAllText(Path.Combine(appBase, "wwwroot", "Pics", "1.png"), "png");
            var environment = new TestEnvironment { ContentRootPath = emptyContentRoot };

            PagesUiExtensions.UsePublishedWebRootWhenMissing(environment, appBase);

            Assert.Equal(Path.Combine(appBase, "wwwroot"), environment.WebRootPath);
            Assert.True(environment.WebRootFileProvider.GetFileInfo("Pics/1.png").Exists);
        }
        finally
        {
            Directory.Delete(appBase, recursive: true);
        }
    }

    [Fact]
    public void Keeps_existing_web_root()
    {
        var webRoot = Directory.CreateDirectory(Path.Combine(emptyContentRoot, "wwwroot")).FullName;
        var provider = new PhysicalFileProvider(webRoot);
        var environment = new TestEnvironment { WebRootPath = webRoot, WebRootFileProvider = provider };

        PagesUiExtensions.UsePublishedWebRootWhenMissing(environment, AppContext.BaseDirectory);

        Assert.Equal(webRoot, environment.WebRootPath);
        Assert.Same(provider, environment.WebRootFileProvider);
    }

    [Fact]
    public void Leaves_environment_alone_when_no_published_wwwroot_exists()
    {
        var environment = new TestEnvironment();

        PagesUiExtensions.UsePublishedWebRootWhenMissing(environment, emptyContentRoot);

        Assert.Null(environment.WebRootPath);
        Assert.IsType<NullFileProvider>(environment.WebRootFileProvider);
    }

    [GeneratedRegex("""(?:src|href)=["'](?<url>/(?:Pics|Content|images)/[^"']+)["']""")]
    private static partial Regex AssetUrls();

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = null!;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ApplicationName { get; set; } = "eShop.Catalog.WebForms";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = "Production";
    }
}
