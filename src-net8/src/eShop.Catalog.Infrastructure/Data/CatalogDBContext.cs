using eShop.Catalog.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eShop.Catalog.Infrastructure.Data
{
    // Port of eShopLegacyMVC.Models.CatalogDBContext (EF6) with the same table/column mapping.
    public class CatalogDBContext : DbContext
    {
        public const string CatalogItemHiLoSequenceName = "catalog_hilo";
        public const string CatalogBrandHiLoSequenceName = "catalog_brand_hilo";
        public const string CatalogTypeHiLoSequenceName = "catalog_type_hilo";

        public CatalogDBContext(DbContextOptions<CatalogDBContext> options) : base(options)
        {
        }

        public DbSet<CatalogItem> CatalogItems { get; set; }

        public DbSet<CatalogBrand> CatalogBrands { get; set; }

        public DbSet<CatalogType> CatalogTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Legacy dbo.*_hilo.Sequence.sql: bigint, START WITH 1, INCREMENT BY 10 (min/max/cache are the bigint defaults).
            builder.HasSequence<long>(CatalogItemHiLoSequenceName).StartsAt(1).IncrementsBy(10);
            builder.HasSequence<long>(CatalogBrandHiLoSequenceName).StartsAt(1).IncrementsBy(10);
            builder.HasSequence<long>(CatalogTypeHiLoSequenceName).StartsAt(1).IncrementsBy(10);

            ConfigureCatalogType(builder.Entity<CatalogType>());
            ConfigureCatalogBrand(builder.Entity<CatalogBrand>());
            ConfigureCatalogItem(builder.Entity<CatalogItem>());

            base.OnModelCreating(builder);
        }

        static void ConfigureCatalogType(EntityTypeBuilder<CatalogType> builder)
        {
            builder.ToTable("CatalogType");

            builder.HasKey(ci => ci.Id);

            // EF6 convention made int "Id" keys IDENTITY columns; EF Core does the same (SqlServer identity).
            builder.Property(ci => ci.Id)
                .IsRequired();

            builder.Property(cb => cb.Type)
                .IsRequired()
                .HasMaxLength(100);
        }

        static void ConfigureCatalogBrand(EntityTypeBuilder<CatalogBrand> builder)
        {
            builder.ToTable("CatalogBrand");

            builder.HasKey(ci => ci.Id);

            builder.Property(ci => ci.Id)
                .IsRequired();

            builder.Property(cb => cb.Brand)
                .IsRequired()
                .HasMaxLength(100);
        }

        static void ConfigureCatalogItem(EntityTypeBuilder<CatalogItem> builder)
        {
            builder.ToTable("Catalog");

            builder.HasKey(ci => ci.Id);

            // Ids come from CatalogItemHiLoGenerator (catalog_hilo), never from the database.
            builder.Property(ci => ci.Id)
                .ValueGeneratedNever()
                .IsRequired();

            builder.Property(ci => ci.Name)
                .IsRequired()
                .HasMaxLength(50);

            // EF6 mapped System.Decimal to decimal(18,2) by default.
            builder.Property(ci => ci.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(ci => ci.PictureFileName)
                .IsRequired();

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
}
