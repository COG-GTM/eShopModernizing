using eShopWCFService.Models;
using eShopWCFService.Models.Infrastructure;

namespace eShopWCFService.Services;

public class CatalogServiceMock : ICatalogService
{
    private readonly List<CatalogItem> _catalogItems;
    private readonly List<CatalogBrand> _catalogBrands;
    private readonly List<CatalogType> _catalogTypes;
    private readonly List<CatalogItemsStock> _catalogItemsStock;
    private readonly List<DiscountItem> _discountItems;

    public CatalogServiceMock()
    {
        _catalogItems = new List<CatalogItem>(PreconfiguredData.GetPreconfiguredCatalogItems());
        _catalogBrands = new List<CatalogBrand>(PreconfiguredData.GetPreconfiguredCatalogBrands());
        _catalogTypes = new List<CatalogType>(PreconfiguredData.GetPreconfiguredCatalogTypes());
        _catalogItemsStock = new List<CatalogItemsStock>(PreconfiguredData.GetPreconfiguredCatalogItemsStock());
        _discountItems = new List<DiscountItem>(PreconfiguredData.GetPreconfiguredDiscountItems());
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        return _catalogItems.FirstOrDefault(x => x.Id == id);
    }

    public IEnumerable<CatalogItem> GetCatalogItems(int brandIdFilter, int typeIdFilter)
    {
        bool brandFilterIsNull = brandIdFilter == 0;
        bool typeFilterIsNull = typeIdFilter == 0;

        return _catalogItems
            .Where(x =>
                (brandFilterIsNull || x.CatalogBrandId == brandIdFilter) &&
                (typeFilterIsNull || x.CatalogTypeId == typeIdFilter))
            .ToList();
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return _catalogTypes;
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return _catalogBrands;
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        var maxId = _catalogItems.Any() ? _catalogItems.Max(i => i.Id) : 0;
        catalogItem.Id = ++maxId;
        _catalogItems.Add(catalogItem);
    }

    public void UpdateCatalogItem(CatalogItem modifiedItem)
    {
        var originalItem = FindCatalogItem(modifiedItem.Id);
        if (originalItem != null)
        {
            _catalogItems[_catalogItems.IndexOf(originalItem)] = modifiedItem;
        }
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        var existing = FindCatalogItem(catalogItem.Id);
        if (existing != null)
        {
            _catalogItems.Remove(existing);
        }
    }

    public int GetAvailableStock(DateTime date, int catalogItemId)
    {
        var stock = _catalogItemsStock
            .FirstOrDefault(x => x.CatalogItemId == catalogItemId && x.Date.Date == date.Date);

        return stock?.AvailableStock ?? 0;
    }

    public void CreateAvailableStock(CatalogItemsStock cat)
    {
        var existing = _catalogItemsStock
            .FirstOrDefault(x => x.CatalogItemId == cat.CatalogItemId && x.Date.Date == cat.Date.Date);

        // Overwrite the existing stock item for that date if we already have one
        // for this item. Otherwise, make a new entry.
        if (existing != null)
        {
            existing.AvailableStock = cat.AvailableStock;
        }
        else
        {
            var maxId = _catalogItemsStock.Any() ? _catalogItemsStock.Max(i => i.StockId) : 0;
            cat.StockId = ++maxId;
            _catalogItemsStock.Add(cat);
        }
    }

    public DiscountItem? GetDiscount(DateTime day)
    {
        return _discountItems
            .FirstOrDefault(y => y.Start.Date <= day.Date && y.End.Date >= day.Date);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
