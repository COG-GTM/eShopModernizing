using eShopCoreModernized.Models;
using eShopCoreModernized.ViewModel;
using Xunit;

namespace eShopCoreModernized.Tests
{
    public class PaginatedItemsViewModelTests
    {
        [Theory]
        [InlineData(10, 25, 3)]
        [InlineData(10, 30, 3)]
        [InlineData(10, 31, 4)]
        [InlineData(5, 0, 0)]
        public void TotalPages_IsCeilingOfCountOverPageSize(int pageSize, long count, int expectedPages)
        {
            var vm = new PaginatedItemsViewModel<CatalogItem>(0, pageSize, count, new List<CatalogItem>());

            Assert.Equal(expectedPages, vm.TotalPages);
        }

        [Fact]
        public void Constructor_SetsAllProperties()
        {
            var data = new List<CatalogItem> { new CatalogItem { Id = 1, Name = "x", PictureFileName = "x.png" } };

            var vm = new PaginatedItemsViewModel<CatalogItem>(2, 10, 100, data);

            Assert.Equal(2, vm.ActualPage);
            Assert.Equal(10, vm.ItemsPerPage);
            Assert.Equal(100, vm.TotalItems);
            Assert.Equal(10, vm.TotalPages);
            Assert.Single(vm.Data);
        }
    }
}
