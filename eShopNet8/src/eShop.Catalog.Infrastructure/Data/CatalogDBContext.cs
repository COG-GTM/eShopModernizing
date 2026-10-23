using eShop.Catalog.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eShop.Catalog.Infrastructure.Data;

// Port of eShopLegacyMVC.Models.CatalogDBContext (EF6). Table, column, key, FK and index names follow the EF6
// conventions the legacy database was created with, so both apps produce the same schema.
public class CatalogDBContext : DbContext
{
    public const string CatalogItemHiLoSequenceName = "catalog_hilo";
    public const string CatalogBrandHiLoSequenceName = "catalog_brand_hilo";
    public const string CatalogTypeHiLoSequenceName = "catalog_type_hilo";
    public const int HiLoIncrement = 10;

    public CatalogDBContext(DbContextOptions<CatalogDBContext> options) : base(options)
    {
    }

    public DbSet<CatalogItem> CatalogItems { get; set; } = null!;

    public DbSet<CatalogBrand> CatalogBrands { get; set; } = null!;

    public DbSet<CatalogType> CatalogTypes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("dbo");

        // Legacy dbo.*.Sequence.sql: AS bigint START WITH 1 INCREMENT BY 10.
        foreach (var sequence in new[] { CatalogItemHiLoSequenceName, CatalogBrandHiLoSequenceName, CatalogTypeHiLoSequenceName })
        {
            builder.HasSequence<long>(sequence).StartsAt(1).IncrementsBy(HiLoIncrement);
        }

        ConfigureCatalogType(builder.Entity<CatalogType>());
        ConfigureCatalogBrand(builder.Entity<CatalogBrand>());
        ConfigureCatalogItem(builder.Entity<CatalogItem>());

        base.OnModelCreating(builder);
    }

    private static void ConfigureCatalogType(EntityTypeBuilder<CatalogType> builder)
    {
        builder.ToTable("CatalogType");

        builder.HasKey(ct => ct.Id).HasName("PK_dbo.CatalogType");

        builder.Property(ct => ct.Id)
            .UseHiLo(CatalogTypeHiLoSequenceName)
            .IsRequired();

        builder.Property(ct => ct.Type)
            .IsRequired()
            .HasMaxLength(100);
    }

    private static void ConfigureCatalogBrand(EntityTypeBuilder<CatalogBrand> builder)
    {
        builder.ToTable("CatalogBrand");

        builder.HasKey(cb => cb.Id).HasName("PK_dbo.CatalogBrand");

        builder.Property(cb => cb.Id)
            .UseHiLo(CatalogBrandHiLoSequenceName)
            .IsRequired();

        builder.Property(cb => cb.Brand)
            .IsRequired()
            .HasMaxLength(100);
    }

    private static void ConfigureCatalogItem(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("Catalog");

        builder.HasKey(ci => ci.Id).HasName("PK_dbo.Catalog");

        builder.Property(ci => ci.Id)
            .UseHiLo(CatalogItemHiLoSequenceName)
            .IsRequired();

        builder.Property(ci => ci.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ci => ci.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(ci => ci.PictureFileName)
            .IsRequired();

        builder.Ignore(ci => ci.PictureUri);

        builder.HasOne(ci => ci.CatalogBrand)
            .WithMany()
            .HasForeignKey(ci => ci.CatalogBrandId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_dbo.Catalog_dbo.CatalogBrand_CatalogBrandId");

        builder.HasOne(ci => ci.CatalogType)
            .WithMany()
            .HasForeignKey(ci => ci.CatalogTypeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_dbo.Catalog_dbo.CatalogType_CatalogTypeId");

        builder.HasIndex(ci => ci.CatalogBrandId).HasDatabaseName("IX_CatalogBrandId");
        builder.HasIndex(ci => ci.CatalogTypeId).HasDatabaseName("IX_CatalogTypeId");
    }
}
