using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Net.Http.Headers;

namespace eShopNet8.Mvc.Hosting
{
    public static class LegacyHeadersExtensions
    {
        /// <summary>
        /// System.Web defaults applied when the response sets nothing itself: HttpCachePolicy Cacheability.Private,
        /// and Content-Type text/html on bodiless error results (HttpNotFound / HttpStatusCodeResult).
        /// </summary>
        public static WebApplication UseLegacySystemWebDefaults(this WebApplication app)
        {
            app.Use(async (context, next) =>
            {
                context.Response.OnStarting(() =>
                {
                    if (!context.Response.Headers.ContainsKey(HeaderNames.CacheControl))
                    {
                        context.Response.Headers.CacheControl = "private";
                    }
                    if (context.Response.StatusCode >= 400 && string.IsNullOrEmpty(context.Response.ContentType))
                    {
                        context.Response.ContentType = "text/html";
                    }
                    return Task.CompletedTask;
                });
                await next();
            });
            return app;
        }
    }

    /// <summary>
    /// MVC 5's AntiForgery adds <c>X-Frame-Options: SAMEORIGIN</c> to every response that renders a token;
    /// ASP.NET Core only adds it when a new cookie token is issued (the first form page of a browser session).
    /// </summary>
    internal sealed class LegacyXFrameOptionsAntiforgery : IAntiforgery
    {
        private readonly IAntiforgery inner;

        private LegacyXFrameOptionsAntiforgery(IAntiforgery inner)
        {
            this.inner = inner;
        }

        /// <summary>Decorates the registered IAntiforgery (DefaultAntiforgery); call after AddAntiforgery.</summary>
        public static void Register(IServiceCollection services)
        {
            var descriptor = services.Last(d => d.ServiceType == typeof(IAntiforgery));
            services.Remove(descriptor);
            services.AddSingleton<IAntiforgery>(sp => new LegacyXFrameOptionsAntiforgery(descriptor.ImplementationType != null
                ? (IAntiforgery)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType)
                : descriptor.ImplementationFactory != null
                    ? (IAntiforgery)descriptor.ImplementationFactory(sp)
                    : (IAntiforgery)descriptor.ImplementationInstance!));
        }

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
        {
            var tokens = inner.GetAndStoreTokens(httpContext);
            if (!httpContext.Response.Headers.ContainsKey(HeaderNames.XFrameOptions))
            {
                httpContext.Response.Headers.XFrameOptions = "SAMEORIGIN";
            }
            return tokens;
        }

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => inner.GetTokens(httpContext);

        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => inner.IsRequestValidAsync(httpContext);

        public Task ValidateRequestAsync(HttpContext httpContext) => inner.ValidateRequestAsync(httpContext);

        public void SetCookieTokenAndHeader(HttpContext httpContext) => inner.SetCookieTokenAndHeader(httpContext);
    }
}
