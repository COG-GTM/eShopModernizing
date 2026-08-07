using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using Microsoft.EntityFrameworkCore;

namespace eShopCoreModernized.Tests
{
    public class BrandServiceTests
    {
        private sealed class StubBrandIdGenerator : IBrandIdGenerator
        {
            private int nextId;

            public StubBrandIdGenerator(int firstId)
            {
                nextId = firstId;
            }

            public Task<int> GetNextSequenceValueAsync(CatalogDBContext db) => Task.FromResult(nextId++);
        }

        private static CatalogDBContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<CatalogDBContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new CatalogDBContext(options);
            db.CatalogBrands.AddRange(
                new CatalogBrand { Id = 2, Brand = ".NET" },
                new CatalogBrand { Id = 1, Brand = "Azure" });
            db.CatalogTypes.Add(new CatalogType { Id = 1, Type = "Mug" });
            db.CatalogItems.Add(new CatalogItem
            {
                Id = 1,
                Name = ".NET Black & White Mug",
                Price = 8.5M,
                CatalogBrandId = 2,
                CatalogTypeId = 1
            });
            db.SaveChanges();

            return db;
        }

        [Fact]
        public async Task GetBrandsAsync_returns_brands_ordered_by_id()
        {
            using var db = CreateContext();
            var service = new BrandService(db, new StubBrandIdGenerator(100));

            var brands = (await service.GetBrandsAsync()).ToList();

            Assert.Equal(new[] { 1, 2 }, brands.Select(b => b.Id));
        }

        [Fact]
        public async Task FindBrandAsync_returns_null_for_unknown_id()
        {
            using var db = CreateContext();
            var service = new BrandService(db, new StubBrandIdGenerator(100));

            Assert.Null(await service.FindBrandAsync(999));
        }

        [Fact]
        public async Task CreateBrandAsync_assigns_id_from_the_sequence_generator()
        {
            using var db = CreateContext();
            var service = new BrandService(db, new StubBrandIdGenerator(100));
            var brand = new CatalogBrand { Brand = "Contoso" };

            await service.CreateBrandAsync(brand);

            Assert.Equal(100, brand.Id);
            Assert.Equal("Contoso", (await db.CatalogBrands.SingleAsync(b => b.Id == 100)).Brand);
        }

        [Fact]
        public async Task UpdateBrandAsync_persists_the_new_name()
        {
            using var db = CreateContext();
            var service = new BrandService(db, new StubBrandIdGenerator(100));

            var brand = await service.FindBrandAsync(1);
            brand!.Brand = "Azure Cloud";
            await service.UpdateBrandAsync(brand);

            Assert.Equal("Azure Cloud", (await db.CatalogBrands.SingleAsync(b => b.Id == 1)).Brand);
        }

        [Fact]
        public async Task RemoveBrandAsync_deletes_the_brand()
        {
            using var db = CreateContext();
            var service = new BrandService(db, new StubBrandIdGenerator(100));

            var brand = await service.FindBrandAsync(1);
            await service.RemoveBrandAsync(brand!);

            Assert.False(await db.CatalogBrands.AnyAsync(b => b.Id == 1));
        }

        [Theory]
        [InlineData(2, true)]
        [InlineData(1, false)]
        public async Task IsBrandInUseAsync_reports_whether_catalog_items_reference_the_brand(int brandId, bool expected)
        {
            using var db = CreateContext();
            var service = new BrandService(db, new StubBrandIdGenerator(100));

            Assert.Equal(expected, await service.IsBrandInUseAsync(brandId));
        }
    }
}
