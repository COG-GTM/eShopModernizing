extern alias MvcApp;
extern alias PagesApp;
using Microsoft.AspNetCore.Mvc.Testing;

namespace eShopNet8.IntegrationTests;

public class ScaffoldSmokeTests
{
    [Fact]
    public async Task Mvc_app_answers_root()
    {
        await using var factory = new WebApplicationFactory<MvcApp::Program>();
        var response = await factory.CreateClient().GetAsync("/");
        Assert.True(response.IsSuccessStatusCode, response.StatusCode.ToString());
    }

    [Fact]
    public async Task Pages_app_answers_root()
    {
        await using var factory = new WebApplicationFactory<PagesApp::Program>();
        var response = await factory.CreateClient().GetAsync("/");
        Assert.True(response.IsSuccessStatusCode, response.StatusCode.ToString());
    }

    [Fact]
    public void Baseline_transcripts_are_copied_to_output()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "baseline", "transcripts");
        Assert.True(Directory.GetFiles(dir, "*.json").Length > 100);
    }
}
