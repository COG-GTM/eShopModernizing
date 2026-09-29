using eShopRazorPages.Models;
using eShopRazorPages.ViewModel;
using Microsoft.EntityFrameworkCore;

namespace eShopRazorPages.Services;

public class CatalogService : ICatalogService
{
    private readonly CatalogDBContext _db;

    public CatalogService(CatalogDBContext db)
    {
        _db = db;
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize, int pageIndex)
    {
        var totalItems = _db.CatalogItems.LongCount();

        var itemsOnPage = _db.CatalogItems
            .AsNoTracking()
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .OrderBy(c => c.Id)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToList();

        return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage);
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        return _db.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .SingleOrDefault(ci => ci.Id == id);
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return _db.CatalogTypes.AsNoTracking().ToList();
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return _db.CatalogBrands.AsNoTracking().ToList();
    }

    public void CreateCatalogItem(CatalogItem catalogItem)
    {
        _db.CatalogItems.Add(catalogItem);
        _db.SaveChanges();
    }

    public void UpdateCatalogItem(CatalogItem catalogItem)
    {
        var existing = _db.CatalogItems.Find(catalogItem.Id);
        if (existing is null)
        {
            _db.Entry(catalogItem).State = EntityState.Modified;
        }
        else if (!ReferenceEquals(existing, catalogItem))
        {
            _db.Entry(existing).CurrentValues.SetValues(catalogItem);
        }

        _db.SaveChanges();
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        var tracked = _db.CatalogItems.Find(catalogItem.Id);
        if (tracked is null)
        {
            return;
        }

        _db.CatalogItems.Remove(tracked);
        _db.SaveChanges();
    }
}
