using Microsoft.EntityFrameworkCore;

namespace eShopPortedWebForms.Models;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<CatalogBrand> CatalogBrands => Set<CatalogBrand>();
    public DbSet<CatalogType> CatalogTypes => Set<CatalogType>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<CatalogBrand>(entity =>
        {
            entity.ToTable("CatalogBrand");
            entity.Property(cb => cb.Brand).IsRequired().HasMaxLength(100);
        });

        builder.Entity<CatalogType>(entity =>
        {
            entity.ToTable("CatalogType");
            entity.Property(ct => ct.Type).IsRequired().HasMaxLength(100);
        });

        builder.Entity<CatalogItem>(entity =>
        {
            entity.ToTable("Catalog");
            entity.Property(ci => ci.Id).UseHiLo("catalog_hilo");
            entity.Property(ci => ci.Name).IsRequired().HasMaxLength(50);
            entity.Property(ci => ci.Price).IsRequired().HasColumnType("decimal(18,2)");
            entity.HasOne(ci => ci.CatalogBrand).WithMany().HasForeignKey(ci => ci.CatalogBrandId);
            entity.HasOne(ci => ci.CatalogType).WithMany().HasForeignKey(ci => ci.CatalogTypeId);
            entity.Ignore(ci => ci.PictureUri);
        });
    }
}
