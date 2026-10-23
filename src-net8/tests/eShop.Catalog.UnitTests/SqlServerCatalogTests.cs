using eShop.Catalog.Core;
using eShop.Catalog.Core.Models;
using eShop.Catalog.Core.Services;
using eShop.Catalog.Infrastructure.Data;
using eShop.Catalog.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Xunit;

namespace eShop.Catalog.UnitTests
{
    /// <summary>One SQL Server 2022 container per test run (Testcontainers; needs Docker).</summary>
    public sealed class SqlServerFixture : IAsyncLifetime
    {
        private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        private int databases;

        public Task InitializeAsync() => container.StartAsync();

        public Task DisposeAsync() => container.DisposeAsync().AsTask();

        /// <summary>A fresh, not yet created database per call so tests never share HiLo or catalog state.</summary>
        public string NewDatabase() => container.GetConnectionString().Replace("Database=master", $"Database=CatalogT7_{Interlocked.Increment(ref databases)}");
    }

    /// <summary>
    /// SQL mode (UseMockData=false) through the public T1 entry points only: AddCatalogData registers CatalogDBContext,
    /// the HiLo id generator and the EF ICatalogService; InitializeCatalogDataAsync creates the schema, the catalog_hilo
    /// sequences and the PreconfiguredData seed (legacy CatalogDBInitializer).
    /// </summary>
    [Trait("Category", "SqlServer")]
    public class SqlServerCatalogTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture sql;

        public SqlServerCatalogTests(SqlServerFixture sql)
        {
            this.sql = sql;
        }

