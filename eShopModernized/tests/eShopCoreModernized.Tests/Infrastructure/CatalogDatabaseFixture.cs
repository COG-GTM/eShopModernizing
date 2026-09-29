using eShopCoreModernized.Models;
using Microsoft.EntityFrameworkCore;

namespace eShopCoreModernized.Tests.Infrastructure
{
    /// <summary>
    /// Per-test relational database backed by in-memory SQLite, created from the real
    /// <see cref="CatalogDBContext"/> model and seeded with a small known catalog.
    /// </summary>
    public sealed class CatalogDatabaseFixture : IDisposable
    {
        public const int SeededItemCount = 12;

        private readonly DbContextOptions<CatalogDBContext> options;

        public CatalogDatabaseFixture(bool seed = true)
        {
            Connection = new HiLoSqliteConnection();
            Connection.Open();

            options = new DbContextOptionsBuilder<CatalogDBContext>()
                .UseSqlite(Connection)
                .Options;

            using var context = CreateContext();
            context.Database.EnsureCreated();

            if (seed)
            {
                Seed(context);
            }
        }

        public HiLoSqliteConnection Connection { get; }

        public CatalogDBContext CreateContext() => new CatalogDBContext(options);

        public void Dispose() => Connection.Dispose();

        private static void Seed(CatalogDBContext context)
        {
            context.CatalogBrands.AddRange(
                new CatalogBrand { Id = 1, Brand = "Azure" },
                new CatalogBrand { Id = 2, Brand = ".NET" });

            context.CatalogTypes.AddRange(
                new CatalogType { Id = 1, Type = "Mug" },
                new CatalogType { Id = 2, Type = "T-Shirt" });

            // Ids are deliberately inserted out of order and start above the hi-lo range
            // so paging order and generated ids can be asserted independently.
            var ids = new[] { 112, 101, 105, 110, 103, 108, 102, 111, 104, 109, 106, 107 };
            foreach (var id in ids)
            {
                context.CatalogItems.Add(new CatalogItem
                {
                    Id = id,
                    Name = $"Item {id}",
                    Description = $"Description {id}",
                    Price = id + 0.5m,
                    PictureFileName = $"{id}.png",
                    CatalogBrandId = id % 2 == 0 ? 2 : 1,
                    CatalogTypeId = id % 3 == 0 ? 1 : 2,
                    AvailableStock = id,
                    RestockThreshold = 5,
                    MaxStockThreshold = 200,
                });
            }

            context.SaveChanges();
            context.ChangeTracker.Clear();
        }
    }
}
