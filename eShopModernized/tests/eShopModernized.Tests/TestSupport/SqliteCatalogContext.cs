using eShopCoreModernized.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace eShopModernized.Tests.TestSupport
{
    /// <summary>
    /// Owns a SQLite in-memory connection together with a <see cref="CatalogDBContext"/>
    /// created over it. The connection is kept open for the lifetime of the instance so
    /// the in-memory database survives across context operations.
    /// </summary>
    internal sealed class SqliteCatalogContext : IDisposable
    {
        private readonly SqliteConnection _connection;

        public CatalogDBContext Context { get; }

        public SqliteCatalogContext()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<CatalogDBContext>()
                .UseSqlite(_connection)
                .Options;

            Context = new CatalogDBContext(options);
            Context.Database.EnsureCreated();
        }

        public static SqliteCatalogContext CreateSeeded()
        {
            var harness = new SqliteCatalogContext();
            harness.Seed();
            return harness;
        }

        public void Seed()
        {
            var brands = new[]
            {
                new CatalogBrand { Id = 1, Brand = ".NET" },
                new CatalogBrand { Id = 2, Brand = "Azure" },
            };
            var types = new[]
            {
                new CatalogType { Id = 1, Type = "Mug" },
                new CatalogType { Id = 2, Type = "T-Shirt" },
            };
            Context.CatalogBrands.AddRange(brands);
            Context.CatalogTypes.AddRange(types);
            Context.CatalogItems.AddRange(
                new CatalogItem { Id = 1, Name = ".NET Bot Black Hoodie", Price = 19.5M, CatalogBrandId = 1, CatalogTypeId = 2, PictureFileName = "1.png" },
                new CatalogItem { Id = 2, Name = ".NET Black & White Mug", Price = 8.5M, CatalogBrandId = 1, CatalogTypeId = 1, PictureFileName = "2.png" },
                new CatalogItem { Id = 3, Name = "Prism White T-Shirt", Price = 12M, CatalogBrandId = 2, CatalogTypeId = 2, PictureFileName = "3.png" },
                new CatalogItem { Id = 4, Name = "Azure Mug", Price = 9M, CatalogBrandId = 2, CatalogTypeId = 1, PictureFileName = "4.png" },
                new CatalogItem { Id = 5, Name = "Azure Black Hoodie", Price = 22M, CatalogBrandId = 2, CatalogTypeId = 2, PictureFileName = "5.png" });
            Context.SaveChanges();

            // Detach everything so reads exercise the real query pipeline (Include, etc.).
            Context.ChangeTracker.Clear();
        }

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }
}
