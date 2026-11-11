using System.Net;
using System.Text.RegularExpressions;
using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.WebForms.Tests.S7_PagesRead;

/// <summary>Each test gets its own host: the mock catalog is a singleton and the delete tests mutate it.</summary>
public sealed partial class PagesIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> factory = new();
    private readonly HttpClient client;

    public PagesIntegrationTests()
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    [Fact]
    public async Task Root_lists_first_page_of_seed_data_like_legacy()
    {
        var html = await GetOk("/");

        Assert.Equal(Enumerable.Range(1, 10).Select(id => $"/Catalog/Edit/{id}"), Hrefs(html, "/Catalog/Edit/"));
        Assert.Equal(Enumerable.Range(1, 10).Select(id => $"/Catalog/Details/{id}"), Hrefs(html, "/Catalog/Details/"));
        Assert.Equal(Enumerable.Range(1, 10).Select(id => $"/Catalog/Delete/{id}"), Hrefs(html, "/Catalog/Delete/"));
        Assert.Contains("<a class=\"btn esh-button esh-button-primary\" href=\"/Catalog/Create\">", html);
        Assert.Contains("<image class=\"esh-thumbnail\" src='/Pics/1.png' />", html);
        Assert.Contains("<span class=\"esh-price\">19.5</span>", html);
        Assert.Contains("<span class=\"esh-price\">8.50</span>", html);
        Assert.Matches(@"<p>\s*\.NET Bot Black Hoodie\s*</p>", html);
        Assert.Matches(@"<p>\s*\.NET\s*</p>", html); // brand of item 1
        Assert.Matches(@"<p>\s*T-Shirt\s*</p>", html); // type of item 1
        Assert.Matches(@"<span class=""esh-pager-item"">Showing 10 of 12 products - Page 1 - 2\s*</span>", html);
        Assert.Contains("class=\"esh-pager-item esh-pager-item--navigable esh-pager-item--hidden\" href=\"/Default/index/-1/size/10\"", html);
        Assert.Contains("class=\"esh-pager-item esh-pager-item--navigable\" href=\"/Default/index/1/size/10\"", html);
    }

    [Fact]
    public async Task Paging_route_renders_page_2_of_3_like_legacy()
    {
        var html = await GetOk("/Default/index/1/size/5");

        Assert.Equal(Enumerable.Range(6, 5).Select(id => $"/Catalog/Details/{id}"), Hrefs(html, "/Catalog/Details/"));
        Assert.Matches(@"Showing 5 of 12 products - Page 2 - 3\s*</span>", html);
        Assert.Contains("<a id=\"ctl00_MainContent_PaginationPrevious\" class=\"esh-pager-item esh-pager-item--navigable\" href=\"/Default/index/0/size/5\">", html);
        Assert.Contains("<a id=\"ctl00_MainContent_PaginationNext\" class=\"esh-pager-item esh-pager-item--navigable\" href=\"/Default/index/2/size/5\">", html);
    }

    [Fact]
    public async Task Last_page_hides_next_link()
    {
        var html = await GetOk("/Default/index/1/size/10");

        Assert.Equal(new[] { "/Catalog/Details/11", "/Catalog/Details/12" }, Hrefs(html, "/Catalog/Details/"));
        Assert.Contains("class=\"esh-pager-item esh-pager-item--navigable esh-pager-item--hidden\" href=\"/Default/index/2/size/10\"", html);
    }

    [Fact]
    public async Task Index_is_not_a_legacy_url()
    {
        using var response = await client.GetAsync("/Index");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Details_renders_legacy_labels_and_values()
    {
        var html = await GetOk("/Catalog/Details/1");

        Assert.Contains("<h2 class=\"esh-body-title\">Details</h2>", html);
        Assert.Contains("<img class=\"col-md-6 esh-picture\" src=\"/Pics/1.png\" />", html);
        foreach (var label in new[] { "Name", "Description", "Brand", "Type", "Price", "Picture name", "Stock", "Restock", "Max stock" })
        {
            Assert.Matches($@"<dt>{label}\s*</dt>", html);
        }

        Assert.Contains("<span>.NET Bot Black Hoodie</span>", html);
        Assert.Contains("<span class=\"esh-price\">19.5</span>", html);
        Assert.Contains("<span>1.png</span>", html);
        Assert.Contains("<span>100</span>", html);
        Assert.Matches(@"<a class=""esh-link-item"" href=""/Catalog/Edit/1"">Edit\s*</a>", html);
        Assert.Matches(@"<a class=""esh-link-item"" href=""\.\./\.\./"">Back to list\s*</a>", html);
    }

    [Fact]
    public async Task Details_unknown_id_returns_legacy_500()
    {
        using var response = await client.GetAsync("/Catalog/Details/999");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Delete_confirm_then_post_with_token_redirects_to_root_and_removes_item()
    {
        var confirm = await GetOk("/Catalog/Delete/2");
        Assert.Contains("<h3>Are you sure you want to delete this?</h3>", confirm);
        Assert.Contains("<span>.NET Black &amp; White Mug</span>", confirm);
        Assert.Contains("value=\"[ Delete ]\"", confirm);
        Assert.Matches(@"<form method=""post""[^>]*>", confirm);
        var token = AntiforgeryToken(confirm);

        using var response = await client.PostAsync("/Catalog/Delete/2", Form(token));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        Assert.Null(Catalog().FindCatalogItem(2));
        var list = await GetOk("/");
        Assert.DoesNotContain("/Catalog/Details/2\"", list);
        Assert.Contains("/Catalog/Details/11\"", list);
        using var gone = await client.GetAsync("/Catalog/Details/2");
        Assert.Equal(HttpStatusCode.InternalServerError, gone.StatusCode);
    }

    [Fact]
    public async Task Delete_post_without_token_is_400_and_changes_nothing()
    {
        await GetOk("/Catalog/Delete/2"); // antiforgery cookie present, form token missing

        using var response = await client.PostAsync("/Catalog/Delete/2", Form(token: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(Catalog().FindCatalogItem(2));
        Assert.Equal(12, Catalog().GetCatalogItemsPaginated(20, 0).TotalItems);
    }

    [Fact]
    public async Task Text_fields_are_html_encoded_on_list_details_and_delete()
    {
        const string Payload = "<script>alert('x')</script>";
        var item = Catalog().FindCatalogItem(3)!;
        item.Name = Payload;
        item.Description = "Cup<T> & \"quoted\"";

        var pages = new[] { await GetOk("/"), await GetOk("/Catalog/Details/3"), await GetOk("/Catalog/Delete/3") };

        foreach (var html in pages)
        {
            Assert.DoesNotContain(Payload, html);
            Assert.Contains("&lt;script&gt;alert(&#x27;x&#x27;)&lt;/script&gt;", html);
            Assert.Contains("Cup&lt;T&gt; &amp; &quot;quoted&quot;", html);
        }

        Assert.Contains("Cup&lt;T&gt; White Mug", pages[0]); // seed item 9
    }

    private ICatalogService Catalog() => factory.Services.GetRequiredService<ICatalogService>();

    private async Task<string> GetOk(string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static FormUrlEncodedContent Form(string? token)
    {
        var fields = new List<KeyValuePair<string, string>> { new("ctl00$MainContent$ctl11", "[ Delete ]") };
        if (token is not null)
        {
            fields.Add(new("__RequestVerificationToken", token));
        }

        return new FormUrlEncodedContent(fields);
    }

    private static string AntiforgeryToken(string html)
    {
        var match = TokenRegex().Match(html);
        Assert.True(match.Success, "antiforgery token not rendered");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static List<string> Hrefs(string html, string prefix) =>
        HrefRegex().Matches(html).Select(m => m.Groups[1].Value).Where(h => h.StartsWith(prefix, StringComparison.Ordinal)).ToList();

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

    [GeneratedRegex("<a class=\"esh-table-link\" href=\"([^\"]+)\"")]
    private static partial Regex HrefRegex();
}
