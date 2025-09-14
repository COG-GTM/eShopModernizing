using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eShopModernizedCore.Models;

public class CatalogTypeConfiguration : IEntityTypeConfiguration<CatalogType>
{
    public void Configure(EntityTypeBuilder<CatalogType> builder)
    {
        builder.ToTable("CatalogType");

        builder.HasKey(ct => ct.Id);

        builder.Property(ct => ct.Id)
            .IsRequired();

        builder.Property(ct => ct.Type)
            .IsRequired()
            .HasMaxLength(100);
    }
}
