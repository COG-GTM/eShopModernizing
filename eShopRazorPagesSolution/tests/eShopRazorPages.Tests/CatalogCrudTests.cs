using System.Net;

namespace eShopRazorPages.Tests;

/// <summary>
/// End-to-end CRUD flows through the legacy URLs. Each test gets its own host because the mock catalog is a
/// mutable singleton.
/// </summary>
public class CatalogCrudTests
{
    [Theory]
    [InlineData("/Catalog/Create")]
    [InlineData("/Catalog/Create.aspx")]
    public async Task Create_adds_item_and_redirects_to_list(string url)
    {
        await using var factory = new CatalogAppFactory();
        var client = factory.CreateNonRedirectingClient();

        var response = await Html.PostFormAsync(client, url, Html.ValidItemForm("Brand New Mug"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);

        var details = await client.GetStringAsync("/Catalog/Details/13");
        Assert.Contains("Brand New Mug", details);
        Assert.Contains("Visual Studio", details);
        Assert.Contains("USB Memory Stick", details);
        Assert.Contains("42.25", details);
        Assert.Contains("dummy.png", details);

        var page2 = await client.GetStringAsync("/Default/index/1/size/10");
        Assert.Contains("Brand New Mug", page2);
        Assert.Contains("Showing 10 of 13 products", page2);
    }

    [Fact]
    public async Task Create_with_invalid_input_redisplays_form_with_legacy_messages()
    {
        await using var factory = new CatalogAppFactory();
        var client = factory.CreateNonRedirectingClient();
        var form = Html.ValidItemForm();
        form["Input.Name"] = "";
        form["Input.Price"] = "12.345";
        form["Input.Stock"] = "-1";
        form["Input.Restock"] = "abc";
        form["Input.Maxstock"] = "10000001";

        var response = await Html.PostFormAsync(client, "/Catalog/Create", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The Name field is required.", html);
        Assert.Contains("The Price must be a positive number with maximum two decimals between 0 and 1 million.", html);
        Assert.Contains("The field Stock must be between 0 and 10 million.", html);
        Assert.Contains("The field Restock must be between 0 and 10 million.", html);
        Assert.Contains("The field Max stock must be between 0 and 10 million.", html);
        Assert.Contains("Showing 10 of 12 products", await client.GetStringAsync("/"));
    }

    [Fact]
    public async Task Create_rejects_unknown_brand_or_type()
    {
        await using var factory = new CatalogAppFactory();
        var client = factory.CreateNonRedirectingClient();
        var form = Html.ValidItemForm();
        form["Input.CatalogBrandId"] = "99";
        form["Input.CatalogTypeId"] = "99";

        var response = await Html.PostFormAsync(client, "/Catalog/Create", form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Select a valid brand.", html);
        Assert.Contains("Select a valid type.", html);
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        await using var factory = new CatalogAppFactory();
        var client = factory.CreateNonRedirectingClient();

        var response = await client.PostAsync("/Catalog/Delete/1", new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Catalog/Details/1")).StatusCode);
    }

    [Fact]
    public async Task Edit_updates_item_and_keeps_picture()
    {
        await using var factory = new CatalogAppFactory();
        var client = factory.CreateNonRedirectingClient();

        var response = await Html.PostFormAsync(client, "/Catalog/Edit/1", Html.ValidItemForm("Renamed Hoodie"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);

        var details = await client.GetStringAsync("/Catalog/Details/1");
        Assert.Contains("Renamed Hoodie", details);
        Assert.Contains("42.25", details);
        Assert.Contains("1.png", details);
        Assert.Contains("Visual Studio", details);
    }

    [Fact]
    public async Task Edit_form_is_prefilled_from_item()
    {
        await using var factory = new CatalogAppFactory();
        var html = await factory.CreateClient().GetStringAsync("/Catalog/Edit/1");

        Assert.Contains("value=\".NET Bot Black Hoodie\"", html);
        Assert.Contains("id=\"Input_Price\" name=\"Input.Price\" value=\"19.5\"", html);
        Assert.Contains("id=\"PictureFileName\" type=\"text\" value=\"1.png\" readonly", html);
        Assert.Contains("<option selected=\"selected\" value=\"2\">.NET</option>", html);
        Assert.Contains("<option selected=\"selected\" value=\"2\">T-Shirt</option>", html);
    }

    [Fact]
    public async Task Edit_with_invalid_input_does_not_update()
    {
        await using var factory = new CatalogAppFactory();
        var client = factory.CreateNonRedirectingClient();
        var form = Html.ValidItemForm("Should Not Save");
        form["Input.Price"] = "-5";

        var response = await Html.PostFormAsync(client, "/Catalog/Edit/1", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The Price must be a positive number", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Should Not Save", await client.GetStringAsync("/Catalog/Details/1"));
    }

    [Fact]
    public async Task Delete_removes_item_and_redirects_to_list()
    {
        await using var factory = new CatalogAppFactory();
        var client = factory.CreateNonRedirectingClient();

        var response = await Html.PostFormAsync(client, "/Catalog/Delete/2", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Catalog/Details/2")).StatusCode);
        Assert.Contains("Showing 10 of 11 products", await client.GetStringAsync("/"));
    }
}
