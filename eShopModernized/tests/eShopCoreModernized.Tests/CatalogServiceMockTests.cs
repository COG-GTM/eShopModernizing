using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Xunit;

namespace eShopCoreModernized.Tests
{
    public class CatalogServiceMockTests
    {
        [Fact]
        public async Task GetCatalogItemsPaginatedAsync_ReturnsPagedItems()
        {
            var service = new CatalogServiceMock();

            var page = await service.GetCatalogItemsPaginatedAsync(pageSize: 2, pageIndex: 0);

            Assert.Equal(2, page.Data.Count());
            Assert.Equal(5, page.TotalItems);
            Assert.Equal(0, page.ActualPage);
        }

        [Fact]
        public async Task FindCatalogItemAsync_ReturnsItem_WhenExists()
        {
            var service = new CatalogServiceMock();

            var item = await service.FindCatalogItemAsync(1);

            Assert.NotNull(item);
            Assert.Equal(".NET Bot Black Hoodie", item!.Name);
        }

        [Fact]
        public async Task FindCatalogItemAsync_ReturnsNull_WhenMissing()
        {
            var service = new CatalogServiceMock();

            var item = await service.FindCatalogItemAsync(9999);

            Assert.Null(item);
        }

        [Fact]
        public async Task CreateCatalogItemAsync_AssignsNewId()
        {
            var service = new CatalogServiceMock();
            var newItem = new CatalogItem { Name = "New Item", Description = "New", Price = 5M, PictureFileName = "new.png", CatalogTypeId = 1, CatalogBrandId = 1 };

            await service.CreateCatalogItemAsync(newItem);

            Assert.True(newItem.Id > 5);
            Assert.NotNull(await service.FindCatalogItemAsync(newItem.Id));
        }

        [Fact]
        public async Task UpdateCatalogItemAsync_ReplacesItem()
        {
            var service = new CatalogServiceMock();
            var updated = new CatalogItem { Id = 1, Name = "Updated Name", Description = "Updated", Price = 1M, PictureFileName = "1.png", CatalogTypeId = 2, CatalogBrandId = 2 };

            await service.UpdateCatalogItemAsync(updated);

            var item = await service.FindCatalogItemAsync(1);
            Assert.Equal("Updated Name", item!.Name);
        }

        [Fact]
        public async Task RemoveCatalogItemAsync_DeletesItem()
        {
            var service = new CatalogServiceMock();
            var item = await service.FindCatalogItemAsync(2);

            await service.RemoveCatalogItemAsync(item!);

            Assert.Null(await service.FindCatalogItemAsync(2));
        }

        [Fact]
        public async Task GetCatalogBrandsAndTypes_ReturnAllEntries()
        {
            var service = new CatalogServiceMock();

            Assert.Equal(5, (await service.GetCatalogBrandsAsync()).Count());
            Assert.Equal(4, (await service.GetCatalogTypesAsync()).Count());
        }
    }
}
