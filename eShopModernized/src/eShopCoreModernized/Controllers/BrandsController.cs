using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using eShopCoreModernized.Models;
using eShopCoreModernized.Services;

namespace eShopCoreModernized.Controllers
{
    public class BrandsController : Controller
    {
        private readonly IBrandService _service;
        private readonly ILogger<BrandsController> _logger;

        public BrandsController(IBrandService service, ILogger<BrandsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Now loading... /Brands/Index");
            return View(await _service.GetBrandsAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            _logger.LogInformation("Now loading... /Brands/Details?id={Id}", id);
            if (id == null)
            {
                return BadRequest();
            }
            var brand = await _service.FindBrandAsync(id.Value);
            if (brand == null)
            {
                return NotFound();
            }

            return View(brand);
        }

        [Authorize]
        public IActionResult Create()
        {
            _logger.LogInformation("Now loading... /Brands/Create");
            return View(new CatalogBrand());
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Brand")] CatalogBrand catalogBrand)
        {
            _logger.LogInformation("Now processing... /Brands/Create?brand={Brand}", catalogBrand.Brand);
            if (!ModelState.IsValid)
            {
                return View(catalogBrand);
            }

            await _service.CreateBrandAsync(catalogBrand);
            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            _logger.LogInformation("Now loading... /Brands/Edit?id={Id}", id);
            if (id == null)
            {
                return BadRequest();
            }
            var brand = await _service.FindBrandAsync(id.Value);
            if (brand == null)
            {
                return NotFound();
            }

            return View(brand);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("Id,Brand")] CatalogBrand catalogBrand)
        {
            _logger.LogInformation("Now processing... /Brands/Edit?id={Id}", catalogBrand.Id);
            if (!ModelState.IsValid)
            {
                return View(catalogBrand);
            }

            var existing = await _service.FindBrandAsync(catalogBrand.Id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.Brand = catalogBrand.Brand;
            await _service.UpdateBrandAsync(existing);
            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            _logger.LogInformation("Now loading... /Brands/Delete?id={Id}", id);
            if (id == null)
            {
                return BadRequest();
            }
            var brand = await _service.FindBrandAsync(id.Value);
            if (brand == null)
            {
                return NotFound();
            }
            ViewBag.IsBrandInUse = await _service.IsBrandInUseAsync(id.Value);

            return View(brand);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation("Now processing... /Brands/DeleteConfirmed?id={Id}", id);
            var brand = await _service.FindBrandAsync(id);
            if (brand == null)
            {
                return NotFound();
            }

            if (await _service.IsBrandInUseAsync(id))
            {
                ModelState.AddModelError(string.Empty, "This brand cannot be deleted while catalog items still reference it.");
                ViewBag.IsBrandInUse = true;
                return View(brand);
            }

            await _service.RemoveBrandAsync(brand);
            return RedirectToAction(nameof(Index));
        }
    }
}
