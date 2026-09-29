using System.Net;
using eShopCatalog.Contracts;
using eShopCatalogApi.Client;

namespace eShopCatalogApi.Tests;

/// <summary>
/// Exercises every operation of the legacy WCF contract through the HTTP client,
/// asserting the same results the WCF service produced for the seeded data.
/// </summary>
public sealed class CatalogServiceClientTests : IDisposable
{
    private readonly CatalogApiFactory _factory = new();
    private readonly CatalogServiceClient _client;

    public CatalogServiceClientTests()
    {
        _client = new CatalogServiceClient(_factory.CreateClient());
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public void GetCatalogBrands_ReturnsSeededBrands()
    {
        var brands = _client.GetCatalogBrands();

        Assert.Equal(5, brands.Length);
        Assert.Contains(brands, b => b.Id == 2 && b.Brand == ".NET");
    }

    [Fact]
    public async Task GetCatalogTypes_ReturnsSeededTypes()
    {
        var types = await _client.GetCatalogTypesAsync();

        Assert.Equal(4, types.Length);
        Assert.Contains(types, t => t.Id == 1 && t.Type == "Mug");
    }

    [Theory]
    [InlineData(null, null, 12)]
    [InlineData(0, 0, 12)]
    [InlineData(2, 0, 6)]
    [InlineData(0, 1, 2)]
    [InlineData(5, 2, 4)]
    [InlineData(3, 0, 0)]
    public void GetCatalogItems_AppliesFiltersWithZeroMeaningAll(int? brand, int? type, int expected)
    {
        var items = _client.GetCatalogItems(brand, type);

        Assert.Equal(expected, items.Length);
        Assert.All(items, i =>
        {
            if (brand is > 0) Assert.Equal(brand, i.CatalogBrandId);
            if (type is > 0) Assert.Equal(type, i.CatalogTypeId);
        });
    }

    [Fact]
    public void FindCatalogItem_ReturnsItemWithBrandAndType()
    {
        var item = _client.FindCatalogItem(1);

        Assert.NotNull(item);
        Assert.Equal(".NET Bot Black Hoodie", item.Name);
        Assert.Equal(19.5M, item.Price);
        Assert.Equal(".NET", item.CatalogBrand?.Brand);
        Assert.Equal("T-Shirt", item.CatalogType?.Type);
    }

    [Fact]
    public void FindCatalogItem_ReturnsNullWhenMissing()
    {
        Assert.Null(_client.FindCatalogItem(999));
    }

    [Fact]
    public void GetAvailableStock_ReturnsStockForDayOrZero()
    {
        Assert.Equal(100, _client.GetAvailableStock(new DateTime(2017, 9, 20, 15, 30, 0), 1));
        Assert.Equal(0, _client.GetAvailableStock(new DateTime(2017, 9, 23), 1));
        Assert.Equal(0, _client.GetAvailableStock(new DateTime(2017, 9, 20), 999));
    }

    [Fact]
    public void CreateAvailableStock_OverwritesExistingEntryForSameDay()
    {
        _client.CreateAvailableStock(new CatalogItemsStock { CatalogItemId = 1, Date = new DateTime(2017, 9, 20, 8, 0, 0), AvailableStock = 7 });

        Assert.Equal(7, _client.GetAvailableStock(new DateTime(2017, 9, 20), 1));
        Assert.Equal(120, _client.GetAvailableStock(new DateTime(2017, 9, 21), 1));
    }

    [Fact]
    public void CreateAvailableStock_AddsNewEntry()
    {
        var localDate = new DateTime(2018, 1, 2, 23, 30, 0, DateTimeKind.Local);

        _client.CreateAvailableStock(new CatalogItemsStock { CatalogItemId = 3, Date = localDate, AvailableStock = 42 });

        Assert.Equal(42, _client.GetAvailableStock(new DateTime(2018, 1, 2), 3));
    }

    [Fact]
    public async Task CatalogItem_CreateUpdateRemove_RoundTrips()
    {
        var item = new CatalogItem
        {
            Name = "USB Stick",
            Description = "16GB USB Stick",
            Price = 9.99M,
            Picturefilename = "13.png",
            CatalogBrandId = 4,
            CatalogTypeId = 4,
        };

        await _client.CreateCatalogItemAsync(item);
        Assert.Equal(13, item.Id);

        var created = await _client.FindCatalogItemAsync(item.Id);
        Assert.NotNull(created);
        Assert.Equal("SQL Server", created.CatalogBrand?.Brand);

        created.Price = 7.25M;
        created.Name = "USB Stick (sale)";
        await _client.UpdateCatalogItemAsync(created);

        var updated = await _client.FindCatalogItemAsync(item.Id);
        Assert.Equal(7.25M, updated?.Price);
        Assert.Equal("USB Stick (sale)", updated?.Name);

        await _client.RemoveCatalogItemAsync(updated!);
        Assert.Null(await _client.FindCatalogItemAsync(item.Id));
    }

    [Fact]
    public void UpdateCatalogItem_ThrowsNotFoundForUnknownItem()
    {
        var ex = Assert.Throws<CatalogServiceException>(() =>
            _client.UpdateCatalogItem(new CatalogItem { Id = 999, Name = "x", CatalogBrandId = 1, CatalogTypeId = 1 }));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public void RemoveCatalogItem_ThrowsNotFoundForUnknownItem()
    {
        var ex = Assert.Throws<CatalogServiceException>(() => _client.RemoveCatalogItem(new CatalogItem { Id = 999 }));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public void GetDiscount_ReturnsActiveDiscountForDay()
    {
        var discount = _client.GetDiscount(new DateTime(2017, 9, 21, 18, 0, 0));

        Assert.NotNull(discount);
        Assert.Equal(0.3, discount.Size, 5);
        Assert.Equal(new DateTime(2017, 9, 18), discount.Start);
        Assert.Equal(new DateTime(2017, 9, 21), discount.End);
    }

    [Fact]
    public void GetDiscount_ReturnsNullWhenNoDiscountRuns()
    {
        Assert.Null(_client.GetDiscount(new DateTime(2018, 1, 1)));
    }
}
