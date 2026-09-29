using eShopCoreModernized.Models;
using eShopCoreModernized.Services;
using eShopCoreModernized.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace eShopCoreModernized.Tests.Services
{
    public sealed class CatalogServiceTests : IDisposable
    {
        private readonly CatalogDatabaseFixture database = new();
        private readonly CatalogItemHiLoGenerator generator = new();

        public void Dispose() => database.Dispose();

        private CatalogService CreateService() => new(database.CreateContext(), generator);

        [Fact]
        public void GetCatalogItemsPaginated_ReturnsFirstPageOrderedByIdWithMetadata()
        {
            using var service = CreateService();

            var page = service.GetCatalogItemsPaginated(pageSize: 5, pageIndex: 0);

            Assert.Equal(0, page.ActualPage);
            Assert.Equal(5, page.ItemsPerPage);
            Assert.Equal(CatalogDatabaseFixture.SeededItemCount, page.TotalItems);
            Assert.Equal(3, page.TotalPages);
            Assert.Equal(new[] { 101, 102, 103, 104, 105 }, page.Data.Select(i => i.Id));
        }

        [Fact]
        public async Task GetCatalogItemsPaginatedAsync_ReturnsFirstPageOrderedByIdWithMetadata()
        {
            using var service = CreateService();

            var page = await service.GetCatalogItemsPaginatedAsync(pageSize: 5, pageIndex: 0);

            Assert.Equal(0, page.ActualPage);
            Assert.Equal(5, page.ItemsPerPage);
            Assert.Equal(CatalogDatabaseFixture.SeededItemCount, page.TotalItems);
            Assert.Equal(3, page.TotalPages);
            Assert.Equal(new[] { 101, 102, 103, 104, 105 }, page.Data.Select(i => i.Id));
        }

        [Fact]
        public void GetCatalogItemsPaginated_SkipsPreviousPagesAndReturnsPartialLastPage()
        {
            using var service = CreateService();

            var page = service.GetCatalogItemsPaginated(pageSize: 5, pageIndex: 2);

            Assert.Equal(2, page.ActualPage);
            Assert.Equal(new[] { 111, 112 }, page.Data.Select(i => i.Id));
        }

        [Fact]
        public async Task GetCatalogItemsPaginatedAsync_PageBeyondEnd_ReturnsEmptyDataButKeepsTotals()
        {
            using var service = CreateService();

            var page = await service.GetCatalogItemsPaginatedAsync(pageSize: 5, pageIndex: 10);

            Assert.Empty(page.Data);
            Assert.Equal(CatalogDatabaseFixture.SeededItemCount, page.TotalItems);
            Assert.Equal(3, page.TotalPages);
        }

        [Fact]
        public async Task GetCatalogItemsPaginated_EagerLoadsBrandAndType()
        {
            using var service = CreateService();

            var sync = service.GetCatalogItemsPaginated(pageSize: 20, pageIndex: 0).Data.ToList();
            var async = (await service.GetCatalogItemsPaginatedAsync(pageSize: 20, pageIndex: 0)).Data.ToList();

            foreach (var item in sync.Concat(async))
            {
                Assert.NotNull(item.CatalogBrand);
                Assert.NotNull(item.CatalogType);
                Assert.Equal(item.CatalogBrandId, item.CatalogBrand!.Id);
                Assert.Equal(item.CatalogTypeId, item.CatalogType!.Id);
            }
        }

        [Fact]
        public void GetCatalogItemsPaginated_EmptyCatalog_ReturnsZeroTotals()
        {
            using var emptyDatabase = new CatalogDatabaseFixture(seed: false);
            using var service = new CatalogService(emptyDatabase.CreateContext(), generator);

            var page = service.GetCatalogItemsPaginated(pageSize: 10, pageIndex: 0);

            Assert.Empty(page.Data);
            Assert.Equal(0, page.TotalItems);
            Assert.Equal(0, page.TotalPages);
        }

        [Fact]
        public void FindCatalogItem_ExistingId_ReturnsItemWithBrandAndType()
        {
            using var service = CreateService();

            var item = service.FindCatalogItem(104);

            Assert.NotNull(item);
            Assert.Equal("Item 104", item!.Name);
            Assert.Equal(104.5m, item.Price);
            Assert.Equal(".NET", item.CatalogBrand?.Brand);
            Assert.Equal("T-Shirt", item.CatalogType?.Type);
        }

        [Fact]
        public async Task FindCatalogItemAsync_ExistingId_ReturnsItemWithBrandAndType()
        {
            using var service = CreateService();

            var item = await service.FindCatalogItemAsync(105);

            Assert.NotNull(item);
            Assert.Equal("Item 105", item!.Name);
            Assert.Equal("Azure", item.CatalogBrand?.Brand);
            Assert.Equal("Mug", item.CatalogType?.Type);
        }

        [Fact]
        public async Task FindCatalogItem_UnknownId_ReturnsNull()
        {
            using var service = CreateService();

            Assert.Null(service.FindCatalogItem(999));
            Assert.Null(await service.FindCatalogItemAsync(999));
        }

        [Fact]
        public async Task GetCatalogBrands_ReturnsAllBrands()
        {
            using var service = CreateService();

            Assert.Equal(new[] { "Azure", ".NET" }, service.GetCatalogBrands().OrderBy(b => b.Id).Select(b => b.Brand));
            Assert.Equal(new[] { "Azure", ".NET" }, (await service.GetCatalogBrandsAsync()).OrderBy(b => b.Id).Select(b => b.Brand));
        }

        [Fact]
        public async Task GetCatalogTypes_ReturnsAllTypes()
        {
            using var service = CreateService();

            Assert.Equal(new[] { "Mug", "T-Shirt" }, service.GetCatalogTypes().OrderBy(t => t.Id).Select(t => t.Type));
            Assert.Equal(new[] { "Mug", "T-Shirt" }, (await service.GetCatalogTypesAsync()).OrderBy(t => t.Id).Select(t => t.Type));
        }

        [Fact]
        public void CreateCatalogItem_AssignsHiLoIdAndPersists()
        {
            var item = NewItem("Created sync");

            using (var service = CreateService())
            {
                service.CreateCatalogItem(item);
            }

            Assert.Equal(HiLoSqliteConnection.SequenceStart, item.Id);
            using var verify = database.CreateContext();
            var stored = verify.CatalogItems.Single(i => i.Id == item.Id);
            Assert.Equal("Created sync", stored.Name);
            Assert.Equal(CatalogDatabaseFixture.SeededItemCount + 1, verify.CatalogItems.Count());
        }

        [Fact]
        public async Task CreateCatalogItemAsync_AssignsHiLoIdAndPersists()
        {
            var item = NewItem("Created async");

            using (var service = CreateService())
            {
                await service.CreateCatalogItemAsync(item);
            }

            Assert.Equal(HiLoSqliteConnection.SequenceStart, item.Id);
            using var verify = database.CreateContext();
            var stored = await verify.CatalogItems.SingleAsync(i => i.Id == item.Id);
            Assert.Equal("Created async", stored.Name);
        }

        [Fact]
        public void CreateCatalogItem_OverwritesCallerSuppliedId()
        {
            var item = NewItem("Has id");
            item.Id = 5000;

            using var service = CreateService();
            service.CreateCatalogItem(item);

            Assert.Equal(HiLoSqliteConnection.SequenceStart, item.Id);
        }

        [Fact]
        public async Task CreateCatalogItem_ConsecutiveCreatesUseSameHiLoBlock()
        {
            var first = NewItem("First");
            var second = NewItem("Second");

            using (var service = CreateService())
            {
                service.CreateCatalogItem(first);
            }
            using (var service = CreateService())
            {
                await service.CreateCatalogItemAsync(second);
            }

            Assert.Equal(first.Id + 1, second.Id);
            Assert.Equal(1, database.Connection.SequenceRequests);
        }

        [Fact]
        public void UpdateCatalogItem_DetachedEntity_PersistsAllScalarChanges()
        {
            var edited = LoadDetached(106);
            edited.Name = "Renamed";
            edited.Price = 42.25m;
            edited.AvailableStock = 7;
            edited.OnReorder = true;
            edited.CatalogBrandId = 1;

            using (var service = CreateService())
            {
                service.UpdateCatalogItem(edited);
            }

            using var verify = database.CreateContext();
            var stored = verify.CatalogItems.Include(i => i.CatalogBrand).Single(i => i.Id == 106);
            Assert.Equal("Renamed", stored.Name);
            Assert.Equal(42.25m, stored.Price);
            Assert.Equal(7, stored.AvailableStock);
            Assert.True(stored.OnReorder);
            Assert.Equal("Azure", stored.CatalogBrand?.Brand);
            Assert.Equal(CatalogDatabaseFixture.SeededItemCount, verify.CatalogItems.Count());
        }

        [Fact]
        public async Task UpdateCatalogItemAsync_DetachedEntity_PersistsChanges()
        {
            var edited = LoadDetached(107);
            edited.Description = "Updated description";

            using (var service = CreateService())
            {
                await service.UpdateCatalogItemAsync(edited);
            }

            using var verify = database.CreateContext();
            Assert.Equal("Updated description", (await verify.CatalogItems.SingleAsync(i => i.Id == 107)).Description);
        }

        [Fact]
        public void UpdateCatalogItem_UnknownId_ThrowsConcurrencyException()
        {
            var ghost = NewItem("Ghost");
            ghost.Id = 999;

            using var service = CreateService();

            Assert.Throws<DbUpdateConcurrencyException>(() => service.UpdateCatalogItem(ghost));
        }

        [Fact]
        public void RemoveCatalogItem_ItemFromFind_DeletesRow()
        {
            using (var service = CreateService())
            {
                var item = service.FindCatalogItem(108);
                service.RemoveCatalogItem(item!);
            }

            using var verify = database.CreateContext();
            Assert.False(verify.CatalogItems.Any(i => i.Id == 108));
            Assert.Equal(CatalogDatabaseFixture.SeededItemCount - 1, verify.CatalogItems.Count());
            Assert.Equal(2, verify.CatalogBrands.Count());
            Assert.Equal(2, verify.CatalogTypes.Count());
        }

        [Fact]
        public async Task RemoveCatalogItemAsync_ItemFromFind_DeletesRow()
        {
            using (var service = CreateService())
            {
                var item = await service.FindCatalogItemAsync(109);
                await service.RemoveCatalogItemAsync(item!);
            }

            using var verify = database.CreateContext();
            Assert.False(await verify.CatalogItems.AnyAsync(i => i.Id == 109));
        }

        [Fact]
        public void Dispose_DisposesUnderlyingContext()
        {
            var context = database.CreateContext();
            var service = new CatalogService(context, generator);

            service.Dispose();

            Assert.Throws<ObjectDisposedException>(() => context.CatalogItems.Count());
        }

        private CatalogItem LoadDetached(int id)
        {
            using var context = database.CreateContext();
            return context.CatalogItems.AsNoTracking().Single(i => i.Id == id);
        }

        private static CatalogItem NewItem(string name) => new()
        {
            Name = name,
            Price = 9.99m,
            CatalogBrandId = 1,
            CatalogTypeId = 1,
            AvailableStock = 10,
        };
    }
}
