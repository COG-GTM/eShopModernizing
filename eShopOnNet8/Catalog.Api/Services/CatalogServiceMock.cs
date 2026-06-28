using Catalog.Api.Infrastructure;
using Catalog.Api.Models;
using Catalog.Api.ViewModel;

namespace Catalog.Api.Services;

// Ported from eShopLegacyMVCSolution/src/eShopLegacyMVC/Services/CatalogServiceMock.cs.
// In-memory implementation used for the demo / mock-data mode and unit tests.
public class CatalogServiceMock : ICatalogService
{
    private readonly List<CatalogItem> _catalogItems;
    private readonly List<CatalogBrand> _brands;
    private readonly List<CatalogType> _types;

    public CatalogServiceMock()
    {
        _catalogItems = PreconfiguredData.GetPreconfiguredCatalogItems();
        _brands = PreconfiguredData.GetPreconfiguredCatalogBrands();
        _types = PreconfiguredData.GetPreconfiguredCatalogTypes();
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize = 10, int pageIndex = 0)
    {
        var items = ComposeCatalogItems(_catalogItems);

        var itemsOnPage = items
            .OrderBy(c => c.Id)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToList();

        return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, items.Count, itemsOnPage);
    }

    public CatalogItem? FindCatalogItem(int id) =>
        _catalogItems.FirstOrDefault(x => x.Id == id);

    public IEnumerable<CatalogType> GetCatalogTypes() => _types;

    public IEnumerable<CatalogBrand> GetCatalogBrands() => _brands;

    public CatalogItem CreateCatalogItem(CatalogItem catalogItem)
    {
        var maxId = _catalogItems.Count == 0 ? 0 : _catalogItems.Max(i => i.Id);
        catalogItem.Id = ++maxId;
        _catalogItems.Add(catalogItem);
        return catalogItem;
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

    private List<CatalogItem> ComposeCatalogItems(List<CatalogItem> items)
    {
        items.ForEach(i => i.CatalogBrand = _brands.First(b => b.Id == i.CatalogBrandId));
        items.ForEach(i => i.CatalogType = _types.First(t => t.Id == i.CatalogTypeId));
        return items;
    }
}
