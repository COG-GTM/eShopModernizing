using eShopCoreModernized.Models;
using eShopModernized.Tests.Common;
using Xunit;

namespace eShopModernized.Tests.Models;

/// <summary>
/// <see cref="CatalogItemHiLoGenerator"/> issues the T-SQL statement
/// <c>SELECT NEXT VALUE FOR catalog_hilo</c>, which only runs against SQL Server.
/// These tests use the <see cref="SqlServerFixture"/> and skip when no server is
/// reachable.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class CatalogItemHiLoGeneratorTests
{
    private readonly SqlServerFixture _fixture;

    public CatalogItemHiLoGeneratorTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public void GetNextSequenceValue_ReturnsMonotonicallyIncreasingValues()
    {
        Skip.IfNot(_fixture.Available, _fixture.SkipReason);

        var generator = new CatalogItemHiLoGenerator();
        using var context = _fixture.CreateContext();

        // First call reads the sequence from the database; subsequent calls are
        // served from the in-memory HiLo block (the "else" branch).
        var first = generator.GetNextSequenceValue(context);
        var second = generator.GetNextSequenceValue(context);
        var third = generator.GetNextSequenceValue(context);

        Assert.True(first > 0);
        Assert.Equal(first + 1, second);
        Assert.Equal(second + 1, third);
    }

    [SkippableFact]
    public async Task GetNextSequenceValueAsync_ReturnsValue()
    {
        Skip.IfNot(_fixture.Available, _fixture.SkipReason);

        var generator = new CatalogItemHiLoGenerator();
        using var context = _fixture.CreateContext();

        var value = await generator.GetNextSequenceValueAsync(context);

        Assert.True(value > 0);
    }
}
