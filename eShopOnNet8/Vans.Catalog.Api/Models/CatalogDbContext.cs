using Microsoft.EntityFrameworkCore;
using Vans.Catalog.Api.Models.Infrastructure;

namespace Vans.Catalog.Api.Models;

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
        modelBuilder.Entity<CatalogItem>().Ignore(c => c.PictureUri);

        modelBuilder.Entity<CatalogItem>()
            .HasData(PreconfiguredData.GetPreconfiguredCatalogItems()
                .Select(i => new CatalogItem
                {
                    Id = i.Id,
                    Name = i.Name,
                    Description = i.Description,
                    Price = i.Price,
                    PictureFileName = i.PictureFileName,
                    CatalogTypeId = i.CatalogTypeId,
                    CatalogBrandId = i.CatalogBrandId,
                    AvailableStock = i.AvailableStock,
                    RestockThreshold = i.RestockThreshold,
                    MaxStockThreshold = i.MaxStockThreshold,
                    OnReorder = i.OnReorder
                }));

        modelBuilder.Entity<CatalogBrand>().HasData(PreconfiguredData.GetPreconfiguredCatalogBrands());
        modelBuilder.Entity<CatalogType>().HasData(PreconfiguredData.GetPreconfiguredCatalogTypes());
    }
}
