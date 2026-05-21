using eShopPorted.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopPorted.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
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

            return Ok(brands);
        }

        public class BrandDTO
        {
            public int Id { get; set; }
            public string Brand { get; set; }
        }
    }
}
