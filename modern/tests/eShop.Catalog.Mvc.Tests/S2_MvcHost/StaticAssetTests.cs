using System.Net;

namespace eShop.Catalog.Mvc.Tests.S2_MvcHost;

public class StaticAssetTests : IClassFixture<S2HostFactory>
{
    private readonly S2HostFactory factory;

    public StaticAssetTests(S2HostFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("/Content/site.css", "text/css", "Content/Site.css")]
    [InlineData("/Content/Site.css", "text/css", "Content/Site.css")]
    [InlineData("/Content/bootstrap.css", "text/css", "Content/bootstrap.css")]
    [InlineData("/Content/custom.css", "text/css", "Content/custom.css")]
    [InlineData("/Content/base.css", "text/css", "Content/base.css")]
    [InlineData("/Images/brand.png", "image/png", "Images/brand.png")]
    [InlineData("/images/brand.png", "image/png", "Images/brand.png")]
    [InlineData("/images/brand_dark.png", "image/png", "Images/brand_dark.png")]
    [InlineData("/images/main_footer_text.png", "image/png", "Images/main_footer_text.png")]
    [InlineData("/images/main_banner.png", "image/png", "Images/main_banner.png")]
    [InlineData("/fonts/Montserrat-Regular.woff", "application/font-woff", "fonts/Montserrat-Regular.woff")]
    [InlineData("/fonts/Montserrat-Bold.ttf", "application/x-font-ttf", "fonts/Montserrat-Bold.ttf")]
    [InlineData("/favicon.ico", "image/x-icon", "favicon.ico")]
    [InlineData("/Pics/1.png", "image/png", "Pics/1.png")]
    [InlineData("/pics/12.png", "image/png", "Pics/12.png")]
    [InlineData("/Pics/dummy.png", "image/png", "Pics/dummy.png")]
    [InlineData("/Scripts/jquery-3.3.1.js", "text/javascript", "Scripts/jquery-3.3.1.js")]
    [InlineData("/scripts/modernizr-2.8.3.js", "text/javascript", "Scripts/modernizr-2.8.3.js")]
    [InlineData("/Scripts/jquery.validate.unobtrusive.js", "text/javascript", "Scripts/jquery.validate.unobtrusive.js")]
    public async Task Legacy_asset_url_returns_200_with_content_type_and_legacy_bytes(string url, string contentType, string legacyFile)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        var expected = await File.ReadAllBytesAsync(Path.Combine(LegacyBaseline.LegacyMvcRoot, legacyFile));
        Assert.Equal(expected, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public void Wwwroot_has_no_case_only_duplicate_paths()
    {
        var webRoot = Path.Combine(LegacyBaseline.ModernRoot, "src", "eShop.Catalog.Mvc", "wwwroot");
        var paths = Directory.EnumerateFileSystemEntries(webRoot, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(webRoot, p))
            .ToList();

        var duplicates = paths.GroupBy(p => p, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key);

        Assert.Empty(duplicates);
        Assert.Contains(paths, p => p == Path.Combine("Content", "Site.css"));
        Assert.Contains(paths, p => p == "Images");
    }

    [Theory]
    [InlineData("/Content/missing.css")]
    [InlineData("/Images/../appsettings.json")]
    [InlineData("/web.config")]
    public async Task Missing_or_outside_webroot_asset_is_404(string url)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
