using eShop.Catalog.Data.Services;
using eShop.Catalog.WebForms.Pages;
using eShop.Catalog.WebForms.Pages.Catalog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace eShop.Catalog.WebForms.Tests.S7_PagesRead;

public class PageModelTests
{
    private const string Hidden = "esh-pager-item--hidden";

    private readonly CatalogServiceMock catalogService = new();

    [Fact]
    public void Index_without_route_values_uses_legacy_defaults()
    {
        var page = Index(index: null, size: null);

        Assert.Equal(IndexModel.DefaultPageIndex, page.Catalog.ActualPage);
        Assert.Equal(IndexModel.DefaultPageSize, page.Catalog.ItemsPerPage);
        Assert.Equal(12, page.Catalog.TotalItems);
        Assert.Equal(2, page.Catalog.TotalPages);
        Assert.Equal(Enumerable.Range(1, 10), page.Catalog.Data.Select(i => i.Id));
    }

    [Fact]
    public void Index_with_only_one_route_value_falls_back_to_defaults()
    {
        var page = Index(index: "1", size: null);

        Assert.Equal(0, page.Catalog.ActualPage);
        Assert.Equal(10, page.Catalog.ItemsPerPage);
    }

    [Fact]
    public void First_page_hides_previous_and_links_next()
    {
        var page = Index("0", "5");

        Assert.Equal("/Default/index/-1/size/5", page.PreviousUrl);
        Assert.Equal("/Default/index/1/size/5", page.NextUrl);
        Assert.Equal("esh-pager-item esh-pager-item--navigable " + Hidden, page.PreviousCssClass);
        Assert.Equal("esh-pager-item esh-pager-item--navigable", page.NextCssClass);
    }

    [Fact]
    public void Middle_page_shows_both_pager_links()
    {
        var page = Index("1", "5");

        Assert.Equal(1, page.Catalog.ActualPage);
        Assert.Equal(3, page.Catalog.TotalPages);
        Assert.Equal("/Default/index/0/size/5", page.PreviousUrl);
        Assert.Equal("/Default/index/2/size/5", page.NextUrl);
        Assert.DoesNotContain(Hidden, page.PreviousCssClass);
        Assert.DoesNotContain(Hidden, page.NextCssClass);
    }

    [Fact]
    public void Last_page_links_previous_and_hides_next()
    {
        var page = Index("2", "5");

        Assert.Equal(new[] { 11, 12 }, page.Catalog.Data.Select(i => i.Id));
        Assert.Equal("/Default/index/1/size/5", page.PreviousUrl);
        Assert.Equal("/Default/index/3/size/5", page.NextUrl);
        Assert.DoesNotContain(Hidden, page.PreviousCssClass);
        Assert.EndsWith(Hidden, page.NextCssClass);
    }

    [Fact]
    public void Single_page_hides_both_pager_links()
    {
        var page = Index("0", "20");

        Assert.EndsWith(Hidden, page.PreviousCssClass);
        Assert.EndsWith(Hidden, page.NextCssClass);
    }

    [Fact]
    public void Details_loads_existing_item()
    {
        var page = new DetailsModel(catalogService, NullLogger<DetailsModel>.Instance);

        var result = page.OnGet("1");

        Assert.IsType<PageResult>(result);
        Assert.Equal(".NET Bot Black Hoodie", page.Item.Name);
    }

    [Fact]
    public void Details_unknown_id_returns_legacy_500()
    {
        var page = new DetailsModel(catalogService, NullLogger<DetailsModel>.Instance);

        var result = page.OnGet("999");

        Assert.Equal(StatusCodes.Status500InternalServerError, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public void Delete_get_unknown_id_returns_legacy_500()
    {
        var page = Delete();

        var result = page.OnGet("999");

        Assert.Equal(StatusCodes.Status500InternalServerError, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public void Delete_post_unknown_id_returns_legacy_500_and_removes_nothing()
    {
        var page = Delete();

        var result = page.OnPost("999");

        Assert.Equal(StatusCodes.Status500InternalServerError, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Equal(12, catalogService.GetCatalogItemsPaginated(20, 0).TotalItems);
    }

    [Fact]
    public void Delete_post_removes_item_and_redirects_to_root_like_response_redirect()
    {
        var page = Delete();

        var result = Assert.IsType<ContentResult>(page.OnPost("2"));

        Assert.Equal(StatusCodes.Status302Found, result.StatusCode);
        Assert.Equal("/", page.Response.Headers.Location.ToString());
        Assert.Contains("Object moved", result.Content);
        Assert.Null(catalogService.FindCatalogItem(2));
        Assert.Equal(11, catalogService.GetCatalogItemsPaginated(20, 0).TotalItems);
    }

    [Fact]
    public void Delete_post_not_raised_by_delete_button_rerenders_and_removes_nothing()
    {
        var page = Delete(withDeleteButton: false);

        var result = page.OnPost("2");

        Assert.IsType<PageResult>(result);
        Assert.NotNull(catalogService.FindCatalogItem(2));
    }

    [Theory]
    [InlineData("/", "./")]
    [InlineData("/Catalog/Details/1", "../../")]
    [InlineData("/Catalog/Delete/12", "../../")]
    [InlineData(null, "./")]
    public void App_root_link_is_relative_to_request_directory_like_webforms(string? path, string expected)
    {
        Assert.Equal(expected, LegacyPage.AppRootRelativeTo(path));
    }

    private IndexModel Index(string? index, string? size)
    {
        var page = new IndexModel(catalogService, NullLogger<IndexModel>.Instance);
        page.OnGet(index, size);
        return page;
    }

    private DeleteModel Delete(bool withDeleteButton = true)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        var fields = new Dictionary<string, StringValues>();
        if (withDeleteButton)
        {
            fields[DeleteModel.DeleteButtonName] = "[ Delete ]";
        }

        httpContext.Request.Form = new FormCollection(fields);
        return new DeleteModel(catalogService, NullLogger<DeleteModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = httpContext },
        };
    }
}
