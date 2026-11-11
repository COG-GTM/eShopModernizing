using System.Globalization;
using System.Net;

namespace eShop.Catalog.Mvc.Tests.S2_MvcHost;

public class CurrencyCultureTests : IClassFixture<S2HostFactory>
{
    private readonly S2HostFactory factory;

    public CurrencyCultureTests(S2HostFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("de-DE,de;q=0.9")]
    public async Task Currency_values_render_with_the_legacy_en_US_dollar_symbol(string? acceptLanguage)
    {
        // Linux containers typically run with the invariant culture, which formats currency as "¤19.50".
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        try
        {
            await AssertDollarPriceAsync(acceptLanguage);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private async Task AssertDollarPriceAsync(string? acceptLanguage)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Catalog/Delete/1");
        if (acceptLanguage is not null)
        {
            request.Headers.Add("Accept-Language", acceptLanguage);
        }

        using var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("$19.50", html);
        Assert.DoesNotContain("\u00A4", html);
    }
}
