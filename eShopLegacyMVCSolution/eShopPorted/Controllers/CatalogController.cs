using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using eShopPorted.Models;
using eShopPorted.Services;

namespace eShopPorted.Controllers
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

        // GET /[?pageSize=3&pageIndex=10]
        public IActionResult Index(int pageSize = 10, int pageIndex = 0)
        {
            _logger.LogInformation("Now loading... /Catalog/Index?pageSize={PageSize}&pageIndex={PageIndex}", pageSize, pageIndex);
            var paginatedItems = service.GetCatalogItemsPaginated(pageSize, pageIndex);
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
            CatalogItem catalogItem = service.FindCatalogItem(id.Value);
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
            ViewBag.CatalogBrandId = new SelectList(service.GetCatalogBrands(), "Id", "Brand");
            ViewBag.CatalogTypeId = new SelectList(service.GetCatalogTypes(), "Id", "Type");
            return View(new CatalogItem());
        }

        // POST: Catalog/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder")] CatalogItem catalogItem)
        {
            _logger.LogInformation("Now processing... /Catalog/Create?catalogItemName={Name}", catalogItem.Name);
            if (ModelState.IsValid)
            {
                service.CreateCatalogItem(catalogItem);
                return RedirectToAction("Index");
            }

            ViewBag.CatalogBrandId = new SelectList(service.GetCatalogBrands(), "Id", "Brand", catalogItem.CatalogBrandId);
            ViewBag.CatalogTypeId = new SelectList(service.GetCatalogTypes(), "Id", "Type", catalogItem.CatalogTypeId);
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
            CatalogItem catalogItem = service.FindCatalogItem(id.Value);
            if (catalogItem == null)
            {
                return NotFound();
            }
            AddUriPlaceHolder(catalogItem);
            ViewBag.CatalogBrandId = new SelectList(service.GetCatalogBrands(), "Id", "Brand", catalogItem.CatalogBrandId);
            ViewBag.CatalogTypeId = new SelectList(service.GetCatalogTypes(), "Id", "Type", catalogItem.CatalogTypeId);
            return View(catalogItem);
        }

        // POST: Catalog/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit([Bind("Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder")] CatalogItem catalogItem)
        {
            _logger.LogInformation("Now processing... /Catalog/Edit?id={Id}", catalogItem.Id);
            if (ModelState.IsValid)
            {
                service.UpdateCatalogItem(catalogItem);
                return RedirectToAction("Index");
            }
            ViewBag.CatalogBrandId = new SelectList(service.GetCatalogBrands(), "Id", "Brand", catalogItem.CatalogBrandId);
            ViewBag.CatalogTypeId = new SelectList(service.GetCatalogTypes(), "Id", "Type", catalogItem.CatalogTypeId);
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
            CatalogItem catalogItem = service.FindCatalogItem(id.Value);
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
            CatalogItem catalogItem = service.FindCatalogItem(id);
            service.RemoveCatalogItem(catalogItem);
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                service.Dispose();
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
            item.PictureUri = Url.RouteUrl(PicController.GetPicRouteName, new { catalogItemId = item.Id }, Request.Scheme);
        }
    }
}
