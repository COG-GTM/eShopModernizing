using Microsoft.EntityFrameworkCore;

namespace eShopLegacyMVC.Models
{
    public class CatalogDBContext : DbContext
    {
        public CatalogDBContext(DbContextOptions<CatalogDBContext> options) : base(options)
        {
        }

        public DbSet<CatalogItem> CatalogItems { get; set; } = null!;

        public DbSet<CatalogBrand> CatalogBrands { get; set; } = null!;

        public DbSet<CatalogType> CatalogTypes { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureCatalogType(modelBuilder);
            ConfigureCatalogBrand(modelBuilder);
            ConfigureCatalogItem(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        void ConfigureCatalogType(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CatalogType>(builder =>
            {
                builder.ToTable("CatalogType");
                builder.HasKey(ci => ci.Id);
                builder.Property(ci => ci.Id).IsRequired();
                builder.Property(cb => cb.Type).IsRequired().HasMaxLength(100);
            });
        }

        void ConfigureCatalogBrand(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CatalogBrand>(builder =>
            {
                builder.ToTable("CatalogBrand");
                builder.HasKey(ci => ci.Id);
                builder.Property(ci => ci.Id).IsRequired();
                builder.Property(cb => cb.Brand).IsRequired().HasMaxLength(100);
            });
        }

        void ConfigureCatalogItem(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CatalogItem>(builder =>
            {
                builder.ToTable("Catalog");
                builder.HasKey(ci => ci.Id);
                builder.Property(ci => ci.Id).ValueGeneratedNever().IsRequired();
                builder.Property(ci => ci.Name).IsRequired().HasMaxLength(50);
                builder.Property(ci => ci.Price).IsRequired();
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
            });
        }
    }
}
