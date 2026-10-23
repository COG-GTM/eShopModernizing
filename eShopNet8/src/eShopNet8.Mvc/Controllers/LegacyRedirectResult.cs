using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace eShopNet8.Mvc.Controllers;

// System.Web's RedirectToAction: 302 plus the classic "Object moved" HTML body recorded in the baseline.
public sealed class LegacyRedirectResult : IActionResult
{
    private readonly string _url;

    public LegacyRedirectResult(string url) => _url = url;

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status302Found;
        response.Headers.Location = _url;
        response.ContentType = "text/html; charset=utf-8";
        await response.WriteAsync(
            "<html><head><title>Object moved</title></head><body>\r\n" +
            $"<h2>Object moved to <a href=\"{WebUtility.HtmlEncode(_url)}\">here</a></h2>\r\n</body></html>\r\n");
    }
}
