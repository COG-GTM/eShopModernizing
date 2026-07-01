using Microsoft.AspNetCore.Mvc.Filters;

namespace eShopCoreModernized.Filters
{
    public class ActionTracerFilter : IActionFilter
    {
        private readonly ILogger<ActionTracerFilter> _logger;

        public ActionTracerFilter(ILogger<ActionTracerFilter> logger)
        {
            _logger = logger;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            _logger.LogDebug(
                "Executing action {Action} on controller {Controller}",
                context.ActionDescriptor.RouteValues["action"],
                context.ActionDescriptor.RouteValues["controller"]);
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            _logger.LogDebug(
                "Executed action {Action} on controller {Controller} with status {Status}",
                context.ActionDescriptor.RouteValues["action"],
                context.ActionDescriptor.RouteValues["controller"],
                context.HttpContext.Response.StatusCode);
        }
    }
}
