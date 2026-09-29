using eShopCatalog.Contracts;
using eShopCatalogApi.Data;
using Microsoft.EntityFrameworkCore;

namespace eShopCatalogApi.Services;

public class CatalogService(CatalogDbContext db) : ICatalogService
{
    public Task<CatalogItem?> FindCatalogItemAsync(int id, CancellationToken cancellationToken = default) =>
        db.CatalogItems
            .AsNoTracking()
            .Include(i => i.CatalogBrand)
            .Include(i => i.CatalogType)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<List<CatalogBrand>> GetCatalogBrandsAsync(CancellationToken cancellationToken = default) =>
        db.CatalogBrands.AsNoTracking().ToListAsync(cancellationToken);

    public Task<List<CatalogType>> GetCatalogTypesAsync(CancellationToken cancellationToken = default) =>
        db.CatalogTypes.AsNoTracking().ToListAsync(cancellationToken);

    public Task<List<CatalogItem>> GetCatalogItemsAsync(int brandIdFilter, int typeIdFilter, CancellationToken cancellationToken = default)
    {
        IQueryable<CatalogItem> query = db.CatalogItems.AsNoTracking();

        if (brandIdFilter != 0)
        {
            query = query.Where(i => i.CatalogBrandId == brandIdFilter);
        }

        if (typeIdFilter != 0)
        {
            query = query.Where(i => i.CatalogTypeId == typeIdFilter);
        }

        return query.OrderBy(i => i.Id).ToListAsync(cancellationToken);
    }

    public async Task<int> GetAvailableStockAsync(DateTime date, int catalogItemId, CancellationToken cancellationToken = default)
    {
        var day = date.Date;
        var stock = await db.CatalogItemsStocks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CatalogItemId == catalogItemId && s.Date == day, cancellationToken);

        return stock?.AvailableStock ?? 0;
    }

    public async Task CreateAvailableStockAsync(CatalogItemsStock catalogItemsStock, CancellationToken cancellationToken = default)
    {
        var day = catalogItemsStock.Date.Date;
        var existing = await db.CatalogItemsStocks
            .FirstOrDefaultAsync(s => s.CatalogItemId == catalogItemsStock.CatalogItemId && s.Date == day, cancellationToken);

        if (existing != null)
        {
            existing.AvailableStock = catalogItemsStock.AvailableStock;
        }
        else
        {
            var maxId = await db.CatalogItemsStocks.MaxAsync(s => (int?)s.StockId, cancellationToken) ?? 0;
            catalogItemsStock.StockId = maxId + 1;
            catalogItemsStock.Date = day;
            db.CatalogItemsStocks.Add(catalogItemsStock);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateCatalogItemAsync(CatalogItem catalogItem, CancellationToken cancellationToken = default)
    {
        var maxId = await db.CatalogItems.MaxAsync(i => (int?)i.Id, cancellationToken) ?? 0;
        catalogItem.Id = maxId + 1;
        catalogItem.CatalogBrand = null;
        catalogItem.CatalogType = null;
        db.CatalogItems.Add(catalogItem);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateCatalogItemAsync(CatalogItem catalogItem, CancellationToken cancellationToken = default)
    {
        if (!await db.CatalogItems.AnyAsync(i => i.Id == catalogItem.Id, cancellationToken))
        {
            return false;
        }

        catalogItem.CatalogBrand = null;
        catalogItem.CatalogType = null;
        db.Entry(catalogItem).State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveCatalogItemAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await db.CatalogItems.FindAsync([id], cancellationToken);
        if (item == null)
        {
            return false;
        }

        db.CatalogItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<DiscountItem?> GetDiscountAsync(DateTime day, CancellationToken cancellationToken = default)
    {
        var date = day.Date;
        return db.DiscountItems
            .AsNoTracking()
            .Where(d => d.Start <= date && d.End >= date)
            .OrderBy(d => d.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
