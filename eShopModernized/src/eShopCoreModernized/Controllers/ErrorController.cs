using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace eShopCoreModernized.Controllers
{
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        [Route("Error")]
        public IActionResult Index()
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionFeature?.Error != null)
            {
                _logger.LogError(exceptionFeature.Error, "Unhandled exception at {Path}", exceptionFeature.Path);
            }

            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return View("Error");
        }
    }
}
