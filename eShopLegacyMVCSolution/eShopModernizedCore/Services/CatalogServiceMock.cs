using eShopModernizedCore.Models;
using eShopModernizedCore.ViewModels;

namespace eShopModernizedCore.Services;

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
            new() { Id = 1, Name = ".NET Bot Black Hoodie", Description = ".NET Bot Black Hoodie", Price = 19.5M, PictureFileName = "1.png", CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 100, RestockThreshold = 0, MaxStockThreshold = 200, OnReorder = false },
            new() { Id = 2, Name = ".NET Black & White Mug", Description = ".NET Black & White Mug", Price = 8.50M, PictureFileName = "2.png", CatalogTypeId = 1, CatalogBrandId = 2, AvailableStock = 89, RestockThreshold = 0, MaxStockThreshold = 200, OnReorder = false },
            new() { Id = 3, Name = "Prism White T-Shirt", Description = "Prism White T-Shirt", Price = 12, PictureFileName = "3.png", CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 56, RestockThreshold = 0, MaxStockThreshold = 200, OnReorder = false },
            new() { Id = 4, Name = ".NET Foundation Sheet", Description = ".NET Foundation Sheet", Price = 12, PictureFileName = "4.png", CatalogTypeId = 3, CatalogBrandId = 2, AvailableStock = 76, RestockThreshold = 0, MaxStockThreshold = 200, OnReorder = false },
            new() { Id = 5, Name = "Roslyn Red Pin", Description = "Roslyn Red Pin", Price = 8.5M, PictureFileName = "5.png", CatalogTypeId = 5, CatalogBrandId = 2, AvailableStock = 55, RestockThreshold = 0, MaxStockThreshold = 200, OnReorder = false }
        };

        foreach (var item in _catalogItems)
        {
            item.CatalogBrand = _catalogBrands.FirstOrDefault(b => b.Id == item.CatalogBrandId);
            item.CatalogType = _catalogTypes.FirstOrDefault(t => t.Id == item.CatalogTypeId);
        }
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
    {
        var totalItems = _catalogItems.Count;
        var itemsOnPage = _catalogItems
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToList();

        return new PaginatedItemsViewModel<CatalogItem>(
            pageIndex, pageSize, totalItems, itemsOnPage);
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        return _catalogItems.FirstOrDefault(ci => ci.Id == id);
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return _catalogTypes;
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return _catalogBrands;
    }

    public Task CreateCatalogItemAsync(CatalogItem catalogItem)
    {
        catalogItem.Id = _catalogItems.Max(i => i.Id) + 1;
        catalogItem.CatalogBrand = _catalogBrands.FirstOrDefault(b => b.Id == catalogItem.CatalogBrandId);
        catalogItem.CatalogType = _catalogTypes.FirstOrDefault(t => t.Id == catalogItem.CatalogTypeId);
        _catalogItems.Add(catalogItem);
        return Task.CompletedTask;
    }

    public Task UpdateCatalogItemAsync(CatalogItem catalogItem)
    {
        var existingItem = _catalogItems.FirstOrDefault(i => i.Id == catalogItem.Id);
        if (existingItem != null)
        {
            var index = _catalogItems.IndexOf(existingItem);
            catalogItem.CatalogBrand = _catalogBrands.FirstOrDefault(b => b.Id == catalogItem.CatalogBrandId);
            catalogItem.CatalogType = _catalogTypes.FirstOrDefault(t => t.Id == catalogItem.CatalogTypeId);
            _catalogItems[index] = catalogItem;
        }
        return Task.CompletedTask;
    }

    public Task RemoveCatalogItemAsync(CatalogItem catalogItem)
    {
        _catalogItems.Remove(catalogItem);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
