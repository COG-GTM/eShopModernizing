using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Persistence;
using eShop.Catalog.Data.Seeding;
using eShop.Catalog.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static eShop.Catalog.Data.Tests.S1_Data.Integration.SqlServerFixture;

namespace eShop.Catalog.Data.Tests.S1_Data.Integration;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class SqlServerCatalogTests
{
    private readonly SqlServerFixture sql;

    public SqlServerCatalogTests(SqlServerFixture sql)
    {
        this.sql = sql;
    }

    private async Task<(ServiceProvider App, string ConnectionString)> StartedAppAsync()
    {
        var connectionString = sql.NewDatabaseConnectionString();
        var app = BuildApp(connectionString);
        await StartAsync(app);
        return (app, connectionString);
    }

    [Fact]
    public async Task App_start_on_empty_database_migrates_and_seeds_legacy_data()
    {
        var (app, cs) = await StartedAppAsync();
        await using var _ = app;

        Assert.Equal(new[] { "20261111211428_InitialCatalogSchema" }, await RowsAsync(cs, "SELECT MigrationId FROM dbo.__EFMigrationsHistory"));
        Assert.Equal(new[] { "1|Mug", "2|T-Shirt", "3|Sheet", "4|USB Memory Stick" }, await RowsAsync(cs, "SELECT Id, Type FROM dbo.CatalogType ORDER BY Id"));
        Assert.Equal(new[] { "1|Azure", "2|.NET", "3|Visual Studio", "4|SQL Server", "5|Other" }, await RowsAsync(cs, "SELECT Id, Brand FROM dbo.CatalogBrand ORDER BY Id"));

        var expected = PreconfiguredData.GetPreconfiguredCatalogItems()
            .Select(i => FormattableString.Invariant($"{i.Id}|{i.Name}|{i.Price:0.00}|{i.PictureFileName}|{i.CatalogTypeId}|{i.CatalogBrandId}|{i.AvailableStock}"));
        Assert.Equal(expected, await RowsAsync(cs, "SELECT Id, Name, Price, PictureFileName, CatalogTypeId, CatalogBrandId, AvailableStock FROM dbo.Catalog ORDER BY Id"));
    }

    [Fact]
    public async Task Schema_matches_legacy_ef6_tables_columns_and_constraint_names()
    {
        var (app, cs) = await StartedAppAsync();
        await using var _ = app;

        Assert.Equal(
            new[]
            {
                "Catalog|AvailableStock|int|NO|", "Catalog|CatalogBrandId|int|NO|", "Catalog|CatalogTypeId|int|NO|",
                "Catalog|Description|nvarchar|YES|-1", "Catalog|Id|int|NO|", "Catalog|MaxStockThreshold|int|NO|",
                "Catalog|Name|nvarchar|NO|50", "Catalog|OnReorder|bit|NO|", "Catalog|PictureFileName|nvarchar|NO|-1",
                "Catalog|Price|decimal|NO|", "Catalog|RestockThreshold|int|NO|",
                "CatalogBrand|Brand|nvarchar|NO|100", "CatalogBrand|Id|int|NO|",
                "CatalogType|Id|int|NO|", "CatalogType|Type|nvarchar|NO|100",
            },
            await RowsAsync(cs, """
                SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME IN ('Catalog','CatalogBrand','CatalogType') ORDER BY TABLE_NAME, COLUMN_NAME
                """));
        Assert.Equal(new[] { "18|2" }, await RowsAsync(cs, "SELECT NUMERIC_PRECISION, NUMERIC_SCALE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Catalog' AND COLUMN_NAME='Price'"));
        Assert.Equal(
            new[] { "FK_dbo.Catalog_dbo.CatalogBrand_CatalogBrandId|CASCADE", "FK_dbo.Catalog_dbo.CatalogType_CatalogTypeId|CASCADE" },
            await RowsAsync(cs, "SELECT name, delete_referential_action_desc FROM sys.foreign_keys ORDER BY name"));
        Assert.Equal(
            new[] { "PK_dbo.Catalog", "PK_dbo.CatalogBrand", "PK_dbo.CatalogType" },
            await RowsAsync(cs, "SELECT name FROM sys.key_constraints WHERE type = 'PK' AND OBJECT_NAME(parent_object_id) <> '__EFMigrationsHistory' ORDER BY name"));
        Assert.Equal(
            new[] { "catalog_brand_hilo|1|10|bigint", "catalog_hilo|1|10|bigint", "catalog_type_hilo|1|10|bigint" },
            await RowsAsync(cs, "SELECT s.name, s.start_value, s.increment, t.name FROM sys.sequences s JOIN sys.types t ON t.user_type_id = s.user_type_id ORDER BY s.name"));
    }

