using eShopCoreAPI.Models;
using eShopCoreAPI.ViewModels;

namespace eShopCoreAPI.Services;

public class CatalogServiceMock : ICatalogService
{
    private readonly List<CatalogItem> _catalogItems;
    private readonly List<CatalogBrand> _catalogBrands;
    private readonly List<CatalogType> _catalogTypes;

    public CatalogServiceMock()
    {
        _catalogBrands = new List<CatalogBrand>
        {
            new() { Id = 1, Brand = "Azure" },
            new() { Id = 2, Brand = ".NET" },
            new() { Id = 3, Brand = "Visual Studio" },
            new() { Id = 4, Brand = "SQL Server" },
            new() { Id = 5, Brand = "Other" }
        };

        _catalogTypes = new List<CatalogType>
        {
            new() { Id = 1, Type = "Mug" },
            new() { Id = 2, Type = "T-Shirt" },
            new() { Id = 3, Type = "Sheet" },
            new() { Id = 4, Type = "USB Memory Stick" }
        };

        _catalogItems = new List<CatalogItem>
        {
            new() { Id = 1, CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 100, Description = ".NET Bot Black Hoodie", Name = ".NET Bot Black Hoodie", Price = 19.5M, PictureFileName = "1.png" },
            new() { Id = 2, CatalogTypeId = 1, CatalogBrandId = 2, AvailableStock = 100, Description = ".NET Black & White Mug", Name = ".NET Black & White Mug", Price = 8.50M, PictureFileName = "2.png" },
            new() { Id = 3, CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100, Description = "Prism White T-Shirt", Name = "Prism White T-Shirt", Price = 12, PictureFileName = "3.png" },
            new() { Id = 4, CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 100, Description = ".NET Foundation T-shirt", Name = ".NET Foundation T-shirt", Price = 12, PictureFileName = "4.png" },
            new() { Id = 5, CatalogTypeId = 3, CatalogBrandId = 5, AvailableStock = 100, Description = "Roslyn Red Sheet", Name = "Roslyn Red Sheet", Price = 8.5M, PictureFileName = "5.png" },
            new() { Id = 6, CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 100, Description = ".NET Blue Hoodie", Name = ".NET Blue Hoodie", Price = 12, PictureFileName = "6.png" },
            new() { Id = 7, CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100, Description = "Roslyn Red T-Shirt", Name = "Roslyn Red T-Shirt", Price = 12, PictureFileName = "7.png" },
            new() { Id = 8, CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100, Description = "Kudu Purple Hoodie", Name = "Kudu Purple Hoodie", Price = 8.5M, PictureFileName = "8.png" },
            new() { Id = 9, CatalogTypeId = 1, CatalogBrandId = 5, AvailableStock = 100, Description = "Cup<T> White Mug", Name = "Cup<T> White Mug", Price = 12, PictureFileName = "9.png" },
            new() { Id = 10, CatalogTypeId = 3, CatalogBrandId = 2, AvailableStock = 100, Description = ".NET Foundation Sheet", Name = ".NET Foundation Sheet", Price = 12, PictureFileName = "10.png" },
            new() { Id = 11, CatalogTypeId = 3, CatalogBrandId = 2, AvailableStock = 100, Description = "Cup<T> Sheet", Name = "Cup<T> Sheet", Price = 8.5M, PictureFileName = "11.png" },
            new() { Id = 12, CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100, Description = "Prism White TShirt", Name = "Prism White TShirt", Price = 12, PictureFileName = "12.png" }
        };

        foreach (var item in _catalogItems)
        {
            item.CatalogBrand = _catalogBrands.FirstOrDefault(b => b.Id == item.CatalogBrandId);
            item.CatalogType = _catalogTypes.FirstOrDefault(t => t.Id == item.CatalogTypeId);
        }
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        return _catalogItems.FirstOrDefault(x => x.Id == id);
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return _catalogBrands;
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
    {
        var totalItems = _catalogItems.Count;
        var itemsOnPage = _catalogItems
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToList();

        return new PaginatedItemsViewModel<CatalogItem>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Count = totalItems,
            Data = itemsOnPage
        };
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return _catalogTypes;
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        catalogItem.Id = _catalogItems.Max(x => x.Id) + 1;
        catalogItem.CatalogBrand = _catalogBrands.FirstOrDefault(b => b.Id == catalogItem.CatalogBrandId);
        catalogItem.CatalogType = _catalogTypes.FirstOrDefault(t => t.Id == catalogItem.CatalogTypeId);
        _catalogItems.Add(catalogItem);
    }

    public void UpdateCatalogItem(CatalogItem catalogItem)
    {
        var existingItem = _catalogItems.FirstOrDefault(x => x.Id == catalogItem.Id);
        if (existingItem != null)
        {
            var index = _catalogItems.IndexOf(existingItem);
            catalogItem.CatalogBrand = _catalogBrands.FirstOrDefault(b => b.Id == catalogItem.CatalogBrandId);
            catalogItem.CatalogType = _catalogTypes.FirstOrDefault(t => t.Id == catalogItem.CatalogTypeId);
            _catalogItems[index] = catalogItem;
        }
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        var existingItem = _catalogItems.FirstOrDefault(x => x.Id == catalogItem.Id);
        if (existingItem != null)
        {
            _catalogItems.Remove(existingItem);
        }
    }

    public void Dispose()
    {
    }
}
