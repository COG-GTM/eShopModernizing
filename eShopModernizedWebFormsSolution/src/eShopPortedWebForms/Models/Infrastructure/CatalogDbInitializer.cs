using Microsoft.EntityFrameworkCore;

namespace eShopPortedWebForms.Models.Infrastructure;

public static class CatalogDbInitializer
{
    public static async Task SeedAsync(CatalogDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        if (!await context.CatalogBrands.AnyAsync())
        {
            context.CatalogBrands.AddRange(PreconfiguredData.GetPreconfiguredCatalogBrands());
        }

        if (!await context.CatalogTypes.AnyAsync())
        {
            context.CatalogTypes.AddRange(PreconfiguredData.GetPreconfiguredCatalogTypes());
        }

        if (!await context.CatalogItems.AnyAsync())
        {
            var items = PreconfiguredData.GetPreconfiguredCatalogItems();
            items.ForEach(i => i.Id = 0);
            context.CatalogItems.AddRange(items);
        }

        await context.SaveChangesAsync();
    }
}
