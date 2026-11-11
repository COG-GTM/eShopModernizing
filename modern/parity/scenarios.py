"""Parity matrix for replay.py: the request list sent to legacy and modern, in order, from a fresh seed.

Each step is a dict:
  id      unique per app
  method  GET | POST
  path    request path (POST without "form" posts an empty body)
  form    POST only: {"from": page to GET first (default: path), "set": {field: value}, "drop": [field names],
                      "submit": value of the submit <input> to click (WebForms), "extra": {field: value}}
          The form is read from the *same side's* rendered page, so tokens/viewstate are real; "set" overrides
          fields present in the form, "extra" adds fields that are not rendered (over-posting).
  note    what the step proves (shown in the report)
  "{last_id}" in a path is resolved per side to the highest catalog id currently listed (ids of items created
  after the sides' data diverged are not assumed).
Order matters: the legacy mock mutates on first list render and the write flow builds on earlier steps
(item 13 is the first id issued on a fresh seed). Steps that legitimately change state differently on each side
(intentional hardenings) are kept at the end of each flow.
"""

WF = "ctl00$MainContent$"

STATIC_MVC = ["/Content/Site.css", "/Content/site.css", "/Images/brand.png", "/images/brand.png",
              "/images/main_footer_text.png", "/fonts/Montserrat-Regular.eot", "/fonts/Montserrat-Regular.woff",
              "/fonts/Montserrat-Regular.woff2", "/fonts/Montserrat-Bold.ttf", "/fonts/Montserrat-Regular.svg", "/favicon.ico", "/Pics/1.png"]
STATIC_WF = ["/Content/site.css", "/Content/Site.css", "/images/brand.png", "/Images/brand.png",
             "/fonts/Montserrat-Regular.eot", "/fonts/Montserrat-Bold.woff2", "/favicon.ico", "/Pics/1.png"]

MVC_VALID = {"Name": "Parity Lipstick", "Description": "Velvet matte, parity run", "CatalogBrandId": "2",
             "CatalogTypeId": "1", "Price": "42.50", "AvailableStock": "10", "RestockThreshold": "2",
             "MaxStockThreshold": "50"}
MVC_EDIT = {"Name": "Parity Lipstick Edited", "Description": "Edited by the parity run", "Price": "39.95",
            "AvailableStock": "7"}

WF_VALID = {WF + "Name": "Parity Serum", WF + "Description": "Hydrating serum, parity run", WF + "Brand": "2",
            WF + "Type": "1", WF + "Price": "42.50", WF + "Stock": "10", WF + "Restock": "2", WF + "Maxstock": "50"}
WF_EDIT = {WF + "Name": "Parity Serum Edited", WF + "Description": "Edited by the parity run", WF + "Price": "39.95",
           WF + "Stock": "7"}

LONG_NAME = "N" * 51
MARKUP = "<script>alert('x')</script><b>Bold</b>"


def get(i, path, note=""):
    return {"id": i, "method": "GET", "path": path, "note": note}


def post(i, path, note="", **form):
    return {"id": i, "method": "POST", "path": path, "form": form, "note": note}


