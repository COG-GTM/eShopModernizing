using eShopWCFService.Models;
using eShopWCFService.Models.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace eShopWCFService.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options)
    {
    }

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<CatalogBrand> CatalogBrands => Set<CatalogBrand>();
    public DbSet<CatalogType> CatalogTypes => Set<CatalogType>();
    public DbSet<CatalogItemsStock> CatalogItemsStocks => Set<CatalogItemsStock>();
    public DbSet<DiscountItem> DiscountItems => Set<DiscountItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogBrand>(builder =>
        {
            builder.ToTable("CatalogBrand");
            builder.HasKey(b => b.Id);
            builder.Property(b => b.Brand).IsUnicode(false).HasMaxLength(50);
        });

        modelBuilder.Entity<CatalogType>(builder =>
        {
            builder.ToTable("CatalogType");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Type).IsUnicode(false).HasMaxLength(50);
        });

        modelBuilder.Entity<CatalogItem>(builder =>
        {
            builder.ToTable("Catalog");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Price).HasColumnType("money");

            builder.HasOne(i => i.CatalogBrand)
                .WithMany()
                .HasForeignKey(i => i.CatalogBrandId);

            builder.HasOne(i => i.CatalogType)
                .WithMany()
                .HasForeignKey(i => i.CatalogTypeId);
        });

        modelBuilder.Entity<CatalogItemsStock>(builder =>
        {
            builder.ToTable("CatalogItemsStock");
            builder.HasKey(s => s.StockId);
            builder.Property(s => s.Date).HasColumnType("date");
        });

        modelBuilder.Entity<DiscountItem>(builder =>
        {
            builder.ToTable("DiscountItem");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Start).HasColumnType("date");
            builder.Property(d => d.End).HasColumnType("date");
        });

        base.OnModelCreating(modelBuilder);
    }

    public static void Seed(CatalogDbContext context)
    {
        context.Database.EnsureCreated();

        if (!context.CatalogBrands.Any())
        {
            context.CatalogBrands.AddRange(PreconfiguredData.GetPreconfiguredCatalogBrands());
            context.SaveChanges();
        }

        if (!context.CatalogTypes.Any())
        {
            context.CatalogTypes.AddRange(PreconfiguredData.GetPreconfiguredCatalogTypes());
            context.SaveChanges();
        }

        if (!context.CatalogItems.Any())
        {
            context.CatalogItems.AddRange(PreconfiguredData.GetPreconfiguredCatalogItems());
            context.SaveChanges();
        }

        if (!context.CatalogItemsStocks.Any())
        {
            context.CatalogItemsStocks.AddRange(PreconfiguredData.GetPreconfiguredCatalogItemsStock());
            context.SaveChanges();
        }

        if (!context.DiscountItems.Any())
        {
            context.DiscountItems.AddRange(PreconfiguredData.GetPreconfiguredDiscountItems());
            context.SaveChanges();
        }
    }
}
