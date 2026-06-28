using Vans.Catalog.Api.Models;
using Vans.Catalog.Api.Models.Infrastructure;
using Vans.Catalog.Api.ViewModel;

namespace Vans.Catalog.Api.Services;

/// <summary>
/// In-memory catalog service, ported from the legacy CatalogServiceMock.
/// </summary>
public class CatalogServiceMock : ICatalogService
{
    private readonly List<CatalogItem> _catalogItems;

    public CatalogServiceMock()
    {
        _catalogItems = new List<CatalogItem>(PreconfiguredData.GetPreconfiguredCatalogItems());
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize = 10, int pageIndex = 0)
    {
        var items = ComposeCatalogItems(_catalogItems);

        var itemsOnPage = items
            .OrderBy(c => c.Id)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToList();

        return new PaginatedItemsViewModel<CatalogItem>(
            pageIndex, pageSize, items.Count, itemsOnPage);
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        return _catalogItems.FirstOrDefault(x => x.Id == id);
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return PreconfiguredData.GetPreconfiguredCatalogTypes();
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return PreconfiguredData.GetPreconfiguredCatalogBrands();
    }

    public CatalogItem CreateCatalogItem(CatalogItem catalogItem)
    {
        var maxId = _catalogItems.Count > 0 ? _catalogItems.Max(i => i.Id) : 0;
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
        _catalogItems.Remove(catalogItem);
    }

    private static List<CatalogItem> ComposeCatalogItems(List<CatalogItem> items)
    {
        var catalogTypes = PreconfiguredData.GetPreconfiguredCatalogTypes().ToList();
        var catalogBrands = PreconfiguredData.GetPreconfiguredCatalogBrands().ToList();
        items.ForEach(i => i.CatalogBrand = catalogBrands.First(b => b.Id == i.CatalogBrandId));
        items.ForEach(i => i.CatalogType = catalogTypes.First(t => t.Id == i.CatalogTypeId));

        return items;
    }
}
