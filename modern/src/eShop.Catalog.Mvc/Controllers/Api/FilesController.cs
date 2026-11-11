using Microsoft.AspNetCore.Mvc;
using eShop.Catalog.Data.Services;

namespace eShop.Catalog.Mvc.Controllers.Api;

/// <summary>
/// Port of legacy <c>eShopLegacyMVC.Controllers.WebApi.FilesController</c>. INTENTIONAL HARDENING: legacy streamed the
/// brand list through <c>BinaryFormatter</c> (<c>eShopLegacy.Utilities.Serializing</c>, an insecure deserialization
/// format, labelled <c>text/html</c>); this returns the same <c>[{"Id":..,"Brand":..}]</c> list as JSON. BinaryFormatter and
/// <c>eShopLegacy.Utilities</c> are retired, not ported.
/// </summary>
[Route("api/files")]
public sealed class FilesController : LegacyApiControllerBase
{
    private static readonly NegotiatedFormat AlwaysJson = WebApiContentNegotiator.Default;

    private readonly ICatalogService service;

    public FilesController(ICatalogService service)
    {
        this.service = service;
    }

    // GET api/files (legacy Get() had no parameters, so api/files/{anything} returned the same payload).
    [HttpGet("{id?}")]
    public IActionResult Get(string? id)
    {
        _ = id;
        return new LegacyWebApiResult(StatusCodes.Status200OK, BrandDto.FromModels(service.GetCatalogBrands()), AlwaysJson);
    }

    [Route("{id?}", Order = 1)]
    public IActionResult OtherMethods(string? id)
    {
        _ = id;
        return LegacyMethodNotAllowed("GET");
    }
}
