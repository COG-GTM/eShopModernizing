using eShopCatalog.Contracts;
using eShopCatalogApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace eShopCatalogApi.Endpoints;

/// <summary>
/// One HTTP endpoint per operation of the legacy WCF <c>ICatalogService</c> contract.
/// Endpoint names match the WCF operation names.
/// </summary>
public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(string.Empty).WithTags("CatalogService");

        group.MapGet(CatalogRoutes.ItemById, FindCatalogItem).WithName(nameof(FindCatalogItem));
        group.MapGet(CatalogRoutes.Brands, GetCatalogBrands).WithName(nameof(GetCatalogBrands));
        group.MapGet(CatalogRoutes.Items, GetCatalogItems).WithName(nameof(GetCatalogItems));
        group.MapGet(CatalogRoutes.Types, GetCatalogTypes).WithName(nameof(GetCatalogTypes));
        group.MapGet(CatalogRoutes.ItemStock, GetAvailableStock).WithName(nameof(GetAvailableStock));
        group.MapPost(CatalogRoutes.Stock, CreateAvailableStock).WithName(nameof(CreateAvailableStock));
        group.MapPost(CatalogRoutes.Items, CreateCatalogItem).WithName(nameof(CreateCatalogItem));
        group.MapPut(CatalogRoutes.ItemById, UpdateCatalogItem).WithName(nameof(UpdateCatalogItem));
        group.MapDelete(CatalogRoutes.ItemById, RemoveCatalogItem).WithName(nameof(RemoveCatalogItem));
        group.MapGet(CatalogRoutes.Discount, GetDiscount).WithName(nameof(GetDiscount));

        return app;
    }

    public static async Task<Results<Ok<CatalogItem>, NotFound>> FindCatalogItem(
        int id, ICatalogService service, CancellationToken cancellationToken)
    {
        var item = await service.FindCatalogItemAsync(id, cancellationToken);
        return item is null ? TypedResults.NotFound() : TypedResults.Ok(item);
    }

    public static async Task<Ok<List<CatalogBrand>>> GetCatalogBrands(
        ICatalogService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetCatalogBrandsAsync(cancellationToken));

    public static async Task<Ok<List<CatalogItem>>> GetCatalogItems(
        ICatalogService service, CancellationToken cancellationToken, int? brandIdFilter = null, int? typeIdFilter = null) =>
        TypedResults.Ok(await service.GetCatalogItemsAsync(brandIdFilter ?? 0, typeIdFilter ?? 0, cancellationToken));

    public static async Task<Ok<List<CatalogType>>> GetCatalogTypes(
        ICatalogService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetCatalogTypesAsync(cancellationToken));

    public static async Task<Ok<int>> GetAvailableStock(
        int catalogItemId, DateTime date, ICatalogService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetAvailableStockAsync(date, catalogItemId, cancellationToken));

    public static async Task<NoContent> CreateAvailableStock(
        CatalogItemsStock catalogItemsStock, ICatalogService service, CancellationToken cancellationToken)
    {
        await service.CreateAvailableStockAsync(catalogItemsStock, cancellationToken);
        return TypedResults.NoContent();
    }

    public static async Task<CreatedAtRoute<CatalogItem>> CreateCatalogItem(
        CatalogItem catalogItem, ICatalogService service, CancellationToken cancellationToken)
    {
        await service.CreateCatalogItemAsync(catalogItem, cancellationToken);
        return TypedResults.CreatedAtRoute(catalogItem, nameof(FindCatalogItem), new { id = catalogItem.Id });
    }

    public static async Task<Results<NoContent, NotFound, BadRequest<string>>> UpdateCatalogItem(
        int id, CatalogItem catalogItem, ICatalogService service, CancellationToken cancellationToken)
    {
        if (catalogItem.Id != id)
        {
            return TypedResults.BadRequest("Route id does not match the catalog item id.");
        }

        return await service.UpdateCatalogItemAsync(catalogItem, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
    }

    public static async Task<Results<NoContent, NotFound>> RemoveCatalogItem(
        int id, ICatalogService service, CancellationToken cancellationToken) =>
        await service.RemoveCatalogItemAsync(id, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();

    public static async Task<Results<Ok<DiscountItem>, NotFound>> GetDiscount(
        DateTime day, ICatalogService service, CancellationToken cancellationToken)
    {
        var discount = await service.GetDiscountAsync(day, cancellationToken);
        return discount is null ? TypedResults.NotFound() : TypedResults.Ok(discount);
    }
}
