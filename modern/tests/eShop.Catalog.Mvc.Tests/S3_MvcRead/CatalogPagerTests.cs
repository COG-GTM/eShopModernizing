using eShop.Catalog.Data.ViewModel;
using eShop.Catalog.Mvc.Paging;

namespace eShop.Catalog.Mvc.Tests.S3_MvcRead;

public class CatalogPagerTests
{
    private const string Visible = "esh-pager-item esh-pager-item--navigable";
    private const string Hidden = "esh-pager-item esh-pager-item--navigable esh-pager-item--hidden";

    private static PaginatedItemsViewModel<object> Page(int pageIndex, int pageSize, long total) =>
        new(pageIndex, pageSize, total, Array.Empty<object>());

    [Fact]
    public void First_page_hides_previous_and_shows_next()
    {
        var model = Page(0, 10, 12);

        Assert.Equal(Hidden, CatalogPager.PreviousCssClass(model));
        Assert.Equal(Visible, CatalogPager.NextCssClass(model));
    }

    [Fact]
    public void Last_page_shows_previous_and_hides_next()
    {
        var model = Page(1, 10, 12);

        Assert.Equal(Visible, CatalogPager.PreviousCssClass(model));
        Assert.Equal(Hidden, CatalogPager.NextCssClass(model));
    }

    [Fact]
    public void Middle_page_shows_both_links()
    {
        var model = Page(1, 5, 12);

        Assert.True(CatalogPager.HasPrevious(model));
        Assert.True(CatalogPager.HasNext(model));
    }

    [Fact]
    public void Single_page_hides_both_links()
    {
        var model = Page(0, 20, 12);

        Assert.False(CatalogPager.HasPrevious(model));
        Assert.False(CatalogPager.HasNext(model));
    }

    [Fact]
    public void Page_past_the_end_hides_next_like_legacy()
    {
        var model = Page(5, 10, 12);

        Assert.True(CatalogPager.HasPrevious(model));
        Assert.False(CatalogPager.HasNext(model));
    }

    [Fact]
    public void Link_route_values_step_one_page_even_out_of_range_like_legacy()
    {
        var model = Page(0, 10, 12);

        Assert.Equal(new { pageSize = 10, pageIndex = -1 }.ToString(), CatalogPager.PreviousRouteValues(model).ToString());
        Assert.Equal(new { pageSize = 10, pageIndex = 1 }.ToString(), CatalogPager.NextRouteValues(model).ToString());
    }

    [Theory]
    [InlineData(0, 10, 12, "Showing 10 of 12 products - Page 1 - 2")]
    [InlineData(1, 5, 12, "Showing 5 of 12 products - Page 2 - 3")]
    [InlineData(2, 5, 12, "Showing 5 of 12 products - Page 3 - 3")]
    public void Summary_matches_legacy_text(int pageIndex, int pageSize, long total, string expected)
    {
        Assert.Equal(expected, CatalogPager.Summary(Page(pageIndex, pageSize, total)));
    }
}
