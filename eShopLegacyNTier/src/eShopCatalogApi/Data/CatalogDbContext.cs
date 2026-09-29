using eShopCatalog.Contracts;
using Microsoft.EntityFrameworkCore;

namespace eShopCatalogApi.Data;

/// <summary>
/// EF Core mapping of the schema created by the legacy EF6 <c>EntityModel</c>,
/// so the API can run against an existing eShopDatabase unchanged.
/// </summary>
public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<CatalogBrand> CatalogBrands => Set<CatalogBrand>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<CatalogItemsStock> CatalogItemsStocks => Set<CatalogItemsStock>();
    public DbSet<CatalogType> CatalogTypes => Set<CatalogType>();
    public DbSet<DiscountItem> DiscountItems => Set<DiscountItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogBrand>(b =>
        {
            b.ToTable("CatalogBrands");
            b.Property(e => e.Id).ValueGeneratedNever();
            b.Property(e => e.Brand).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<CatalogType>(b =>
        {
            b.ToTable("CatalogTypes");
            b.Property(e => e.Id).ValueGeneratedNever();
            b.Property(e => e.Type).HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<CatalogItem>(b =>
        {
            b.ToTable("CatalogItems");
            b.Property(e => e.Id).ValueGeneratedNever();
            b.Property(e => e.Price).HasColumnType("money").HasPrecision(19, 4);
            b.HasOne(e => e.CatalogBrand).WithMany().HasForeignKey(e => e.CatalogBrandId);
            b.HasOne(e => e.CatalogType).WithMany().HasForeignKey(e => e.CatalogTypeId);
        });

        modelBuilder.Entity<CatalogItemsStock>(b =>
        {
            b.ToTable("CatalogItemsStock");
            b.HasKey(e => e.StockId);
            b.Property(e => e.StockId).ValueGeneratedNever();
            b.Property(e => e.Date).HasColumnType("date");
        });

        modelBuilder.Entity<DiscountItem>(b =>
        {
            b.ToTable("DiscountItems");
            b.Property(e => e.Start).HasColumnType("date");
            b.Property(e => e.End).HasColumnType("date");
        });
    }
}
