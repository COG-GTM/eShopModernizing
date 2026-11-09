using EShop.Storefront.Mvc.Catalog;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EShop.Storefront.Tests.Mvc;

public class LegacyXFrameOptionsAntiforgeryTests
{
    private sealed class ExistingCookieAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => new("request", "cookie", "__RequestVerificationToken", null);
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => GetAndStoreTokens(httpContext);
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }

    [Fact]
    public void Rendering_a_token_always_sends_sameorigin_like_mvc5()
    {
        var context = new DefaultHttpContext();
        new LegacyXFrameOptionsAntiforgery(new ExistingCookieAntiforgery()).GetAndStoreTokens(context);
        Assert.Equal("SAMEORIGIN", context.Response.Headers.XFrameOptions.ToString());
    }

    [Fact]
    public void An_existing_header_is_kept()
    {
        var context = new DefaultHttpContext();
        context.Response.Headers.XFrameOptions = "DENY";
        new LegacyXFrameOptionsAntiforgery(new ExistingCookieAntiforgery()).GetAndStoreTokens(context);
        Assert.Equal("DENY", context.Response.Headers.XFrameOptions.ToString());
    }
}
