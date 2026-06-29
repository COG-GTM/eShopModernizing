using Xunit;
using eShopCoreModernized.Models;
using eShopCoreModernized.ViewModel;

namespace eShopCoreModernized.Tests;

public class PaginatedItemsViewModelTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var data = new List<CatalogItem>
        {
            new() { Id = 1, Name = "A" },
            new() { Id = 2, Name = "B" },
        };

        var viewModel = new PaginatedItemsViewModel<CatalogItem>(2, 10, 25, data);

        Assert.Equal(2, viewModel.ActualPage);
        Assert.Equal(10, viewModel.ItemsPerPage);
        Assert.Equal(25, viewModel.TotalItems);
        Assert.Equal(data, viewModel.Data);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(25, 10, 3)]
    [InlineData(5, 5, 1)]
    [InlineData(1, 10, 1)]
    [InlineData(100, 7, 15)]
    public void TotalPages_UsesCeilingCalculation(long count, int pageSize, int expectedPages)
    {
        var viewModel = new PaginatedItemsViewModel<CatalogItem>(0, pageSize, count, new List<CatalogItem>());

        Assert.Equal(expectedPages, viewModel.TotalPages);
    }

    [Fact]
    public void TotalPages_IsSettable()
    {
        var viewModel = new PaginatedItemsViewModel<CatalogItem>(0, 10, 5, new List<CatalogItem>())
        {
            TotalPages = 99,
        };

        Assert.Equal(99, viewModel.TotalPages);
    }
}
