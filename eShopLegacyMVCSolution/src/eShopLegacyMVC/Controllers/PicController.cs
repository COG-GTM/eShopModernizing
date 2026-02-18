using eShopLegacyMVC.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopLegacyMVC.Controllers
{
    public class PicController : Controller
    {
        public const string GetPicRouteName = "GetPicRouteTemplate";

        private readonly ICatalogService service;
        private readonly ILogger<PicController> _logger;
        private readonly IWebHostEnvironment _env;

        public PicController(ICatalogService service, ILogger<PicController> logger, IWebHostEnvironment env)
        {
            this.service = service;
            _logger = logger;
            _env = env;
        }

        [HttpGet]
        public IActionResult Index(int catalogItemId)
        {
            _logger.LogInformation("Now loading... /items/{CatalogItemId}/pic", catalogItemId);

            if (catalogItemId <= 0)
            {
                return BadRequest();
            }

            var item = service.FindCatalogItem(catalogItemId);

            if (item != null)
            {
                var webRoot = Path.Combine(_env.ContentRootPath, "Pics");
                var path = Path.Combine(webRoot, item.PictureFileName);

                string imageFileExtension = Path.GetExtension(item.PictureFileName);
                string mimetype = GetImageMimeTypeFromImageFileExtension(imageFileExtension);

                var buffer = System.IO.File.ReadAllBytes(path);

                return File(buffer, mimetype);
            }

            return NotFound();
        }

        private static string GetImageMimeTypeFromImageFileExtension(string extension)
        {
            string mimetype = extension switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".tiff" => "image/tiff",
                ".wmf" => "image/wmf",
                ".jp2" => "image/jp2",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream",
            };

            return mimetype;
        }
    }
}
