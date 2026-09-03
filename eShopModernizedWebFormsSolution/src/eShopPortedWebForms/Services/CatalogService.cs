using eShopPortedWebForms.Models;
using eShopPortedWebForms.ViewModel;
using Microsoft.EntityFrameworkCore;

namespace eShopPortedWebForms.Services;

public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext db;

    public CatalogService(CatalogDbContext db)
    {
        this.db = db;
    }

    public async Task<PaginatedItemsViewModel<CatalogItem>> GetCatalogItemsPaginatedAsync(int pageSize, int pageIndex)
    {
        var totalItems = await db.CatalogItems.LongCountAsync();

        var itemsOnPage = await db.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .OrderBy(c => c.Id)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage);
    }

    public Task<CatalogItem?> FindCatalogItemAsync(int id)
    {
        return db.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .FirstOrDefaultAsync(ci => ci.Id == id);
    }

    public async Task<IEnumerable<CatalogType>> GetCatalogTypesAsync()
    {
        return await db.CatalogTypes.ToListAsync();
    }

    public async Task<IEnumerable<CatalogBrand>> GetCatalogBrandsAsync()
    {
        return await db.CatalogBrands.ToListAsync();
    }

    public async Task CreateCatalogItemAsync(CatalogItem catalogItem)
    {
        db.CatalogItems.Add(catalogItem);
        await db.SaveChangesAsync();
    }

    public async Task UpdateCatalogItemAsync(CatalogItem catalogItem)
    {
        db.Entry(catalogItem).State = EntityState.Modified;
        await db.SaveChangesAsync();
    }

    public async Task RemoveCatalogItemAsync(CatalogItem catalogItem)
    {
        db.CatalogItems.Remove(catalogItem);
        await db.SaveChangesAsync();
    }
}
