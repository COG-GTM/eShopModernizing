using eShopCoreModernized.ViewModel;
using Xunit;

namespace eShopModernized.Tests.ViewModel
{
    public class PaginatedItemsViewModelTests
    {
        [Theory]
        [InlineData(0, 10, 0, 0)]
        [InlineData(0, 10, 10, 1)]
        [InlineData(0, 10, 11, 2)]
        [InlineData(0, 3, 10, 4)]
        public void Constructor_ComputesTotalPages(int pageIndex, int pageSize, long count, int expectedPages)
        {
            var model = new PaginatedItemsViewModel<string>(pageIndex, pageSize, count, new List<string>());

            Assert.Equal(expectedPages, model.TotalPages);
        }

        [Fact]
        public void Constructor_AssignsProvidedValues()
        {
            var data = new List<string> { "a", "b" };

            var model = new PaginatedItemsViewModel<string>(pageIndex: 2, pageSize: 5, count: 12, data: data);

            Assert.Equal(2, model.ActualPage);
            Assert.Equal(5, model.ItemsPerPage);
            Assert.Equal(12, model.TotalItems);
            Assert.Same(data, model.Data);
        }
    }
}
