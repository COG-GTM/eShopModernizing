using System.Net;
using System.Text.RegularExpressions;
using eShop.Catalog.Data.Models;
using eShop.Catalog.Data.Services;
using eShop.Catalog.WebForms.Pages.Catalog;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Catalog.WebForms.Tests.S8_PagesWrite;

/// <summary>Each test gets its own host so the singleton mock catalog starts at the 12 seeded items.</summary>
public sealed partial class FormRoundTripTests : IDisposable
{
    internal const string ChromeUserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";
    private const string P = CatalogItemFormInput.FieldNamePrefix;
    private readonly WebApplicationFactory<Program> factory = new();
    private readonly HttpClient client;

    public FormRoundTripTests()
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private ICatalogService Catalog => factory.Services.GetRequiredService<ICatalogService>();

    private long Count => Catalog.GetCatalogItemsPaginated(100, 0).TotalItems;

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    [Theory]
    [InlineData("/Catalog/Create")]
    [InlineData("/Catalog/Create.aspx")]
    public async Task Get_Create_renders_legacy_fields_labels_options_and_validators(string url)
    {
        var html = await GetOk(url);

        Assert.Contains("<form method=\"post\" action=\"Create\" id=\"ctl00_ctl01\">", html);
        Assert.Contains("<h2 class=\"esh-body-title\">Create</h2>", html);
        foreach (var label in new[] { "Name", "Description", "Brand", "Type", "Price", "Picture name", "Stock", "Restock", "Max stock" })
        {
            Assert.Contains($"<label class=\"control-label col-md-2\">{label}</label>", html);
        }

        Assert.Contains("<input type=\"text\" name=\"ctl00$MainContent$Name\" id=\"ctl00_MainContent_Name\" class=\"form-control\" />", html);
        Assert.Contains("<input type=\"text\" value=\"0.00\" name=\"ctl00$MainContent$Price\" id=\"ctl00_MainContent_Price\" class=\"form-control\" />", html);
        foreach (var field in new[] { "Stock", "Restock", "Maxstock" })
        {
            Assert.Contains($"<input type=\"text\" value=\"0\" name=\"ctl00$MainContent${field}\" id=\"ctl00_MainContent_{field}\" class=\"form-control\" />", html);
        }

        Assert.Equal(["Azure", ".NET", "Visual Studio", "SQL Server", "Other"], Options(html, "Brand").Select(o => o.Text));
        Assert.Equal(["Mug", "T-Shirt", "Sheet", "USB Memory Stick"], Options(html, "Type").Select(o => o.Text));
        Assert.Equal(["1", "2", "3", "4", "5"], Options(html, "Brand").Select(o => o.Value));

        Assert.DoesNotContain("<span", html);
        Assert.DoesNotContain("<script", html);
        Assert.Contains("<div class=\"col-md-4 esh-form-information\">", html);
        Assert.Contains("value=\"[ Create ]\" class=\"btn esh-button esh-button-primary\" />", html);
        Assert.Matches(TokenPattern(), html);
    }

    [Fact]
    public async Task Get_Create_for_script_capable_browser_renders_unobtrusive_validators_and_script()
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(ChromeUserAgent);

        var html = await GetOk("/Catalog/Create");

