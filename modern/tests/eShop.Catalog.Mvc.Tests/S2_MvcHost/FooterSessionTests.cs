using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace eShop.Catalog.Mvc.Tests.S2_MvcHost;

public class FooterSessionTests : IClassFixture<S2HostFactory>
{
    private const string SessionCookie = ".eShop.Catalog.Mvc.Session";
    private readonly S2HostFactory factory;

    public FooterSessionTests(S2HostFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Footer_timestamp_is_stable_with_the_session_cookie_and_changes_without_it()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        using var first = await client.GetAsync("/__s2-probe/layout");
        var cookie = Assert.Single(first.Headers.GetValues("Set-Cookie"), c => c.StartsWith(SessionCookie + "=", StringComparison.Ordinal));
        var firstFooter = LegacyBaseline.FooterText(await first.Content.ReadAsStringAsync());

        factory.Time.Advance(TimeSpan.FromMinutes(3));
        using var withCookie = new HttpRequestMessage(HttpMethod.Get, "/__s2-probe/layout");
        withCookie.Headers.Add("Cookie", cookie.Split(';')[0]);
        using var second = await client.SendAsync(withCookie);
        var secondFooter = LegacyBaseline.FooterText(await second.Content.ReadAsStringAsync());

        factory.Time.Advance(TimeSpan.FromMinutes(3));
        using var third = await client.GetAsync("/__s2-probe/layout");
        var thirdFooter = LegacyBaseline.FooterText(await third.Content.ReadAsStringAsync());

        Assert.NotNull(firstFooter);
        Assert.Equal(firstFooter, secondFooter);
        Assert.False(second.Headers.Contains("Set-Cookie"));
        Assert.NotEqual(firstFooter, thirdFooter);
        Assert.True(third.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Footer_is_stable_across_two_pages_of_one_browser_session()
    {
        using var browser = factory.CreateClient();

        var first = LegacyBaseline.FooterText(await browser.GetStringAsync("/__s2-probe/layout"));
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        using var errorPage = await browser.GetAsync("/__s2-probe/throw");
        var second = LegacyBaseline.FooterText(await errorPage.Content.ReadAsStringAsync());

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Footer_shows_machine_name_and_en_US_start_time_regardless_of_Accept_Language()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__s2-probe/layout");
        request.Headers.Add("Accept-Language", "de-DE,de;q=0.9");
        var expectedTime = factory.Time.GetLocalNow().DateTime.ToString("M/d/yyyy h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"{Environment.MachineName}, {expectedTime}", LegacyBaseline.FooterText(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Static_files_and_health_checks_do_not_start_a_session()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        using var css = await client.GetAsync("/Content/site.css");
        using var health = await client.GetAsync("/healthz");

        Assert.False(css.Headers.Contains("Set-Cookie"));
        Assert.False(health.Headers.Contains("Set-Cookie"));
    }
}
