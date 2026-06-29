using eShopCoreModernized.ViewModel;

namespace eShopCoreModernized.Tests;

public class PaginatedItemsViewModelTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var data = new List<string> { "a", "b", "c" };

        var vm = new PaginatedItemsViewModel<string>(2, 10, 50, data);

        Assert.Equal(2, vm.ActualPage);
        Assert.Equal(10, vm.ItemsPerPage);
        Assert.Equal(50, vm.TotalItems);
        Assert.Equal(data, vm.Data);
    }

    [Fact]
    public void TotalPages_CalculatedCorrectly_ExactDivision()
    {
        var vm = new PaginatedItemsViewModel<string>(0, 10, 30, Array.Empty<string>());

        Assert.Equal(3, vm.TotalPages);
    }

    [Fact]
    public void TotalPages_RoundsUp_WhenNotExactDivision()
    {
        var vm = new PaginatedItemsViewModel<string>(0, 10, 25, Array.Empty<string>());

        Assert.Equal(3, vm.TotalPages);
    }

    [Fact]
    public void TotalPages_IsOne_WhenSinglePage()
    {
        var vm = new PaginatedItemsViewModel<string>(0, 10, 5, Array.Empty<string>());

        Assert.Equal(1, vm.TotalPages);
    }

    [Fact]
    public void TotalPages_IsSettable()
    {
        var vm = new PaginatedItemsViewModel<string>(0, 10, 100, Array.Empty<string>());
        vm.TotalPages = 42;

        Assert.Equal(42, vm.TotalPages);
    }
}
