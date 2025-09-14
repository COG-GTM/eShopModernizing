using eShopCoreModernized.Models;
using eShopCoreModernized.ViewModel;

namespace eShopCoreModernized.Services
{
    public class CatalogServiceMock : ICatalogService
    {
        private readonly List<CatalogItem> catalogItems;
        private readonly List<CatalogBrand> catalogBrands;
        private readonly List<CatalogType> catalogTypes;

        public CatalogServiceMock()
        {
            catalogBrands = new List<CatalogBrand>
            {
                new CatalogBrand { Id = 1, Brand = "Azure" },
                new CatalogBrand { Id = 2, Brand = ".NET" },
                new CatalogBrand { Id = 3, Brand = "Visual Studio" },
                new CatalogBrand { Id = 4, Brand = "SQL Server" },
                new CatalogBrand { Id = 5, Brand = "Other" }
            };

            catalogTypes = new List<CatalogType>
            {
                new CatalogType { Id = 1, Type = "Mug" },
                new CatalogType { Id = 2, Type = "T-Shirt" },
                new CatalogType { Id = 3, Type = "Sheet" },
                new CatalogType { Id = 4, Type = "USB Memory Stick" }
            };

            catalogItems = new List<CatalogItem>
            {
                new CatalogItem { Id = 1, Name = ".NET Bot Black Hoodie", Description = ".NET Bot Black Hoodie", Price = 19.5M, PictureFileName = "1.png", CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 100, RestockThreshold = 0, MaxStockThreshold = 0, OnReorder = false },
                new CatalogItem { Id = 2, Name = ".NET Black & White Mug", Description = ".NET Black & White Mug", Price = 8.50M, PictureFileName = "2.png", CatalogTypeId = 1, CatalogBrandId = 2, AvailableStock = 100, RestockThreshold = 0, MaxStockThreshold = 0, OnReorder = false },
                new CatalogItem { Id = 3, Name = "Prism White T-Shirt", Description = "Prism White T-Shirt", Price = 12M, PictureFileName = "3.png", CatalogTypeId = 2, CatalogBrandId = 5, AvailableStock = 100, RestockThreshold = 0, MaxStockThreshold = 0, OnReorder = false },
                new CatalogItem { Id = 4, Name = ".NET Foundation T-shirt", Description = ".NET Foundation T-shirt", Price = 12M, PictureFileName = "4.png", CatalogTypeId = 2, CatalogBrandId = 2, AvailableStock = 100, RestockThreshold = 0, MaxStockThreshold = 0, OnReorder = false },
                new CatalogItem { Id = 5, Name = "Roslyn Red Sheet", Description = "Roslyn Red Sheet", Price = 8.5M, PictureFileName = "5.png", CatalogTypeId = 3, CatalogBrandId = 2, AvailableStock = 100, RestockThreshold = 0, MaxStockThreshold = 0, OnReorder = false }
            };

            foreach (var item in catalogItems)
            {
                item.CatalogBrand = catalogBrands.FirstOrDefault(b => b.Id == item.CatalogBrandId);
                item.CatalogType = catalogTypes.FirstOrDefault(t => t.Id == item.CatalogTypeId);
            }
        }

        public Task<PaginatedItemsViewModel<CatalogItem>> GetCatalogItemsPaginatedAsync(int pageSize, int pageIndex)
        {
            return Task.FromResult(GetCatalogItemsPaginated(pageSize, pageIndex));
        }

        public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
        {
            var totalItems = catalogItems.Count;
            var itemsOnPage = catalogItems
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToList();

            return new PaginatedItemsViewModel<CatalogItem>(
                pageIndex, pageSize, totalItems, itemsOnPage);
        }

        public Task<CatalogItem?> FindCatalogItemAsync(int id)
        {
            return Task.FromResult(FindCatalogItem(id));
        }

        public CatalogItem? FindCatalogItem(int id)
        {
            return catalogItems.FirstOrDefault(ci => ci.Id == id);
        }

        public Task<IEnumerable<CatalogType>> GetCatalogTypesAsync()
        {
            return Task.FromResult<IEnumerable<CatalogType>>(catalogTypes);
        }

        public IEnumerable<CatalogType> GetCatalogTypes()
        {
            return catalogTypes;
        }

        public Task<IEnumerable<CatalogBrand>> GetCatalogBrandsAsync()
        {
            return Task.FromResult<IEnumerable<CatalogBrand>>(catalogBrands);
        }

        public IEnumerable<CatalogBrand> GetCatalogBrands()
        {
            return catalogBrands;
        }

        public Task CreateCatalogItemAsync(CatalogItem catalogItem)
        {
            CreateCatalogItem(catalogItem);
            return Task.CompletedTask;
        }

        public void CreateCatalogItem(CatalogItem catalogItem)
        {
            catalogItem.Id = catalogItems.Max(i => i.Id) + 1;
            catalogItems.Add(catalogItem);
        }

        public Task UpdateCatalogItemAsync(CatalogItem catalogItem)
        {
            UpdateCatalogItem(catalogItem);
            return Task.CompletedTask;
        }

        public void UpdateCatalogItem(CatalogItem catalogItem)
        {
            var existingItem = catalogItems.FirstOrDefault(i => i.Id == catalogItem.Id);
            if (existingItem != null)
            {
                var index = catalogItems.IndexOf(existingItem);
                catalogItems[index] = catalogItem;
            }
        }

        public Task RemoveCatalogItemAsync(CatalogItem catalogItem)
        {
            RemoveCatalogItem(catalogItem);
            return Task.CompletedTask;
        }

        public void RemoveCatalogItem(CatalogItem catalogItem)
        {
            catalogItems.RemoveAll(i => i.Id == catalogItem.Id);
        }

        public void Dispose()
        {
        }
    }
}
