using eShopCoreModernized.Models;
using eShopCoreModernized.ViewModel;

namespace eShopCoreModernized.Tests.ViewModel
{
    public class PaginatedItemsViewModelTests
    {
        [Theory]
        [InlineData(0, 10, 0)]
        [InlineData(1, 10, 1)]
        [InlineData(10, 10, 1)]
        [InlineData(11, 10, 2)]
        [InlineData(12, 5, 3)]
        public void TotalPages_RoundsUp(long count, int pageSize, int expectedPages)
        {
            var model = new PaginatedItemsViewModel<CatalogItem>(0, pageSize, count, Array.Empty<CatalogItem>());

            Assert.Equal(expectedPages, model.TotalPages);
        }

        [Fact]
        public void Constructor_ExposesPagingValuesAndData()
        {
            var data = new[] { new CatalogItem { Id = 1 } };

            var model = new PaginatedItemsViewModel<CatalogItem>(3, 10, 42, data);

            Assert.Equal(3, model.ActualPage);
            Assert.Equal(10, model.ItemsPerPage);
            Assert.Equal(42, model.TotalItems);
            Assert.Same(data, model.Data);
        }
    }
}
