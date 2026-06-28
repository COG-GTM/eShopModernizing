using Catalog.Api.Infrastructure;
using Catalog.Api.Models;
using Catalog.Api.ViewModel;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

// EF Core implementation of the catalog service. The legacy app used
// Entity Framework 6 with a HiLo id generator; EF Core handles key generation.
public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext _db;

    public CatalogService(CatalogDbContext db) => _db = db;

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
    {
        var totalItems = _db.CatalogItems.LongCount();

        var itemsOnPage = _db.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .OrderBy(c => c.Id)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToList();

        return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage);
    }

    public CatalogItem? FindCatalogItem(int id) =>
        _db.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .FirstOrDefault(c => c.Id == id);

    public IEnumerable<CatalogBrand> GetCatalogBrands() => _db.CatalogBrands.ToList();

    public IEnumerable<CatalogType> GetCatalogTypes() => _db.CatalogTypes.ToList();

    public CatalogItem CreateCatalogItem(CatalogItem catalogItem)
    {
        _db.CatalogItems.Add(catalogItem);
        _db.SaveChanges();
        return catalogItem;
    }

    public void UpdateCatalogItem(CatalogItem catalogItem)
    {
        _db.CatalogItems.Update(catalogItem);
        _db.SaveChanges();
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        var existing = _db.CatalogItems.Find(catalogItem.Id);
        if (existing != null)
        {
            _db.CatalogItems.Remove(existing);
            _db.SaveChanges();
        }
    }
}
