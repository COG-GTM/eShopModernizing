namespace eShopNet8.Mvc.Api
{
    /// <summary>
    /// Web API 2 on System.Web marks every controller response without its own cache policy as uncacheable:
    /// <c>Cache-Control: no-cache</c>, <c>Pragma: no-cache</c>, <c>Expires: -1</c> (including 404s).
    /// </summary>
    public static class LegacyApiCacheHeaders
    {
        public static TBuilder WithLegacyNoCacheHeaders<TBuilder>(this TBuilder builder)
            where TBuilder : IEndpointConventionBuilder
        {
            return builder.AddEndpointFilter(async (context, next) =>
            {
                var headers = context.HttpContext.Response.Headers;
                headers.CacheControl = "no-cache";
                headers.Pragma = "no-cache";
                headers.Expires = "-1";
                return await next(context);
            });
        }
    }
}