MVC = [
    get("list-root", "/", "catalog list, first page"),
    get("list-p1-s10", "/Catalog/Index?pageSize=10&pageIndex=1", "paging: second page of 10"),
    get("list-p2-s5", "/Catalog/Index?pageSize=5&pageIndex=2", "paging: third page of 5"),
    get("list-p0-s3", "/Catalog/Index?pageSize=3&pageIndex=0", "paging: page size 3"),
    get("list-p99", "/Catalog/Index?pageSize=10&pageIndex=99", "paging: past the last page"),
    get("list-nonint", "/Catalog/Index?pageSize=abc&pageIndex=x", "paging: non-int query values"),
    get("details-1", "/Catalog/Details/1", "details, valid id"),
    get("details-999", "/Catalog/Details/999", "details, unknown id"),
    get("details-missing", "/Catalog/Details", "details, missing id"),
    get("details-nonint", "/Catalog/Details/abc", "details, non-int id"),
    get("pic-1", "/items/1/pic", "picture endpoint"),
    get("pic-999", "/items/999/pic", "picture, unknown id"),
    get("pic-0", "/items/0/pic", "picture, id 0"),
    get("pic-nonint", "/items/abc/pic", "picture, non-int id"),
] + [get("static" + p.replace("/", "-").replace(".", "-"), p, "static asset") for p in STATIC_MVC] + [
    get("api", "/api", "MVC attribute route /api"),
    get("api-brands", "/api/brands", "Web API brands"),
    get("api-brands-2", "/api/brands/2", "Web API brand by id"),
    get("api-brands-999", "/api/brands/999", "Web API brand, unknown id"),
    get("api-files", "/api/files", "Web API files (BinaryFormatter in legacy)"),
    get("create-get", "/Catalog/Create", "create form"),
    get("edit-1-get", "/Catalog/Edit/1", "edit form, seed item"),
    get("edit-999-get", "/Catalog/Edit/999", "edit, unknown id"),
    get("edit-missing-get", "/Catalog/Edit", "edit, missing id"),
    get("delete-1-get", "/Catalog/Delete/1", "delete confirmation, seed item"),
    get("delete-999-get", "/Catalog/Delete/999", "delete, unknown id"),
    post("create-post-empty", "/Catalog/Create", "validation: empty form",
         set={"Name": "", "Price": "", "AvailableStock": "", "RestockThreshold": "", "MaxStockThreshold": ""}),
    post("create-post-noname", "/Catalog/Create", "validation: Name required", set=dict(MVC_VALID, Name="")),
    post("create-post-price-text", "/Catalog/Create", "validation: Price not a number",
         set=dict(MVC_VALID, Price="abc")),
    post("create-post-price-range", "/Catalog/Create", "validation: Price out of range",
         set=dict(MVC_VALID, Price="2000000")),
    post("create-post-price-3dp", "/Catalog/Create", "validation: Price with 3 decimals",
         set=dict(MVC_VALID, Price="1.234")),
    post("create-post-stock-range", "/Catalog/Create", "validation: stock fields out of range",
         set=dict(MVC_VALID, AvailableStock="-1", RestockThreshold="20000000", MaxStockThreshold="x")),
    post("create-post-no-antiforgery", "/Catalog/Create", "missing antiforgery token",
         set=MVC_VALID, drop=["__RequestVerificationToken"]),
    get("list-after-noaf", "/Catalog/Index?pageSize=10&pageIndex=1", "no item was created by the rejected post"),
    post("create-post-valid", "/Catalog/Create", "round-trip: create", set=MVC_VALID),
    get("details-13", "/Catalog/Details/13", "round-trip: new item has id 13"),
    get("list-after-create", "/Catalog/Index?pageSize=10&pageIndex=1", "round-trip: new item listed"),
    get("pic-13", "/items/13/pic", "round-trip: new item picture (default)"),
    get("edit-13-get", "/Catalog/Edit/13", "round-trip: edit form of the new item"),
    post("edit-13-post-noname", "/Catalog/Edit/13", "edit validation: Name required", set={"Name": ""}),
    post("edit-13-post-price-text", "/Catalog/Edit/13", "edit validation: Price not a number",
         set={"Price": "twelve"}),
    post("edit-13-post-no-antiforgery", "/Catalog/Edit/13", "edit without antiforgery token",
         set=MVC_EDIT, drop=["__RequestVerificationToken"]),
    get("details-13-after-noaf", "/Catalog/Details/13", "item unchanged by the rejected edit"),
    post("edit-13-post-valid", "/Catalog/Edit/13", "round-trip: edit", set=MVC_EDIT),
    get("details-13-edited", "/Catalog/Details/13", "round-trip: edit persisted"),
    get("delete-13-get", "/Catalog/Delete/13", "round-trip: delete confirmation"),
    post("delete-13-post-no-antiforgery", "/Catalog/Delete/13", "delete without antiforgery token",
         drop=["__RequestVerificationToken"]),
    get("details-13-after-noaf-delete", "/Catalog/Details/13", "item still there after the rejected delete"),
    post("delete-13-post", "/Catalog/Delete/13", "round-trip: delete"),
    get("details-13-deleted", "/Catalog/Details/13", "round-trip: item gone"),
    get("list-after-delete", "/Catalog/Index?pageSize=10&pageIndex=1", "round-trip: list back to seed"),
    # --- intentional hardenings (state may diverge from here on) ---
    post("edit-1-overpost-picture", "/Catalog/Edit/1", "over-post PictureFileName with a traversal path",
         set={"PictureFileName": "../Web.config"}),
    get("pic-1-after-overpost", "/items/1/pic", "picture of item 1 after the over-post attempt"),
    get("details-1-after-overpost", "/Catalog/Details/1", "details of item 1 after the over-post attempt"),
    post("create-post-markup", "/Catalog/Create", "markup in Name and Description",
         set=dict(MVC_VALID, Name=MARKUP, Description=MARKUP)),
    get("details-last-markup", "/Catalog/Details/{last_id}", "markup item rendered HTML-encoded"),
    post("create-post-long-name", "/Catalog/Create", "Name longer than 50 characters",
         set=dict(MVC_VALID, Name=LONG_NAME)),
    post("create-post-price-overflow", "/Catalog/Create", "Price beyond Int32",
         set=dict(MVC_VALID, Price="99999999999")),
]

