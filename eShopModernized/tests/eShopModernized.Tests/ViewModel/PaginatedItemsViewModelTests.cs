using eShopCoreModernized.Models;
using eShopCoreModernized.ViewModel;
using Xunit;

namespace eShopModernized.Tests.ViewModel;

public class PaginatedItemsViewModelTests
{
    [Fact]
    public void Constructor_SetsProperties_AndComputesTotalPages()
    {
        var data = new List<CatalogItem> { new() { Id = 1 }, new() { Id = 2 } };

        var model = new PaginatedItemsViewModel<CatalogItem>(pageIndex: 1, pageSize: 10, count: 25, data: data);

        Assert.Equal(1, model.ActualPage);
        Assert.Equal(10, model.ItemsPerPage);
        Assert.Equal(25, model.TotalItems);
        Assert.Equal(3, model.TotalPages);
        Assert.Same(data, model.Data);
    }

    [Fact]
    public void Constructor_ComputesExactTotalPages_WhenEvenlyDivisible()
    {
        var model = new PaginatedItemsViewModel<CatalogItem>(0, 5, 20, new List<CatalogItem>());

        Assert.Equal(4, model.TotalPages);
    }

    [Fact]
    public void TotalPages_IsSettable()
    {
        var model = new PaginatedItemsViewModel<CatalogItem>(0, 5, 20, new List<CatalogItem>())
        {
            TotalPages = 99
        };

        Assert.Equal(99, model.TotalPages);
    }
}
