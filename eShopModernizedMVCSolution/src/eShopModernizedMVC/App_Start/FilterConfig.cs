using eShopModernizedMVC.Filters;
using System.Web.Mvc;

namespace eShopModernizedMVC
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new ActionTracerFilter());
            filters.Add(new HandleErrorAttribute());
            // Global no-cache default — safe for mutations and auth actions.
            // Read-only actions opt in via [OutputCache] on the action method.
            filters.Add(new OutputCacheAttribute
            {
                VaryByParam = "*",
                Duration = 0,
                NoStore = true,
            });
        }
    }
}
