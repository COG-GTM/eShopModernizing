using Microsoft.AspNetCore.Mvc;
using eShopCoreModernized.Services;
using eShopCoreModernized.Models;

namespace eShopCoreModernized.Controllers.Api
{
    [Route("api/brands")]
    [ApiController]
    public class BrandsApiController : ControllerBase
    {
        private readonly IBrandService _service;
        private readonly ILogger<BrandsApiController> _logger;

        public BrandsApiController(IBrandService service, ILogger<BrandsApiController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CatalogBrand>>> Get()
        {
            var brands = await _service.GetBrandsAsync();
            return Ok(brands);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CatalogBrand>> Get(int id)
        {
            var brand = await _service.FindBrandAsync(id);
            if (brand == null)
            {
                return NotFound();
            }

            return Ok(brand);
        }

        [HttpPost]
        public async Task<ActionResult<CatalogBrand>> Post([FromBody] CatalogBrand brand)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Now processing... POST /api/brands?brand={Brand}", brand.Brand);
            await _service.CreateBrandAsync(brand);

            return CreatedAtAction(nameof(Get), new { id = brand.Id }, brand);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] CatalogBrand brand)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existing = await _service.FindBrandAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            _logger.LogInformation("Now processing... PUT /api/brands/{Id}", id);
            existing.Brand = brand.Brand;
            await _service.UpdateBrandAsync(existing);

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var brandToDelete = await _service.FindBrandAsync(id);
            if (brandToDelete == null)
            {
                return NotFound();
            }

            if (await _service.IsBrandInUseAsync(id))
            {
                return Conflict($"Brand {id} is still referenced by catalog items.");
            }

            _logger.LogInformation("Now processing... DELETE /api/brands/{Id}", id);
            await _service.RemoveBrandAsync(brandToDelete);

            return NoContent();
        }
    }
}
