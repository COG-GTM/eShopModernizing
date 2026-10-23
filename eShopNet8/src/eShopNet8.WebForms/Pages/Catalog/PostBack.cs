using Microsoft.AspNetCore.Antiforgery;

namespace eShopNet8.WebForms.Pages.Catalog;

public static class PostBack
{
    // A WebForms button click only fired for a genuine postback that carried the button's name. Here a valid
    // antiforgery token replaces __VIEWSTATE/__EVENTVALIDATION; anything else re-renders the page without changes.
    public static async Task<bool> IsButtonClickAsync(HttpContext context, IAntiforgery antiforgery, string buttonName)
    {
        return context.Request.HasFormContentType
            && context.Request.Form.ContainsKey(buttonName)
            && await antiforgery.IsRequestValidAsync(context);
    }

    // Convert.ToInt32(Page.RouteData.Values["id"]): missing -> 0, non-numeric -> FormatException (500).
    public static int RouteId(RouteValueDictionary values) => Convert.ToInt32(values["id"]);
}
