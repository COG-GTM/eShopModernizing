using EShop.Catalog.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EShop.Catalog.Data;

/// <summary>Legacy EF6 <c>CatalogDBContext</c> on EF Core 8: same tables, columns, keys and relationships.</summary>
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
        ConfigureCatalogType(builder.Entity<CatalogType>());
        ConfigureCatalogBrand(builder.Entity<CatalogBrand>());
        ConfigureCatalogItem(builder.Entity<CatalogItem>());
    }

    private static void ConfigureCatalogType(EntityTypeBuilder<CatalogType> builder)
    {
        builder.ToTable("CatalogType");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Type).IsRequired().HasMaxLength(100);
    }

    private static void ConfigureCatalogBrand(EntityTypeBuilder<CatalogBrand> builder)
    {
        builder.ToTable("CatalogBrand");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Brand).IsRequired().HasMaxLength(100);
    }

    private static void ConfigureCatalogItem(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("Catalog");
        builder.HasKey(ci => ci.Id);
        builder.Property(ci => ci.Id).ValueGeneratedNever();
        builder.Property(ci => ci.Name).IsRequired().HasMaxLength(50);
        builder.Property(ci => ci.Price).IsRequired().HasPrecision(18, 2);
        builder.Property(ci => ci.PictureFileName).IsRequired();
        builder.Ignore(ci => ci.PictureUri);

        builder.HasOne(ci => ci.CatalogBrand)
            .WithMany()
            .HasForeignKey(ci => ci.CatalogBrandId)
            .IsRequired();

        builder.HasOne(ci => ci.CatalogType)
            .WithMany()
            .HasForeignKey(ci => ci.CatalogTypeId)
            .IsRequired();
    }
}
