using eShopCoreModernized.Models;
using eShopCoreModernized.Tests.Infrastructure;

namespace eShopCoreModernized.Tests.Models
{
    public sealed class CatalogItemHiLoGeneratorTests : IDisposable
    {
        private readonly CatalogDatabaseFixture database = new(seed: false);

        public void Dispose() => database.Dispose();

        [Fact]
        public void GetNextSequenceValue_FirstCall_ReturnsDatabaseSequenceValue()
        {
            var generator = new CatalogItemHiLoGenerator();
            using var context = database.CreateContext();

            Assert.Equal(HiLoSqliteConnection.SequenceStart, generator.GetNextSequenceValue(context));
            Assert.Equal(1, database.Connection.SequenceRequests);
        }

        [Fact]
        public void GetNextSequenceValue_HandsOutBlockOfTenBeforeQueryingAgain()
        {
            var generator = new CatalogItemHiLoGenerator();
            using var context = database.CreateContext();

            var ids = Enumerable.Range(0, 10).Select(_ => generator.GetNextSequenceValue(context)).ToList();

            Assert.Equal(Enumerable.Range(1, 10), ids);
            Assert.Equal(1, database.Connection.SequenceRequests);
        }

        [Fact]
        public void GetNextSequenceValue_ExhaustedBlock_FetchesNextHiValue()
        {
            var generator = new CatalogItemHiLoGenerator();
            using var context = database.CreateContext();

            var ids = Enumerable.Range(0, 21).Select(_ => generator.GetNextSequenceValue(context)).ToList();

            Assert.Equal(Enumerable.Range(1, 21), ids);
            Assert.Equal(3, database.Connection.SequenceRequests);
        }

        [Fact]
        public async Task GetNextSequenceValueAsync_SharesStateWithSyncOverload()
        {
            var generator = new CatalogItemHiLoGenerator();
            using var context = database.CreateContext();

            var first = await generator.GetNextSequenceValueAsync(context);
            var second = generator.GetNextSequenceValue(context);
            var third = await generator.GetNextSequenceValueAsync(context);

            Assert.Equal(new[] { 1, 2, 3 }, new[] { first, second, third });
            Assert.Equal(1, database.Connection.SequenceRequests);
        }

        [Fact]
        public void GetNextSequenceValue_SeparateGenerators_ReserveDistinctBlocks()
        {
            var generatorA = new CatalogItemHiLoGenerator();
            var generatorB = new CatalogItemHiLoGenerator();
            using var context = database.CreateContext();

            var a = generatorA.GetNextSequenceValue(context);
            var b = generatorB.GetNextSequenceValue(context);
            var a2 = generatorA.GetNextSequenceValue(context);

            Assert.Equal(1, a);
            Assert.Equal(11, b);
            Assert.Equal(2, a2);
        }

        [Fact]
        public async Task GetNextSequenceValueAsync_ConcurrentCallers_NeverReceiveDuplicateIds()
        {
            var generator = new CatalogItemHiLoGenerator();
            using var context = database.CreateContext();

            var ids = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => generator.GetNextSequenceValueAsync(context)));

            Assert.Equal(50, ids.Distinct().Count());
            Assert.Equal(Enumerable.Range(1, 50), ids.OrderBy(i => i));
            Assert.Equal(5, database.Connection.SequenceRequests);
        }
    }
}
