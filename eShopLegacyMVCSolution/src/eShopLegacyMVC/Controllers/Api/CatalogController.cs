using Microsoft.AspNetCore.Mvc;

namespace eShopLegacyMVC.Controllers.Api
{
    [Route("api")]
    public class CatalogController2 : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return Json(new { Message = "Hello World!" });
        }
    }
}
