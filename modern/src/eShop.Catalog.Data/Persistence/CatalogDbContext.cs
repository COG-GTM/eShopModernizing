using eShop.Catalog.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eShop.Catalog.Data.Persistence;

/// <summary>
/// EF Core 8 port of the legacy EF6 CatalogDBContext. Table, column, key, index and FK names match the schema EF6
/// Code First generated for the legacy apps (dbo.Catalog, dbo.CatalogBrand, dbo.CatalogType).
/// </summary>
public class CatalogDbContext : DbContext
{
    public const string CatalogItemHiLoSequenceName = "catalog_hilo";
    public const string CatalogBrandHiLoSequenceName = "catalog_brand_hilo";
    public const string CatalogTypeHiLoSequenceName = "catalog_type_hilo";
    public const int HiLoBlockSize = 10;

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options)
    {
    }

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    public DbSet<CatalogBrand> CatalogBrands => Set<CatalogBrand>();

    public DbSet<CatalogType> CatalogTypes => Set<CatalogType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");

        // Legacy Models/Infrastructure/dbo.*.Sequence.sql: bigint, START WITH 1, INCREMENT BY 10.
        modelBuilder.HasSequence<long>(CatalogItemHiLoSequenceName).StartsAt(1).IncrementsBy(HiLoBlockSize);
        modelBuilder.HasSequence<long>(CatalogBrandHiLoSequenceName).StartsAt(1).IncrementsBy(HiLoBlockSize);
        modelBuilder.HasSequence<long>(CatalogTypeHiLoSequenceName).StartsAt(1).IncrementsBy(HiLoBlockSize);

        ConfigureCatalogType(modelBuilder.Entity<CatalogType>());
        ConfigureCatalogBrand(modelBuilder.Entity<CatalogBrand>());
        ConfigureCatalogItem(modelBuilder.Entity<CatalogItem>());
    }

    private static void ConfigureCatalogType(EntityTypeBuilder<CatalogType> builder)
    {
        builder.ToTable("CatalogType");

        builder.HasKey(ct => ct.Id).HasName("PK_dbo.CatalogType");

        // EF6 convention: int key "Id" without DatabaseGeneratedOption => IDENTITY(1,1).
        builder.Property(ct => ct.Id).UseIdentityColumn();

        builder.Property(ct => ct.Type)
            .IsRequired()
            .HasMaxLength(100);
    }

    private static void ConfigureCatalogBrand(EntityTypeBuilder<CatalogBrand> builder)
    {
        builder.ToTable("CatalogBrand");

        builder.HasKey(cb => cb.Id).HasName("PK_dbo.CatalogBrand");

        builder.Property(cb => cb.Id).UseIdentityColumn();

        builder.Property(cb => cb.Brand)
            .IsRequired()
            .HasMaxLength(100);
    }

    private static void ConfigureCatalogItem(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("Catalog");

        builder.HasKey(ci => ci.Id).HasName("PK_dbo.Catalog");

        // Legacy: DatabaseGeneratedOption.None + CatalogItemHiLoGenerator (singleton, block of 10 from catalog_hilo).
        builder.Property(ci => ci.Id)
            .UseHiLo(CatalogItemHiLoSequenceName);

        builder.Property(ci => ci.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ci => ci.Description);

        builder.Property(ci => ci.Price)
            .IsRequired()
            .HasPrecision(18, 2);

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
