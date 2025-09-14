using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using eShopModernized.Models;
using eShopModernized.Services;

namespace eShopModernized.Controllers
{
    public class CatalogController : Controller
    {
        private readonly ILogger<CatalogController> _logger;
        private readonly ICatalogService service;

        public CatalogController(ICatalogService service, ILogger<CatalogController> logger)
        {
            this.service = service;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int pageSize = 10, int pageIndex = 0)
        {
            _logger.LogInformation("Now loading... /Catalog/Index?pageSize={PageSize}&pageIndex={PageIndex}", pageSize, pageIndex);
            var paginatedItems = await service.GetCatalogItemsPaginatedAsync(pageSize, pageIndex);
            ChangeUriPlaceholder(paginatedItems.Data);
            return View(paginatedItems);
        }

        public async Task<IActionResult> Details(int? id)
        {
            _logger.LogInformation("Now loading... /Catalog/Details?id={Id}", id);
            if (id == null)
            {
                return BadRequest();
            }
            CatalogItem? catalogItem = await service.FindCatalogItemAsync(id.Value);
            if (catalogItem == null)
            {
                return NotFound();
            }
            AddUriPlaceHolder(catalogItem);

            return View(catalogItem);
        }

        public async Task<IActionResult> Create()
        {
            _logger.LogInformation("Now loading... /Catalog/Create");
            ViewBag.CatalogBrandId = new SelectList(await service.GetCatalogBrandsAsync(), "Id", "Brand");
            ViewBag.CatalogTypeId = new SelectList(await service.GetCatalogTypesAsync(), "Id", "Type");
            return View(new CatalogItem());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder")] CatalogItem catalogItem)
        {
            _logger.LogInformation("Now processing... /Catalog/Create?catalogItemName={CatalogItemName}", catalogItem.Name);
            if (ModelState.IsValid)
            {
                await service.CreateCatalogItemAsync(catalogItem);
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CatalogBrandId = new SelectList(await service.GetCatalogBrandsAsync(), "Id", "Brand", catalogItem.CatalogBrandId);
            ViewBag.CatalogTypeId = new SelectList(await service.GetCatalogTypesAsync(), "Id", "Type", catalogItem.CatalogTypeId);
            return View(catalogItem);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            _logger.LogInformation("Now loading... /Catalog/Edit?id={Id}", id);
            if (id == null)
            {
                return BadRequest();
            }
            CatalogItem? catalogItem = await service.FindCatalogItemAsync(id.Value);
            if (catalogItem == null)
            {
                return NotFound();
            }
            AddUriPlaceHolder(catalogItem);
            ViewBag.CatalogBrandId = new SelectList(await service.GetCatalogBrandsAsync(), "Id", "Brand", catalogItem.CatalogBrandId);
            ViewBag.CatalogTypeId = new SelectList(await service.GetCatalogTypesAsync(), "Id", "Type", catalogItem.CatalogTypeId);
            return View(catalogItem);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder")] CatalogItem catalogItem)
        {
            _logger.LogInformation("Now processing... /Catalog/Edit?id={Id}", catalogItem.Id);
            if (ModelState.IsValid)
            {
                await service.UpdateCatalogItemAsync(catalogItem);
                return RedirectToAction(nameof(Index));
            }
            ViewBag.CatalogBrandId = new SelectList(await service.GetCatalogBrandsAsync(), "Id", "Brand", catalogItem.CatalogBrandId);
            ViewBag.CatalogTypeId = new SelectList(await service.GetCatalogTypesAsync(), "Id", "Type", catalogItem.CatalogTypeId);
            return View(catalogItem);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            _logger.LogInformation("Now loading... /Catalog/Delete?id={Id}", id);
            if (id == null)
            {
                return BadRequest();
            }
            CatalogItem? catalogItem = await service.FindCatalogItemAsync(id.Value);
            if (catalogItem == null)
            {
                return NotFound();
            }
            AddUriPlaceHolder(catalogItem);

            return View(catalogItem);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation("Now processing... /Catalog/DeleteConfirmed?id={Id}", id);
            CatalogItem? catalogItem = await service.FindCatalogItemAsync(id);
            if (catalogItem != null)
            {
                await service.RemoveCatalogItemAsync(catalogItem);
            }
            return RedirectToAction(nameof(Index));
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
            item.PictureUri = Url.RouteUrl("GetPicRouteTemplate", new { catalogItemId = item.Id }, Request.Scheme);
        }
    }
}
