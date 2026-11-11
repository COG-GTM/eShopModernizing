using System.Net;
using eShop.Catalog.Data.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using static eShop.Catalog.Mvc.Tests.S4_MvcWrite.S4Http;

namespace eShop.Catalog.Mvc.Tests.S4_MvcWrite;

/// <summary>Create / Edit / Delete round trips over <see cref="WebApplicationFactory{TEntryPoint}"/> (mock catalog).</summary>
public sealed class WriteFlowIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> factory = new();
    private readonly HttpClient client;

    public WriteFlowIntegrationTests()
    {
        client = CreateClient(factory);
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    private long TotalItems => CatalogOf(factory).GetCatalogItemsPaginated(100, 0).TotalItems;

    [Fact]
    public async Task Get_create_renders_legacy_form_with_lookups_defaults_and_token()
    {
        var (html, token) = await GetFormAsync(client, "/Catalog/Create");

        Assert.NotEmpty(token);
        Assert.Matches("<form action=\"/Catalog/Create\" method=\"post\">\\s*<input name=\"__RequestVerificationToken\" type=\"hidden\"", html);
        foreach (var field in new[] { "Name", "Description", "CatalogBrandId", "CatalogTypeId", "Price", "AvailableStock", "RestockThreshold", "MaxStockThreshold" })
        {
            Assert.Contains($"name=\"{field}\"", html);
        }

        Assert.True(html.IndexOf("id=\"Name\"", StringComparison.Ordinal) < html.IndexOf("id=\"Description\"", StringComparison.Ordinal));
        Assert.True(html.IndexOf("id=\"Price\"", StringComparison.Ordinal) < html.IndexOf("id=\"AvailableStock\"", StringComparison.Ordinal));
        Assert.Equal(5, CountOptions(html, "CatalogBrandId"));
        Assert.Equal(4, CountOptions(html, "CatalogTypeId"));
        Assert.Contains("id=\"Price\" name=\"Price\" type=\"text\" value=\"0.00\"", html);
        Assert.Contains("Uploading images not allowed for this version.", html);
        Assert.Contains("data-val-required=\"The Name field is required.\"", html);
        Assert.Contains("data-val-regex=\"The field Price must be a positive number with maximum two decimals.\" data-val-regex-pattern=\"^\\d+(\\.\\d{0,2})*$\"", html);
        Assert.Contains("data-val-range=\"The field Stock must be between 0 and 10 million.\"", html);
        Assert.Contains("<span class=\"field-validation-valid text-danger\" data-valmsg-for=\"Name\" data-valmsg-replace=\"true\"></span>", html);
        Assert.DoesNotContain("PictureFileName\" name=\"PictureFileName\"", html);
    }

    [Fact]
    public async Task Valid_create_redirects_to_root_and_appends_item_13()
    {
        var (_, token) = await GetFormAsync(client, "/Catalog/Create");

        using var response = await PostFormAsync(client, "/Catalog/Create", ValidCreateForm(token));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        Assert.Equal(13, TotalItems);
        var last = CatalogOf(factory).GetCatalogItemsPaginated(100, 0).Data.Last();
        Assert.Equal("MECCA Max Lip Balm", last.Name);
        Assert.Equal(24.00m, last.Price);
        Assert.Equal(CatalogItem.DefaultPictureName, last.PictureFileName);
    }

    [Fact]
    public async Task Invalid_create_rerenders_with_legacy_validation_messages_and_no_data_change()
    {
        var (_, token) = await GetFormAsync(client, "/Catalog/Create");
        var form = ValidCreateForm(token);
        form["Name"] = string.Empty;
        form["Price"] = "12.555";
        form["AvailableStock"] = "-1";

        using var response = await PostFormAsync(client, "/Catalog/Create", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<span class=\"field-validation-error text-danger\" data-valmsg-for=\"Name\" data-valmsg-replace=\"true\">The Name field is required.</span>", html);
        Assert.Contains("data-valmsg-for=\"Price\" data-valmsg-replace=\"true\">The field Price must be a positive number with maximum two decimals.</span>", html);
        Assert.Contains("data-valmsg-for=\"AvailableStock\" data-valmsg-replace=\"true\">The field Stock must be between 0 and 10 million.</span>", html);
        Assert.Contains("<div class=\"validation-summary-errors text-danger\"><ul><li style=\"display:none\"></li>", html);
        Assert.Contains("value=\"12.555\"", html);
        Assert.Equal(5, CountOptions(html, "CatalogBrandId"));
        Assert.Equal(12, TotalItems);
    }

    [Fact]
    public async Task Valid_edit_of_item_1_redirects_and_persists_price_keeping_picture()
    {
        var (html, token) = await GetFormAsync(client, "/Catalog/Edit/1");
        Assert.Contains("id=\"Price\" name=\"Price\" type=\"text\" value=\"19.50\"", html);
        Assert.Contains("<input Title=\"Not allowed for edition\" class=\"form-control text-box single-line\" id=\"PictureFileName\" name=\"PictureFileName\" readonly=\"readonly\" type=\"text\" value=\"1.png\" />", html);

        using var response = await PostFormAsync(client, "/Catalog/Edit/1", EditItem1Form(token, "21.00"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        var item = CatalogOf(factory).FindCatalogItem(1)!;
        Assert.Equal(21.00m, item.Price);
        Assert.Equal("1.png", item.PictureFileName);
        var (after, _) = await GetFormAsync(client, "/Catalog/Edit/1");
        Assert.Contains("id=\"Price\" name=\"Price\" type=\"text\" value=\"21.00\"", after);
    }

    [Fact]
    public async Task Invalid_edit_rerenders_with_messages_and_stored_picture_name()
    {
        var (_, token) = await GetFormAsync(client, "/Catalog/Edit/1");
        var form = EditItem1Form(token, "abc");
        form["Name"] = string.Empty;
        form["PictureFileName"] = "evil.png";

        using var response = await PostFormAsync(client, "/Catalog/Edit/1", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(">The Name field is required.</span>", html);
        Assert.Contains(">The value &#x27;abc&#x27; is not valid for Price.</span>", html);
        Assert.Contains("value=\"1.png\"", html);
        Assert.DoesNotContain("evil.png", html);
        Assert.Equal(19.5m, CatalogOf(factory).FindCatalogItem(1)!.Price);
    }

    [Fact]
    public async Task Delete_confirm_and_post_removes_item_2()
    {
        var (html, token) = await GetFormAsync(client, "/Catalog/Delete/2");
        Assert.Contains("Are you sure you want to delete this?", html);
        Assert.Contains(".NET Black &amp; White Mug", html);
        Assert.DoesNotContain("jquery.validate", html);

        using var response = await PostFormAsync(client, "/Catalog/Delete/2", new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        Assert.Null(CatalogOf(factory).FindCatalogItem(2));
        Assert.Equal(11, TotalItems);
        using var gone = await client.GetAsync("/Catalog/Delete/2");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Theory]
    [InlineData("/Catalog/Create")]
    [InlineData("/Catalog/Edit/1")]
    [InlineData("/Catalog/Delete/2")]
    public async Task Post_without_antiforgery_token_returns_400_and_changes_nothing(string path)
    {
        // Cookie issued by a real GET, but no form token: still rejected.
        await GetFormAsync(client, path);
        var form = path.Contains("Edit") ? EditItem1Form(null, "21.00") : ValidCreateForm(null);

        using var withCookie = await PostFormAsync(client, path, form);
        using var fresh = CreateClient(factory);
        using var withoutCookie = await PostFormAsync(fresh, path, form);

        Assert.Equal(HttpStatusCode.BadRequest, withCookie.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutCookie.StatusCode);
        Assert.Equal(12, TotalItems);
        Assert.Equal(19.5m, CatalogOf(factory).FindCatalogItem(1)!.Price);
        Assert.NotNull(CatalogOf(factory).FindCatalogItem(2));
    }

    [Fact]
    public async Task Over_posted_picture_and_reorder_fields_are_ignored()
    {
        var (_, editToken) = await GetFormAsync(client, "/Catalog/Edit/1");
        var edit = EditItem1Form(editToken, "21.00");
        edit["PictureFileName"] = "../../../etc/passwd";
        edit["PictureUri"] = "http://evil.example/x.png";
        edit["OnReorder"] = "true";
        using var editResponse = await PostFormAsync(client, "/Catalog/Edit/1", edit);

        var (_, createToken) = await GetFormAsync(client, "/Catalog/Create");
        var create = ValidCreateForm(createToken);
        create["PictureFileName"] = "../evil.png";
        create["OnReorder"] = "true";
        create["Id"] = "1";
        using var createResponse = await PostFormAsync(client, "/Catalog/Create", create);

        Assert.Equal(HttpStatusCode.Found, editResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Found, createResponse.StatusCode);
        var edited = CatalogOf(factory).FindCatalogItem(1)!;
        Assert.Equal("1.png", edited.PictureFileName);
        Assert.False(edited.OnReorder);
        Assert.Equal(21.00m, edited.Price);
        var created = CatalogOf(factory).GetCatalogItemsPaginated(100, 0).Data.Last();
        Assert.Equal(CatalogItem.DefaultPictureName, created.PictureFileName);
        Assert.False(created.OnReorder);
        Assert.NotEqual(1, created.Id);
        Assert.Equal(13, TotalItems);
    }

    [Theory]
    [InlineData("/Catalog/Edit", HttpStatusCode.BadRequest)]
    [InlineData("/Catalog/Edit/999", HttpStatusCode.NotFound)]
    [InlineData("/Catalog/Delete", HttpStatusCode.BadRequest)]
    [InlineData("/Catalog/Delete/999", HttpStatusCode.NotFound)]
    public async Task Missing_or_unknown_id_matches_legacy_status(string path, HttpStatusCode expected)
    {
        using var response = await client.GetAsync(path);

        Assert.Equal(expected, response.StatusCode);
    }

    private static int CountOptions(string html, string selectId)
    {
        var start = html.IndexOf($"<select class=\"form-control\" id=\"{selectId}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"select {selectId} not found");
        var end = html.IndexOf("</select>", start, StringComparison.Ordinal);
        return html[start..end].Split("<option ").Length - 1;
    }
}
