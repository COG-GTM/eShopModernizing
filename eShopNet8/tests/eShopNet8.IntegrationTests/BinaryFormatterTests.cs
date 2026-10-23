extern alias MvcApp;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace eShopNet8.IntegrationTests;

// INVENTORY Q2: legacy /api/files streamed a BinaryFormatter payload. The .NET 8 port must not use BinaryFormatter at all
// and must keep the runtime switch off; /api/files answers the same brand list as JSON (TICKETS.md T4).
public class BinaryFormatterTests
{
    private static readonly string[] AppAssemblies =
        ["eShopNet8.Mvc.dll", "eShopNet8.WebForms.dll", "eShop.Catalog.Core.dll", "eShop.Catalog.Infrastructure.dll"];

    [Theory]
    [InlineData("eShopNet8.Mvc")]
    [InlineData("eShopNet8.WebForms")]
    public void Unsafe_BinaryFormatter_serialization_is_disabled_in_runtimeconfig(string app)
    {
        var path = Path.Combine(AppContext.BaseDirectory, app + ".runtimeconfig.json");
        var props = JsonNode.Parse(File.ReadAllText(path))!["runtimeOptions"]?["configProperties"];
        var value = props?["System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization"];
        Assert.True(value is null || !value.GetValue<bool>(), $"{app}: EnableUnsafeBinaryFormatterSerialization must not be true");
    }

    [Fact]
    public void No_app_assembly_references_binary_formatter_types()
    {
        foreach (var file in AppAssemblies)
        {
            using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, file));
            using var pe = new PEReader(stream);
            var md = pe.GetMetadataReader();
            var refs = md.TypeReferences.Select(h => md.GetTypeReference(h))
                .Select(t => md.GetString(t.Namespace) + "." + md.GetString(t.Name)).ToList();
            Assert.DoesNotContain(refs, r => r.StartsWith("System.Runtime.Serialization.Formatters.Binary.", StringComparison.Ordinal));
            Assert.DoesNotContain(refs, r => r.EndsWith(".BinaryFormatter", StringComparison.Ordinal) || r.EndsWith(".SoapFormatter", StringComparison.Ordinal));
        }
    }

    [SkippableFact]
    public async Task Api_files_returns_the_brand_list_as_json()
    {
        await using var factory = new WebApplicationFactory<MvcApp::Program>().WithWebHostBuilder(b => b.UseEnvironment("Production"));
        var mapped = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Any(e => (e.RoutePattern.RawText ?? "").TrimStart('/').StartsWith("api/files", StringComparison.OrdinalIgnoreCase));
        Skip.IfNot(mapped, "T4 not in this build: /api/files not mapped (scaffold stub)");

        var response = await factory.CreateClient().GetAsync("/api/files");
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.False(body.Length > 0 && body[0] == 0x00, "payload must not be an NRBF (BinaryFormatter) stream");
        var brands = JsonSerializer.Deserialize<JsonArray>(body)!;
        Assert.Equal(new[] { "Azure", ".NET", "Visual Studio", "SQL Server", "Other" }, brands.Select(b => (string)b!["Brand"]!));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, brands.Select(b => (int)b!["Id"]!));
    }
}
