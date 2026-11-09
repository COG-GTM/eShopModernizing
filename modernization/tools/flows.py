"""Ordered flow definitions for the transcript recorder (one browser-like session per app).
Order matters: read-only flows run first, then mutating flows each followed by a data-state snapshot.
"""


def get(name, path, flow, **kw):
    return dict(name=name, path=path, flow=flow, **kw)


def state(name, path):
    return dict(name=name, path=path, flow="data-state", state=True)


VALID_ITEM = {"Name": "Transcript Item", "Description": "Created by the transcript recorder",
              "Price": "12.34", "AvailableStock": "10", "RestockThreshold": "2", "MaxStockThreshold": "50"}

# ---------------------------------------------------------------- MVC (eShopLegacyMVC, :5100)
MVC_STATE = "/Catalog/Index?pageSize=100&pageIndex=0"
MVC = [
    get("list-root", "/", "list"),
    get("list-catalog-index", "/Catalog/Index", "list"),
    get("list-catalog-default-route", "/Catalog", "list"),
]
for size, index in [(10, 0), (10, 1), (10, 2), (5, 0), (5, 1), (5, 2), (5, 3), (20, 0), (12, 0), (1, 0), (1, 11), (3, 3), (100, 0)]:
    MVC.append(get(f"list-size{size}-page{index}", f"/Catalog/Index?pageSize={size}&pageIndex={index}", "list"))
MVC += [
    get("list-page-only", "/Catalog/Index?pageIndex=1", "list"),
    get("list-size-only", "/Catalog/Index?pageSize=4", "list"),
    get("list-size0", "/Catalog/Index?pageSize=0&pageIndex=0", "list"),
    get("list-nonnumeric-params", "/Catalog/Index?pageSize=abc&pageIndex=xyz", "list"),
]
for i in (1, 5, 12):
    MVC.append(get(f"details-{i}", f"/Catalog/Details/{i}", "details"))
MVC += [
    get("details-missing-999", "/Catalog/Details/999", "details"),
    get("details-no-id", "/Catalog/Details", "details"),
    get("details-nonnumeric-id", "/Catalog/Details/abc", "details"),
    get("create-get", "/Catalog/Create", "create"),
]
_create_invalid = {
    "missing-name": {"Name": ""},
    # "-5" is intentionally NOT recorded for MVC: it fails both [RegularExpression] and [Range] and Mono
    # reports whichever attribute it reflects first, which varies between app starts (see CONTRACT.md).
    # The regex message is covered by price-three-decimals and the range message by price-too-large.
    "price-three-decimals": {"Price": "1.234"},
    "price-too-large": {"Price": "2000000"},
    "price-not-number": {"Price": "abc"},
    "price-empty": {"Price": ""},
    "stock-negative": {"AvailableStock": "-1"},
    "restock-too-large": {"RestockThreshold": "20000000"},
    "maxstock-not-number": {"MaxStockThreshold": "abc"},
}
for k, v in _create_invalid.items():
    MVC.append(dict(name=f"create-post-invalid-{k}", flow="create",
                    form={"page": "/Catalog/Create", "fields": {**VALID_ITEM, **v}}))
