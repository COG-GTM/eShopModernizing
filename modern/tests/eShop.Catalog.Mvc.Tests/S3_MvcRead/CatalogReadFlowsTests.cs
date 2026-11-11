using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Services;
using eShop.Catalog.Data.ViewModel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.Mvc.Tests.S3_MvcRead;

public class CatalogReadFlowsTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Legacy Pics/1.png as recorded in modern/parity/baseline/mock/legacy-mvc/12-items-1-pic.txt.
    private const string LegacyPic1Sha256 = "0487bc526da61586780355f4f50aa368f09c0a212809b1f08444382f4d789eb0";

    private static readonly string[] Page1Names =
    {
        ".NET Bot Black Hoodie", ".NET Black &amp; White Mug", "Prism White T-Shirt", ".NET Foundation T-shirt",
        "Roslyn Red Sheet", ".NET Blue Hoodie", "Roslyn Red T-Shirt", "Kudu Purple Hoodie", "Cup&lt;T&gt; White Mug",
        ".NET Foundation Sheet",
    };

    private static readonly string[] Page1Prices =
    {
        "$19.50", "$8.50", "$12.00", "$12.00", "$8.50", "$12.00", "$12.00", "$8.50", "$12.00", "$12.00",
    };

    private readonly WebApplicationFactory<Program> factory;

    public CatalogReadFlowsTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Catalog")]
    [InlineData("/Catalog/Index")]
    public async Task Index_first_page_matches_legacy(string url)
    {
        var html = await GetHtml(url);

        Assert.Contains("Showing 10 of 12 products - Page 1 - 2", Normalise(html));
        Assert.Equal(Page1Names, Cells(html, 1));
        Assert.Equal(Page1Prices, Regex.Matches(html, "<td class=\"esh-table-price\">\\s*(.*?)\\s*</td>").Select(m => m.Groups[1].Value));
        Assert.Contains("<a class=\"esh-pager-item esh-pager-item--navigable esh-pager-item--hidden\" href=\"/?pageSize=10&amp;pageIndex=-1\">Previous</a>", html);
        Assert.Contains("<a class=\"esh-pager-item esh-pager-item--navigable\" href=\"/?pageSize=10&amp;pageIndex=1\">Next</a>", html);
        Assert.Contains("<img class=\"esh-thumbnail\" src=\"http://localhost/items/1/pic\" />", html);
    }

    [Fact]
    public async Task Index_table_header_and_row_links_match_legacy()
    {
        var html = Normalise(await GetHtml("/"));

        Assert.Contains("<tr class=\"esh-table-header\"> <th> </th> <th> Name </th> <th> Description </th> <th> Brand </th> <th> Type </th> <th> Price </th> <th> Picture name </th> <th> Stock </th> <th> Restock </th> <th> Max stock </th> <th></th> </tr>", html);
        Assert.Contains("<a class=\"esh-table-link\" href=\"/Catalog/Details/1\">Details</a>", html);
        Assert.Contains("<a class=\"btn esh-button esh-button-primary\" href=\"/Catalog/Create\">Create New</a>", html);
    }

    [Fact]
    public async Task Index_middle_page_with_page_size_5()
    {
        var html = await GetHtml("/?pageSize=5&pageIndex=1");

        Assert.Contains("Showing 5 of 12 products - Page 2 - 3", Normalise(html));
        Assert.Contains("<a class=\"esh-pager-item esh-pager-item--navigable\" href=\"/?pageSize=5&amp;pageIndex=0\">Previous</a>", html);
        Assert.Contains("<a class=\"esh-pager-item esh-pager-item--navigable\" href=\"/?pageSize=5&amp;pageIndex=2\">Next</a>", html);
        Assert.Equal(new[] { ".NET Blue Hoodie", "Roslyn Red T-Shirt", "Kudu Purple Hoodie", "Cup&lt;T&gt; White Mug", ".NET Foundation Sheet" }, Cells(html, 1));
    }

    [Fact]
    public async Task Index_last_page_hides_next()
    {
        var html = await GetHtml("/Catalog/Index?pageSize=10&pageIndex=1");

        Assert.Contains("Showing 10 of 12 products - Page 2 - 2", Normalise(html));
        Assert.Contains("<a class=\"esh-pager-item esh-pager-item--navigable esh-pager-item--hidden\" href=\"/?pageSize=10&amp;pageIndex=2\">Next</a>", html);
    }

    [Fact]
    public async Task Details_1_has_legacy_dd_values()
    {
        using var client = CreateClient();
        // Legacy capture order: the mock fills Brand/Type when the list is first rendered (legacy quirk kept by the S0 mock).
        (await client.GetAsync("/")).Dispose();

        using var response = await client.GetAsync("/Catalog/Details/1");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dds = Regex.Matches(html, "<dd>\\s*(.*?)\\s*</dd>", RegexOptions.Singleline).Select(m => m.Groups[1].Value);
        Assert.Equal(new[] { ".NET Bot Black Hoodie", ".NET Bot Black Hoodie", ".NET", "T-Shirt", "$19.50", "1.png", "100", "0", "0" }, dds);
        Assert.Contains("<img class=\"col-md-6 esh-picture\" src=\"http://localhost/items/1/pic\" />", html);
        Assert.Contains("<a class=\"esh-link-item\" href=\"/Catalog/Edit/1\">Edit</a>", html);
        Assert.Contains("<a class=\"esh-link-item\" href=\"/\">Back to List</a>", html);
    }

    [Theory]
    [InlineData("/Catalog/Details", HttpStatusCode.BadRequest)]
    [InlineData("/Catalog/Details/abc", HttpStatusCode.BadRequest)]
    [InlineData("/Catalog/Details/999", HttpStatusCode.NotFound)]
    [InlineData("/items/0/pic", HttpStatusCode.BadRequest)]
    [InlineData("/items/-1/pic", HttpStatusCode.BadRequest)]
    [InlineData("/items/999/pic", HttpStatusCode.NotFound)]
    public async Task Error_statuses_match_legacy_with_empty_html_body(string url, HttpStatusCode expected)
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Pic_1_is_byte_identical_to_legacy_png()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/items/1/pic");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(await File.ReadAllBytesAsync(LegacyPicPath("1.png")), bytes);
        Assert.Equal(LegacyPic1Sha256, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData("../../Program.cs")]
    [InlineData("/etc/passwd")]
    public async Task Pic_rejects_picture_file_names_outside_pics(string pictureFileName)
    {
        var item = new CatalogItem { Id = 1, Name = "x", PictureFileName = pictureFileName };
        using var app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<ICatalogService>(new SingleItemCatalogService(item))));
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/items/1/pic");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<string> GetHtml(string url)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string Normalise(string html) => Regex.Replace(html, "\\s+", " ");

    private static string[] Cells(string html, int column)
    {
        var body = html[html.IndexOf("<tbody>", StringComparison.Ordinal)..html.IndexOf("</tbody>", StringComparison.Ordinal)];
        return Regex.Matches(body, "<tr>(.*?)</tr>", RegexOptions.Singleline)
            .Select(row => Regex.Matches(row.Groups[1].Value, "<td[^>]*>\\s*(.*?)\\s*</td>", RegexOptions.Singleline)[column].Groups[1].Value)
            .ToArray();
    }

    private static string LegacyPicPath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "eShopLegacyMVCSolution")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "eShopLegacyMVCSolution", "src", "eShopLegacyMVC", "Pics", fileName);
    }

    private sealed class SingleItemCatalogService : ICatalogService
    {
        private readonly CatalogItem item;

        public SingleItemCatalogService(CatalogItem item) => this.item = item;

        public CatalogItem? FindCatalogItem(int id) => id == item.Id ? item : null;

        public IEnumerable<CatalogBrand> GetCatalogBrands() => Array.Empty<CatalogBrand>();

        public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex) =>
            new(pageIndex, pageSize, 1, new[] { item });

        public IEnumerable<CatalogType> GetCatalogTypes() => Array.Empty<CatalogType>();

        public void CreateCatalogItem(CatalogItem catalogItem) => throw new NotSupportedException();

        public void UpdateCatalogItem(CatalogItem catalogItem) => throw new NotSupportedException();

        public void RemoveCatalogItem(CatalogItem catalogItem) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
