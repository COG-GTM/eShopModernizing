using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace eShop.Catalog.Data.Persistence;

/// <summary>Used only by <c>dotnet ef migrations</c>; no database connection is opened at design time.</summary>
public sealed class CatalogDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer("Server=localhost;Database=CatalogDb;Trusted_Connection=False;TrustServerCertificate=True")
            .Options;
        return new CatalogDbContext(options);
    }
}
