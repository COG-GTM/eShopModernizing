using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace eShop.Catalog.Mvc.Controllers.Api;

/// <summary>
/// Legacy Web API claimed every <c>api/{controller}/{id}</c> URI, so an unknown controller answered with a Web API 404
/// <c>HttpError</c> body. <c>/api</c> itself never matched that route and stays a plain 404 (the legacy MVC
/// <c>CatalogController2</c> "Hello World" route is retired: it was already unreachable on the legacy host).
/// </summary>
public sealed class UnknownApiController : LegacyApiControllerBase
{
    [Route("api/{name}/{id?}", Order = 2)]
    public IActionResult NoSuchController(string name, string? id)
    {
        _ = id;
        return new LegacyWebApiResult(
            StatusCodes.Status404NotFound,
            LegacyHttpError.NoController(Request.GetDisplayUrl(), name));
    }
}
