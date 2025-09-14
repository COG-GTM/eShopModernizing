using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eShopModernizedCore.Models;

public class CatalogBrandConfiguration : IEntityTypeConfiguration<CatalogBrand>
{
    public void Configure(EntityTypeBuilder<CatalogBrand> builder)
    {
        builder.ToTable("CatalogBrand");

        builder.HasKey(cb => cb.Id);

        builder.Property(cb => cb.Id)
            .IsRequired();

        builder.Property(cb => cb.Brand)
            .IsRequired()
            .HasMaxLength(100);
    }
}
