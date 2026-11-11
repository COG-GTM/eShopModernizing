using eShop.Catalog.Mvc.Controllers.Api;

namespace eShop.Catalog.Mvc.Tests.S5_MvcWebApi;

/// <summary>Expected values were observed on the legacy app (Web API 2 on Mono + xsp4).</summary>
public class ContentNegotiationAndBindingTests
{
    [Theory]
    [InlineData(null, "application/json")]
    [InlineData("application/json", "application/json")]
    [InlineData("text/json", "text/json")]
    [InlineData("application/xml", "application/xml")]
    [InlineData("text/xml", "text/xml")]
    [InlineData("*/*", "application/json")]
    [InlineData("application/*", "application/json")]
    [InlineData("text/*", "text/json")]
    [InlineData("text/plain", "application/json")]
    [InlineData("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8", "application/xml")]
    [InlineData("text/html,application/json;q=0.9,*/*;q=0.8", "application/json")]
    [InlineData("application/json;q=0.5, application/xml;q=0.4", "application/json")]
    [InlineData("application/xml;q=0.5, application/json;q=0.4", "application/xml")]
    [InlineData("*/*, application/xml", "application/xml")]
    [InlineData("application/json;q=0, text/xml", "text/xml")]
    [InlineData("not a media type", "application/json")]
    public void Negotiates_like_web_api_2(string? accept, string expectedMediaType)
    {
        var result = WebApiContentNegotiator.Negotiate(accept is null ? null : new[] { accept });

        Assert.Equal(expectedMediaType, result.MediaType);
        Assert.Equal(expectedMediaType + "; charset=utf-8", result.ContentType);
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("+2", 2)]
    [InlineData(" 2 ", 2)]
    [InlineData("-1", -1)]
    [InlineData("0x2", 2)]
    [InlineData("#2", 2)]
    [InlineData("&h1F", 31)]
    public void Int_binding_accepts_what_web_api_2_accepted(string text, int expected)
    {
        Assert.True(LegacyInt32Parser.TryParse(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("0x")]
    public void Int_binding_rejects_what_web_api_2_rejected(string? text)
    {
        Assert.False(LegacyInt32Parser.TryParse(text, out _));
    }
}
