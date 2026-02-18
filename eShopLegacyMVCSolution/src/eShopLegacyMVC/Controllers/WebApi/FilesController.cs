using eShopLegacyMVC.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace eShopLegacyMVC.Controllers.WebApi
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly ICatalogService _service;

        public FilesController(ICatalogService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var brands = _service.GetCatalogBrands()
                .Select(b => new BrandDTO
                {
                    Id = b.Id,
                    Brand = b.Brand
                }).ToList();

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(brands);
            return File(jsonBytes, "application/json");
        }

        public class BrandDTO
        {
            public int Id { get; set; }
            public string Brand { get; set; } = string.Empty;
        }
    }
}
