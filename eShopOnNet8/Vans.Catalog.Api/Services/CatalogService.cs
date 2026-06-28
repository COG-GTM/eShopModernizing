using Microsoft.EntityFrameworkCore;
using Vans.Catalog.Api.Models;
using Vans.Catalog.Api.ViewModel;

namespace Vans.Catalog.Api.Services;

/// <summary>
/// EF Core backed catalog service. Wired to EF Core InMemory by default, but the
/// same code works against a real relational provider (e.g. SQL Server) once configured.
/// </summary>
public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext _db;

    public CatalogService(CatalogDbContext db)
    {
        _db = db;
    }

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

        return new PaginatedItemsViewModel<CatalogItem>(
            pageIndex, pageSize, totalItems, itemsOnPage);
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        return _db.CatalogItems
            .Include(c => c.CatalogBrand)
            .Include(c => c.CatalogType)
            .FirstOrDefault(c => c.Id == id);
    }

    public IEnumerable<CatalogType> GetCatalogTypes()
    {
        return _db.CatalogTypes.ToList();
    }

    public IEnumerable<CatalogBrand> GetCatalogBrands()
    {
        return _db.CatalogBrands.ToList();
    }

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
        _db.CatalogItems.Remove(catalogItem);
        _db.SaveChanges();
    }
}
