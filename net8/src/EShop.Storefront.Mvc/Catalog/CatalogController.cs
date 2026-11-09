// Owned by T2 (MVC catalog UI). Port of legacy eShopLegacyMVC.Controllers.CatalogController.
using EShop.Catalog.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EShop.Storefront.Mvc.Catalog;

[LegacyAntiforgeryFailure]
public class CatalogController : Controller
{
    private const string BindableFields =
        "Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder";

    private readonly ICatalogService service;
    private readonly ILogger<CatalogController> logger;

    public CatalogController(ICatalogService service, ILogger<CatalogController> logger)
    {
        this.service = service;
        this.logger = logger;
    }

    // GET /[?pageSize=3&pageIndex=10]
    public IActionResult Index(int pageSize = 10, int pageIndex = 0)
    {
        logger.LogInformation("Now loading... /Catalog/Index?pageSize={PageSize}&pageIndex={PageIndex}", pageSize, pageIndex);
        var paginatedItems = service.GetCatalogItemsPaginated(pageSize, pageIndex);
        foreach (var item in paginatedItems.Data)
        {
            AddUriPlaceHolder(item);
        }
        return View(paginatedItems);
    }

    // GET: Catalog/Details/5
    public IActionResult Details(int? id)
    {
        logger.LogInformation("Now loading... /Catalog/Details?id={Id}", id);
        return WithItem(id, item => View(item));
    }

    // GET: Catalog/Create
    public IActionResult Create()
    {
        logger.LogInformation("Now loading... /Catalog/Create");
        PopulateSelectLists();
        return View(new CatalogItem());
    }

    // POST: Catalog/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create([Bind(BindableFields)] CatalogItem catalogItem)
    {
        logger.LogInformation("Now processing... /Catalog/Create?catalogItemName={Name}", catalogItem.Name);
        ApplyLegacyRequiredMessages();
        if (ModelState.IsValid)
        {
            service.CreateCatalogItem(catalogItem);
            return RedirectToAction(nameof(Index));
        }

        PopulateSelectLists(catalogItem);
        return View(catalogItem);
    }

    // GET: Catalog/Edit/5
    public IActionResult Edit(int? id)
    {
        logger.LogInformation("Now loading... /Catalog/Edit?id={Id}", id);
        return WithItem(id, item =>
        {
            PopulateSelectLists(item);
            return View(item);
        });
    }

    // POST: Catalog/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit([Bind(BindableFields)] CatalogItem catalogItem)
    {
        logger.LogInformation("Now processing... /Catalog/Edit?id={Id}", catalogItem.Id);
        ApplyLegacyRequiredMessages();
        if (ModelState.IsValid)
        {
            service.UpdateCatalogItem(catalogItem);
            return RedirectToAction(nameof(Index));
        }

        PopulateSelectLists(catalogItem);
        return View(catalogItem);
    }

    // GET: Catalog/Delete/5
    public IActionResult Delete(int? id)
    {
        logger.LogInformation("Now loading... /Catalog/Delete?id={Id}", id);
        return WithItem(id, item => View(item));
    }

    // POST: Catalog/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        logger.LogInformation("Now processing... /Catalog/DeleteConfirmed?id={Id}", id);
        var catalogItem = service.FindCatalogItem(id);
        if (catalogItem != null)
        {
            service.RemoveCatalogItem(catalogItem);
        }
        return RedirectToAction(nameof(Index));
    }

    private IActionResult WithItem(int? id, Func<CatalogItem, IActionResult> render)
    {
        if (id == null)
        {
            return BadRequest();
        }
        var catalogItem = service.FindCatalogItem(id.Value);
        if (catalogItem == null)
        {
            return NotFound();
        }
        AddUriPlaceHolder(catalogItem);
        return render(catalogItem);
    }

    private void PopulateSelectLists(CatalogItem? selected = null)
    {
        ViewBag.CatalogBrandId = new SelectList(service.GetCatalogBrands(), "Id", "Brand", selected?.CatalogBrandId);
        ViewBag.CatalogTypeId = new SelectList(service.GetCatalogTypes(), "Id", "Type", selected?.CatalogTypeId);
    }

    // Legacy: absolute URL of the "GetPicRoute" route (/items/{catalogItemId}/pic, served by T3).
    private void AddUriPlaceHolder(CatalogItem item)
    {
        item.PictureUri = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/items/{item.Id}/pic";
    }

    // MVC 5 reported an empty value for a non-nullable property as "The {0} field is required." (implicit [Required]);
    // ASP.NET Core model binding reports "The value '' is invalid." instead.
    private void ApplyLegacyRequiredMessages()
    {
        foreach (var (key, entry) in ModelState)
        {
            if (!string.IsNullOrEmpty(entry.AttemptedValue) || entry.Errors.Count == 0)
            {
                continue;
            }
            var property = MetadataProvider.GetMetadataForType(typeof(CatalogItem)).Properties[key];
            if (property == null || !property.ModelType.IsValueType || property.IsNullableValueType)
            {
                continue;
            }
            entry.Errors.Clear();
            entry.Errors.Add($"The {property.GetDisplayName()} field is required.");
        }
    }
}
