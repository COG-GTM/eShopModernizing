using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using static eShop.Catalog.Mvc.Tests.S4_MvcWrite.S4Http;

namespace eShop.Catalog.Mvc.Tests.S4_MvcWrite;

/// <summary>Length / range limits on Create and Edit over <see cref="WebApplicationFactory{TEntryPoint}"/> (mock catalog).</summary>
public sealed class FieldLimitIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> factory = new();
    private readonly HttpClient client;

    public FieldLimitIntegrationTests()
    {
        client = CreateClient(factory);
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    [Theory]
    [InlineData("/Catalog/Create", "Name", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", FieldLimits.NameLength)]
    [InlineData("/Catalog/Edit/1", "Name", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", FieldLimits.NameLength)]
    [InlineData("/Catalog/Create", "Price", "99999999999999999999.99", FieldLimits.PriceRange)]
    [InlineData("/Catalog/Edit/1", "Price", "99999999999999999999.99", FieldLimits.PriceRange)]
    [InlineData("/Catalog/Create", "Price", "2147483648", FieldLimits.PriceRange)]
    [InlineData("/Catalog/Edit/1", "AvailableStock", "2147483648", "The value &#x27;2147483648&#x27; is not valid for Stock.")]
    public async Task Over_limit_value_rerenders_with_message_and_changes_nothing(string path, string field, string value, string expectedMessage)
    {
        var before = FieldLimits.Snapshot(CatalogOf(factory));
        var (_, token) = await GetFormAsync(client, path);
        var form = path.Contains("Edit") ? EditItem1Form(token, "19.50") : ValidCreateForm(token);
        form[field] = value;

        using var response = await PostFormAsync(client, path, form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($"<span class=\"field-validation-error text-danger\" data-valmsg-for=\"{field}\" data-valmsg-replace=\"true\">{expectedMessage}</span>", html);
        Assert.Equal(before, FieldLimits.Snapshot(CatalogOf(factory)));
    }

    [Theory]
    [InlineData("/Catalog/Create")]
    [InlineData("/Catalog/Edit/1")]
    public async Task Fifty_character_name_is_saved(string path)
    {
        var name = new string('M', 50);
        var (_, token) = await GetFormAsync(client, path);
        var form = path.Contains("Edit") ? EditItem1Form(token, "19.50") : ValidCreateForm(token);
        form["Name"] = name;

        using var response = await PostFormAsync(client, path, form);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        Assert.Contains(CatalogOf(factory).GetCatalogItemsPaginated(100, 0).Data, i => i.Name == name);
    }

    [Theory]
    [InlineData("/Catalog/Create")]
    [InlineData("/Catalog/Edit/1")]
    public async Task Form_markup_keeps_legacy_client_validation_attributes(string path)
    {
        var (html, _) = await GetFormAsync(client, path);

        Assert.DoesNotContain("data-val-length", html);
        Assert.DoesNotContain("data-val-maxlength", html);
        Assert.DoesNotContain("maxlength=", html);
        Assert.Contains("data-val-range=\"The field Price must be between 0 and 1000000.\" data-val-range-max=\"1000000\" data-val-range-min=\"0\"", html);
    }
}
