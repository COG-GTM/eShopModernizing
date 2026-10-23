using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eShopNet8.Mvc.Extensions;

public static class LegacyValidationSummaryExtensions
{
    // MVC 5 ValidationSummary(excludePropertyErrors: true, ...) rendered the summary container (with a hidden item)
    // whenever ModelState was invalid; ASP.NET Core renders nothing unless there is a model-level error.
    public static IHtmlContent LegacyValidationSummary(this IHtmlHelper html, string cssClass)
    {
        var modelState = html.ViewData.ModelState;
        if (modelState.IsValid)
        {
            return HtmlString.Empty;
        }

        var list = new TagBuilder("ul");
        var prefix = html.ViewData.TemplateInfo.HtmlFieldPrefix;
        if (modelState.TryGetValue(prefix, out var entry) && entry.Errors.Count > 0)
        {
            foreach (var error in entry.Errors)
            {
                var item = new TagBuilder("li");
                item.InnerHtml.SetContent(error.ErrorMessage);
                list.InnerHtml.AppendHtml(item);
            }
        }
        else
        {
            list.InnerHtml.AppendHtml("<li style=\"display:none\"></li>");
        }

        var div = new TagBuilder("div");
        div.AddCssClass(cssClass);
        div.AddCssClass("validation-summary-errors");
        div.InnerHtml.AppendHtml(list);
        return div;
    }
}
