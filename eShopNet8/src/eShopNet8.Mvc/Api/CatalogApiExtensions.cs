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

        // Web API 2 action selection also bound Delete(int id) from the query string (api/brands?id=1).
        brands.MapDelete("", DeleteBrandFromQuery);

        // Web API 2 answered any other verb on a matched controller with 405, an Allow header and an HttpError body.
        // These method-agnostic endpoints run only when no verb-specific endpoint above matches.
        brands.Map("", (HttpContext context) => context.Request.Query.ContainsKey("id")
            ? WebApiResults.MethodNotAllowed(context, "GET", "DELETE")
            : WebApiResults.MethodNotAllowed(context, "GET")).WithOrder(1);
        brands.Map("/{id}", (HttpContext context) => WebApiResults.MethodNotAllowed(context, "GET", "DELETE")).WithOrder(1);
        files.Map("", (HttpContext context) => WebApiResults.MethodNotAllowed(context, "GET")).WithOrder(1);
        files.Map("/{id}", (HttpContext context) => WebApiResults.MethodNotAllowed(context, "GET")).WithOrder(1);

        return app;
    }

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
        return brand is null ? WebApiResults.EmptyNotFound(context) : WebApiResults.Negotiated(context, BrandDto.From(brand));
    }

    // DELETE api/brands/{id}: 404 for an unknown id, otherwise 200 with an empty body and nothing deleted
    // ("demo only - don't actually delete", INVENTORY Q3, reproduced on purpose).
    private static IResult DeleteBrand(HttpContext context, int id, [FromServices] ICatalogService service)
    {
        var brandToDelete = service.GetCatalogBrands().FirstOrDefault(x => x.Id == id);
        return brandToDelete is null ? WebApiResults.EmptyNotFound(context) : Results.Ok();
    }

    // GET api/files: the legacy endpoint streamed the brand list as an NRBF (binary formatter) payload, an insecure
    // deserialisation sink for any client (INVENTORY Q2). Replaced on purpose with the same {Id, Brand} list as JSON.
    private static IResult DeleteBrandFromQuery(HttpContext context, [FromServices] ICatalogService service)
    {
        if (!context.Request.Query.TryGetValue("id", out var idValues))
        {
            return WebApiResults.MethodNotAllowed(context, "GET");
        }

        return int.TryParse(idValues[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? DeleteBrand(context, id, service)
            : WebApiResults.InvalidInt32Parameter(context, "Delete");
    }

    private static IResult GetFiles([FromServices] ICatalogService service)
    {
        var brands = service.GetCatalogBrands().Select(BrandDto.From).ToList();
        return Results.Json(brands, WebApiResults.JsonOptions);
    }
}
