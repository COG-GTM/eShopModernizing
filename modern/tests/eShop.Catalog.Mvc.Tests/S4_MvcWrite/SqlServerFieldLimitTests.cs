using System.Net;
using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using static eShop.Catalog.Mvc.Tests.S4_MvcWrite.S4Http;

namespace eShop.Catalog.Mvc.Tests.S4_MvcWrite;

/// <summary>One SQL Server 2022 container (Docker) shared by the S4 SQL-mode tests; each test gets its own database.</summary>
public sealed class S4SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => container.StartAsync();

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    public WebApplicationFactory<Program> NewSqlHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("UseMockData", "false")
            .UseSetting("ConnectionStrings:CatalogDBContext", new SqlConnectionStringBuilder(container.GetConnectionString())
            {
                InitialCatalog = "S4Limits_" + Guid.NewGuid().ToString("N")[..12],
                TrustServerCertificate = true,
            }.ConnectionString));
}

/// <summary>Create / Edit limits against EF Core + SQL Server, where <c>Catalog.Name</c> is <c>nvarchar(50)</c>.</summary>
public sealed class SqlServerFieldLimitTests : IClassFixture<S4SqlServerFixture>, IDisposable
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public SqlServerFieldLimitTests(S4SqlServerFixture sql)
    {
        factory = sql.NewSqlHost();
        client = CreateClient(factory);
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    private string Snapshot()
    {
        using var scope = factory.Services.CreateScope();
        return FieldLimits.Snapshot(scope.ServiceProvider.GetRequiredService<ICatalogService>());
    }

    [Theory]
    [InlineData("/Catalog/Create", "Name", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", FieldLimits.NameLength)]
    [InlineData("/Catalog/Edit/1", "Name", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", FieldLimits.NameLength)]
    [InlineData("/Catalog/Create", "Price", "99999999999999999999.99", FieldLimits.PriceRange)]
    public async Task Over_limit_value_rerenders_with_message_and_writes_nothing(string path, string field, string value, string expectedMessage)
    {
        var (_, token) = await GetFormAsync(client, path);
        var before = Snapshot();
        var form = path.Contains("Edit") ? EditItem1Form(token, "19.50") : ValidCreateForm(token);
        form[field] = value;

        using var response = await PostFormAsync(client, path, form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"data-valmsg-for=\"{field}\" data-valmsg-replace=\"true\">{expectedMessage}</span>", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task Fifty_character_name_is_persisted()
    {
        var name = new string('M', 50);
        var (_, token) = await GetFormAsync(client, "/Catalog/Create");
        var form = ValidCreateForm(token);
        form["Name"] = name;

        using var response = await PostFormAsync(client, "/Catalog/Create", form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        Assert.Contains($"|{name}|", Snapshot());
    }
}
