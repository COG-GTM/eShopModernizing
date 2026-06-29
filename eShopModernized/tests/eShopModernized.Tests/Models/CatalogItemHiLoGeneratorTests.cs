using eShopCoreModernized.Models;
using eShopModernized.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eShopModernized.Tests.Models
{
    public class CatalogItemHiLoGeneratorTests
    {
        private const int SeedValue = 1000;

        private static CatalogDBContext CreateContext(FakeSequenceConnection connection)
        {
            var options = new DbContextOptionsBuilder<CatalogDBContext>()
                .UseSqlite(connection)
                .Options;
            return new CatalogDBContext(options);
        }

        [Fact]
        public void GetNextSequenceValue_FirstCall_QueriesDatabaseForSequence()
        {
            var connection = new FakeSequenceConnection(SeedValue);
            using var db = CreateContext(connection);
            var generator = new CatalogItemHiLoGenerator();

            var value = generator.GetNextSequenceValue(db);

            Assert.Equal(SeedValue, value);
            Assert.Equal(1, connection.ExecuteScalarCount);
            Assert.Equal(1, connection.OpenCount);
            Assert.Contains("NEXT VALUE FOR catalog_hilo", connection.LastCommandText);
        }

        [Fact]
        public void GetNextSequenceValue_WithinBlock_IncrementsWithoutHittingDatabase()
        {
            var connection = new FakeSequenceConnection(SeedValue);
            using var db = CreateContext(connection);
            var generator = new CatalogItemHiLoGenerator();

            var first = generator.GetNextSequenceValue(db);
            var second = generator.GetNextSequenceValue(db);
            var third = generator.GetNextSequenceValue(db);

            Assert.Equal(SeedValue, first);
            Assert.Equal(SeedValue + 1, second);
            Assert.Equal(SeedValue + 2, third);
            // Only the first call should reach the database for the current HiLo block.
            Assert.Equal(1, connection.ExecuteScalarCount);
        }

        [Fact]
        public void GetNextSequenceValue_ExhaustsBlock_QueriesDatabaseAgain()
        {
            var connection = new FakeSequenceConnection(SeedValue);
            using var db = CreateContext(connection);
            var generator = new CatalogItemHiLoGenerator();

            // HiLoIncrement is 10: the 1st and 11th calls hit the DB.
            var values = Enumerable.Range(0, 11).Select(_ => generator.GetNextSequenceValue(db)).ToList();

            Assert.Equal(2, connection.ExecuteScalarCount);
            Assert.Equal(SeedValue, values[0]);
            Assert.Equal(SeedValue + 9, values[9]);
            Assert.Equal(SeedValue, values[10]);
        }

        [Fact]
        public async Task GetNextSequenceValueAsync_ReturnsSameSequenceAsSync()
        {
            var connection = new FakeSequenceConnection(SeedValue);
            using var db = CreateContext(connection);
            var generator = new CatalogItemHiLoGenerator();

            var first = await generator.GetNextSequenceValueAsync(db);
            var second = await generator.GetNextSequenceValueAsync(db);

            Assert.Equal(SeedValue, first);
            Assert.Equal(SeedValue + 1, second);
            Assert.Equal(1, connection.ExecuteScalarCount);
        }
    }
}
