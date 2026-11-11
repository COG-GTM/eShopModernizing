using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Seeding;
using eShop.Catalog.Data.ViewModel;

namespace eShop.Catalog.Data.Services;

/// <summary>
/// In-memory port of the legacy CatalogServiceMock (registered as a singleton, like legacy Autofac SingleInstance).
/// </summary>
public class CatalogServiceMock : ICatalogService
{
    private readonly List<CatalogItem> catalogItems;

    public CatalogServiceMock()
    {
        catalogItems = new List<CatalogItem>(PreconfiguredData.GetPreconfiguredCatalogItems());
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize = 10, int pageIndex = 0)
    {
        var items = ComposeCatalogItems(catalogItems);

        var itemsOnPage = items
            .OrderBy(c => c.Id)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToList();

        return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, items.Count, itemsOnPage);
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        return catalogItems.FirstOrDefault(x => x.Id == id);
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return PreconfiguredData.GetPreconfiguredCatalogTypes();
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return PreconfiguredData.GetPreconfiguredCatalogBrands();
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        var maxId = catalogItems.Max(i => i.Id);
        catalogItem.Id = ++maxId;
        catalogItems.Add(catalogItem);
    }

    public void UpdateCatalogItem(CatalogItem catalogItem)
    {
        var originalItem = FindCatalogItem(catalogItem.Id);
        if (originalItem != null)
        {
            catalogItems[catalogItems.IndexOf(originalItem)] = catalogItem;
        }
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        catalogItems.Remove(catalogItem);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static List<CatalogItem> ComposeCatalogItems(List<CatalogItem> items)
    {
        var catalogTypes = PreconfiguredData.GetPreconfiguredCatalogTypes().ToList();
        var catalogBrands = PreconfiguredData.GetPreconfiguredCatalogBrands().ToList();
        items.ForEach(i => i.CatalogBrand = catalogBrands.First(b => b.Id == i.CatalogBrandId));
        items.ForEach(i => i.CatalogType = catalogTypes.First(b => b.Id == i.CatalogTypeId));

        return items;
    }
}
