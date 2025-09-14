using eShopModernizedCore.Models;
using eShopModernizedCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eShopModernizedCore.Controllers;

public class CatalogController : Controller
{
    private readonly ICatalogService _catalogService;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(ICatalogService catalogService, ILogger<CatalogController> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    // GET /[?pageSize=3&pageIndex=10]
    public IActionResult Index(int pageSize = 10, int pageIndex = 0)
    {
        _logger.LogInformation("Now loading... /Catalog/Index?pageSize={PageSize}&pageIndex={PageIndex}", pageSize, pageIndex);
        var paginatedItems = _catalogService.GetCatalogItemsPaginated(pageSize, pageIndex);
        ChangeUriPlaceholder(paginatedItems.Data);
        return View(paginatedItems);
    }

    // GET: Catalog/Details/5
    public IActionResult Details(int? id)
    {
        _logger.LogInformation("Now loading... /Catalog/Details?id={Id}", id);
        if (id == null)
        {
            return BadRequest();
        }

        var catalogItem = _catalogService.FindCatalogItem(id.Value);
        if (catalogItem == null)
        {
            return NotFound();
        }

        AddUriPlaceHolder(catalogItem);
        return View(catalogItem);
    }

    // GET: Catalog/Create
    public IActionResult Create()
    {
        _logger.LogInformation("Now loading... /Catalog/Create");
        ViewBag.CatalogBrandId = new SelectList(_catalogService.GetCatalogBrands(), "Id", "Brand");
        ViewBag.CatalogTypeId = new SelectList(_catalogService.GetCatalogTypes(), "Id", "Type");
        return View(new CatalogItem());
    }

    // POST: Catalog/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder")] CatalogItem catalogItem)
    {
        _logger.LogInformation("Now processing... /Catalog/Create?catalogItemName={CatalogItemName}", catalogItem.Name);
        if (ModelState.IsValid)
        {
            await _catalogService.CreateCatalogItemAsync(catalogItem);
            return RedirectToAction(nameof(Index));
        }

        ViewBag.CatalogBrandId = new SelectList(_catalogService.GetCatalogBrands(), "Id", "Brand", catalogItem.CatalogBrandId);
        ViewBag.CatalogTypeId = new SelectList(_catalogService.GetCatalogTypes(), "Id", "Type", catalogItem.CatalogTypeId);
        return View(catalogItem);
    }

    // GET: Catalog/Edit/5
    public IActionResult Edit(int? id)
    {
        _logger.LogInformation("Now loading... /Catalog/Edit?id={Id}", id);
        if (id == null)
        {
            return BadRequest();
        }

        var catalogItem = _catalogService.FindCatalogItem(id.Value);
        if (catalogItem == null)
        {
            return NotFound();
        }

        AddUriPlaceHolder(catalogItem);
        ViewBag.CatalogBrandId = new SelectList(_catalogService.GetCatalogBrands(), "Id", "Brand", catalogItem.CatalogBrandId);
        ViewBag.CatalogTypeId = new SelectList(_catalogService.GetCatalogTypes(), "Id", "Type", catalogItem.CatalogTypeId);
        return View(catalogItem);
    }

    // POST: Catalog/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder")] CatalogItem catalogItem)
    {
        if (id != catalogItem.Id)
        {
            return NotFound();
        }

        _logger.LogInformation("Now processing... /Catalog/Edit?id={Id}", catalogItem.Id);
        if (ModelState.IsValid)
        {
            await _catalogService.UpdateCatalogItemAsync(catalogItem);
            return RedirectToAction(nameof(Index));
        }

        ViewBag.CatalogBrandId = new SelectList(_catalogService.GetCatalogBrands(), "Id", "Brand", catalogItem.CatalogBrandId);
        ViewBag.CatalogTypeId = new SelectList(_catalogService.GetCatalogTypes(), "Id", "Type", catalogItem.CatalogTypeId);
        return View(catalogItem);
    }

    // GET: Catalog/Delete/5
    public IActionResult Delete(int? id)
    {
        _logger.LogInformation("Now loading... /Catalog/Delete?id={Id}", id);
        if (id == null)
        {
            return BadRequest();
        }

        var catalogItem = _catalogService.FindCatalogItem(id.Value);
        if (catalogItem == null)
        {
            return NotFound();
        }

        AddUriPlaceHolder(catalogItem);
        return View(catalogItem);
    }

    // POST: Catalog/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        _logger.LogInformation("Now processing... /Catalog/DeleteConfirmed?id={Id}", id);
        var catalogItem = _catalogService.FindCatalogItem(id);
        if (catalogItem != null)
        {
            await _catalogService.RemoveCatalogItemAsync(catalogItem);
        }
        return RedirectToAction(nameof(Index));
    }

    protected override void Dispose(bool disposing)
    {
        _logger.LogDebug("Now disposing");
        if (disposing)
        {
            _catalogService.Dispose();
        }
        base.Dispose(disposing);
    }

    private void ChangeUriPlaceholder(IEnumerable<CatalogItem> items)
    {
        foreach (var catalogItem in items)
        {
            AddUriPlaceHolder(catalogItem);
        }
    }

    private void AddUriPlaceHolder(CatalogItem item)
    {
        item.PictureUri = Url.Action("GetImage", "Pic", new { catalogItemId = item.Id }, Request.Scheme);
    }
}
