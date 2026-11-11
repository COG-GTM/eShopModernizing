using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShop.Catalog.Mvc.Controllers.Api;

/// <summary>Port of legacy <c>eShopLegacyMVC.Controllers.WebApi.BrandsController</c> (Web API 2).</summary>
[Route("api/brands")]
public sealed class BrandsController : LegacyApiControllerBase
{
    private const string LegacyType = "eShopLegacyMVC.Controllers.WebApi.BrandsController";

    private readonly ICatalogService service;

    public BrandsController(ICatalogService service)
    {
        this.service = service;
    }

    // GET api/brands, GET api/brands/5, GET api/brands?id=5
    [HttpGet("{id?}")]
    public IActionResult Get(string? id)
    {
        if (!TryGetRawId(id, out var rawId))
        {
            return LegacyOk(BrandDto.FromModels(service.GetCatalogBrands()));
        }

        if (!LegacyInt32Parser.TryParse(rawId, out var brandId))
        {
            return LegacyInvalidId("System.Web.Http.IHttpActionResult Get(Int32)", LegacyType);
        }

        var brand = service.GetCatalogBrands().FirstOrDefault(x => x.Id == brandId);
        return brand is null ? LegacyStatus(StatusCodes.Status404NotFound) : LegacyOk(BrandDto.FromModel(brand));
    }

    // DELETE api/brands/5: demo only, never deletes (legacy 200 / 404 with empty body).
    [HttpDelete("{id?}")]
    public IActionResult Delete(string? id)
    {
        if (!TryGetRawId(id, out var rawId))
        {
            return LegacyMethodNotAllowed("GET");
        }

        if (!LegacyInt32Parser.TryParse(rawId, out var brandId))
        {
            return LegacyInvalidId("System.Web.Http.IHttpActionResult Delete(Int32)", LegacyType);
        }

        var exists = service.GetCatalogBrands().Any(x => x.Id == brandId);
        return LegacyStatus(exists ? StatusCodes.Status200OK : StatusCodes.Status404NotFound);
    }

    // Any other method (POST, PUT, PATCH, HEAD, OPTIONS, ...): Web API 405 with the verbs the URI supports.
    [Route("{id?}", Order = 1)]
    public IActionResult OtherMethods(string? id)
    {
        return TryGetRawId(id, out _) ? LegacyMethodNotAllowed("GET", "DELETE") : LegacyMethodNotAllowed("GET");
    }
}
