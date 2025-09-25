using Microsoft.AspNetCore.Mvc;
using eShopCoreModernized.Services;
using eShopCoreModernized.Models;

namespace eShopCoreModernized.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CatalogItemsController : ControllerBase
    {
        private readonly ICatalogService _service;
        private readonly ILogger<CatalogItemsController> _logger;

        public CatalogItemsController(ICatalogService service, ILogger<CatalogItemsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CatalogItem>>> Get(int brandIdFilter = 0, int typeIdFilter = 0)
        {
            _logger.LogInformation("Getting catalog items with brandIdFilter={BrandIdFilter}, typeIdFilter={TypeIdFilter}", brandIdFilter, typeIdFilter);
            
            try
            {
                var items = await _service.GetCatalogItemsAsync(brandIdFilter, typeIdFilter);
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting catalog items");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
