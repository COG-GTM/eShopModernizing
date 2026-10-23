using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.ViewModel;
using Xunit;

namespace eShop.Catalog.UnitTests
{
    public class PaginatedItemsViewModelTests
    {
        [Theory]
        [InlineData(12, 10, 2)]
        [InlineData(10, 10, 1)]
        [InlineData(11, 10, 2)]
        [InlineData(0, 10, 0)]
        [InlineData(12, 1, 12)]
        [InlineData(12, 100, 1)]
        [InlineData(13, 5, 3)]
        public void TotalPages_is_the_ceiling_of_count_over_page_size(long count, int pageSize, int expected)
        {
            var vm = new PaginatedItemsViewModel<CatalogItem>(0, pageSize, count, new List<CatalogItem>());

            Assert.Equal(expected, vm.TotalPages);
        }

        [Fact]
        public void Properties_echo_the_constructor_arguments()
        {
            var data = new List<CatalogItem> { new CatalogItem { Id = 7 } };

            var vm = new PaginatedItemsViewModel<CatalogItem>(3, 4, 13, data);

            Assert.Equal(3, vm.ActualPage);
            Assert.Equal(4, vm.ItemsPerPage);
            Assert.Equal(13, vm.TotalItems);
            Assert.Same(data, vm.Data);
        }

        [Fact]
        public void Page_size_zero_throws_DivideByZeroException_like_legacy()
        {
            // Legacy /?pageSize=0 is recorded as HTTP 500 (decimal division by zero).
            Assert.Throws<DivideByZeroException>(() => new PaginatedItemsViewModel<CatalogItem>(0, 0, 12, new List<CatalogItem>()));
        }

        [Fact]
        public void TotalPages_is_settable()
        {
            var vm = new PaginatedItemsViewModel<CatalogItem>(0, 10, 12, new List<CatalogItem>()) { TotalPages = 9 };

            Assert.Equal(9, vm.TotalPages);
        }
    }
}
