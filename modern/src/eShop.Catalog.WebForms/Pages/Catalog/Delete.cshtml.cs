using eShop.Catalog.Data.Services;
using Microsoft.AspNetCore.Mvc;

namespace eShop.Catalog.WebForms.Pages.Catalog;

/// <summary>
/// Port of legacy Catalog/Delete.aspx: GET shows the confirmation, POST removes the item and redirects to "/".
/// Razor Pages validates the antiforgery token on POST, so a missing token is a 400 with no data change
/// (intentional hardening; legacy answered 500).
/// </summary>
public class DeleteModel : CatalogItemPageModel
{
    public const string RedirectLocation = "/";

    /// <summary>Posted name of the legacy "[ Delete ]" asp:Button; only that button raises Delete_Click.</summary>
    public const string DeleteButtonName = "ctl00$MainContent$ctl11";

    public DeleteModel(ICatalogService catalogService, ILogger<DeleteModel> logger)
        : base(catalogService, logger)
    {
    }

    protected override string LegacyPagePath => "/Catalog/Delete.aspx";

    public IActionResult OnGet(string? id) => TryLoadItem(id) ? Page() : LegacyNotFound();

    public IActionResult OnPost(string? id)
    {
        if (!TryLoadItem(id))
        {
            return LegacyNotFound();
        }

        if (!Request.HasFormContentType || !Request.Form.ContainsKey(DeleteButtonName))
        {
            // A postback not raised by the Delete button just re-renders the confirmation, like legacy.
            return Page();
        }

        CatalogService.RemoveCatalogItem(Item);
        return LegacyRedirect(RedirectLocation);
    }

    /// <summary>System.Web Response.Redirect: 302 with a relative Location and the "Object moved" body.</summary>
    private ContentResult LegacyRedirect(string location)
    {
        Response.Headers.Location = location;
        return new ContentResult
        {
            StatusCode = StatusCodes.Status302Found,
            ContentType = "text/html; charset=utf-8",
            Content = "<html><head><title>Object moved</title></head><body>\r\n"
                + $"<h2>Object moved to <a href=\"{System.Net.WebUtility.HtmlEncode(location)}\">here</a>.</h2>\r\n"
                + "</body></html>\r\n",
        };
    }
}
