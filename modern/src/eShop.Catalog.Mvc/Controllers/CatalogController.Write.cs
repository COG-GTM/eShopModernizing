using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Services;
using eShop.Catalog.Mvc.Models;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eShop.Catalog.Mvc.Controllers;

/// <summary>
/// Slice S4: legacy CatalogController Create / Edit / Delete (GET + POST). The service is injected per action so this
/// partial doesn't depend on the constructor/fields declared by the S3 half (CatalogController.cs).
/// </summary>
public partial class CatalogController : Controller
{
    // GET: Catalog/Create
    [HttpGet]
    public IActionResult Create([FromServices] ICatalogService service)
    {
        PopulateWriteSelectLists(service, null, null);
        return View(CatalogItemInputModel.FromEntity(new CatalogItem()));
    }

    // POST: Catalog/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create([FromServices] ICatalogService service, CatalogItemInputModel catalogItem)
    {
        if (!LookupsExist(service, catalogItem))
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            service.CreateCatalogItem(catalogItem.ToNewEntity());
            return RedirectToCatalogIndex();
        }

        catalogItem.PictureFileName = CatalogItem.DefaultPictureName;
        PopulateWriteSelectLists(service, catalogItem.CatalogBrandId, catalogItem.CatalogTypeId);
        return View(catalogItem);
    }

    // GET: Catalog/Edit/5
    [HttpGet]
    public IActionResult Edit([FromServices] ICatalogService service, int? id)
    {
        if (id == null)
        {
            return BadRequest();
        }

        var item = service.FindCatalogItem(id.Value);
        if (item == null)
        {
            return NotFound();
        }

        SetPictureUri(item);
        PopulateWriteSelectLists(service, item.CatalogBrandId, item.CatalogTypeId);
        return View(CatalogItemInputModel.FromEntity(item));
    }

    // POST: Catalog/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit([FromServices] ICatalogService service, CatalogItemInputModel catalogItem)
    {
        if (!LookupsExist(service, catalogItem))
        {
            return BadRequest();
        }

        var existing = service.FindCatalogItem(catalogItem.Id);
        if (ModelState.IsValid)
        {
            // Legacy mock UpdateCatalogItem is a no-op for unknown ids and still redirects.
            if (existing != null)
            {
                catalogItem.ApplyTo(existing);
                service.UpdateCatalogItem(existing);
            }

            return RedirectToCatalogIndex();
        }

        // Legacy re-renders the bound model: no PictureUri (img src omitted); the picture name is never taken from the request.
        catalogItem.PictureFileName = existing?.PictureFileName;
        PopulateWriteSelectLists(service, catalogItem.CatalogBrandId, catalogItem.CatalogTypeId);
        return View(catalogItem);
    }

    // GET: Catalog/Delete/5
    [HttpGet]
    public IActionResult Delete([FromServices] ICatalogService service, int? id)
    {
        if (id == null)
        {
            return BadRequest();
        }

        var item = service.FindCatalogItem(id.Value);
        if (item == null)
        {
            return NotFound();
        }

        // The legacy mock only fills these navigations once the list page has rendered; resolve them here so the
        // confirmation page shows brand/type regardless of navigation order.
        item.CatalogBrand ??= service.GetCatalogBrands().FirstOrDefault(b => b.Id == item.CatalogBrandId);
        item.CatalogType ??= service.GetCatalogTypes().FirstOrDefault(t => t.Id == item.CatalogTypeId);
        SetPictureUri(item);
        return View(item);
    }

    // POST: Catalog/Delete/5
    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed([FromServices] ICatalogService service, int id)
    {
        var item = service.FindCatalogItem(id);
        if (item != null)
        {
            service.RemoveCatalogItem(item);
        }

        return RedirectToCatalogIndex();
    }

    // Legacy RedirectToAction("Index") resolves to "/" (Catalog/Index are the route defaults).
    private RedirectResult RedirectToCatalogIndex()
    {
        return Redirect(Url.Content("~/"));
    }

    // The dropdowns only offer known ids; anything else is a tampered request. Rejecting it before it is stored keeps
    // a dangling brand/type id out of the catalog (legacy SQL failed the FK constraint, the mock broke every list read).
    private static bool LookupsExist(ICatalogService service, CatalogItemInputModel input)
    {
        return (input.CatalogBrandId is not { } brandId || service.GetCatalogBrands().Any(b => b.Id == brandId))
            && (input.CatalogTypeId is not { } typeId || service.GetCatalogTypes().Any(t => t.Id == typeId));
    }

    private void PopulateWriteSelectLists(ICatalogService service, int? selectedBrandId, int? selectedTypeId)
    {
        ViewData["CatalogBrandId"] = new SelectList(service.GetCatalogBrands(), "Id", "Brand", selectedBrandId);
        ViewData["CatalogTypeId"] = new SelectList(service.GetCatalogTypes(), "Id", "Type", selectedTypeId);
    }

    // Same absolute URL as legacy Url.RouteUrl(PicController.GetPicRouteName, ..., Request.Url.Scheme).
    private void SetPictureUri(CatalogItem item)
    {
        item.PictureUri = UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, $"/items/{item.Id}/pic");
    }
}