    [Fact]
    public async Task Item_created_in_same_process_after_seeding_gets_id_13_then_14()
    {
        var (app, _) = await StartedAppAsync();
        await using var __ = app;

        int first, second;
        using (var scope = app.CreateScope())
        {
            var item = new CatalogItem { Name = "Lip Glide", Price = 39M, PictureFileName = "13.png", CatalogBrandId = 5, CatalogTypeId = 1 };
            scope.ServiceProvider.GetRequiredService<ICatalogService>().CreateCatalogItem(item);
            first = item.Id;
        }

        using (var scope = app.CreateScope())
        {
            var item = new CatalogItem { Name = "Glow Drops", Price = 49M, PictureFileName = "14.png", CatalogBrandId = 5, CatalogTypeId = 1 };
            scope.ServiceProvider.GetRequiredService<ICatalogService>().CreateCatalogItem(item);
            second = item.Id;
        }

        Assert.Equal((13, 14), (first, second));
    }

    [Fact]
    public async Task New_process_on_seeded_database_draws_the_next_hilo_block()
    {
        var (app, cs) = await StartedAppAsync();
        await app.DisposeAsync();

        // EF Core caches HiLo state in its internal service provider (shared per process); disabling that cache
        // gives this context the state a freshly started process would have.
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(cs).EnableServiceProviderCaching(false).Options;
        await using var db = new CatalogDbContext(options);
        var item = new CatalogItem { Name = "After restart", PictureFileName = "x.png", CatalogBrandId = 1, CatalogTypeId = 1 };
        new CatalogService(db).CreateCatalogItem(item);

        Assert.Equal(21, item.Id);
        Assert.Equal(13, await ScalarAsync<int>(cs, "SELECT COUNT(*) FROM dbo.Catalog"));
    }

    [Fact]
    public async Task Get_catalog_items_paginated_10_0_returns_first_ten_by_id_with_total_12()
    {
        var (app, _) = await StartedAppAsync();
        await using var __ = app;
        using var scope = app.CreateScope();

        var page = scope.ServiceProvider.GetRequiredService<ICatalogService>().GetCatalogItemsPaginated(10, 0);

        Assert.Equal((12L, 2, 10), (page.TotalItems, page.TotalPages, page.Data.Count()));
        Assert.Equal(Enumerable.Range(1, 10), page.Data.Select(i => i.Id));
        Assert.Equal(".NET Bot Black Hoodie", page.Data.First().Name);
        Assert.All(page.Data, i => Assert.NotNull(i.CatalogBrand));
        Assert.All(page.Data, i => Assert.NotNull(i.CatalogType));
    }

    [Fact]
    public async Task Crud_round_trip()
    {
        var (app, cs) = await StartedAppAsync();
        await using var _ = app;
        int id;

        using (var scope = app.CreateScope())
        {
            var item = new CatalogItem
            {
                Name = "Hydra Serum", Description = "30ml", Price = 89.95M, PictureFileName = "serum.png",
                CatalogBrandId = 3, CatalogTypeId = 2, AvailableStock = 4, RestockThreshold = 1, MaxStockThreshold = 10, OnReorder = true,
            };
            scope.ServiceProvider.GetRequiredService<ICatalogService>().CreateCatalogItem(item);
            id = item.Id;
        }

        using (var scope = app.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ICatalogService>();
            var read = service.FindCatalogItem(id)!;
            Assert.Equal(("Hydra Serum", "30ml", 89.95M, "Visual Studio", "T-Shirt", 4, true),
                (read.Name, read.Description, read.Price, read.CatalogBrand!.Brand, read.CatalogType!.Type, read.AvailableStock, read.OnReorder));

            service.UpdateCatalogItem(new CatalogItem
            {
                Id = id, Name = "Hydra Serum XL", Description = "50ml", Price = 120M, PictureFileName = "serum.png",
                CatalogBrandId = 4, CatalogTypeId = 3, AvailableStock = 9,
            });
        }

        using (var scope = app.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ICatalogService>();
            var updated = service.FindCatalogItem(id)!;
            Assert.Equal(("Hydra Serum XL", 120M, 4, 3, 9, false), (updated.Name, updated.Price, updated.CatalogBrandId, updated.CatalogTypeId, updated.AvailableStock, updated.OnReorder));

            service.RemoveCatalogItem(updated);
        }

        using (var scope = app.CreateScope())
        {
            Assert.Null(scope.ServiceProvider.GetRequiredService<ICatalogService>().FindCatalogItem(id));
        }

        Assert.Equal(12, await ScalarAsync<int>(cs, "SELECT COUNT(*) FROM dbo.Catalog"));
    }

