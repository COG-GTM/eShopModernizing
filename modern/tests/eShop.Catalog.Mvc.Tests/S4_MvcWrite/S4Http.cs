using System.Net;
using System.Text.RegularExpressions;
using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.Mvc.Tests.S4_MvcWrite;

/// <summary>Helpers shared by the S4 integration tests: one fresh host (and in-memory catalog) per test.</summary>
internal static partial class S4Http
{
    public static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public static ICatalogService CatalogOf(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<ICatalogService>();

    public static string ExtractAntiforgeryToken(string html)
    {
        var match = TokenInput().Match(html);
        Assert.True(match.Success, "antiforgery token input not found");
        return WebUtility.HtmlDecode(match.Groups["value"].Value);
    }

    public static async Task<(string Html, string Token)> GetFormAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        return (html, ExtractAntiforgeryToken(html));
    }

    public static Task<HttpResponseMessage> PostFormAsync(HttpClient client, string path, IDictionary<string, string> fields) =>
        client.PostAsync(path, new FormUrlEncodedContent(fields));

    public static Dictionary<string, string> ValidCreateForm(string? token) => WithToken(token, new()
    {
        ["Name"] = "MECCA Max Lip Balm",
        ["Description"] = "Hydrating lip balm",
        ["CatalogBrandId"] = "5",
        ["CatalogTypeId"] = "3",
        ["Price"] = "24.00",
        ["AvailableStock"] = "50",
        ["RestockThreshold"] = "5",
        ["MaxStockThreshold"] = "500",
    });

    public static Dictionary<string, string> EditItem1Form(string? token, string price) => WithToken(token, new()
    {
        ["Id"] = "1",
        ["Name"] = ".NET Bot Black Hoodie",
        ["Description"] = ".NET Bot Black Hoodie",
        ["CatalogBrandId"] = "2",
        ["CatalogTypeId"] = "2",
        ["Price"] = price,
        ["PictureFileName"] = "1.png",
        ["AvailableStock"] = "100",
        ["RestockThreshold"] = "0",
        ["MaxStockThreshold"] = "0",
    });

    private static Dictionary<string, string> WithToken(string? token, Dictionary<string, string> fields)
    {
        if (token != null)
        {
            fields["__RequestVerificationToken"] = token;
        }

        return fields;
    }

    [GeneratedRegex("<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<value>[^\"]+)\"")]
    private static partial Regex TokenInput();
}
