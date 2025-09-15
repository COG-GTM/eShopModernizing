using Microsoft.AspNetCore.Mvc;
using eShopCoreModernized.Services;
using System.Text.Json;

namespace eShopCoreModernized.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly ICatalogService _service;
        private readonly ILogger<FilesController> _logger;

        public FilesController(ICatalogService service, ILogger<FilesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var brands = (await _service.GetCatalogBrandsAsync())
                .Select(b => new BrandDTO
                {
                    Id = b.Id,
                    Brand = b.Brand
                }).ToList();

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(brands);
            
            return File(jsonBytes, "application/json");
        }

        [Serializable]
        public class BrandDTO
        {
            public int Id { get; set; }
            public string Brand { get; set; } = string.Empty;
        }
    }
}