    [Theory]
    [InlineData("19.5", "19.50")]
    [InlineData("1234567890123456.78", "1234567890123456.78")]
    [InlineData("0.01", "0.01")]
    [InlineData("12.345", "12.35")]
    [InlineData("12.344", "12.34")]
    public async Task Price_is_stored_as_decimal_18_2(string price, string expected)
    {
        var (app, cs) = await StartedAppAsync();
        await using var _ = app;
        int id;
        using (var scope = app.CreateScope())
        {
            var item = new CatalogItem { Name = "Priced", Price = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture), PictureFileName = "p.png", CatalogBrandId = 1, CatalogTypeId = 1 };
            scope.ServiceProvider.GetRequiredService<ICatalogService>().CreateCatalogItem(item);
            id = item.Id;
        }

        Assert.Equal(new[] { expected }, await RowsAsync(cs, $"SELECT Price FROM dbo.Catalog WHERE Id = {id}"));
        using (var scope = app.CreateScope())
        {
            Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
                scope.ServiceProvider.GetRequiredService<ICatalogService>().FindCatalogItem(id)!.Price);
        }
    }

    [Fact]
    public async Task Price_overflowing_decimal_18_2_is_rejected()
    {
        var (app, _) = await StartedAppAsync();
        await using var __ = app;
        using var scope = app.CreateScope();
        var item = new CatalogItem { Name = "Too pricey", Price = 12345678901234567M, PictureFileName = "p.png", CatalogBrandId = 1, CatalogTypeId = 1 };

        Assert.ThrowsAny<Exception>(() => scope.ServiceProvider.GetRequiredService<ICatalogService>().CreateCatalogItem(item));
    }

    [Fact]
    public async Task Name_is_limited_to_50_characters()
    {
        var (app, cs) = await StartedAppAsync();
        await using var _ = app;

        using (var scope = app.CreateScope())
        {
            var ok = new CatalogItem { Name = new string('a', 50), PictureFileName = "p.png", CatalogBrandId = 1, CatalogTypeId = 1 };
            scope.ServiceProvider.GetRequiredService<ICatalogService>().CreateCatalogItem(ok);
            Assert.Equal(50, (await ScalarAsync<string>(cs, $"SELECT Name FROM dbo.Catalog WHERE Id = {ok.Id}")).Length);
        }

        using (var scope = app.CreateScope())
        {
            var tooLong = new CatalogItem { Name = new string('b', 51), PictureFileName = "p.png", CatalogBrandId = 1, CatalogTypeId = 1 };
            var ex = Assert.Throws<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<ICatalogService>().CreateCatalogItem(tooLong));
            Assert.Contains("truncated", ex.InnerException!.Message);
        }

        Assert.Equal(13, await ScalarAsync<int>(cs, "SELECT COUNT(*) FROM dbo.Catalog"));
    }

    [Fact]
    public async Task Restart_is_idempotent_and_does_not_reseed()
    {
        var (app, cs) = await StartedAppAsync();
        await app.DisposeAsync();

        await using var restarted = BuildApp(cs);
        await StartAsync(restarted);

        Assert.Equal(new[] { "12|5|4" }, await RowsAsync(cs, "SELECT (SELECT COUNT(*) FROM dbo.Catalog), (SELECT COUNT(*) FROM dbo.CatalogBrand), (SELECT COUNT(*) FROM dbo.CatalogType)"));
    }

    [Theory]
    [InlineData("DROP TABLE dbo.__EFMigrationsHistory")]
    [InlineData("DELETE FROM dbo.__EFMigrationsHistory")]
    public async Task Existing_ef6_database_is_adopted_without_recreating_tables_or_reseeding(string removeHistory)
    {
        var (app, cs) = await StartedAppAsync();
        await app.DisposeAsync();
        // Legacy EF6 databases have the same schema and data but no EF Core history (EF6 used dbo.__MigrationHistory).
        // An empty history table is what an adoption interrupted between creating the table and inserting the row leaves.
        await ScalarAsync<int>(cs, $"{removeHistory}; UPDATE dbo.Catalog SET Name = N'Edited in legacy' WHERE Id = 1; SELECT 0");

        await using var restarted = BuildApp(cs);
        await StartAsync(restarted);

        Assert.Equal(new[] { "20261111211428_InitialCatalogSchema" }, await RowsAsync(cs, "SELECT MigrationId FROM dbo.__EFMigrationsHistory"));
        Assert.Equal(new[] { "12|Edited in legacy" }, await RowsAsync(cs, "SELECT (SELECT COUNT(*) FROM dbo.Catalog), (SELECT Name FROM dbo.Catalog WHERE Id = 1)"));
    }

    [Fact]
    public async Task Partial_legacy_schema_is_refused()
    {
        var cs = sql.NewDatabaseConnectionString();
        var master = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs) { InitialCatalog = "master" }.ConnectionString;
        var database = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs).InitialCatalog;
        await ScalarAsync<int>(master, $"CREATE DATABASE [{database}]; SELECT 0");
        await ScalarAsync<int>(cs, "CREATE TABLE dbo.CatalogBrand (Id int NOT NULL PRIMARY KEY, Brand nvarchar(100) NOT NULL); SELECT 0");
        await using var app = BuildApp(cs);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(app));

        Assert.Contains("1 of 3 tables", ex.Message);
    }

    [Fact]
    public async Task Two_apps_starting_concurrently_on_one_database_seed_exactly_once()
    {
        var cs = sql.NewDatabaseConnectionString();
        await using var mvc = BuildApp(cs);
        await using var webForms = BuildApp(cs);

        await Task.WhenAll(StartAsync(mvc), StartAsync(webForms));

        Assert.Equal(new[] { "12|5|4" }, await RowsAsync(cs, "SELECT (SELECT COUNT(*) FROM dbo.Catalog), (SELECT COUNT(*) FROM dbo.CatalogBrand), (SELECT COUNT(*) FROM dbo.CatalogType)"));
        Assert.Equal(Enumerable.Range(1, 12).Select(i => i.ToString()), await RowsAsync(cs, "SELECT Id FROM dbo.Catalog ORDER BY Id"));
    }

    [Fact]
    public async Task Initializer_holds_sp_getapplock_while_migrating_and_seeding()
    {
        var cs = sql.NewDatabaseConnectionString();
        await using var app = BuildApp(cs);
        var database = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs).InitialCatalog;
        var master = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs) { InitialCatalog = "master" }.ConnectionString;

        await using var holder = new Microsoft.Data.SqlClient.SqlConnection(master);
        await holder.OpenAsync();
        await using (var take = new Microsoft.Data.SqlClient.SqlCommand(
            $"EXEC sp_getapplock @Resource = N'{CatalogDatabaseInitializer.AppLockResourcePrefix}{database}', @LockMode = 'Exclusive', @LockOwner = 'Session'", holder))
        {
            await take.ExecuteNonQueryAsync();
        }

        var initializer = app.GetRequiredService<CatalogDatabaseInitializer>();
        initializer.LockTimeout = TimeSpan.FromSeconds(2);

        await Assert.ThrowsAsync<TimeoutException>(() => initializer.InitializeAsync());
        Assert.Equal(0, await ScalarAsync<int>(master, $"SELECT COUNT(*) FROM sys.databases WHERE name = '{database}'"));
    }
}
