using Microsoft.AspNetCore.Mvc;

namespace eShopNet8.WebForms.Pages.Catalog;

// Response.Redirect("~"): 302 to "/" with the classic ASP.NET "Object moved" body.
public class LegacyRedirectResult : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status302Found;
        response.Headers.Location = "/";
        response.ContentType = "text/html; charset=utf-8";
        await response.WriteAsync("<html><head><title>Object moved</title></head><body>\r\n<h2>Object moved to <a href=\"/\">here</a></h2>\r\n</body></html>\r\n");
    }
}
