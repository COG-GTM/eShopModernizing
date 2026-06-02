using eShopWCFService.Data;
using eShopWCFService.Models;
using Microsoft.EntityFrameworkCore;

namespace eShopWCFService.Services;

public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext _context;

    public CatalogService(CatalogDbContext context)
    {
        _context = context;
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        var item = _context.CatalogItems.FirstOrDefault(x => x.Id == id);
        if (item != null)
        {
            item.CatalogBrand = _context.CatalogBrands.FirstOrDefault(x => x.Id == item.CatalogBrandId);
            item.CatalogType = _context.CatalogTypes.FirstOrDefault(x => x.Id == item.CatalogTypeId);
        }

        return item;
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return _context.CatalogTypes.ToList();
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return _context.CatalogBrands.ToList();
    }

    public IEnumerable<CatalogItem> GetCatalogItems(int brandIdFilter, int typeIdFilter)
    {
        bool brandFilterIsNull = brandIdFilter == 0;
        bool typeFilterIsNull = typeIdFilter == 0;

        return _context.CatalogItems
            .Where(x =>
                (brandFilterIsNull || x.CatalogBrandId == brandIdFilter) &&
                (typeFilterIsNull || x.CatalogTypeId == typeIdFilter))
            .ToList();
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        var maxId = _context.CatalogItems.Any() ? _context.CatalogItems.Max(i => i.Id) : 0;
        catalogItem.Id = ++maxId;
        _context.CatalogItems.Add(catalogItem);
        _context.SaveChanges();
    }

    public void UpdateCatalogItem(CatalogItem catalogItem)
    {
        _context.Entry(catalogItem).State = EntityState.Modified;
        _context.SaveChanges();
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        var existing = _context.CatalogItems.FirstOrDefault(x => x.Id == catalogItem.Id);
        if (existing != null)
        {
            _context.CatalogItems.Remove(existing);
            _context.SaveChanges();
        }
    }

    public int GetAvailableStock(DateTime date, int catalogItemId)
    {
        var stock = _context.CatalogItemsStocks
            .Where(x => x.CatalogItemId == catalogItemId)
            .AsEnumerable()
            .FirstOrDefault(y => y.Date.Date == date.Date);

        return stock?.AvailableStock ?? 0;
    }

    public void CreateAvailableStock(CatalogItemsStock catalogItemsStock)
    {
        var existing = _context.CatalogItemsStocks
            .Where(x => x.CatalogItemId == catalogItemsStock.CatalogItemId)
            .AsEnumerable()
            .FirstOrDefault(y => y.Date.Date == catalogItemsStock.Date.Date);

        // Overwrite the existing stock item for that date if we already have one
        // for this item. Otherwise, make a new entry.
        if (existing != null)
        {
            existing.AvailableStock = catalogItemsStock.AvailableStock;
            _context.Entry(existing).State = EntityState.Modified;
            _context.SaveChanges();
        }
        else
        {
            var maxId = _context.CatalogItemsStocks.Any() ? _context.CatalogItemsStocks.Max(i => i.StockId) : 0;
            catalogItemsStock.StockId = ++maxId;
            _context.CatalogItemsStocks.Add(catalogItemsStock);
            _context.SaveChanges();
        }
    }

    public DiscountItem? GetDiscount(DateTime day)
    {
        return _context.DiscountItems
            .AsEnumerable()
            .FirstOrDefault(y => y.Start.Date <= day.Date && y.End.Date >= day.Date);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
