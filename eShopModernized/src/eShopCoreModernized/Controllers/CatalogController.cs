using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using eShopCoreModernized.Configuration;

namespace eShopCoreModernized.Controllers
{
    public class CatalogController : Controller
    {
        private readonly ILogger<CatalogController> _logger;
        private readonly ICatalogService _service;
        private readonly IImageService _imageService;
        private readonly ICatalogConfiguration _catalogConfiguration;

        public CatalogController(
            ICatalogService service, 
            IImageService imageService,
            ICatalogConfiguration catalogConfiguration,
            ILogger<CatalogController> logger)
        {
            _service = service;
            _imageService = imageService;
            _catalogConfiguration = catalogConfiguration;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int pageSize = 10, int pageIndex = 0)
        {
            _logger.LogInformation("Now loading... /Catalog/Index?pageSize={PageSize}&pageIndex={PageIndex}", pageSize, pageIndex);
            var paginatedItems = await _service.GetCatalogItemsPaginatedAsync(pageSize, pageIndex);
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
            CatalogItem? catalogItem = await _service.FindCatalogItemAsync(id.Value);
            if (catalogItem == null)
            {
                return NotFound();
            }
            AddUriPlaceHolder(catalogItem);

            return View(catalogItem);
        }

        [Authorize]
        public async Task<IActionResult> Create()
        {
            _logger.LogInformation("Now loading... /Catalog/Create");
            ViewBag.CatalogBrandId = new SelectList(await _service.GetCatalogBrandsAsync(), "Id", "Brand");
            ViewBag.CatalogTypeId = new SelectList(await _service.GetCatalogTypesAsync(), "Id", "Type");
            ViewBag.UseAzureStorage = _catalogConfiguration.UseAzureStorage;
            
            return View(new CatalogItem()
            {
                PictureUri = _imageService.UrlDefaultImage()
            });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder,TempImageName")] CatalogItem catalogItem)
        {
            _logger.LogInformation("Now processing... /Catalog/Create?catalogItemName={CatalogItemName}", catalogItem.Name);
            if (ModelState.IsValid)
            {
                if (!string.IsNullOrEmpty(catalogItem.TempImageName))
                {
                    var fileName = Path.GetFileName(catalogItem.TempImageName);
                    catalogItem.PictureFileName = fileName;
                }

                await _service.CreateCatalogItemAsync(catalogItem);
                
                if (!string.IsNullOrEmpty(catalogItem.TempImageName))
                {
                    await _imageService.UpdateImageAsync(catalogItem);
                }
                
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CatalogBrandId = new SelectList(await _service.GetCatalogBrandsAsync(), "Id", "Brand", catalogItem.CatalogBrandId);
            ViewBag.CatalogTypeId = new SelectList(await _service.GetCatalogTypesAsync(), "Id", "Type", catalogItem.CatalogTypeId);
            ViewBag.UseAzureStorage = _catalogConfiguration.UseAzureStorage;
            return View(catalogItem);
        }

        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            _logger.LogInformation("Now loading... /Catalog/Edit?id={Id}", id);
            if (id == null)
            {
                return BadRequest();
            }
            CatalogItem? catalogItem = await _service.FindCatalogItemAsync(id.Value);
            if (catalogItem == null)
            {
                return NotFound();
            }
            AddUriPlaceHolder(catalogItem);
            ViewBag.CatalogBrandId = new SelectList(await _service.GetCatalogBrandsAsync(), "Id", "Brand", catalogItem.CatalogBrandId);
            ViewBag.CatalogTypeId = new SelectList(await _service.GetCatalogTypesAsync(), "Id", "Type", catalogItem.CatalogTypeId);
            ViewBag.UseAzureStorage = _catalogConfiguration.UseAzureStorage;
            return View(catalogItem);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("Id,Name,Description,Price,PictureFileName,CatalogTypeId,CatalogBrandId,AvailableStock,RestockThreshold,MaxStockThreshold,OnReorder,TempImageName")] CatalogItem catalogItem)
        {
            _logger.LogInformation("Now processing... /Catalog/Edit?id={Id}", catalogItem.Id);
            if (ModelState.IsValid)
            {
                if (!string.IsNullOrEmpty(catalogItem.TempImageName))
                {
                    await _imageService.UpdateImageAsync(catalogItem);
                    var fileName = Path.GetFileName(catalogItem.TempImageName);
                    catalogItem.PictureFileName = fileName;
                }

                await _service.UpdateCatalogItemAsync(catalogItem);
                return RedirectToAction(nameof(Index));
            }
            ViewBag.CatalogBrandId = new SelectList(await _service.GetCatalogBrandsAsync(), "Id", "Brand", catalogItem.CatalogBrandId);
            ViewBag.CatalogTypeId = new SelectList(await _service.GetCatalogTypesAsync(), "Id", "Type", catalogItem.CatalogTypeId);
            ViewBag.UseAzureStorage = _catalogConfiguration.UseAzureStorage;
            return View(catalogItem);
        }

        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            _logger.LogInformation("Now loading... /Catalog/Delete?id={Id}", id);
            if (id == null)
            {
                return BadRequest();
            }
            CatalogItem? catalogItem = await _service.FindCatalogItemAsync(id.Value);
            if (catalogItem == null)
            {
                return NotFound();
            }
            AddUriPlaceHolder(catalogItem);

            return View(catalogItem);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation("Now processing... /Catalog/DeleteConfirmed?id={Id}", id);
            CatalogItem? catalogItem = await _service.FindCatalogItemAsync(id);
            if (catalogItem != null)
            {
                await _service.RemoveCatalogItemAsync(catalogItem);
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
            item.PictureUri = _imageService.BuildUrlImage(item);
        }
    }
}
