using System.Globalization;
using eShop.Catalog.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShopNet8.Mvc.Api;

// Port of the legacy Web API 2 controllers (Controllers/WebApi/BrandsController + FilesController, route api/{controller}/{id})
// as minimal-API endpoint groups. The undiscovered Controllers/Api/CatalogController2 ("/api", INVENTORY Q1) is
// intentionally not ported, so "/api" stays 404.
public static class CatalogApiExtensions
{
    public static IServiceCollection AddCatalogApi(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public static WebApplication MapCatalogApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        var brands = api.MapGroup("/brands");
        brands.MapGet("", GetBrands);
        brands.MapGet("/{id:int}", (HttpContext context, int id, [FromServices] ICatalogService service) => GetBrand(context, id, service));
        brands.MapDelete("/{id:int}", DeleteBrand);
        // Web API 2 matched api/brands/{id} for any id and failed parameter binding with 400 for non-Int32 values.
        brands.MapGet("/{id}", (HttpContext context) => WebApiResults.InvalidInt32Parameter(context, "Get"));
        brands.MapDelete("/{id}", (HttpContext context) => WebApiResults.InvalidInt32Parameter(context, "Delete"));

        // Web API 2 routed api/files/{id} to FilesController.Get() as well (the id is ignored).
        var files = api.MapGroup("/files");
        files.MapGet("", GetFiles);
        files.MapGet("/{id}", GetFiles);

        // Web API 2 answered other verbs on a matched controller with 405, an Allow header and an HttpError body.
        brands.MapMethods("", OtherMethods("GET"), (HttpContext context) => WebApiResults.MethodNotAllowed(context, "GET"));
        brands.MapMethods("/{id}", OtherMethods("GET", "DELETE"), (HttpContext context) => WebApiResults.MethodNotAllowed(context, "GET", "DELETE"));
        files.MapMethods("", OtherMethods("GET"), (HttpContext context) => WebApiResults.MethodNotAllowed(context, "GET"));
        files.MapMethods("/{id}", OtherMethods("GET"), (HttpContext context) => WebApiResults.MethodNotAllowed(context, "GET"));

        return app;
    }

    private static readonly string[] AllMethods = { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS" };

    private static string[] OtherMethods(params string[] allowed) => AllMethods.Except(allowed).ToArray();

    // GET api/brands (and GET api/brands?id=N, which Web API 2 action selection routed to Get(int id)).
    private static IResult GetBrands(HttpContext context, [FromServices] ICatalogService service)
    {
        if (context.Request.Query.TryGetValue("id", out var idValues))
        {
            return int.TryParse(idValues[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                ? GetBrand(context, id, service)
                : WebApiResults.InvalidInt32Parameter(context, "Get");
        }

        var brands = service.GetCatalogBrands().Select(BrandDto.From).ToList();
        return WebApiResults.Negotiated(context, brands);
    }

    // GET api/brands/{id}: 200 brand or 404 with an empty body.
    private static IResult GetBrand(HttpContext context, int id, ICatalogService service)
    {
        var brand = service.GetCatalogBrands().FirstOrDefault(x => x.Id == id);
        return brand is null ? Results.NotFound() : WebApiResults.Negotiated(context, BrandDto.From(brand));
    }

    // DELETE api/brands/{id}: 404 for an unknown id, otherwise 200 with an empty body and nothing deleted
    // ("demo only - don't actually delete", INVENTORY Q3, reproduced on purpose).
    private static IResult DeleteBrand(int id, [FromServices] ICatalogService service)
    {
        var brandToDelete = service.GetCatalogBrands().FirstOrDefault(x => x.Id == id);
        return brandToDelete is null ? Results.NotFound() : Results.Ok();
    }

    // GET api/files: the legacy endpoint streamed the brand list as an NRBF (binary formatter) payload, an insecure
    // deserialisation sink for any client (INVENTORY Q2). Replaced on purpose with the same {Id, Brand} list as JSON.
    private static IResult GetFiles([FromServices] ICatalogService service)
    {
        var brands = service.GetCatalogBrands().Select(BrandDto.From).ToList();
        return Results.Json(brands, WebApiResults.JsonOptions);
    }
}
