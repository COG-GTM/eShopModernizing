using eShopCoreModernized.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopCoreModernized.Controllers
{
    public class PicController : Controller
    {
        private readonly ILogger<PicController> _logger;
        private readonly ICatalogService service;
        private readonly IWebHostEnvironment _environment;

        public const string GetPicRouteName = "GetPicRouteTemplate";

        public PicController(ICatalogService service, ILogger<PicController> logger, IWebHostEnvironment environment)
        {
            this.service = service;
            _logger = logger;
            _environment = environment;
        }

        [HttpGet]
        [Route("items/{catalogItemId:int}/pic", Name = GetPicRouteName)]
        public async Task<IActionResult> Index(int catalogItemId)
        {
            _logger.LogInformation("Now loading... /items/Index?{CatalogItemId}/pic", catalogItemId);

            if (catalogItemId <= 0)
            {
                return BadRequest();
            }

            var item = await service.FindCatalogItemAsync(catalogItemId);

            if (item != null)
            {
                var webRoot = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "Pics");
                var path = Path.Combine(webRoot, item.PictureFileName);

                if (System.IO.File.Exists(path))
                {
                    string imageFileExtension = Path.GetExtension(item.PictureFileName);
                    string mimetype = GetImageMimeTypeFromImageFileExtension(imageFileExtension);

                    var buffer = await System.IO.File.ReadAllBytesAsync(path);
                    return File(buffer, mimetype);
                }
            }

            return NotFound();
        }

        private static string GetImageMimeTypeFromImageFileExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".tiff" => "image/tiff",
                ".wmf" => "image/wmf",
                ".jp2" => "image/jp2",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }
    }
}
