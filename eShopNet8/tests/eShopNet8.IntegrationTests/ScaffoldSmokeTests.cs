extern alias MvcApp;
extern alias PagesApp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace eShopNet8.IntegrationTests;

public class ScaffoldSmokeTests
{
    private const string MvcPlaceholder = "eShopNet8.Mvc scaffold - catalog UI pending (T3)";
    private const string PagesPlaceholder = "eShopNet8.WebForms scaffold - Razor Pages catalog pending (T5)";

    [Fact]
    public async Task Mvc_app_answers_root()
    {
        await using var factory = new WebApplicationFactory<MvcApp::Program>().WithWebHostBuilder(b => b.UseEnvironment("Production"));
        var response = await factory.CreateClient().GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, response.StatusCode.ToString());
        if (body != MvcPlaceholder)
            Assert.Contains("<title>Index - Catalog manager (MVC)</title>", body);
    }

    [Fact]
    public async Task Pages_app_answers_root()
    {
        await using var factory = new WebApplicationFactory<PagesApp::Program>().WithWebHostBuilder(b => b.UseEnvironment("Production"));
        var response = await factory.CreateClient().GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, response.StatusCode.ToString());
        if (body != PagesPlaceholder)
            Assert.Contains("Home Page - Catalog manager (Web Forms)", body);
    }

    [Fact]
    public void Baseline_transcripts_are_copied_to_output()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "baseline", "transcripts");
        Assert.True(Directory.GetFiles(dir, "*.json").Length > 100);
    }
}