        Assert.Contains("<span data-val-controltovalidate=\"ctl00_MainContent_Name\" data-val-errormessage=\"The Name field is required.\" data-val-display=\"Dynamic\" id=\"ctl00_MainContent_ctl00\" class=\"field-validation-valid text-danger\" data-val=\"true\" data-val-evaluationfunction=\"RequiredFieldValidatorEvaluateIsValid\" data-val-initialvalue=\"\" style=\"display:none;\">The Name field is required.</span>", html);
        Assert.Contains("<span data-val-controltovalidate=\"ctl00_MainContent_Price\" data-val-errormessage=\"The Price must be a positive number with maximum two decimals between 0 and 1 million.\" data-val-display=\"Dynamic\" id=\"ctl00_MainContent_ctl01\" class=\"text-danger\" data-val=\"true\" data-val-type=\"Currency\" data-val-decimalchar=\".\" data-val-groupchar=\",\" data-val-digits=\"2\" data-val-groupsize=\"3\" data-val-evaluationfunction=\"RangeValidatorEvaluateIsValid\" data-val-maximumvalue=\"1000000\" data-val-minimumvalue=\"0\" style=\"display:none;\">The Price must be a positive number with maximum two decimals between 0 and 1 million.</span>", html);
        Assert.Contains("<span data-val-controltovalidate=\"ctl00_MainContent_Stock\" data-val-errormessage=\"The field Stock must be between 0 and 10 million.\" data-val-display=\"Dynamic\" id=\"ctl00_MainContent_ctl02\" class=\"text-danger\" data-val=\"true\" data-val-type=\"Integer\" data-val-evaluationfunction=\"RangeValidatorEvaluateIsValid\" data-val-maximumvalue=\"10000000\" data-val-minimumvalue=\"0\" style=\"display:none;\">The field Stock must be between 0 and 10 million.</span>", html);
        Assert.Contains("id=\"ctl00_MainContent_ctl03\" class=\"text-danger\" data-val=\"true\"", html);
        Assert.Contains(">The field Restock must be between 0 and 10 million.</span>", html);
        Assert.Contains("id=\"ctl00_MainContent_ctl04\" class=\"text-danger\" data-val=\"true\"", html);
        Assert.Contains(">The field Max stock must be between 0 and 10 million.</span>", html);
        Assert.Contains("form.addEventListener('submit'", html);
    }

    [Fact]
    public async Task Valid_Create_post_redirects_to_root_and_adds_item_13()
    {
        Assert.Equal(12L, Count);

        using var response = await Post("/Catalog/Create", await Token("/Catalog/Create"), ValidCreateForm("New Mug"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.OriginalString);
        Assert.Equal(13L, Count);
        var created = Catalog.FindCatalogItem(13)!;
        Assert.Equal("New Mug", created.Name);
        Assert.Equal("Created description", created.Description);
        Assert.Equal(3, created.CatalogBrandId);
        Assert.Equal(4, created.CatalogTypeId);
        Assert.Equal(12.34m, created.Price);
        Assert.Equal((5, 2, 50), (created.AvailableStock, created.RestockThreshold, created.MaxStockThreshold));
        Assert.Equal(CatalogItem.DefaultPictureName, created.PictureFileName);
        Assert.False(created.OnReorder);
    }

    [Fact]
    public async Task Create_post_cannot_overpost_PictureFileName_or_OnReorder()
    {
        var form = ValidCreateForm("Overposted");
        form[P + "PictureFileName"] = "../../web.config";
        form["Input.PictureFileName"] = "../../web.config";
        form["PictureFileName"] = "../../web.config";
        form["OnReorder"] = "true";
        form["Input.OnReorder"] = "true";

        using var response = await Post("/Catalog/Create", await Token("/Catalog/Create"), form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var created = Catalog.FindCatalogItem(13)!;
        Assert.Equal(CatalogItem.DefaultPictureName, created.PictureFileName);
        Assert.False(created.OnReorder);
    }

    [Theory]
    [InlineData("Name", "", CatalogItemFormInput.NameRequiredMessage)]
    [InlineData("Price", "1000000.01", CatalogItemFormInput.PriceRangeMessage)]
    [InlineData("Price", "-1", CatalogItemFormInput.PriceRangeMessage)]
    [InlineData("Stock", "10000001", CatalogItemFormInput.StockRangeMessage)]
    [InlineData("Restock", "-1", CatalogItemFormInput.RestockRangeMessage)]
    [InlineData("Maxstock", "abc", CatalogItemFormInput.MaxStockRangeMessage)]
    public async Task Invalid_Create_post_rerenders_with_legacy_message_and_no_data_change(string field, string value, string message)
    {
        var form = ValidCreateForm("Should not be saved");
        form[P + field] = value;

        using var response = await Post("/Catalog/Create", await Token("/Catalog/Create"), form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches($"<span id=\"ctl00_MainContent_ctl0\\d\" class=\"[a-z -]+\">{Regex.Escape(message)}</span>", html);
        Assert.Equal([message], DownlevelMessages(html));
        Assert.Equal(12L, Count);
        Assert.Contains("<option selected=\"selected\" value=\"3\">Visual Studio</option>", html);
        if (value.Length > 0)
        {
            Assert.Contains($"value=\"{value}\" name=\"ctl00$MainContent${field}\"", html);
        }
    }

    [Fact]
    public async Task Create_post_without_antiforgery_token_is_400_with_no_data_change()
    {
        await GetOk("/Catalog/Create");

        using var response = await Post("/Catalog/Create", token: null, ValidCreateForm("No token"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(12L, Count);
    }

    [Fact]
    public async Task Create_post_with_unknown_brand_is_rejected_with_no_data_change()
    {
        var form = ValidCreateForm("Bad brand");
        form[P + "Brand"] = "99";

        using var response = await Post("/Catalog/Create", await Token("/Catalog/Create"), form);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(12L, Count);
    }

    [Fact]
    public async Task Get_Edit_renders_legacy_form_with_readonly_picture()
    {
        var html = await GetOk("/Catalog/Edit/1");

        Assert.Contains("<form method=\"post\" action=\"1\" id=\"ctl00_ctl01\">", html);
        Assert.Contains("<img class=\"col-md-6 esh-picture\" src=\"/Pics/1.png\" />", html);
        Assert.Contains("<input type=\"text\" value=\".NET Bot Black Hoodie\" name=\"ctl00$MainContent$Name\" id=\"ctl00_MainContent_Name\" class=\"form-control\" />", html);
        Assert.Contains("<input type=\"text\" value=\"19.5\" name=\"ctl00$MainContent$Price\" id=\"ctl00_MainContent_Price\" class=\"form-control\" />", html);
        Assert.Contains("<input type=\"text\" value=\"1.png\" readonly=\"ReadOnly\" name=\"ctl00$MainContent$PictureFileName\" id=\"ctl00_MainContent_PictureFileName\" title=\"Not allowed for edition\" class=\"form-control\" />", html);
        Assert.Contains("<option selected=\"selected\" value=\"2\">.NET</option>", html);
        Assert.Contains("<option selected=\"selected\" value=\"2\">T-Shirt</option>", html);
        Assert.Equal(["Azure", ".NET", "Visual Studio", "SQL Server", "Other"], Options(html, "BrandDropDownList").Select(o => o.Text));
        Assert.Contains("<label class=\"control-label col-md-4\">Max stock</label>", html);
        Assert.Contains("value=\"[ Save ]\" class=\"btn esh-button esh-button-primary\" />", html);
        Assert.Matches(TokenPattern(), html);
    }

    [Fact]
    public async Task Valid_Edit_post_redirects_to_root_updates_values_and_keeps_PictureFileName()
    {
        var before = Catalog.FindCatalogItem(1)!;
        var onReorder = before.OnReorder;
        var form = new Dictionary<string, string>
        {
            [P + "Name"] = "Updated Hoodie",
            [P + "Description"] = "Updated description",
            [P + "BrandDropDownList"] = "4",
            [P + "TypeDropDownList"] = "1",
            [P + "Price"] = "21.75",
            [P + "PictureFileName"] = "../../evil.png",
            [P + "Stock"] = "7",
            [P + "Restock"] = "3",
            [P + "Maxstock"] = "70",
            ["OnReorder"] = (!onReorder).ToString(),
            ["Input.OnReorder"] = (!onReorder).ToString(),
        };

        using var response = await Post("/Catalog/Edit/1", await Token("/Catalog/Edit/1"), form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.OriginalString);
        var after = Catalog.FindCatalogItem(1)!;
        Assert.Equal("Updated Hoodie", after.Name);
        Assert.Equal("Updated description", after.Description);
        Assert.Equal(4, after.CatalogBrandId);
        Assert.Equal(1, after.CatalogTypeId);
        Assert.Equal(21.75m, after.Price);
        Assert.Equal((7, 3, 70), (after.AvailableStock, after.RestockThreshold, after.MaxStockThreshold));
        Assert.Equal("1.png", after.PictureFileName);
        Assert.Equal(onReorder, after.OnReorder);
        Assert.Equal(12L, Count);
    }

    [Fact]
    public async Task Invalid_Edit_post_rerenders_with_legacy_messages_and_no_data_change()
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(ChromeUserAgent);
        var form = new Dictionary<string, string>
        {
            [P + "Name"] = "",
            [P + "Description"] = "x",
            [P + "BrandDropDownList"] = "4",
            [P + "TypeDropDownList"] = "1",
            [P + "Price"] = "1.999",
            [P + "Stock"] = "-5",
            [P + "Restock"] = "0",
            [P + "Maxstock"] = "0",
        };

        using var response = await Post("/Catalog/Edit/1", await Token("/Catalog/Edit/1"), form);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            [CatalogItemFormInput.NameRequiredMessage, CatalogItemFormInput.PriceRangeMessage, CatalogItemFormInput.StockRangeMessage],
            UplevelInvalidMessages(html));
        Assert.Contains("data-val-isvalid=\"False\" id=\"ctl00_MainContent_ctl01\" class=\"field-validation-valid text-danger\" data-val=\"true\" data-val-evaluationfunction=\"RequiredFieldValidatorEvaluateIsValid\" data-val-initialvalue=\"\">The Name field is required.</span>", html);
        Assert.Contains("value=\"1.png\" readonly=\"ReadOnly\"", html);
        var item = Catalog.FindCatalogItem(1)!;
        Assert.Equal(".NET Bot Black Hoodie", item.Name);
        Assert.Equal(19.5m, item.Price);
        Assert.Equal(100, item.AvailableStock);
    }

    [Fact]
    public async Task Edit_post_without_antiforgery_token_is_400_with_no_data_change()
    {
        await GetOk("/Catalog/Edit/1");
        var form = new Dictionary<string, string> { [P + "Name"] = "No token", [P + "BrandDropDownList"] = "1", [P + "TypeDropDownList"] = "1", [P + "Price"] = "1", [P + "Stock"] = "1", [P + "Restock"] = "1", [P + "Maxstock"] = "1" };

        using var response = await Post("/Catalog/Edit/1", token: null, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(".NET Bot Black Hoodie", Catalog.FindCatalogItem(1)!.Name);
    }

    [Fact]
    public async Task Edit_post_with_unknown_type_is_rejected_with_no_data_change()
    {
        var form = new Dictionary<string, string> { [P + "Name"] = "Bad type", [P + "BrandDropDownList"] = "1", [P + "TypeDropDownList"] = "42", [P + "Price"] = "1", [P + "Stock"] = "1", [P + "Restock"] = "1", [P + "Maxstock"] = "1" };

        using var response = await Post("/Catalog/Edit/1", await Token("/Catalog/Edit/1"), form);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(".NET Bot Black Hoodie", Catalog.FindCatalogItem(1)!.Name);
        Assert.Equal(2, Catalog.FindCatalogItem(1)!.CatalogTypeId);
    }

    private static Dictionary<string, string> ValidCreateForm(string name)
    {
        return new Dictionary<string, string>
        {
            [P + "Name"] = name,
            [P + "Description"] = "Created description",
            [P + "Brand"] = "3",
            [P + "Type"] = "4",
            [P + "Price"] = "12.34",
            [P + "Stock"] = "5",
            [P + "Restock"] = "2",
            [P + "Maxstock"] = "50",
        };
    }

    private static List<(string Value, string Text)> Options(string html, string dropDown)
    {
        var select = Regex.Match(html, $"<select name=\"ctl00\\$MainContent\\${dropDown}\"[^>]*>(.*?)</select>", RegexOptions.Singleline).Groups[1].Value;
        return Regex.Matches(select, "<option(?: selected=\"selected\")? value=\"([^\"]*)\">([^<]*)</option>")
            .Select(m => (m.Groups[1].Value, WebUtility.HtmlDecode(m.Groups[2].Value)))
            .ToList();
    }

    private static List<string> UplevelInvalidMessages(string html)
    {
        return Regex.Matches(html, "data-val-isvalid=\"False\"[^>]*>([^<]*)</span>").Select(m => m.Groups[1].Value).ToList();
    }

    private static List<string> DownlevelMessages(string html)
    {
        return Regex.Matches(html, "<span id=\"ctl00_MainContent_ctl\\d+\" class=\"[^\"]*\">([^<]*)</span>").Select(m => m.Groups[1].Value).ToList();
    }

    private async Task<string> GetOk(string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> Token(string url)
    {
        return TokenPattern().Match(await GetOk(url)).Groups[1].Value;
    }

    private async Task<HttpResponseMessage> Post(string url, string? token, Dictionary<string, string> form)
    {
        if (token is not null)
        {
            form["__RequestVerificationToken"] = token;
        }

        return await client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    [GeneratedRegex("<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\" />")]
    private static partial Regex TokenPattern();
}
