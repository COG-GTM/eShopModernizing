using Microsoft.AspNetCore.Mvc;
using eShopCoreModernized.Services;
using eShopCoreModernized.Models;

namespace eShopCoreModernized.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BrandsController : ControllerBase
    {
        private readonly ICatalogService _service;
        private readonly ILogger<BrandsController> _logger;

        public BrandsController(ICatalogService service, ILogger<BrandsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CatalogBrand>>> Get()
        {
            var brands = await _service.GetCatalogBrandsAsync();
            return Ok(brands);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CatalogBrand>> Get(int id)
        {
            var brands = await _service.GetCatalogBrandsAsync();
            var brand = brands.FirstOrDefault(x => x.Id == id);
            
            if (brand == null)
            {
                return NotFound();
            }

            return Ok(brand);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var brands = await _service.GetCatalogBrandsAsync();
            var brandToDelete = brands.FirstOrDefault(x => x.Id == id);
            
            if (brandToDelete == null)
            {
                return NotFound();
            }

            return Ok();
        }
    }
}
