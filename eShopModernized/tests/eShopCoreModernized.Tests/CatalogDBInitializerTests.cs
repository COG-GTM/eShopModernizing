using eShopCoreModernized.Infrastructure;
using eShopCoreModernized.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace eShopCoreModernized.Tests
{
    public class CatalogDBInitializerTests
    {
        private static CatalogDBContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<CatalogDBContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new CatalogDBContext(options);
        }

        [Fact]
        public async Task SeedAsync_PopulatesEmptyDatabase()
        {
            using var context = CreateContext();

            await CatalogDBInitializer.SeedAsync(context, NullLogger.Instance);

            Assert.Equal(12, await context.CatalogItems.CountAsync());
            Assert.Equal(5, await context.CatalogBrands.CountAsync());
            Assert.Equal(4, await context.CatalogTypes.CountAsync());
        }

        [Fact]
        public async Task SeedAsync_IsIdempotent()
        {
            using var context = CreateContext();

            await CatalogDBInitializer.SeedAsync(context, NullLogger.Instance);
            await CatalogDBInitializer.SeedAsync(context, NullLogger.Instance);

            Assert.Equal(12, await context.CatalogItems.CountAsync());
        }
    }
}
