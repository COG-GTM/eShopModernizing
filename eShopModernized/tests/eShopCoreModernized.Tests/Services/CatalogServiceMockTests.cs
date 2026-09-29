using eShopCoreModernized.Models;
using eShopCoreModernized.Services;

namespace eShopCoreModernized.Tests.Services
{
    public class CatalogServiceMockTests
    {
        private readonly CatalogServiceMock service = new();

        [Fact]
        public async Task GetCatalogItemsPaginated_PagesSeedData()
        {
            var first = service.GetCatalogItemsPaginated(pageSize: 2, pageIndex: 0);
            var last = await service.GetCatalogItemsPaginatedAsync(pageSize: 2, pageIndex: 2);

            Assert.Equal(5, first.TotalItems);
            Assert.Equal(3, first.TotalPages);
            Assert.Equal(new[] { 1, 2 }, first.Data.Select(i => i.Id));
            Assert.Equal(new[] { 5 }, last.Data.Select(i => i.Id));
        }

        [Fact]
        public async Task FindCatalogItem_ResolvesBrandAndType()
        {
            var item = await service.FindCatalogItemAsync(1);

            Assert.NotNull(item);
            Assert.Equal(".NET", item!.CatalogBrand?.Brand);
            Assert.Equal("T-Shirt", item.CatalogType?.Type);
            Assert.Null(service.FindCatalogItem(999));
        }

        [Fact]
        public async Task GetBrandsAndTypes_ReturnSeedLookups()
        {
            Assert.Equal(5, (await service.GetCatalogBrandsAsync()).Count());
            Assert.Equal(4, service.GetCatalogTypes().Count());
        }

        [Fact]
        public async Task CreateCatalogItem_AssignsNextIdAndAddsItem()
        {
            var item = new CatalogItem { Name = "New", CatalogBrandId = 1, CatalogTypeId = 1 };

            await service.CreateCatalogItemAsync(item);

            Assert.Equal(6, item.Id);
            Assert.Same(item, service.FindCatalogItem(6));
        }

        [Fact]
        public void UpdateCatalogItem_ReplacesExistingItem()
        {
            var replacement = new CatalogItem { Id = 3, Name = "Replaced", CatalogBrandId = 1, CatalogTypeId = 1 };

            service.UpdateCatalogItem(replacement);

            Assert.Same(replacement, service.FindCatalogItem(3));
            Assert.Equal(5, service.GetCatalogItemsPaginated(10, 0).TotalItems);
        }

        [Fact]
        public void UpdateCatalogItem_UnknownId_IsIgnored()
        {
            service.UpdateCatalogItem(new CatalogItem { Id = 999, Name = "Ghost" });

            Assert.Null(service.FindCatalogItem(999));
            Assert.Equal(5, service.GetCatalogItemsPaginated(10, 0).TotalItems);
        }

        [Fact]
        public async Task RemoveCatalogItem_RemovesById()
        {
            await service.RemoveCatalogItemAsync(new CatalogItem { Id = 2 });

            Assert.Null(service.FindCatalogItem(2));
            Assert.Equal(4, service.GetCatalogItemsPaginated(10, 0).TotalItems);
        }
    }
}
