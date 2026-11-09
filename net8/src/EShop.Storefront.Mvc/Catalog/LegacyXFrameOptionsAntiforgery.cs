// MVC 5 AntiForgery.GetHtml() sent "X-Frame-Options: SAMEORIGIN" on every response that rendered a token. ASP.NET Core
// only does so when it issues a new antiforgery cookie, so pages viewed with an existing cookie lost the header.
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Net.Http.Headers;

namespace EShop.Storefront.Mvc.Catalog;

public sealed class LegacyXFrameOptionsAntiforgery(IAntiforgery inner) : IAntiforgery
{
    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
    {
        var tokens = inner.GetAndStoreTokens(httpContext);
        if (!httpContext.Response.HasStarted && !httpContext.Response.Headers.ContainsKey(HeaderNames.XFrameOptions))
            httpContext.Response.Headers.XFrameOptions = "SAMEORIGIN";
        return tokens;
    }

    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => inner.GetTokens(httpContext);
    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => inner.IsRequestValidAsync(httpContext);
    public Task ValidateRequestAsync(HttpContext httpContext) => inner.ValidateRequestAsync(httpContext);
    public void SetCookieTokenAndHeader(HttpContext httpContext) => inner.SetCookieTokenAndHeader(httpContext);

    public static void Decorate(IServiceCollection services)
    {
        var original = services.Last(d => d.ServiceType == typeof(IAntiforgery));
        services.Remove(original);
        services.AddSingleton<IAntiforgery>(sp => new LegacyXFrameOptionsAntiforgery(
            original.ImplementationFactory?.Invoke(sp) as IAntiforgery
            ?? (IAntiforgery)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!)));
    }
}
