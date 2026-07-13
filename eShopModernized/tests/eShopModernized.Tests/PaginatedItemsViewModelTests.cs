using eShopCoreModernized.Models;
using eShopCoreModernized.ViewModel;

namespace eShopModernized.Tests;

public class PaginatedItemsViewModelTests
{
    [Theory]
    [InlineData(0, 10, 0, 0)]      // no items -> no pages
    [InlineData(0, 10, 10, 1)]     // exactly one full page
    [InlineData(0, 10, 11, 2)]     // one over a full page -> ceiling rounds up
    [InlineData(0, 10, 5, 1)]      // partial page still counts as a page
    [InlineData(0, 3, 10, 4)]      // 10/3 = 3.33 -> 4
    [InlineData(0, 1, 5, 5)]       // page size of one
    [InlineData(0, 100, 5, 1)]     // page larger than total item count
    public void Constructor_ComputesTotalPagesUsingCeiling(int pageIndex, int pageSize, long count, int expectedTotalPages)
    {
        var vm = new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, count, Enumerable.Empty<CatalogItem>());

        Assert.Equal(expectedTotalPages, vm.TotalPages);
    }

    [Fact]
    public void Constructor_PopulatesAllPropertiesFromArguments()
    {
        var data = new[]
        {
            new CatalogItem { Id = 1, Name = "A" },
            new CatalogItem { Id = 2, Name = "B" },
        };

        var vm = new PaginatedItemsViewModel<CatalogItem>(pageIndex: 2, pageSize: 5, count: 42, data: data);

        Assert.Equal(2, vm.ActualPage);
        Assert.Equal(5, vm.ItemsPerPage);
        Assert.Equal(42, vm.TotalItems);
        Assert.Same(data, vm.Data);
        Assert.Equal(2, vm.Data.Count());
    }

    [Fact]
    public void Constructor_PreservesLargeTotalItemsCount()
    {
        long largeCount = (long)int.MaxValue + 100;

        var vm = new PaginatedItemsViewModel<CatalogItem>(0, 1000, largeCount, Enumerable.Empty<CatalogItem>());

        Assert.Equal(largeCount, vm.TotalItems);
    }
}
