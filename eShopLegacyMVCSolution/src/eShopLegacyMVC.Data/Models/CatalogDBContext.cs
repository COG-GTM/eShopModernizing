using System.Threading;
using System.Threading.Tasks;
using eShopLegacyMVC.Models.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eShopLegacyMVC.Models
{
    public class CatalogDBContext : DbContext
    {
        public const string CatalogItemHiLoSequenceName = "catalog_hilo";
        public const string CatalogBrandHiLoSequenceName = "catalog_brand_hilo";
        public const string CatalogTypeHiLoSequenceName = "catalog_type_hilo";

        private const int HiLoSequenceIncrement = 10;

        public CatalogDBContext(DbContextOptions<CatalogDBContext> options) : base(options)
        {
        }

        public DbSet<CatalogItem> CatalogItems { get; set; }

        public DbSet<CatalogBrand> CatalogBrands { get; set; }

        public DbSet<CatalogType> CatalogTypes { get; set; }

        /// <summary>
        /// Equivalent of EF6's <c>Configuration.ValidateOnSaveEnabled</c>: when true (the default), Added and Modified
        /// entities are validated against their data annotations and model facets before they are saved.
        /// </summary>
        public bool ValidateOnSaveEnabled { get; set; } = true;

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ValidateBeforeSave();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ValidateBeforeSave();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            ConfigureHiLoSequences(builder);
            ConfigureCatalogType(builder.Entity<CatalogType>());
            ConfigureCatalogBrand(builder.Entity<CatalogBrand>());
            ConfigureCatalogItem(builder.Entity<CatalogItem>());

            base.OnModelCreating(builder);
        }

        private void ValidateBeforeSave()
        {
            if (!ValidateOnSaveEnabled)
            {
                return;
            }

            if (ChangeTracker.AutoDetectChangesEnabled)
            {
                ChangeTracker.DetectChanges();
            }

            EntityValidator.ThrowIfInvalid(ChangeTracker);
        }

        // Replaces the legacy Models/Infrastructure/dbo.*_hilo.Sequence.sql scripts, so the sequences are created
        // together with the tables by EnsureCreated() (or by migrations) instead of by raw scripts during seeding.
        static void ConfigureHiLoSequences(ModelBuilder builder)
        {
            builder.HasSequence<long>(CatalogItemHiLoSequenceName).StartsAt(1).IncrementsBy(HiLoSequenceIncrement);
            builder.HasSequence<long>(CatalogBrandHiLoSequenceName).StartsAt(1).IncrementsBy(HiLoSequenceIncrement);
            builder.HasSequence<long>(CatalogTypeHiLoSequenceName).StartsAt(1).IncrementsBy(HiLoSequenceIncrement);
        }

        // Constraint and index names below reproduce the EF6 Code First naming conventions so the EF Core model
        // lines up with databases created by the legacy application.
        static void ConfigureCatalogType(EntityTypeBuilder<CatalogType> builder)
        {
            builder.ToTable("CatalogType");

            builder.HasKey(ci => ci.Id)
                .HasName("PK_dbo.CatalogType");

            builder.Property(ci => ci.Id)
                .UseIdentityColumn()
                .IsRequired();

            builder.Property(cb => cb.Type)
                .IsRequired()
                .HasMaxLength(100);
        }

        static void ConfigureCatalogBrand(EntityTypeBuilder<CatalogBrand> builder)
        {
            builder.ToTable("CatalogBrand");

            builder.HasKey(ci => ci.Id)
                .HasName("PK_dbo.CatalogBrand");

            builder.Property(ci => ci.Id)
                .UseIdentityColumn()
                .IsRequired();

            builder.Property(cb => cb.Brand)
                .IsRequired()
                .HasMaxLength(100);
        }

        static void ConfigureCatalogItem(EntityTypeBuilder<CatalogItem> builder)
        {
            builder.ToTable("Catalog");

            builder.HasKey(ci => ci.Id)
                .HasName("PK_dbo.Catalog");

            builder.Property(ci => ci.Id)
                .ValueGeneratedNever()
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

            builder.HasIndex(ci => ci.CatalogBrandId)
                .HasDatabaseName("IX_CatalogBrandId");

            builder.HasIndex(ci => ci.CatalogTypeId)
                .HasDatabaseName("IX_CatalogTypeId");
        }
    }
}
