using System.Globalization;
using EShop.Catalog.Contracts;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;

namespace EShop.Storefront.Mvc.Api;

/// <summary>
/// Reproduces Web API 2 controller/action selection for the legacy <c>api/{controller}/{id}</c> route:
/// BrandsController (Get(), Get(int id), Delete(int id)) and FilesController (Get()).
/// </summary>
internal static class LegacyWebApi
{
    private const string BrandsControllerType = "eShopLegacyMVC.Controllers.WebApi.BrandsController";

    public static Task DispatchAsync(HttpContext context)
    {
        var controller = (string?)context.Request.RouteValues["controller"] ?? string.Empty;
        LegacyWebApiResponse.AddNoCacheHeaders(context.Response);

        if (controller.Equals("brands", StringComparison.OrdinalIgnoreCase))
        {
            return BrandsAsync(context);
        }

        if (controller.Equals("files", StringComparison.OrdinalIgnoreCase))
        {
            return FilesAsync(context);
        }

        return LegacyWebApiResponse.WriteErrorAsync(context, StatusCodes.Status404NotFound,
            $"No HTTP resource was found that matches the request URI '{context.Request.GetDisplayUrl()}'.",
            $"No type was found that matches the controller named '{controller}'.");
    }

    private static Task BrandsAsync(HttpContext context)
    {
        var request = context.Request;
        // Web API value providers: query string first, then route data.
        string? rawId = request.Query.TryGetValue("id", out var queryId)
            ? queryId.FirstOrDefault() ?? string.Empty
            : (string?)request.RouteValues["id"];
        var hasId = rawId is not null;

        if (HttpMethods.IsGet(request.Method))
        {
            if (!hasId)
            {
                return LegacyWebApiResponse.WriteBrandsAsync(context, Service(context).GetCatalogBrands().ToList());
            }

            if (!TryBindInt32(rawId!, out var id))
            {
                return WriteNullIdParameterAsync(context, "Get");
            }

            var brand = Service(context).GetCatalogBrands().FirstOrDefault(b => b.Id == id);
            return brand is null
                ? LegacyWebApiResponse.WriteEmptyAsync(context, StatusCodes.Status404NotFound)
                : LegacyWebApiResponse.WriteBrandAsync(context, brand);
        }

        if (HttpMethods.IsDelete(request.Method) && hasId)
        {
            if (!TryBindInt32(rawId!, out var id))
            {
                return WriteNullIdParameterAsync(context, "Delete");
            }

            // Legacy is demo-only: it never deletes, it only reports whether the brand exists.
            var exists = Service(context).GetCatalogBrands().Any(b => b.Id == id);
            return LegacyWebApiResponse.WriteEmptyAsync(context, exists ? StatusCodes.Status200OK : StatusCodes.Status404NotFound);
        }

        return LegacyWebApiResponse.WriteMethodNotAllowedAsync(context, hasId ? "GET, DELETE" : "GET");
    }

    private static Task FilesAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return LegacyWebApiResponse.WriteMethodNotAllowedAsync(context, "GET");
        }

        var brands = Service(context).GetCatalogBrands().Select(b => new BrandListNrbfWriter.BrandDto(b.Id, b.Brand)).ToList();
        var useMockData = context.RequestServices.GetService<IOptions<CatalogOptions>>()?.Value.UseMockData ?? true;

        // Legacy mock mode projects an in-memory List (ToList pre-sizes the backing array); SQL mode projects an EF DbSet,
        // so List<T>.Add grows the array 4, 8, 16, ... and BinaryFormatter writes the trailing null slots too.
        var capacity = useMockData ? brands.Count : BrandListNrbfWriter.GrownCapacity(brands.Count);
        var payload = BrandListNrbfWriter.Write(brands, capacity);

        // StreamContent without headers: System.Web falls back to its default text/html content type.
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html";
        context.Response.ContentLength = payload.Length;
        return context.Response.Body.WriteAsync(payload).AsTask();
    }

    private static Task WriteNullIdParameterAsync(HttpContext context, string action) =>
        LegacyWebApiResponse.WriteErrorAsync(context, StatusCodes.Status400BadRequest,
            "The request is invalid.",
            $"The parameters dictionary contains a null entry for parameter 'id' of non-nullable type 'System.Int32' for method " +
            $"'System.Web.Http.IHttpActionResult {action}(Int32)' in '{BrandsControllerType}'. An optional parameter must be a " +
            "reference type, a nullable type, or be declared as an optional parameter.");

    /// <summary>Web API binds simple types with <see cref="System.ComponentModel.Int32Converter"/> semantics (invariant culture).</summary>
    internal static bool TryBindInt32(string value, out int result)
    {
        var text = value.Trim();
        try
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || text.StartsWith("&h", StringComparison.OrdinalIgnoreCase))
            {
                result = Convert.ToInt32(text[2..], 16);
                return true;
            }

            if (text.StartsWith('#'))
            {
                result = Convert.ToInt32(text[1..], 16);
                return true;
            }
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            result = 0;
            return false;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static ICatalogService Service(HttpContext context) => context.RequestServices.GetRequiredService<ICatalogService>();
}
