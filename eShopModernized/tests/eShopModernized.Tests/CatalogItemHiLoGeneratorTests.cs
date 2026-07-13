using System.Reflection;
using eShopCoreModernized.Models;

namespace eShopModernized.Tests;

/// <summary>
/// Tests for <see cref="CatalogItemHiLoGenerator"/>.
///
/// The generator's "cold" path (when its lo-id block is exhausted) issues a raw
/// <c>SELECT NEXT VALUE FOR catalog_hilo</c> against the DB connection, which
/// requires a real SQL Server and cannot be exercised with the EF Core in-memory
/// provider. The unit-testable behavior is the in-memory bookkeeping: once a
/// block has been reserved, the generator hands out <c>HiLoIncrement</c>
/// consecutive ids without touching the database.
///
/// To reach that "warm" path deterministically without a database, these tests
/// pre-seed the generator's private <c>sequenceId</c>/<c>remainningLoIds</c>
/// fields via reflection. In the warm path the <c>db</c> argument is never
/// dereferenced, so a null context is passed to prove no DB access occurs.
/// </summary>
public class CatalogItemHiLoGeneratorTests
{
    private const int HiLoIncrement = 10;

    private static void Prime(CatalogItemHiLoGenerator gen, int sequenceId, int remainingLoIds)
    {
        SetField(gen, "sequenceId", sequenceId);
        SetField(gen, "remainningLoIds", remainingLoIds);
    }

    private static void SetField(object target, string name, object value)
    {
        var field = typeof(CatalogItemHiLoGenerator)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static int GetField(object target, string name)
    {
        var field = typeof(CatalogItemHiLoGenerator)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return (int)field!.GetValue(target)!;
    }

    [Fact]
    public void GetNextSequenceValue_WarmBlock_IncrementsWithoutDatabaseAccess()
    {
        var gen = new CatalogItemHiLoGenerator();
        Prime(gen, sequenceId: 100, remainingLoIds: 5);

        // db is null: the warm path must not touch it.
        var value = gen.GetNextSequenceValue(db: null!);

        Assert.Equal(101, value);
        Assert.Equal(4, GetField(gen, "remainningLoIds"));
    }

    [Fact]
    public void GetNextSequenceValue_WarmBlock_HandsOutConsecutiveIds()
    {
        var gen = new CatalogItemHiLoGenerator();
        Prime(gen, sequenceId: 200, remainingLoIds: HiLoIncrement - 1);

        var issued = new List<int>();
        for (int i = 0; i < HiLoIncrement - 1; i++)
        {
            issued.Add(gen.GetNextSequenceValue(db: null!));
        }

        Assert.Equal(Enumerable.Range(201, HiLoIncrement - 1), issued);
        Assert.Equal(0, GetField(gen, "remainningLoIds"));
    }

    [Fact]
    public async Task GetNextSequenceValueAsync_WarmBlock_ReturnsNextId()
    {
        var gen = new CatalogItemHiLoGenerator();
        Prime(gen, sequenceId: 50, remainingLoIds: 3);

        var value = await gen.GetNextSequenceValueAsync(db: null!);

        Assert.Equal(51, value);
        Assert.Equal(2, GetField(gen, "remainningLoIds"));
    }

    [Fact]
    public void GetNextSequenceValue_ExhaustedWarmBlock_FallsBackToDatabase()
    {
        // With no remaining lo-ids the generator must consult the DB for a fresh
        // block. Passing a null context proves it takes the cold path (rather
        // than silently reusing stale bookkeeping) by throwing on DB access.
        var gen = new CatalogItemHiLoGenerator();
        Prime(gen, sequenceId: 100, remainingLoIds: 0);

        Assert.ThrowsAny<Exception>(() => gen.GetNextSequenceValue(db: null!));
    }

    [Fact]
    public void GetNextSequenceValue_ConcurrentWarmCalls_ProduceUniqueContiguousIds()
    {
        var gen = new CatalogItemHiLoGenerator();
        const int callCount = 500;
        Prime(gen, sequenceId: 1000, remainingLoIds: callCount);

        var results = new int[callCount];
        Parallel.For(0, callCount, i =>
        {
            results[i] = gen.GetNextSequenceValue(db: null!);
        });

        var distinct = results.Distinct().ToList();
        Assert.Equal(callCount, distinct.Count);
        Assert.Equal(Enumerable.Range(1001, callCount), distinct.OrderBy(x => x));
    }
}