MVC += [
    dict(name="create-post-missing-antiforgery", flow="create",
         form={"page": "/Catalog/Create", "fields": VALID_ITEM, "drop": ["__RequestVerificationToken"]}),
    dict(name="create-post-valid", flow="create",
         form={"page": "/Catalog/Create", "fields": {**VALID_ITEM, "CatalogBrandId": "2", "CatalogTypeId": "1"}}),
    state("state-after-create", MVC_STATE),
    get("details-created-13", "/Catalog/Details/13", "details"),
    get("edit-get-1", "/Catalog/Edit/1", "edit"),
    get("edit-get-missing-999", "/Catalog/Edit/999", "edit"),
    get("edit-get-no-id", "/Catalog/Edit", "edit"),
    dict(name="edit-post-invalid-missing-name", flow="edit", form={"page": "/Catalog/Edit/1", "fields": {"Name": ""}}),
    dict(name="edit-post-invalid-price-too-large", flow="edit", form={"page": "/Catalog/Edit/1", "fields": {"Price": "2000000"}}),
    dict(name="edit-post-valid", flow="edit",
         form={"page": "/Catalog/Edit/1", "fields": {"Name": "Edited Hoodie", "Price": "21.75", "AvailableStock": "7",
                                                     "CatalogBrandId": "3", "CatalogTypeId": "2"}}),
    state("state-after-edit", MVC_STATE),
    get("details-edited-1", "/Catalog/Details/1", "details"),
    get("delete-get-2", "/Catalog/Delete/2", "delete"),
    get("delete-get-missing-999", "/Catalog/Delete/999", "delete"),
    get("delete-get-no-id", "/Catalog/Delete", "delete"),
    dict(name="delete-post-2", flow="delete", form={"page": "/Catalog/Delete/2"}),
    state("state-after-delete", MVC_STATE),
    get("details-deleted-2", "/Catalog/Details/2", "details"),
    # pictures
    get("pic-item-1", "/items/1/pic", "pics"),
    get("pic-item-12", "/items/12/pic", "pics"),
    get("pic-item-created-13", "/items/13/pic", "pics"),
    get("pic-item-missing-999", "/items/999/pic", "pics"),
    get("pic-item-nonnumeric", "/items/abc/pic", "pics"),
    # Web API
    get("api-brands-json", "/api/brands", "api", headers={"Accept": "application/json"}),
    get("api-brands-xml", "/api/brands", "api", headers={"Accept": "application/xml"}),
    get("api-brands-default-accept", "/api/brands", "api"),
    get("api-brand-1", "/api/brands/1", "api", headers={"Accept": "application/json"}),
    get("api-brand-1-xml", "/api/brands/1", "api", headers={"Accept": "application/xml"}),
    get("api-brand-missing-999", "/api/brands/999", "api", headers={"Accept": "application/json"}),
    dict(name="api-brand-delete-1", path="/api/brands/1", method="DELETE", flow="api"),
    dict(name="api-brand-delete-missing-999", path="/api/brands/999", method="DELETE", flow="api"),
    get("api-brands-after-delete", "/api/brands", "api", headers={"Accept": "application/json"}),
    dict(name="api-brands-post-not-allowed", path="/api/brands", method="POST", flow="api",
         headers={"Content-Type": "application/json"}, data='{"Id":9,"Brand":"X"}'),
    dict(name="api-brands-put-not-allowed", path="/api/brands/1", method="PUT", flow="api",
         headers={"Content-Type": "application/json"}, data='{"Id":1,"Brand":"X"}'),
    get("api-types", "/api/types", "api", headers={"Accept": "application/json"}),
    get("api-files", "/api/files", "api"),
    get("api-root", "/api", "api"),
    get("api-catalog-hello", "/api/catalog", "api"),
    # static content and bundles
    get("static-bundle-css", "/Content/css", "static"),
    get("static-bundle-jquery", "/bundles/jquery", "static"),
    get("static-bundle-jqueryval", "/bundles/jqueryval", "static"),
    get("static-bundle-modernizr", "/bundles/modernizr", "static"),
    get("static-bundle-bootstrap", "/bundles/bootstrap", "static"),
    get("static-site-css", "/Content/Site.css", "static"),
    get("static-custom-css", "/Content/custom.css", "static"),
    get("static-brand-image", "/images/brand.png", "static"),
    get("static-pic-file", "/Pics/1.png", "static"),
    get("static-favicon", "/favicon.ico", "static"),
    get("static-jquery-file", "/Scripts/jquery-3.3.1.js", "static"),
    # error pages
    get("error-unknown-path", "/does-not-exist", "errors"),
    get("error-unknown-action", "/Catalog/NoSuchAction", "errors"),
]

# ---------------------------------------------------------------- WebForms (eShopLegacyWebForms, :5101)
WF_STATE = "/Default/index/0/size/100"
WF_ITEM = {"Name": "Transcript Item", "Description": "Created by the transcript recorder",
           "Price": "12.34", "Stock": "10", "Restock": "2", "Maxstock": "50"}
