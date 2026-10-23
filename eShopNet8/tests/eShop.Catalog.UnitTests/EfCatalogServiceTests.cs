using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Seed;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eShop.Catalog.UnitTests;

// EF Core CatalogService + CatalogDBContext registered by AddCatalogData (UseMockData=false). Schema/HiLo metadata and
// service semantics run everywhere (SQL Server model without a connection, EF InMemory store); the database-level
// checks (seeding, sequences, HiLo ids, SQL errors) need ESHOP_TEST_SQL_CONNECTION (a SQL Server login, no database).
public class EfCatalogServiceTests
{
    private const string NoServer = "Server=127.0.0.1,1;Database=unit;User Id=sa;Password=unused;TrustServerCertificate=True";

    private static IServiceCollection SqlModeServices(string connectionString)
    {
        var services = TestServices.Collection(TestServices.Config(("UseMockData", "false"), ("ConnectionStrings:CatalogDBContext", connectionString)));
        Skip.IfNot(services.Any(s => s.ServiceType == typeof(ICatalogService)), "T1 not in this build: AddCatalogData registers nothing (scaffold stub)");
        return services;
    }

    private static Type ContextType(IServiceCollection services) =>
        services.First(s => typeof(DbContext).IsAssignableFrom(s.ServiceType) && s.ServiceType != typeof(DbContext)).ServiceType;

    // Same registrations, EF InMemory store instead of SQL Server; seeded with PreconfiguredData like CatalogDBInitializer.
    private static ServiceProvider InMemoryProvider()
    {
        var services = SqlModeServices(NoServer);
        var ctxType = ContextType(services);
        var optionsType = typeof(DbContextOptions<>).MakeGenericType(ctxType);
        services.RemoveAll(optionsType);
        services.RemoveAll<DbContextOptions>();
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(typeof(DbContextOptionsBuilder<>).MakeGenericType(ctxType))!;
        builder.UseInMemoryDatabase("catalog-" + Guid.NewGuid());
        services.AddSingleton(optionsType, builder.Options);
        services.AddSingleton<DbContextOptions>(sp => (DbContextOptions)sp.GetRequiredService(optionsType));
        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = (DbContext)scope.ServiceProvider.GetRequiredService(ctxType);
        db.Set<CatalogBrand>().AddRange(PreconfiguredData.GetPreconfiguredCatalogBrands());
        db.Set<CatalogType>().AddRange(PreconfiguredData.GetPreconfiguredCatalogTypes());
        db.Set<CatalogItem>().AddRange(PreconfiguredData.GetPreconfiguredCatalogItems());
        db.SaveChanges();
        return sp;
    }

    [SkippableFact]
    public void Model_matches_the_legacy_EF6_schema()
    {
        var services = SqlModeServices(NoServer);
        using var scope = services.BuildServiceProvider().CreateScope();
        var db = (DbContext)scope.ServiceProvider.GetRequiredService(ContextType(services));
        var model = db.GetService<IDesignTimeModel>().Model;

        IEntityType E<T>() => model.FindEntityType(typeof(T))!;
        Assert.Equal("Catalog", E<CatalogItem>().GetTableName());
        Assert.Equal("CatalogBrand", E<CatalogBrand>().GetTableName());
        Assert.Equal("CatalogType", E<CatalogType>().GetTableName());

        var item = E<CatalogItem>();
        Assert.Null(item.FindProperty(nameof(CatalogItem.PictureUri)));
        Assert.Equal(50, item.FindProperty(nameof(CatalogItem.Name))!.GetMaxLength());
        Assert.False(item.FindProperty(nameof(CatalogItem.Name))!.IsNullable);
        Assert.Equal("decimal(18,2)", item.FindProperty(nameof(CatalogItem.Price))!.GetColumnType());
        Assert.False(item.FindProperty(nameof(CatalogItem.PictureFileName))!.IsNullable);
        Assert.Equal(100, E<CatalogBrand>().FindProperty(nameof(CatalogBrand.Brand))!.GetMaxLength());
        Assert.Equal(100, E<CatalogType>().FindProperty(nameof(CatalogType.Type))!.GetMaxLength());
    }

