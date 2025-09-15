using Microsoft.EntityFrameworkCore;
using eShopCoreAPI.Models;

namespace eShopCoreAPI.Data;

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
        ConfigureCatalogType(modelBuilder.Entity<CatalogType>());
        ConfigureCatalogBrand(modelBuilder.Entity<CatalogBrand>());
        ConfigureCatalogItem(modelBuilder.Entity<CatalogItem>());

        base.OnModelCreating(modelBuilder);
    }

    private void ConfigureCatalogType(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<CatalogType> builder)
    {
        builder.ToTable("CatalogType");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Id)
            .IsRequired();

        builder.Property(cb => cb.Type)
            .IsRequired()
            .HasMaxLength(100);
    }

    private void ConfigureCatalogBrand(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<CatalogBrand> builder)
    {
        builder.ToTable("CatalogBrand");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Id)
            .IsRequired();

        builder.Property(cb => cb.Brand)
            .IsRequired()
            .HasMaxLength(100);
    }

    private void ConfigureCatalogItem(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("Catalog");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Id)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(ci => ci.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ci => ci.Price)
            .IsRequired();

        builder.Property(ci => ci.PictureFileName);

        builder.Ignore(ci => ci.PictureUri);
        builder.Ignore(ci => ci.TempImageName);

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
