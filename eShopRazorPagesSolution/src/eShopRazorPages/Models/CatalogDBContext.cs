using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eShopRazorPages.Models;

/// <summary>
/// EF Core equivalent of the legacy EF6 <c>CatalogDBContext</c>. Table, column and sequence names match
/// the schema created by the WebForms app so both apps can share the same database.
/// </summary>
public class CatalogDBContext : DbContext
{
    public const string CatalogItemHiLoSequenceName = "catalog_hilo";

    public CatalogDBContext(DbContextOptions<CatalogDBContext> options)
        : base(options)
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
        builder.HasKey(ct => ct.Id);
        builder.Property(ct => ct.Type)
            .IsRequired()
            .HasMaxLength(100);
    }

    private static void ConfigureCatalogBrand(EntityTypeBuilder<CatalogBrand> builder)
    {
        builder.ToTable("CatalogBrand");
        builder.HasKey(cb => cb.Id);
        builder.Property(cb => cb.Brand)
            .IsRequired()
            .HasMaxLength(100);
    }

    private static void ConfigureCatalogItem(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("Catalog");
        builder.HasKey(ci => ci.Id);
        builder.Property(ci => ci.Id)
            .UseHiLo(CatalogItemHiLoSequenceName);
        builder.Property(ci => ci.Name)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(ci => ci.Price)
            .IsRequired()
            .HasColumnType("decimal(18,2)");
        builder.Property(ci => ci.PictureFileName)
            .IsRequired();
        builder.Ignore(ci => ci.PictureUri);
        builder.HasOne(ci => ci.CatalogBrand)
            .WithMany()
            .HasForeignKey(ci => ci.CatalogBrandId);
        builder.HasOne(ci => ci.CatalogType)
            .WithMany()
            .HasForeignKey(ci => ci.CatalogTypeId);
    }
}
