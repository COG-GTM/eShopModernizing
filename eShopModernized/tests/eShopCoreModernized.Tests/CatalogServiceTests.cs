using eShopCoreModernized.Infrastructure;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace eShopCoreModernized.Tests
{
    public class CatalogServiceTests
    {
        private static CatalogDBContext CreateSeededContext()
        {
            var options = new DbContextOptionsBuilder<CatalogDBContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var context = new CatalogDBContext(options);
            CatalogDBInitializer.SeedAsync(context, NullLogger.Instance).GetAwaiter().GetResult();
            return context;
        }

        [Fact]
        public async Task GetCatalogItemsPaginatedAsync_ReturnsOrderedPage()
        {
            using var context = CreateSeededContext();
            var service = new CatalogService(context, new CatalogItemHiLoGenerator());

            var page = await service.GetCatalogItemsPaginatedAsync(pageSize: 5, pageIndex: 1);

            Assert.Equal(12, page.TotalItems);
            Assert.Equal(5, page.Data.Count());
            Assert.Equal(6, page.Data.First().Id);
        }

        [Fact]
        public async Task FindCatalogItemAsync_IncludesBrandAndType()
        {
            using var context = CreateSeededContext();
            var service = new CatalogService(context, new CatalogItemHiLoGenerator());

            var item = await service.FindCatalogItemAsync(1);

            Assert.NotNull(item);
            Assert.NotNull(item!.CatalogBrand);
            Assert.NotNull(item.CatalogType);
        }

        [Fact]
        public async Task RemoveCatalogItemAsync_DeletesItem()
        {
            using var context = CreateSeededContext();
            var service = new CatalogService(context, new CatalogItemHiLoGenerator());
            var item = await service.FindCatalogItemAsync(3);

            await service.RemoveCatalogItemAsync(item!);

            Assert.Null(await service.FindCatalogItemAsync(3));
        }

        [Fact]
        public async Task UpdateCatalogItemAsync_PersistsChanges()
        {
            using var context = CreateSeededContext();
            var service = new CatalogService(context, new CatalogItemHiLoGenerator());
            var item = await service.FindCatalogItemAsync(2);
            item!.Price = 42M;

            await service.UpdateCatalogItemAsync(item);

            var reloaded = await service.FindCatalogItemAsync(2);
            Assert.Equal(42M, reloaded!.Price);
        }
    }
}
