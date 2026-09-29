using Microsoft.EntityFrameworkCore;

namespace eShopCatalogApi.Data;

/// <summary>
/// Equivalent of the legacy <c>CreateDatabaseIfNotExists</c> initializer: creates the schema
/// if the database does not exist and seeds it when empty.
/// </summary>
public static class CatalogDbInitializer
{
    public static async Task InitializeAsync(CatalogDbContext context, CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);

        if (await context.CatalogTypes.AnyAsync(cancellationToken))
        {
            return;
        }

        context.CatalogTypes.AddRange(PreconfiguredData.GetPreconfiguredCatalogTypes());
        context.CatalogBrands.AddRange(PreconfiguredData.GetPreconfiguredCatalogBrands());
        context.CatalogItems.AddRange(PreconfiguredData.GetPreconfiguredCatalogItems());
        context.CatalogItemsStocks.AddRange(PreconfiguredData.GetPreconfiguredCatalogItemsStock());
        context.DiscountItems.AddRange(PreconfiguredData.GetPreconfiguredDiscountItems());
        await context.SaveChangesAsync(cancellationToken);
    }
}
