using Microsoft.EntityFrameworkCore;
using Timberland.Catalog.Api.Infrastructure;
using Timberland.Catalog.Api.Models;
using Timberland.Catalog.Api.ViewModel;

namespace Timberland.Catalog.Api.Services;

/// <summary>
/// EF Core backed catalog service. In this demo it is wired to the EF Core
/// InMemory provider; a full migration would swap in a real SQL Server / Npgsql
/// provider without changing this code.
/// </summary>
public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext _db;

    public CatalogService(CatalogDbContext db)
    {
        _db = db;
    }

    public PaginatedItemsViewModel<CatalogItem> GetCatalogItemsPaginated(int pageSize = 10, int pageIndex = 0)
    {
        var totalItems = _db.CatalogItems.LongCount();

        var itemsOnPage = _db.CatalogItems
            .OrderBy(c => c.Id)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToList();

        Compose(itemsOnPage);

        return new PaginatedItemsViewModel<CatalogItem>(
            pageIndex, pageSize, totalItems, itemsOnPage);
    }

    public CatalogItem? FindCatalogItem(int id)
    {
        var item = _db.CatalogItems.AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (item != null)
        {
            Compose(new List<CatalogItem> { item });
        }
        return item;
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
        _db.CatalogItems.Update(catalogItem);
        _db.SaveChanges();
    }

    public void RemoveCatalogItem(CatalogItem catalogItem)
    {
        var tracked = _db.CatalogItems.FirstOrDefault(x => x.Id == catalogItem.Id);
        if (tracked != null)
        {
            _db.CatalogItems.Remove(tracked);
            _db.SaveChanges();
        }
    }

    private void Compose(List<CatalogItem> items)
    {
        var brands = _db.CatalogBrands.AsNoTracking().ToList();
        var types = _db.CatalogTypes.AsNoTracking().ToList();
        items.ForEach(i => i.CatalogBrand = brands.FirstOrDefault(b => b.Id == i.CatalogBrandId));
        items.ForEach(i => i.CatalogType = types.FirstOrDefault(t => t.Id == i.CatalogTypeId));
    }
}