        private async Task<IHost> StartAsync(string connectionString = null, ICatalogSaveValidator saveValidator = null)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Production" });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
            {
                [CatalogSettings.UseMockDataKey] = "false",
                ["ConnectionStrings:" + CatalogSettings.ConnectionStringName] = connectionString ?? sql.NewDatabase(),
            });
            builder.Services.AddCatalogData(builder.Configuration);
            builder.Services.AddCatalogServices(builder.Configuration);
            if (saveValidator != null)
            {
                builder.Services.AddSingleton(saveValidator);
            }
            var host = builder.Build();
            await host.InitializeCatalogDataAsync();
            return host;
        }

        private static T InScope<T>(IHost host, Func<ICatalogService, T> action)
        {
            using var scope = host.Services.CreateScope();
            return action(scope.ServiceProvider.GetRequiredService<ICatalogService>());
        }

        private static CatalogItem NewItem(string name) => new CatalogItem
        {
            Name = name, Description = "T7", Price = 9.99M, CatalogBrandId = 2, CatalogTypeId = 1,
            AvailableStock = 5, RestockThreshold = 1, MaxStockThreshold = 10,
        };

        [Fact]
        public async Task Sql_mode_registers_an_ef_service_not_the_mock()
        {
            using var host = await StartAsync();

            var service = InScope(host, s => s);

            Assert.NotEqual("CatalogServiceMock", service.GetType().Name);
        }

        [Fact]
        public async Task Initializer_seeds_the_preconfigured_catalog()
        {
            using var host = await StartAsync();

            var page = InScope(host, s => s.GetCatalogItemsPaginated(10, 0));

            Assert.Equal(12, page.TotalItems);
            Assert.Equal(2, page.TotalPages);
            Assert.Equal(Enumerable.Range(1, 10), page.Data.Select(i => i.Id));
            Assert.Equal(".NET Bot Black Hoodie", page.Data.First().Name);
            Assert.Equal(19.50M, page.Data.First().Price);
            Assert.Equal(".NET", page.Data.First().CatalogBrand.Brand);
            Assert.Equal("T-Shirt", page.Data.First().CatalogType.Type);
            Assert.Equal(new[] { "Azure", ".NET", "Visual Studio", "SQL Server", "Other" }, InScope(host, s => s.GetCatalogBrands().OrderBy(b => b.Id).Select(b => b.Brand).ToList()));
            Assert.Equal(new[] { "Mug", "T-Shirt", "Sheet", "USB Memory Stick" }, InScope(host, s => s.GetCatalogTypes().OrderBy(t => t.Id).Select(t => t.Type).ToList()));
        }

        [Fact]
        public async Task Initializer_is_idempotent_across_restarts()
        {
            var db = sql.NewDatabase();
            using (await StartAsync(db))
            {
            }
            using var host = await StartAsync(db);

            Assert.Equal(12, InScope(host, s => s.GetCatalogItemsPaginated(10, 0).TotalItems));
        }

        [Fact]
        public async Task HiLo_assigns_unique_increasing_ids_above_the_seed()
        {
            using var host = await StartAsync();

            var ids = new List<int>();
            for (var i = 0; i < 25; i++)
            {
                ids.Add(InScope(host, s =>
                {
                    var item = NewItem($"HiLo {i}");
                    s.CreateCatalogItem(item);
                    return item.Id;
                }));
            }

            Assert.All(ids, id => Assert.True(id > 12, $"id {id} collides with the seed"));
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.Equal(ids.OrderBy(i => i), ids);
            Assert.Equal(37, InScope(host, s => s.GetCatalogItemsPaginated(10, 0).TotalItems));
        }

        [Fact]
        public async Task Crud_round_trip_persists_through_ef()
        {
            using var host = await StartAsync();
            var id = InScope(host, s =>
            {
                var item = NewItem("EF Mug");
                s.CreateCatalogItem(item);
                return item.Id;
            });

            var created = InScope(host, s => s.FindCatalogItem(id));
            Assert.Equal("EF Mug", created.Name);
            Assert.Equal(9.99M, created.Price);

            InScope(host, s =>
            {
                var item = s.FindCatalogItem(id);
                item.Name = "EF Mug v2";
                item.Price = 12.25M;
                item.CatalogBrandId = 5;
                s.UpdateCatalogItem(item);
                return 0;
            });
            var updated = InScope(host, s => s.FindCatalogItem(id));
            Assert.Equal("EF Mug v2", updated.Name);
            Assert.Equal(12.25M, updated.Price);
            Assert.Equal(5, updated.CatalogBrandId);

            InScope(host, s =>
            {
                s.RemoveCatalogItem(s.FindCatalogItem(id));
                return 0;
            });
            Assert.Null(InScope(host, s => s.FindCatalogItem(id)));
            Assert.Equal(12, InScope(host, s => s.GetCatalogItemsPaginated(10, 0).TotalItems));
        }

        private sealed class RejectNegativeStock : ICatalogSaveValidator
        {
            public void Validate(CatalogItem catalogItem)
            {
                if (catalogItem.AvailableStock < 0)
                {
                    throw new CatalogItemValidationException(new[] { "negative stock" });
                }
            }
        }

        [Fact]
        public async Task Save_validator_rejects_create_and_update_without_writing()
        {
            using var host = await StartAsync(saveValidator: new RejectNegativeStock());
            var bad = NewItem("Bad Mug");
            bad.AvailableStock = -1;

            Assert.Throws<CatalogItemValidationException>(() => InScope(host, s => { s.CreateCatalogItem(bad); return 0; }));
            Assert.Equal(12, InScope(host, s => s.GetCatalogItemsPaginated(10, 0).TotalItems));

            Assert.Throws<CatalogItemValidationException>(() => InScope(host, s =>
            {
                var item = s.FindCatalogItem(1);
                item.AvailableStock = -1;
                s.UpdateCatalogItem(item);
                return 0;
            }));
            Assert.NotEqual(-1, InScope(host, s => s.FindCatalogItem(1).AvailableStock));
        }

        [Fact]
        public async Task Without_a_save_validator_ef_core_saves_out_of_range_values()
        {
            using var host = await StartAsync();
            var item = NewItem("Negative Mug");
            item.AvailableStock = -1;

            InScope(host, s => { s.CreateCatalogItem(item); return 0; });

            Assert.Equal(-1, InScope(host, s => s.FindCatalogItem(item.Id).AvailableStock));
        }

        [Fact]
        public async Task Page_size_zero_throws_like_legacy()
        {
            using var host = await StartAsync();

            Assert.Throws<DivideByZeroException>(() => InScope(host, s => s.GetCatalogItemsPaginated(0, 0)));
        }
    }
}
