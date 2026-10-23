using Microsoft.AspNetCore.Mvc;

namespace eShopNet8.WebForms.Pages
{
    // Response.Redirect("~"): 302 with Location "/" and the "Object moved" text/html body ASP.NET writes
    // (the baseline compares the MIME type of 3xx responses, so an empty redirect is not enough).
    public sealed class LegacyRedirectResult : IActionResult
    {
        public LegacyRedirectResult(string url)
        {
            Url = url;
        }

        public string Url { get; }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            var encoded = System.Net.WebUtility.HtmlEncode(Url);
            response.StatusCode = StatusCodes.Status302Found;
            response.Headers.Location = Url;
            response.ContentType = "text/html; charset=utf-8";
            await response.WriteAsync(
                $"<html><head><title>Object moved</title></head><body>\r\n<h2>Object moved to <a href=\"{encoded}\">here</a>.</h2>\r\n</body></html>\r\n");
        }
    }
}
