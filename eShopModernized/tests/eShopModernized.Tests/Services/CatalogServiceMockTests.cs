using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Xunit;

namespace eShopModernized.Tests.Services
{
    public class CatalogServiceMockTests
    {
        private static CatalogServiceMock CreateService() => new CatalogServiceMock();

        [Fact]
        public void GetCatalogBrands_ReturnsSeededBrands()
        {
            var service = CreateService();

            var brands = service.GetCatalogBrands().ToList();

            Assert.Equal(5, brands.Count);
            Assert.Contains(brands, b => b.Brand == "Azure");
            Assert.Contains(brands, b => b.Brand == ".NET");
        }

        [Fact]
        public void GetCatalogTypes_ReturnsSeededTypes()
        {
            var service = CreateService();

            var types = service.GetCatalogTypes().ToList();

            Assert.Equal(4, types.Count);
            Assert.Contains(types, t => t.Type == "Mug");
        }

        [Fact]
        public async Task GetCatalogBrandsAsync_ReturnsSameDataAsSync()
        {
            var service = CreateService();

            var brands = await service.GetCatalogBrandsAsync();

            Assert.Equal(5, brands.Count());
        }

        [Fact]
        public async Task GetCatalogTypesAsync_ReturnsSameDataAsSync()
        {
            var service = CreateService();

            var types = await service.GetCatalogTypesAsync();

            Assert.Equal(4, types.Count());
        }

        [Fact]
        public void FindCatalogItem_ExistingId_ReturnsItemWithRelations()
        {
            var service = CreateService();

            var item = service.FindCatalogItem(1);

            Assert.NotNull(item);
            Assert.Equal(".NET Bot Black Hoodie", item!.Name);
            Assert.NotNull(item.CatalogBrand);
            Assert.NotNull(item.CatalogType);
            Assert.Equal(".NET", item.CatalogBrand!.Brand);
        }

        [Fact]
        public void FindCatalogItem_UnknownId_ReturnsNull()
        {
            var service = CreateService();

            Assert.Null(service.FindCatalogItem(9999));
        }

        [Fact]
        public async Task FindCatalogItemAsync_ExistingId_ReturnsItem()
        {
            var service = CreateService();

            var item = await service.FindCatalogItemAsync(2);

            Assert.NotNull(item);
            Assert.Equal(2, item!.Id);
        }

        [Fact]
        public void GetCatalogItemsPaginated_FirstPage_RespectsPageSize()
        {
            var service = CreateService();

            var page = service.GetCatalogItemsPaginated(pageSize: 2, pageIndex: 0);

            Assert.Equal(0, page.ActualPage);
            Assert.Equal(2, page.ItemsPerPage);
            Assert.Equal(5, page.TotalItems);
            Assert.Equal(3, page.TotalPages);
            Assert.Equal(2, page.Data.Count());
        }

        [Fact]
        public void GetCatalogItemsPaginated_LastPage_ReturnsRemainingItems()
        {
            var service = CreateService();

            var page = service.GetCatalogItemsPaginated(pageSize: 2, pageIndex: 2);

            Assert.Single(page.Data);
        }

        [Fact]
        public async Task GetCatalogItemsPaginatedAsync_ReturnsPagedResult()
        {
            var service = CreateService();

            var page = await service.GetCatalogItemsPaginatedAsync(pageSize: 10, pageIndex: 0);

            Assert.Equal(5, page.Data.Count());
            Assert.Equal(1, page.TotalPages);
        }

        [Fact]
        public void CreateCatalogItem_AssignsNextIdAndAddsItem()
        {
            var service = CreateService();
            var newItem = new CatalogItem { Name = "New Item", Price = 1.0M };

            service.CreateCatalogItem(newItem);

            Assert.Equal(6, newItem.Id);
            Assert.NotNull(service.FindCatalogItem(6));
        }

        [Fact]
        public async Task CreateCatalogItemAsync_AddsItem()
        {
            var service = CreateService();

            await service.CreateCatalogItemAsync(new CatalogItem { Name = "Async Item" });

            var page = service.GetCatalogItemsPaginated(100, 0);
            Assert.Equal(6, page.TotalItems);
        }

        [Fact]
        public void UpdateCatalogItem_ExistingItem_ReplacesItem()
        {
            var service = CreateService();
            var updated = new CatalogItem { Id = 1, Name = "Updated Name", Price = 99M };

            service.UpdateCatalogItem(updated);

            var item = service.FindCatalogItem(1);
            Assert.Equal("Updated Name", item!.Name);
            Assert.Equal(99M, item.Price);
        }

        [Fact]
        public void UpdateCatalogItem_UnknownItem_DoesNothing()
        {
            var service = CreateService();

            service.UpdateCatalogItem(new CatalogItem { Id = 9999, Name = "ghost" });

            Assert.Null(service.FindCatalogItem(9999));
        }

        [Fact]
        public async Task UpdateCatalogItemAsync_ReplacesItem()
        {
            var service = CreateService();

            await service.UpdateCatalogItemAsync(new CatalogItem { Id = 2, Name = "Async Updated" });

            Assert.Equal("Async Updated", service.FindCatalogItem(2)!.Name);
        }

        [Fact]
        public void RemoveCatalogItem_ExistingItem_RemovesIt()
        {
            var service = CreateService();

            service.RemoveCatalogItem(new CatalogItem { Id = 1 });

            Assert.Null(service.FindCatalogItem(1));
        }

        [Fact]
        public async Task RemoveCatalogItemAsync_RemovesIt()
        {
            var service = CreateService();

            await service.RemoveCatalogItemAsync(new CatalogItem { Id = 3 });

            Assert.Null(service.FindCatalogItem(3));
        }

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            var service = CreateService();

            var exception = Record.Exception(() => service.Dispose());

            Assert.Null(exception);
        }
    }
}
