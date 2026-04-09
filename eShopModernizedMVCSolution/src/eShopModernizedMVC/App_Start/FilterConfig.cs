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
            // Allow output caching globally with a 60-second duration for
            // read-only catalog pages. Actions that must not be cached (e.g.
            // form posts, authenticated mutations) should override with
            // [OutputCache(Duration = 0, NoStore = true)] on the action.
            filters.Add(new OutputCacheAttribute
            {
                VaryByParam = "*",
                Duration = 60,
                Location = System.Web.UI.OutputCacheLocation.Server,
            });
        }
    }
}
