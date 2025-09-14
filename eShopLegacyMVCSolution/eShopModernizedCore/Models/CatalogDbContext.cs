using Microsoft.EntityFrameworkCore;

namespace eShopModernizedCore.Models;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<CatalogItem> CatalogItems { get; set; } = null!;
    public DbSet<CatalogBrand> CatalogBrands { get; set; } = null!;
    public DbSet<CatalogType> CatalogTypes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CatalogTypeConfiguration());
        modelBuilder.ApplyConfiguration(new CatalogBrandConfiguration());
        modelBuilder.ApplyConfiguration(new CatalogItemConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
