using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace eShopNet8.WebForms.Pages
{
    // Shared code-behind helpers reproducing System.Web.UI.Page behaviour the catalog pages rely on.
    public abstract class LegacyPageModel : PageModel
    {
        public const string ControlPrefix = "ctl00$MainContent$";

        // Convert.ToInt32(Page.RouteData.Values[key]): FormatException / OverflowException surface as HTTP 500.
        protected int RouteInt(string key) => Convert.ToInt32(RouteData.Values[key]);

        protected string Posted(string controlId) => Request.Form[ControlPrefix + controlId].ToString();

        protected bool Clicked(string buttonId) => Request.Form.ContainsKey(ControlPrefix + buttonId);

        // Event validation: a DropDownList only accepts values it rendered ("Invalid postback or callback argument").
        protected static string ValidatedSelection(string posted, IEnumerable<string> allowed, string controlId)
        {
            if (!allowed.Contains(posted))
            {
                throw new ArgumentException($"Invalid postback or callback argument for {ControlPrefix}{controlId}.");
            }
            return posted;
        }

        protected IActionResult RedirectToRoot() => new LegacyRedirectResult(Url.Content("~/"));
    }
}