    [SkippableFact]
    public void Ids_come_from_hilo_sequences_incrementing_by_10()
    {
        var services = SqlModeServices(NoServer);
        using var scope = services.BuildServiceProvider().CreateScope();
        var db = (DbContext)scope.ServiceProvider.GetRequiredService(ContextType(services));
        var model = db.GetService<IDesignTimeModel>().Model;

        Assert.Equal("catalog_hilo", model.FindEntityType(typeof(CatalogItem))!.FindProperty("Id")!.GetHiLoSequenceName());
        Assert.Equal("catalog_brand_hilo", model.FindEntityType(typeof(CatalogBrand))!.FindProperty("Id")!.GetHiLoSequenceName());
        Assert.Equal("catalog_type_hilo", model.FindEntityType(typeof(CatalogType))!.FindProperty("Id")!.GetHiLoSequenceName());
        foreach (var name in new[] { "catalog_hilo", "catalog_brand_hilo", "catalog_type_hilo" })
        {
            var seq = model.GetSequences().Single(s => s.Name == name);
            Assert.Equal(10, seq.IncrementBy);
            Assert.Equal(1, seq.StartValue);
        }
    }

    [SkippableFact]
    public void Paging_orders_by_id_and_includes_brand_and_type()
    {
        using var sp = InMemoryProvider();
        using var scope = sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var page = svc.GetCatalogItemsPaginated(5, 2);
        Assert.Equal(new[] { 11, 12 }, page.Data.Select(i => i.Id));
        Assert.Equal(12, page.TotalItems);
        Assert.Equal(3, page.TotalPages);
        Assert.All(page.Data, i => Assert.NotNull(i.CatalogBrand));
        Assert.Equal("Other", page.Data.Last().CatalogBrand!.Brand);
        Assert.Throws<DivideByZeroException>(() => svc.GetCatalogItemsPaginated(0, 0));
    }

    [SkippableFact]
    public void Find_includes_navigation_properties_without_a_prior_list()
    {
        using var sp = InMemoryProvider();
        using var scope = sp.CreateScope();
        var item = scope.ServiceProvider.GetRequiredService<ICatalogService>().FindCatalogItem(12)!;
        Assert.Equal("Other", item.CatalogBrand?.Brand);
        Assert.Equal("T-Shirt", item.CatalogType?.Type);
        Assert.Null(scope.ServiceProvider.GetRequiredService<ICatalogService>().FindCatalogItem(999));
    }

    [SkippableFact]
    public void Create_update_remove_round_trip()
    {
        using var sp = InMemoryProvider();
        int id;
        using (var scope = sp.CreateScope())
        {
            var item = new CatalogItem { Name = "Parity Test Hoodie", Price = 24.99m, CatalogBrandId = 3, CatalogTypeId = 2, AvailableStock = 42 };
            scope.ServiceProvider.GetRequiredService<ICatalogService>().CreateCatalogItem(item);
            id = item.Id;
            Assert.NotEqual(0, id);
        }
        using (var scope = sp.CreateScope())
        {
            Assert.Equal(13, scope.ServiceProvider.GetRequiredService<ICatalogService>().GetCatalogItemsPaginated(100, 0).TotalItems);
        }
        // Like the MVC Edit POST: a fresh request scope attaches the posted (untracked) item as Modified.
        using (var scope = sp.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ICatalogService>().UpdateCatalogItem(new CatalogItem { Id = id, Name = "Edited", Price = 21.50m, CatalogBrandId = 3, CatalogTypeId = 2, PictureFileName = "dummy.png" });
        }
        using (var scope = sp.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<ICatalogService>();
            Assert.Equal("Edited", svc.FindCatalogItem(id)!.Name);
            svc.RemoveCatalogItem(svc.FindCatalogItem(id)!);
            Assert.Null(svc.FindCatalogItem(id));
        }
    }

