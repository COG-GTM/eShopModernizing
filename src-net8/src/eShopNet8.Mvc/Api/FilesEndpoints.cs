using eShop.Catalog.Infrastructure;
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
            endpoints.MapGet("/api/files", Get).WithLegacyNoCacheHeaders();
            // Web API 2's DefaultApi route (api/{controller}/{id}) still selected the parameterless Get() when an id
            // segment was present, so /api/files/{anything} returned the same payload.
            endpoints.MapGet("/api/files/{id}", Get).WithLegacyNoCacheHeaders();
            return endpoints;
        }

        internal static IResult Get(ICatalogService service, IConfiguration configuration)
        {
            var brands = service.GetCatalogBrands()
                .Select(b => new BrandDto
                {
                    Id = b.Id,
                    Brand = b.Brand
                }).ToList();

            // EF6's materialised query is an unsized source for ToList(); the mock catalog's list is sized.
            int? capacity = configuration.GetCatalogSettings().UseMockData ? null : BrandDtoNrbfWriter.GrownCapacity(brands.Count);
            return Results.Bytes(BrandDtoNrbfWriter.Serialize(brands, capacity), ContentType);
        }
    }
}