WEBFORMS = [
    get("list-root", "/", "catalog list, first page"),
    get("list-default-1-10", "/Default/index/1/size/10", "paging: second page of 10"),
    get("list-default-0-5", "/Default/index/0/size/5", "paging: first page of 5"),
    get("list-default-2-5", "/Default/index/2/size/5", "paging: third page of 5"),
    get("list-default-99", "/Default/index/99/size/10", "paging: past the last page"),
    get("default-aspx", "/Default.aspx", "Default.aspx alias"),
    get("about-aspx", "/About.aspx", "About page"),
    get("contact-aspx", "/Contact.aspx", "Contact page"),
    get("about", "/About", "About without extension"),
    get("contact", "/Contact", "Contact without extension"),
    get("details-1", "/Catalog/Details/1", "details, valid id"),
    get("details-999", "/Catalog/Details/999", "details, unknown id"),
    get("details-nonint", "/Catalog/Details/abc", "details, non-int id"),
    get("details-aspx", "/Catalog/Details.aspx?id=1", "Details.aspx with a query-string id (legacy reads RouteData)"),
    get("edit-aspx", "/Catalog/Edit.aspx?id=1", "Edit.aspx with a query-string id"),
    get("delete-aspx", "/Catalog/Delete.aspx?id=1", "Delete.aspx with a query-string id"),
] + [get("static" + p.replace("/", "-").replace(".", "-"), p, "static asset") for p in STATIC_WF] + [
    get("create-get", "/Catalog/Create", "create form"),
    get("edit-1-get", "/Catalog/Edit/1", "edit form, seed item"),
    get("delete-1-get", "/Catalog/Delete/1", "delete confirmation, seed item"),
    post("create-post-price-text", "/Catalog/Create", "validation: Price not a number",
         set=dict(WF_VALID, **{WF + "Price": "abc"}), submit="[ Create ]"),
    post("create-post-no-viewstate", "/Catalog/Create", "post without __VIEWSTATE / antiforgery token",
         set=WF_VALID, submit="[ Create ]", drop=["__VIEWSTATE", "__VIEWSTATEGENERATOR", "__EVENTVALIDATION",
                                                  "__RequestVerificationToken"]),
    get("list-after-noaf", "/Default/index/1/size/10", "no item was created by the rejected post"),
    post("create-post-valid", "/Catalog/Create", "round-trip: create", set=WF_VALID, submit="[ Create ]"),
    get("details-13", "/Catalog/Details/13", "round-trip: new item has id 13"),
    get("list-after-create", "/Default/index/1/size/10", "round-trip: new item listed"),
    get("edit-13-get", "/Catalog/Edit/13", "round-trip: edit form of the new item"),
    post("edit-13-post-valid", "/Catalog/Edit/13", "round-trip: edit", set=WF_EDIT, submit="[ Save ]"),
    get("details-13-edited", "/Catalog/Details/13", "round-trip: edit persisted"),
    get("delete-13-get", "/Catalog/Delete/13", "round-trip: delete confirmation"),
    post("delete-13-post-no-button", "/Catalog/Delete/13", "postback without the Delete button field"),
    get("details-13-after-nobutton", "/Catalog/Details/13", "item still there"),
    post("delete-13-post", "/Catalog/Delete/13", "round-trip: delete", submit="[ Delete ]"),
    get("list-after-delete", "/Default/index/1/size/10", "round-trip: list back to seed"),
    get("details-13-deleted", "/Catalog/Details/13", "round-trip: deleted item (legacy NRE)"),
    # --- intentional hardenings (state may diverge from here on) ---
    post("create-post-markup", "/Catalog/Create", "markup in Name and Description",
         set=dict(WF_VALID, **{WF + "Name": MARKUP, WF + "Description": MARKUP}), submit="[ Create ]"),
    get("details-last-markup", "/Catalog/Details/{last_id}", "markup item rendered HTML-encoded"),
    # --- posts that bypass the client-side validators (legacy only checks ModelState, not Page.IsValid) ---
    post("create-post-price-unparseable", "/Catalog/Create", "Price unparseable (bypassing client validation)",
         set=dict(WF_VALID, **{WF + "Price": "12,34,5"}), submit="[ Create ]"),
    post("create-post-long-name", "/Catalog/Create", "Name longer than 50 characters",
         set=dict(WF_VALID, **{WF + "Name": LONG_NAME}), submit="[ Create ]"),
    post("create-post-noname", "/Catalog/Create", "Name empty (bypassing client validation)",
         set=dict(WF_VALID, **{WF + "Name": ""}), submit="[ Create ]"),
    post("create-post-price-range", "/Catalog/Create", "Price out of range (bypassing client validation)",
         set=dict(WF_VALID, **{WF + "Price": "2000000"}), submit="[ Create ]"),
    post("create-post-stock-range", "/Catalog/Create", "Stock out of range (bypassing client validation)",
         set=dict(WF_VALID, **{WF + "Stock": "20000000"}), submit="[ Create ]"),
    post("edit-1-post-price-range", "/Catalog/Edit/1", "edit: Price -5 (bypassing client validation)",
         set={WF + "Price": "-5"}, submit="[ Save ]"),
]

FLOWS = {"mvc": MVC, "webforms": WEBFORMS}
