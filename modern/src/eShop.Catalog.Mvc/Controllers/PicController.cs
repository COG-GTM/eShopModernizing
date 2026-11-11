using eShop.Catalog.Data.Services;
using eShop.Catalog.Mvc.Pictures;
using Microsoft.AspNetCore.Mvc;

namespace eShop.Catalog.Mvc.Controllers;

public class PicController : Controller
{
    public const string GetPicRouteName = "GetPicRouteTemplate";

    private readonly ICatalogService service;
    private readonly IWebHostEnvironment environment;
    private readonly ILogger<PicController> logger;

    public PicController(ICatalogService service, IWebHostEnvironment environment, ILogger<PicController> logger)
    {
        this.service = service;
        this.environment = environment;
        this.logger = logger;
    }

    // GET: items/5/pic
    [HttpGet]
    [Route("items/{catalogItemId:int}/pic", Name = GetPicRouteName)]
    public IActionResult Index(int catalogItemId)
    {
        logger.LogInformation("Now loading... /items/Index?{CatalogItemId}/pic", catalogItemId);

        if (catalogItemId <= 0)
        {
            return CatalogController.EmptyStatus(StatusCodes.Status400BadRequest);
        }

        var item = service.FindCatalogItem(catalogItemId);
        if (item == null)
        {
            return CatalogController.EmptyStatus(StatusCodes.Status404NotFound);
        }

        var picsRoot = Path.Combine(environment.WebRootPath ?? environment.ContentRootPath, PicturePaths.FolderName);
        if (!PicturePaths.TryResolve(picsRoot, item.PictureFileName, out var path))
        {
            logger.LogWarning("Rejected picture path {PictureFileName} for catalog item {CatalogItemId}", item.PictureFileName, catalogItemId);
            return CatalogController.EmptyStatus(StatusCodes.Status404NotFound);
        }

        var mimetype = ImageMimeTypes.FromExtension(Path.GetExtension(item.PictureFileName));
        var buffer = System.IO.File.ReadAllBytes(path);

        return File(buffer, mimetype);
    }
}
