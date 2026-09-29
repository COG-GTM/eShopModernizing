using eShopRazorPages.Models;
using eShopRazorPages.Models.Infrastructure;
using eShopRazorPages.ViewModel;

namespace eShopRazorPages.Services;

/// <summary>
/// In-memory catalog used when <c>UseMockData</c> is true. Registered as a singleton so data survives
/// between requests, mirroring the legacy Autofac registration.
/// </summary>
public class CatalogServiceMock : ICatalogService
{
    private readonly object _sync = new();
    private readonly List<CatalogItem> _catalogItems = PreconfiguredData.GetPreconfiguredCatalogItems();
    private readonly List<CatalogBrand> _catalogBrands = PreconfiguredData.GetPreconfiguredCatalogBrands();
    private readonly List<CatalogType> _catalogTypes = PreconfiguredData.GetPreconfiguredCatalogTypes();

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
    {
        lock (_sync)
        {
            var items = ComposeCatalogItems(_catalogItems).ToList();

            var itemsOnPage = items
                .OrderBy(c => c.Id)
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToList();

            return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, items.Count, itemsOnPage);
        }
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        lock (_sync)
        {
            var item = _catalogItems.FirstOrDefault(x => x.Id == id);
            return item is null ? null : Compose(item);
        }
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        lock (_sync)
        {
            return _catalogTypes.ToList();
        }
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        lock (_sync)
        {
            return _catalogBrands.ToList();
        }
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        lock (_sync)
        {
            var maxId = _catalogItems.Count == 0 ? 0 : _catalogItems.Max(i => i.Id);
            catalogItem.Id = maxId + 1;
            _catalogItems.Add(catalogItem);
        }
    }

    public void UpdateCatalogItem(CatalogItem modifiedItem)
    {
        lock (_sync)
        {
            var index = _catalogItems.FindIndex(i => i.Id == modifiedItem.Id);
            if (index >= 0)
            {
                _catalogItems[index] = modifiedItem;
            }
        }
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        lock (_sync)
        {
            _catalogItems.RemoveAll(i => i.Id == catalogItem.Id);
        }
    }

    private IEnumerable<CatalogItem> ComposeCatalogItems(IEnumerable<CatalogItem> items)
    {
        return items.Select(Compose);
    }

    private CatalogItem Compose(CatalogItem item)
    {
        item.CatalogBrand = _catalogBrands.FirstOrDefault(b => b.Id == item.CatalogBrandId);
        item.CatalogType = _catalogTypes.FirstOrDefault(t => t.Id == item.CatalogTypeId);
        return item;
    }
}