WF = [
    get("list-root", "/", "list"),
    get("list-default", "/Default", "list"),
    get("list-default-aspx", "/Default.aspx", "list"),
]
for size, index in [(10, 0), (10, 1), (10, 2), (5, 0), (5, 1), (5, 2), (20, 0), (12, 0), (1, 0), (1, 11), (3, 3), (100, 0)]:
    WF.append(get(f"list-size{size}-page{index}", f"/Default/index/{index}/size/{size}", "list"))
WF += [get("list-nonnumeric-route", "/Default/index/abc/size/xyz", "list")]
for i in (1, 5, 12):
    WF.append(get(f"details-{i}", f"/Catalog/Details/{i}", "details"))
WF += [
    get("details-missing-999", "/Catalog/Details/999", "details"),
    get("details-nonnumeric-id", "/Catalog/Details/abc", "details"),
    get("create-get", "/Catalog/Create", "create"),
]
_wf_invalid = {
    "missing-name": {"Name": ""},
    "price-negative": {"Price": "-5"},
    "price-not-number": {"Price": "abc"},
    "price-too-large": {"Price": "99999999999999999"},
    "stock-negative": {"Stock": "-1"},
    "restock-not-number": {"Restock": "abc"},
    "maxstock-too-large": {"Maxstock": "20000000"},
}
for k, v in _wf_invalid.items():
    WF.append(dict(name=f"create-post-invalid-{k}", flow="create",
                   form={"page": "/Catalog/Create", "fields": {**WF_ITEM, **v}, "submit": "[ Create ]"}))
WF += [
    dict(name="create-post-valid", flow="create",
         form={"page": "/Catalog/Create", "fields": {**WF_ITEM, "Brand": "2", "Type": "1"}, "submit": "[ Create ]"}),
    state("state-after-create", WF_STATE),
    get("details-created-13", "/Catalog/Details/13", "details"),
    get("edit-get-1", "/Catalog/Edit/1", "edit"),
    get("edit-get-missing-999", "/Catalog/Edit/999", "edit"),
    dict(name="edit-post-invalid-missing-name", flow="edit",
         form={"page": "/Catalog/Edit/1", "fields": {"Name": ""}, "submit": "[ Save ]"}),
    dict(name="edit-post-invalid-stock-negative", flow="edit",
         form={"page": "/Catalog/Edit/1", "fields": {"Stock": "-1"}, "submit": "[ Save ]"}),
    dict(name="edit-post-valid", flow="edit",
         form={"page": "/Catalog/Edit/1", "fields": {"Name": "Edited Hoodie", "Price": "21.75", "Stock": "7",
                                                     "BrandDropDownList": "3", "TypeDropDownList": "2"},
               "submit": "[ Save ]"}),
    state("state-after-edit", WF_STATE),
    get("details-edited-1", "/Catalog/Details/1", "details"),
    get("delete-get-2", "/Catalog/Delete/2", "delete"),
    get("delete-get-missing-999", "/Catalog/Delete/999", "delete"),
    dict(name="delete-post-2", flow="delete", form={"page": "/Catalog/Delete/2", "submit": "[ Delete ]"}),
    state("state-after-delete", WF_STATE),
    get("details-deleted-2", "/Catalog/Details/2", "details"),
    get("static-bundle-css", "/Content/css", "static"),
    get("static-site-css", "/Content/site.css", "static"),
    get("static-brand-image", "/images/brand.png", "static"),
    get("static-pic-file", "/Pics/1.png", "static"),
    get("static-webforms-js", "/Scripts/WebForms/WebForms.js", "static"),
    get("static-favicon", "/favicon.ico", "static"),
    get("error-unknown-path", "/does-not-exist", "errors"),
    get("error-unknown-page", "/NoSuchPage.aspx", "errors"),
]

FLOWS = {"mvc": MVC, "webforms": WF}
