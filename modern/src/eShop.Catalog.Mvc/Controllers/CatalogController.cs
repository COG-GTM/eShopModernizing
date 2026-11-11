using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShop.Catalog.Mvc.Controllers;

/// <summary>Read flows (S3). Create/Edit/Delete live in CatalogController.Write.cs (S4).</summary>
public partial class CatalogController : Controller
{
    private readonly ICatalogService service;
    private readonly ILogger<CatalogController> logger;

    public CatalogController(ICatalogService service, ILogger<CatalogController> logger)
    {
        this.service = service;
        this.logger = logger;
    }

    // GET /[?pageSize=3&pageIndex=10]
    public IActionResult Index(int pageSize = 10, int pageIndex = 0)
    {
        logger.LogInformation("Now loading... /Catalog/Index?pageSize={PageSize}&pageIndex={PageIndex}", pageSize, pageIndex);
        var paginatedItems = service.GetCatalogItemsPaginated(pageSize, pageIndex);
        ChangeUriPlaceholder(paginatedItems.Data);
        return View(paginatedItems);
    }

    // GET: Catalog/Details/5
    public IActionResult Details(int? id)
    {
        logger.LogInformation("Now loading... /Catalog/Details?id={Id}", id);
        if (id == null)
        {
            return EmptyStatus(StatusCodes.Status400BadRequest);
        }

        var catalogItem = service.FindCatalogItem(id.Value);
        if (catalogItem == null)
        {
            return EmptyStatus(StatusCodes.Status404NotFound);
        }

        AddUriPlaceHolder(catalogItem);

        return View(catalogItem);
    }

    /// <summary>Legacy HttpStatusCodeResult / HttpNotFound: empty body with a text/html content type.</summary>
    internal static ContentResult EmptyStatus(int statusCode) =>
        new() { StatusCode = statusCode, ContentType = "text/html", Content = string.Empty };

    private void ChangeUriPlaceholder(IEnumerable<CatalogItem> items)
    {
        foreach (var catalogItem in items)
        {
            AddUriPlaceHolder(catalogItem);
        }
    }

    private void AddUriPlaceHolder(CatalogItem item)
    {
        item.PictureUri = Url.RouteUrl(PicController.GetPicRouteName, new { catalogItemId = item.Id }, Request.Scheme);
    }
}
