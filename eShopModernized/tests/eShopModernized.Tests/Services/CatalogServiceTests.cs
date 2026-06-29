using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using eShopModernized.Tests.TestSupport;
using Xunit;

namespace eShopModernized.Tests.Services
{
    public class CatalogServiceTests
    {
        private static CatalogService CreateService(SqliteCatalogContext harness)
            => new CatalogService(harness.Context, new CatalogItemHiLoGenerator());

        [Fact]
        public void GetCatalogItemsPaginated_FirstPage_RespectsPageSizeAndIncludesRelations()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);

            var page = service.GetCatalogItemsPaginated(pageSize: 2, pageIndex: 0);

            Assert.Equal(0, page.ActualPage);
            Assert.Equal(2, page.ItemsPerPage);
            Assert.Equal(5, page.TotalItems);
            Assert.Equal(3, page.TotalPages);
            var items = page.Data.ToList();
            Assert.Equal(2, items.Count);
            Assert.All(items, i => Assert.NotNull(i.CatalogBrand));
            Assert.All(items, i => Assert.NotNull(i.CatalogType));
        }

        [Fact]
        public void GetCatalogItemsPaginated_LastPage_ReturnsRemainingItems()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);

            var page = service.GetCatalogItemsPaginated(pageSize: 2, pageIndex: 2);

            Assert.Single(page.Data);
        }

        [Fact]
        public async Task GetCatalogItemsPaginatedAsync_ReturnsPagedResult()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);

            var page = await service.GetCatalogItemsPaginatedAsync(pageSize: 10, pageIndex: 0);

            Assert.Equal(5, page.Data.Count());
            Assert.Equal(5, page.TotalItems);
            Assert.Equal(1, page.TotalPages);
        }

        [Fact]
        public void FindCatalogItem_ExistingId_ReturnsItemWithRelations()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);

            var item = service.FindCatalogItem(1);

            Assert.NotNull(item);
            Assert.Equal(".NET Bot Black Hoodie", item!.Name);
            Assert.Equal(".NET", item.CatalogBrand!.Brand);
            Assert.Equal("T-Shirt", item.CatalogType!.Type);
        }

        [Fact]
        public void FindCatalogItem_UnknownId_ReturnsNull()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);

            Assert.Null(service.FindCatalogItem(9999));
        }

        [Fact]
        public async Task FindCatalogItemAsync_ExistingId_ReturnsItem()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);

            var item = await service.FindCatalogItemAsync(2);

            Assert.NotNull(item);
            Assert.Equal(2, item!.Id);
        }

        [Fact]
        public async Task FindCatalogItemAsync_UnknownId_ReturnsNull()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);

            Assert.Null(await service.FindCatalogItemAsync(9999));
        }

        [Fact]
        public void GetCatalogTypes_ReturnsSeededTypes()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);

            var types = service.GetCatalogTypes().ToList();

            Assert.Equal(2, types.Count);
            Assert.Contains(types, t => t.Type == "Mug");
        }

        [Fact]
        public async Task GetCatalogTypesAsync_ReturnsSeededTypes()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);

            var types = await service.GetCatalogTypesAsync();

            Assert.Equal(2, types.Count());
        }

        [Fact]
        public void GetCatalogBrands_ReturnsSeededBrands()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);

            var brands = service.GetCatalogBrands().ToList();

            Assert.Equal(2, brands.Count);
            Assert.Contains(brands, b => b.Brand == "Azure");
        }

        [Fact]
        public async Task GetCatalogBrandsAsync_ReturnsSeededBrands()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);

            var brands = await service.GetCatalogBrandsAsync();

            Assert.Equal(2, brands.Count());
        }

        [Fact]
        public void UpdateCatalogItem_ExistingItem_PersistsChanges()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);
            var updated = new CatalogItem { Id = 1, Name = "Updated Name", Price = 99M, CatalogBrandId = 1, CatalogTypeId = 2, PictureFileName = "1.png" };

            service.UpdateCatalogItem(updated);

            var item = service.FindCatalogItem(1);
            Assert.Equal("Updated Name", item!.Name);
            Assert.Equal(99M, item.Price);
        }

        [Fact]
        public async Task UpdateCatalogItemAsync_PersistsChanges()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);
            var updated = new CatalogItem { Id = 2, Name = "Async Updated", Price = 5M, CatalogBrandId = 1, CatalogTypeId = 1, PictureFileName = "2.png" };

            await service.UpdateCatalogItemAsync(updated);

            Assert.Equal("Async Updated", service.FindCatalogItem(2)!.Name);
        }

        [Fact]
        public void RemoveCatalogItem_ExistingItem_RemovesIt()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);
            var item = service.FindCatalogItem(1)!;

            service.RemoveCatalogItem(item);

            Assert.Null(service.FindCatalogItem(1));
        }

        [Fact]
        public async Task RemoveCatalogItemAsync_RemovesIt()
        {
            using var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);
            var item = await service.FindCatalogItemAsync(3);

            await service.RemoveCatalogItemAsync(item!);

            Assert.Null(service.FindCatalogItem(3));
        }

        [Fact]
        public void Dispose_DisposesUnderlyingContext()
        {
            var harness = SqliteCatalogContext.CreateSeeded();
            var service = CreateService(harness);

            service.Dispose();

            Assert.Throws<ObjectDisposedException>(() => harness.Context.CatalogItems.ToList());
            harness.Dispose();
        }
    }
}
