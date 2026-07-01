using eShopCoreModernized.Models;
using Microsoft.EntityFrameworkCore;

namespace eShopModernized.Tests.Common;

/// <summary>
/// Builds <see cref="CatalogDBContext"/> instances backed by the EF Core
/// in-memory provider. Fast and hermetic – no external database required.
/// </summary>
public static class InMemoryContext
{
    public static CatalogDBContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<CatalogDBContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        return new CatalogDBContext(options);
    }

    public static CatalogDBContext CreateSeeded(string? databaseName = null)
    {
        var context = Create(databaseName);
        context.CatalogBrands.AddRange(TestData.Brands());
        context.CatalogTypes.AddRange(TestData.Types());
        context.CatalogItems.AddRange(TestData.Items());
        context.SaveChanges();
        return context;
    }
}
