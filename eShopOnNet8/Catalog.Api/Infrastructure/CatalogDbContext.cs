using Catalog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Infrastructure;

// Replaces the legacy Entity Framework 6 CatalogDBContext (System.Data.Entity)
// with EF Core. Configured with the InMemory provider for the demo; swap
// UseInMemoryDatabase for UseSqlServer to target a real Azure SQL database.
public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<CatalogBrand> CatalogBrands => Set<CatalogBrand>();
    public DbSet<CatalogType> CatalogTypes => Set<CatalogType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Price maps to decimal(18,2) when targeting a relational provider
        // (configured by the SQL Server provider). PictureUri is a computed
        // value, not persisted.
        modelBuilder.Entity<CatalogItem>(b => b.Ignore(ci => ci.PictureUri));
    }

    public void EnsureSeeded()
    {
        if (CatalogBrands.Any()) return;
        CatalogBrands.AddRange(PreconfiguredData.GetPreconfiguredCatalogBrands());
        CatalogTypes.AddRange(PreconfiguredData.GetPreconfiguredCatalogTypes());
        CatalogItems.AddRange(PreconfiguredData.GetPreconfiguredCatalogItems());
        SaveChanges();
    }
}
