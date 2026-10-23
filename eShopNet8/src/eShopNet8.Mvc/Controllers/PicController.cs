using eShop.Catalog.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopNet8.Mvc.Controllers;

public class PicController : Controller
{
    public const string GetPicRouteName = "GetPicRouteTemplate";

    private readonly ICatalogService _service;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PicController> _logger;

    public PicController(ICatalogService service, IWebHostEnvironment env, ILogger<PicController> logger)
    {
        _service = service;
        _env = env;
        _logger = logger;
    }

    // GET: items/5/pic
    [HttpGet]
    [Route("items/{catalogItemId:int}/pic", Name = GetPicRouteName)]
    public IActionResult Index(int catalogItemId)
    {
        _logger.LogInformation("Now loading... /items/Index?{CatalogItemId}/pic", catalogItemId);
        if (catalogItemId <= 0)
        {
            return BadRequest();
        }
        var item = _service.FindCatalogItem(catalogItemId);
        if (item == null)
        {
            return NotFound();
        }

        // Divergence: PictureFileName is user-bindable on Create/Edit, so only a plain file name inside Pics/ is served
        // (legacy Path.Combine would follow "../" out of the folder). A missing file still fails with 500 as in legacy.
        var fileName = Path.GetFileName(item.PictureFileName ?? string.Empty);
        if (fileName.Length == 0 || fileName != item.PictureFileName)
        {
            return NotFound();
        }
        var path = Path.Combine(_env.WebRootPath, "Pics", fileName);
        var buffer = System.IO.File.ReadAllBytes(path);
        return File(buffer, GetImageMimeTypeFromImageFileExtension(Path.GetExtension(fileName)));
    }

    private static string GetImageMimeTypeFromImageFileExtension(string extension) => extension switch
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
}
