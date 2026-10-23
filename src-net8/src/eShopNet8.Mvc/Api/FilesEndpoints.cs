using eShop.Catalog.Core.Services;

namespace eShopNet8.Mvc.Api
{
    /// <summary>
    /// Port of legacy Web API 2 <c>FilesController</c>: streams the brands as a BinaryFormatter (MS-NRBF) payload of
    /// <c>List&lt;FilesController+BrandDTO&gt;</c>. BinaryFormatter is disabled in .NET 8, so the bytes come from
    /// <see cref="BrandDtoNrbfWriter"/> instead.
    /// </summary>
    public static class FilesEndpoints
    {
        public const string ContentType = "application/octet-stream";

        public static IEndpointRouteBuilder MapFilesApi(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/files", Get);
            // Web API 2's DefaultApi route (api/{controller}/{id}) still selected the parameterless Get() when an id
            // segment was present, so /api/files/{anything} returned the same payload.
            endpoints.MapGet("/api/files/{id}", Get);
            return endpoints;
        }

        internal static IResult Get(ICatalogService service)
        {
            var brands = service.GetCatalogBrands()
                .Select(b => new BrandDto
                {
                    Id = b.Id,
                    Brand = b.Brand
                }).ToList();

            return Results.Bytes(BrandDtoNrbfWriter.Serialize(brands), ContentType);
        }
    }
}
