using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eShopNet8.Mvc.Controllers;

public class CatalogController : Controller
{
    private const string BindableFields =
        "Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder";

    private readonly ICatalogService _service;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(ICatalogService service, ILogger<CatalogController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET /[?pageSize=3&pageIndex=10]
    [HttpGet]
    public IActionResult Index(int pageSize = 10, int pageIndex = 0)
    {
        _logger.LogInformation("Now loading... /Catalog/Index?pageSize={PageSize}&pageIndex={PageIndex}", pageSize, pageIndex);
        var paginatedItems = _service.GetCatalogItemsPaginated(pageSize, pageIndex);
        foreach (var catalogItem in paginatedItems.Data)
        {
            AddUriPlaceHolder(catalogItem);
        }
        return View(paginatedItems);
    }

    // GET: Catalog/Details/5
    [HttpGet]
    public IActionResult Details(int? id)
    {
        _logger.LogInformation("Now loading... /Catalog/Details?id={Id}", id);
        if (id == null)
        {
            return BadRequest();
        }
        var catalogItem = _service.FindCatalogItem(id.Value);
        if (catalogItem == null)
        {
            return NotFound();
        }
        AddUriPlaceHolder(catalogItem);
        return View(catalogItem);
    }

    // GET: Catalog/Create
    [HttpGet]
    public IActionResult Create()
    {
        _logger.LogInformation("Now loading... /Catalog/Create");
        ViewBag.CatalogBrandId = new SelectList(_service.GetCatalogBrands(), "Id", "Brand");
        ViewBag.CatalogTypeId = new SelectList(_service.GetCatalogTypes(), "Id", "Type");
        return View(new CatalogItem());
    }

    // POST: Catalog/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create([Bind(BindableFields)] CatalogItem catalogItem)
    {
        _logger.LogInformation("Now processing... /Catalog/Create?catalogItemName={Name}", catalogItem.Name);
        if (ModelState.IsValid)
        {
            _service.CreateCatalogItem(catalogItem);
            return new LegacyRedirectResult(Url.Action(nameof(Index))!);
        }
        ViewBag.CatalogBrandId = new SelectList(_service.GetCatalogBrands(), "Id", "Brand", catalogItem.CatalogBrandId);
        ViewBag.CatalogTypeId = new SelectList(_service.GetCatalogTypes(), "Id", "Type", catalogItem.CatalogTypeId);
        return View(catalogItem);
    }

    // GET: Catalog/Edit/5
    [HttpGet]
    public IActionResult Edit(int? id)
    {
        _logger.LogInformation("Now loading... /Catalog/Edit?id={Id}", id);
        if (id == null)
        {
            return BadRequest();
        }
        var catalogItem = _service.FindCatalogItem(id.Value);
        if (catalogItem == null)
        {
            return NotFound();
        }
        AddUriPlaceHolder(catalogItem);
        ViewBag.CatalogBrandId = new SelectList(_service.GetCatalogBrands(), "Id", "Brand", catalogItem.CatalogBrandId);
        ViewBag.CatalogTypeId = new SelectList(_service.GetCatalogTypes(), "Id", "Type", catalogItem.CatalogTypeId);
        return View(catalogItem);
    }

    // POST: Catalog/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit([Bind(BindableFields)] CatalogItem catalogItem)
    {
        _logger.LogInformation("Now processing... /Catalog/Edit?id={Id}", catalogItem.Id);
        if (ModelState.IsValid)
        {
            _service.UpdateCatalogItem(catalogItem);
            return new LegacyRedirectResult(Url.Action(nameof(Index))!);
        }
        ViewBag.CatalogBrandId = new SelectList(_service.GetCatalogBrands(), "Id", "Brand", catalogItem.CatalogBrandId);
        ViewBag.CatalogTypeId = new SelectList(_service.GetCatalogTypes(), "Id", "Type", catalogItem.CatalogTypeId);
        return View(catalogItem);
    }

    // GET: Catalog/Delete/5
    [HttpGet]
    public IActionResult Delete(int? id)
    {
        _logger.LogInformation("Now loading... /Catalog/Delete?id={Id}", id);
        if (id == null)
        {
            return BadRequest();
        }
        var catalogItem = _service.FindCatalogItem(id.Value);
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
    public IActionResult DeleteConfirmed(int id)
    {
        _logger.LogInformation("Now processing... /Catalog/DeleteConfirmed?id={Id}", id);
        var catalogItem = _service.FindCatalogItem(id);
        // Legacy passes the lookup result straight through; the service decides what an unknown id does (INVENTORY Q18).
        _service.RemoveCatalogItem(catalogItem!);
        return new LegacyRedirectResult(Url.Action(nameof(Index))!);
    }

    private void AddUriPlaceHolder(CatalogItem item)
    {
        item.PictureUri = Url.RouteUrl(PicController.GetPicRouteName, new { catalogItemId = item.Id }, Request.Scheme);
    }
}
