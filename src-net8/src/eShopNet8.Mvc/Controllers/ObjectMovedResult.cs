using Microsoft.AspNetCore.Mvc;

namespace eShopNet8.Mvc.Controllers
{
    /// <summary>
    /// 302 redirect with the HTML body ASP.NET (System.Web) writes for RedirectToAction ("Object moved to here"),
    /// so redirects keep the legacy text/html Content-Type instead of ASP.NET Core's body-less 302.
    /// </summary>
    public class ObjectMovedResult : ActionResult
    {
        public ObjectMovedResult(string url)
        {
            Url = url;
        }

        public string Url { get; }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            var href = System.Net.WebUtility.HtmlEncode(Url);
            response.StatusCode = StatusCodes.Status302Found;
            response.Headers.Location = Url;
            response.ContentType = "text/html; charset=utf-8";
            await response.WriteAsync($"<html><head><title>Object moved</title></head><body>\r\n<h2>Object moved to <a href=\"{href}\">here</a>.</h2>\r\n</body></html>\r\n");
        }
    }
}
