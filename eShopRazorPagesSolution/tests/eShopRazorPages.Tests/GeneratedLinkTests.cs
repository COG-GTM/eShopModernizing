namespace eShopRazorPages.Tests;

/// <summary>Links rendered by the ported pages must use the legacy URL shapes.</summary>
public class GeneratedLinkTests : IClassFixture<CatalogAppFactory>
{
    private readonly CatalogAppFactory _factory;

    public GeneratedLinkTests(CatalogAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_page_links_use_legacy_urls()
    {
        var html = await _factory.CreateClient().GetStringAsync("/");

        Assert.Contains("href=\"/Catalog/Create\"", html);
        Assert.Contains("href=\"/Catalog/Edit/1\"", html);
        Assert.Contains("href=\"/Catalog/Details/1\"", html);
        Assert.Contains("href=\"/Catalog/Delete/1\"", html);
        Assert.Contains("id=\"PaginationNext\" href=\"/Default/index/1/size/10\" class=\"esh-pager-item esh-pager-item--navigable\"", html);
        Assert.Contains("id=\"PaginationPrevious\" href=\"/Default/index/-1/size/10\" class=\"esh-pager-item esh-pager-item--navigable esh-pager-item--hidden\"", html);
        Assert.Contains("src=\"/Pics/1.png\"", html);
    }

    [Fact]
    public async Task Last_page_hides_next_link()
    {
        var html = await _factory.CreateClient().GetStringAsync("/Default/index/1/size/10");

        Assert.Contains("id=\"PaginationPrevious\" href=\"/Default/index/0/size/10\" class=\"esh-pager-item esh-pager-item--navigable\"", html);
        Assert.Contains("id=\"PaginationNext\" href=\"/Default/index/2/size/10\" class=\"esh-pager-item esh-pager-item--navigable esh-pager-item--hidden\"", html);
    }

    [Fact]
    public async Task Details_page_links_to_edit_and_list()
    {
        var html = await _factory.CreateClient().GetStringAsync("/Catalog/Details/3");

        Assert.Contains("href=\"/Catalog/Edit/3\"", html);
        Assert.Contains("href=\"/\" class=\"esh-link-item\">Back to list", html);
        Assert.Contains("src=\"/Pics/3.png\"", html);
        Assert.Contains("Prism White T-Shirt", html);
        Assert.Contains("Other", html);
    }

    [Fact]
    public async Task Layout_renders_session_info_footer()
    {
        var html = await _factory.CreateClient().GetStringAsync("/");

        Assert.Contains($"<span id=\"SessionInfoLabel\">{Environment.MachineName}, ", html);
    }
}
