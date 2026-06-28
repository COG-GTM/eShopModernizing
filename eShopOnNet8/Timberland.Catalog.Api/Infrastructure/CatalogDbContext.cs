using Microsoft.EntityFrameworkCore;
using Timberland.Catalog.Api.Models;
using Timberland.Catalog.Api.Models.Infrastructure;

namespace Timberland.Catalog.Api.Infrastructure;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<CatalogBrand> CatalogBrands => Set<CatalogBrand>();
    public DbSet<CatalogType> CatalogTypes => Set<CatalogType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogItem>().Ignore(i => i.CatalogBrand).Ignore(i => i.CatalogType);
    }

    /// <summary>
    /// Seeds the EF Core InMemory database with the same preconfigured data
    /// used by the legacy CatalogDBInitializer.
    /// </summary>
    public void EnsureSeeded()
    {
        if (CatalogBrands.Any())
        {
            return;
        }

        CatalogBrands.AddRange(PreconfiguredData.GetPreconfiguredCatalogBrands());
        CatalogTypes.AddRange(PreconfiguredData.GetPreconfiguredCatalogTypes());
        CatalogItems.AddRange(PreconfiguredData.GetPreconfiguredCatalogItems());
        SaveChanges();
    }
}
