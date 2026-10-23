namespace eShopNet8.Mvc.Extensions;

// Same content as the legacy Views/Shared/Error.cshtml rendered by HandleErrorAttribute (status 500).
public static class LegacyErrorPage
{
    public const string Html =
        "<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <meta name=\"viewport\" content=\"width=device-width\" />\r\n" +
        "    <title>Error</title>\r\n</head>\r\n<body>\r\n    <hgroup>\r\n        <h1>Error.</h1>\r\n" +
        "        <h2>An error occurred while processing your request.</h2>\r\n    </hgroup>\r\n</body>\r\n</html>\r\n";

    public static async Task WriteAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(Html);
    }
}
