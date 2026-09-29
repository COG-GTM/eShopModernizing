using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace eShopRazorPages.Tests;

/// <summary>Hosts the app in-memory with the default mock catalog (UseMockData=true).</summary>
public sealed class CatalogAppFactory : WebApplicationFactory<Program>
{
    public HttpClient CreateNonRedirectingClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}

internal static partial class Html
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();

    public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var match = AntiforgeryTokenRegex().Match(html);
        Assert.True(match.Success, $"No antiforgery token found on {url}");
        return match.Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, IDictionary<string, string> fields)
    {
        var token = await GetAntiforgeryTokenAsync(client, url);
        var form = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = token };
        return await client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    public static Dictionary<string, string> ValidItemForm(string name = "Test Item") => new()
    {
        ["Input.Name"] = name,
        ["Input.Description"] = "Created by tests",
        ["Input.CatalogBrandId"] = "3",
        ["Input.CatalogTypeId"] = "4",
        ["Input.Price"] = "42.25",
        ["Input.Stock"] = "7",
        ["Input.Restock"] = "2",
        ["Input.Maxstock"] = "20",
    };
}
