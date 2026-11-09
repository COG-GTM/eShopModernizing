using System.Globalization;
using EShop.Catalog.Contracts;

namespace EShop.Storefront.Mvc.Api;

/// <summary>Legacy <c>PicController.Index</c>: <c>[HttpGet, Route("items/{catalogItemId:int}/pic")]</c> serving <c>~/Pics/&lt;PictureFileName&gt;</c>.</summary>
internal static class PicEndpoint
{
    public static async Task HandleAsync(HttpContext context, ILoggerFactory loggerFactory, IWebHostEnvironment environment)
    {
        // MVC 5 [HttpGet] attribute route: any other verb falls through to the conventional route and 404s.
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var catalogItemId = int.Parse((string)context.Request.RouteValues["catalogItemId"]!, NumberStyles.Integer, CultureInfo.InvariantCulture);
        loggerFactory.CreateLogger(typeof(PicEndpoint)).LogInformation("Now loading... /items/Index?{CatalogItemId}/pic", catalogItemId);

        if (catalogItemId <= 0)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "text/html";
            return;
        }

        var item = context.RequestServices.GetRequiredService<ICatalogService>().FindCatalogItem(catalogItemId);
        if (item is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/html";
            return;
        }

        var picsRoot = Path.GetFullPath(Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "Pics"));
        var path = Path.GetFullPath(Path.Combine(picsRoot, item.PictureFileName));

        // PictureFileName is user-editable; legacy Path.Combine would read any file. Only serve files under Pics/.
        if (!path.StartsWith(picsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/html";
            return;
        }

        // Missing file: legacy File.ReadAllBytes throws -> 500 through the app's exception handling.
        var buffer = await File.ReadAllBytesAsync(path, context.RequestAborted);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = GetImageMimeTypeFromImageFileExtension(Path.GetExtension(item.PictureFileName));
        context.Response.ContentLength = buffer.Length;
        await context.Response.Body.WriteAsync(buffer, context.RequestAborted);
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
