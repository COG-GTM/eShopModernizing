using System.Net;

namespace eShopRazorPages.Tests;

/// <summary>Every URL the WebForms app answered must still resolve to the equivalent Razor Page.</summary>
public class LegacyUrlTests : IClassFixture<CatalogAppFactory>
{
    private readonly CatalogAppFactory _factory;

    public LegacyUrlTests(CatalogAppFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/", "<title>Home Page -")]
    [InlineData("/Default", "<title>Home Page -")]
    [InlineData("/default", "<title>Home Page -")]
    [InlineData("/Default.aspx", "<title>Home Page -")]
    [InlineData("/Default/index/0/size/10", "<title>Home Page -")]
    [InlineData("/Default/index/1/size/5", "Showing 5 of 12 products - Page 2 - 3")]
    [InlineData("/default/INDEX/2/SIZE/5", "Showing 5 of 12 products - Page 3 - 3")]
    [InlineData("/Catalog/Create", "<h2 class=\"esh-body-title\">Create</h2>")]
    [InlineData("/Catalog/Create/", "<h2 class=\"esh-body-title\">Create</h2>")]
    [InlineData("/Catalog/Create.aspx", "<h2 class=\"esh-body-title\">Create</h2>")]
    [InlineData("/Catalog/Edit/1", "<h2 class=\"esh-body-title\">Edit</h2>")]
    [InlineData("/catalog/edit/1", "<h2 class=\"esh-body-title\">Edit</h2>")]
    [InlineData("/Catalog/Details/1", "<h2 class=\"esh-body-title\">Details</h2>")]
    [InlineData("/Catalog/Delete/1", "<h2 class=\"esh-body-title\">Delete</h2>")]
    public async Task Legacy_page_urls_render_the_ported_page(string url, string expectedContent)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedContent, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/Pics/1.png", "image/png")]
    [InlineData("/Pics/dummy.png", "image/png")]
    [InlineData("/images/brand.png", "image/png")]
    [InlineData("/images/main_banner.png", "image/png")]
    [InlineData("/Content/site.css", "text/css")]
    [InlineData("/Content/bootstrap.css", "text/css")]
    [InlineData("/fonts/Montserrat-Regular.woff", "application/font-woff")]
    [InlineData("/Scripts/jquery-3.3.1.min.js", "text/javascript")]
    [InlineData("/favicon.ico", "image/x-icon")]
    public async Task Legacy_static_asset_urls_are_served(string url, string contentType)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/Catalog/Edit/999")]
    [InlineData("/Catalog/Details/999")]
    [InlineData("/Catalog/Delete/999")]
    [InlineData("/Catalog/Edit/abc")]
    [InlineData("/Catalog/Edit")]
    [InlineData("/Catalog/Edit.aspx")]
    [InlineData("/Default/index/0/size/0")]
    [InlineData("/Default/index/x/size/10")]
    public async Task Unknown_items_and_malformed_urls_return_404(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Pagination_is_read_from_route_data_only()
    {
        var html = await _factory.CreateClient().GetStringAsync("/Default?index=1&size=5");

        Assert.Contains("Showing 10 of 12 products - Page 1 - 2", html);
    }

    [Fact]
    public async Task Negative_page_index_falls_back_to_first_page()
    {
        var html = await _factory.CreateClient().GetStringAsync("/Default/index/-1/size/10");

        Assert.Contains("Showing 10 of 12 products - Page 1 - 2", html);
    }

    [Fact]
    public async Task Page_beyond_the_end_shows_empty_template()
    {
        var html = await _factory.CreateClient().GetStringAsync("/Default/index/5/size/10");

        Assert.Contains("No data was returned.", html);
    }
}