    [SkippableFact]
    public void Update_and_remove_of_unknown_items_throw_like_legacy_Q6_Q18()
    {
        using var sp = InMemoryProvider();
        using (var scope = sp.CreateScope())
            Assert.ThrowsAny<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<ICatalogService>()
                .UpdateCatalogItem(new CatalogItem { Id = 999, Name = "Ghost", PictureFileName = "dummy.png" }));
        using (var scope = sp.CreateScope())
            Assert.ThrowsAny<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<ICatalogService>()
                .RemoveCatalogItem(new CatalogItem { Id = 999, Name = "Ghost" }));
    }

    // ---- real SQL Server (CatalogDBInitializer, sequences, HiLo, provider errors) -------------------------------------

    private static string SqlConnection()
    {
        var cs = Environment.GetEnvironmentVariable("ESHOP_TEST_SQL_CONNECTION");
        Skip.If(string.IsNullOrWhiteSpace(cs), "set ESHOP_TEST_SQL_CONNECTION (SQL Server login, e.g. the `sql` container) to run database tests");
        return cs!.TrimEnd(';') + ";Database=eShopNet8UnitTests_" + Guid.NewGuid().ToString("N")[..12] + ";TrustServerCertificate=True";
    }

    [SkippableFact]
    public async Task Sql_initializer_seeds_catalog_sequences_and_next_id_is_13()
    {
        var services = SqlModeServices(SqlConnection());
        await using var sp = services.BuildServiceProvider();
        try
        {
            await sp.InitializeCatalogDataAsync();
            using (var scope = sp.CreateScope())
            {
                var db = (DbContext)scope.ServiceProvider.GetRequiredService(ContextType(services));
                Assert.Equal(12, db.Set<CatalogItem>().Count());
                Assert.Equal(5, db.Set<CatalogBrand>().Count());
                Assert.Equal(4, db.Set<CatalogType>().Count());
                Assert.Equal(new[] { "catalog_brand_hilo", "catalog_hilo", "catalog_type_hilo" },
                    db.Database.SqlQueryRaw<string>("SELECT name AS [Value] FROM sys.sequences ORDER BY name").ToList());
                Assert.Equal(Enumerable.Range(1, 12), db.Set<CatalogItem>().OrderBy(i => i.Id).Select(i => i.Id));

                var svc = scope.ServiceProvider.GetRequiredService<ICatalogService>();
                var created = new CatalogItem { Name = "Parity Test Hoodie", Price = 24.99m, CatalogBrandId = 3, CatalogTypeId = 2 };
                svc.CreateCatalogItem(created);
                Assert.Equal(13, created.Id);
            }

            // Re-running the initializer against an existing database does not reseed (CreateDatabaseIfNotExists).
            await sp.InitializeCatalogDataAsync();
            using (var scope = sp.CreateScope())
                Assert.Equal(13, ((DbContext)scope.ServiceProvider.GetRequiredService(ContextType(services))).Set<CatalogItem>().Count());
        }
        finally
        {
            using var scope = sp.CreateScope();
            await ((DbContext)scope.ServiceProvider.GetRequiredService(ContextType(services))).Database.EnsureDeletedAsync();
        }
    }

    [SkippableFact]
    public async Task Sql_negative_paging_and_unknown_ids_fail_like_legacy_sql_mode_Q18()
    {
        var services = SqlModeServices(SqlConnection());
        await using var sp = services.BuildServiceProvider();
        try
        {
            await sp.InitializeCatalogDataAsync();
            using var scope = sp.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ICatalogService>();
            Assert.ThrowsAny<Exception>(() => svc.GetCatalogItemsPaginated(10, -1));
            Assert.ThrowsAny<Exception>(() => svc.GetCatalogItemsPaginated(-1, 0));
            Assert.ThrowsAny<DbUpdateException>(() => svc.UpdateCatalogItem(new CatalogItem { Id = 999, Name = "Ghost", PictureFileName = "dummy.png", CatalogBrandId = 1, CatalogTypeId = 1 }));
        }
        finally
        {
            using var scope = sp.CreateScope();
            await ((DbContext)scope.ServiceProvider.GetRequiredService(ContextType(services))).Database.EnsureDeletedAsync();
        }
    }
}
