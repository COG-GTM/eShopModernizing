using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using eShopModernized.Tests.Common;
using Xunit;

namespace eShopModernized.Tests.Services;

/// <summary>
/// Covers <see cref="CatalogService"/> create paths, which depend on the SQL
/// Server HiLo sequence (<c>SELECT NEXT VALUE FOR catalog_hilo</c>). These tests
/// require a reachable SQL Server (see <see cref="SqlServerFixture"/>) and skip
/// automatically when one is not available.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class CatalogServiceSqlTests
{
    private readonly SqlServerFixture _fixture;

    public CatalogServiceSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    private CatalogService CreateService() =>
        new(_fixture.CreateContext(), new CatalogItemHiLoGenerator());

    [SkippableFact]
    public void CreateCatalogItem_AssignsSequenceId_AndPersists()
    {
        Skip.IfNot(_fixture.Available, _fixture.SkipReason);

        var item = new CatalogItem
        {
            Name = "Sync Created",
            PictureFileName = "sync.png",
            CatalogBrandId = 1,
            CatalogTypeId = 1,
            Price = 12m
        };

        CreateService().CreateCatalogItem(item);

        Assert.True(item.Id > 0);
        using var context = _fixture.CreateContext();
        Assert.NotNull(context.CatalogItems.Find(item.Id));
    }

    [SkippableFact]
    public async Task CreateCatalogItemAsync_AssignsSequenceId_AndPersists()
    {
        Skip.IfNot(_fixture.Available, _fixture.SkipReason);

        var item = new CatalogItem
        {
            Name = "Async Created",
            PictureFileName = "async.png",
            CatalogBrandId = 2,
            CatalogTypeId = 2,
            Price = 24m
        };

        await CreateService().CreateCatalogItemAsync(item);

        Assert.True(item.Id > 0);
        using var context = _fixture.CreateContext();
        Assert.NotNull(await context.CatalogItems.FindAsync(item.Id));
    }
}
