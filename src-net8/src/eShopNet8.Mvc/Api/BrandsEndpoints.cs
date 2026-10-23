using eShop.Catalog.Core.Services;

namespace eShopNet8.Mvc.Api
{
    /// <summary>Port of legacy Web API 2 <c>BrandsController</c> (route <c>api/{controller}/{id}</c>).</summary>
    public static class BrandsEndpoints
    {
        public static IEndpointRouteBuilder MapBrandsApi(this IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/brands").WithLegacyNoCacheHeaders();
            group.MapGet("/", GetAll);
            group.MapGet("/{id}", Get);
            group.MapDelete("/{id}", Delete);
            return endpoints;
        }

        internal static IResult GetAll(HttpRequest request, ICatalogService service)
        {
            var brands = service.GetCatalogBrands();
            return Negotiated(request, () => LegacyBrandXml.Serialize(brands), brands);
        }

        // Legacy binds `int id`; a non-int segment fails binding and Web API 2 answers 400.
        internal static IResult Get(string id, HttpRequest request, ICatalogService service)
        {
            if (!int.TryParse(id, out var brandId))
            {
                return Results.BadRequest();
            }

            var brand = service.GetCatalogBrands().FirstOrDefault(x => x.Id == brandId);
            if (brand == null)
            {
                return Results.NotFound();
            }

            return Negotiated(request, () => LegacyBrandXml.Serialize(brand), brand);
        }

        // Web API 2 content negotiation: JSON by default, XML (DataContractSerializer) when the Accept header prefers it.
        private static IResult Negotiated<T>(HttpRequest request, Func<byte[]> xml, T value)
        {
            var (format, mediaType) = LegacyContentNegotiator.Negotiate(request);
            var contentType = mediaType + "; charset=utf-8";
            return format == LegacyApiFormat.Xml
                ? Results.Bytes(xml(), contentType)
                : Results.Json(value, CatalogApiJson.Options, contentType);
        }

        // Demo only, as in legacy: nothing is deleted. Legacy returned an HttpResponseMessage without content,
        // so the host's default text/html Content-Type went out with the empty 200 body.
        internal static IResult Delete(string id, ICatalogService service)
        {
            if (!int.TryParse(id, out var brandId))
            {
                return Results.BadRequest();
            }

            var brandToDelete = service.GetCatalogBrands().FirstOrDefault(x => x.Id == brandId);
            if (brandToDelete == null)
            {
                return Results.NotFound();
            }

            return Results.Text(string.Empty, "text/html", statusCode: StatusCodes.Status200OK);
        }
    }
}
