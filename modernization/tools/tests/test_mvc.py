"""MVC storefront (legacy eShopLegacyMVC) characterization. Mutating tests run last and clean up."""
import pytest

from helpers import errors, form_post, rows, soup

VALID = {"Name": "Characterization Item", "Description": "char", "Price": "12.34", "CatalogBrandId": "2",
         "CatalogTypeId": "1", "AvailableStock": "10", "RestockThreshold": "2", "MaxStockThreshold": "50"}


def test_list_default_shows_first_10_ordered_by_id(mvc):
    r = mvc.get("/")
    assert r.status_code == 200 and "text/html" in r.headers["content-type"]
    rs = rows(r)
    assert len(rs) == 10
    assert rs[0][1] == ".NET Bot Black Hoodie" and rs[0][5] == "$19.50"


@pytest.mark.parametrize("size,index,count", [(10, 0, 10), (10, 1, 2), (10, 2, 0), (5, 0, 5), (5, 2, 2), (12, 0, 12),
                                              (20, 0, 12), (1, 11, 1), (3, 3, 3)])
def test_list_paging(mvc, size, index, count):
    r = mvc.get(f"/Catalog/Index?pageSize={size}&pageIndex={index}")
    assert r.status_code == 200
    assert len(rows(r)) == count


def test_list_page_size_zero_is_server_error(mvc):
    assert mvc.get("/Catalog/Index?pageSize=0&pageIndex=0").status_code == 500


def test_list_nonnumeric_params_fall_back_to_defaults(mvc):
    assert len(rows(mvc.get("/Catalog/Index?pageSize=abc&pageIndex=xyz"))) == 10


@pytest.mark.parametrize("item,name", [(1, ".NET Bot Black Hoodie"), (5, "Roslyn Red Sheet")])
def test_details(mvc, item, name):
    r = mvc.get(f"/Catalog/Details/{item}")
    assert r.status_code == 200 and name in r.text


@pytest.mark.parametrize("path,status", [("/Catalog/Details/999", 404), ("/Catalog/Details", 400),
                                         ("/Catalog/Edit/999", 404), ("/Catalog/Edit", 400),
                                         ("/Catalog/Delete/999", 404), ("/Catalog/Delete", 400),
                                         ("/does-not-exist", 404), ("/Catalog/NoSuchAction", 404)])
def test_missing_and_bad_requests(mvc, path, status):
    assert mvc.get(path).status_code == status


def test_create_get_form(mvc):
    r = mvc.get("/Catalog/Create")
    assert r.status_code == 200
    names = {i.get("name") for i in soup(r).find_all(["input", "select"])}
    assert {"Name", "Description", "CatalogBrandId", "CatalogTypeId", "Price", "AvailableStock",
            "RestockThreshold", "MaxStockThreshold", "__RequestVerificationToken"} <= names
    brands = [o.get_text().strip() for o in soup(r).select("select[name=CatalogBrandId] option")]
    assert brands == ["Azure", ".NET", "Visual Studio", "SQL Server", "Other"]


@pytest.mark.parametrize("field,value,message", [
    ("Name", "", "The Name field is required."),
    ("Price", "1.234", "The field Price must be a positive number with maximum two decimals."),
    ("Price", "2000000", "The field Price must be between 0 and 1000000."),
    ("Price", "abc", "The value 'abc' is not valid for Price."),
    ("Price", "", "The Price field is required."),
    ("AvailableStock", "-1", "The field Stock must be between 0 and 10 million."),
    ("RestockThreshold", "20000000", "The field Restock must be between 0 and 10 million."),
    ("MaxStockThreshold", "abc", "The value 'abc' is not valid for Max stock."),
])
def test_create_validation(mvc, field, value, message):
    r = form_post(mvc, "/Catalog/Create", {**VALID, field: value})
    assert r.status_code == 200
    assert message in errors(r)


def test_create_without_antiforgery_token_is_rejected(mvc):
    r = form_post(mvc, "/Catalog/Create", VALID, drop=["__RequestVerificationToken"])
    assert r.status_code == 500  # legacy MVC 5 HttpAntiForgeryException; CONTRACT.md: reproduce 500


def test_pic_endpoint(mvc):
    r = mvc.get("/items/1/pic")
    assert r.status_code == 200 and r.headers["content-type"] == "image/png" and len(r.content) == 151640
    assert mvc.get("/items/999/pic").status_code == 404


def test_api_brands_json(mvc):
    r = mvc.get("/api/brands", headers={"Accept": "application/json"})
    assert r.status_code == 200 and r.headers["content-type"].startswith("application/json")
    assert r.json() == [{"Id": 1, "Brand": "Azure"}, {"Id": 2, "Brand": ".NET"}, {"Id": 3, "Brand": "Visual Studio"},
                        {"Id": 4, "Brand": "SQL Server"}, {"Id": 5, "Brand": "Other"}]


def test_api_brands_xml_content_negotiation(mvc):
    r = mvc.get("/api/brands", headers={"Accept": "application/xml"})
    assert r.status_code == 200 and r.headers["content-type"].startswith("application/xml")
    assert "<Brand>Azure</Brand>" in r.text


def test_api_brand_by_id(mvc):
    assert mvc.get("/api/brands/1", headers={"Accept": "application/json"}).json() == {"Id": 1, "Brand": "Azure"}
    assert mvc.get("/api/brands/999").status_code == 404


def test_api_brand_delete_is_non_mutating(mvc):
    assert mvc.request("DELETE", "/api/brands/1").status_code == 200
    assert mvc.request("DELETE", "/api/brands/999").status_code == 404
    assert len(mvc.get("/api/brands", headers={"Accept": "application/json"}).json()) == 5


def test_api_brands_unsupported_verbs(mvc):
    assert mvc.request("POST", "/api/brands", json={"Id": 9, "Brand": "X"}).status_code == 405
    assert mvc.request("PUT", "/api/brands/1", json={"Id": 1, "Brand": "X"}).status_code == 405


def test_api_files_returns_binary_brand_list(mvc):
    r = mvc.get("/api/files")
    assert r.status_code == 200
    assert r.content[:1] == b"\x00"  # MS-NRBF SerializationHeaderRecord
    assert b"Visual Studio" in r.content


@pytest.mark.parametrize("path,ctype", [("/Content/css", "text/css"), ("/bundles/jquery", "javascript"),
                                        ("/bundles/jqueryval", "javascript"), ("/bundles/modernizr", "javascript"),
                                        ("/bundles/bootstrap", "javascript"), ("/images/brand.png", "image/png"),
                                        ("/Pics/1.png", "image/png"), ("/favicon.ico", "image")])
def test_static_content(mvc, path, ctype):
    r = mvc.get(path)
    assert r.status_code == 200 and ctype in r.headers["content-type"]


def test_zz_create_edit_delete_roundtrip(mvc):
    r = form_post(mvc, "/Catalog/Create", VALID)
    assert r.status_code == 302 and r.headers["location"].endswith("/")
    all_rows = rows(mvc.get("/Catalog/Index?pageSize=100&pageIndex=0"))
    assert len(all_rows) == 13
    assert all_rows[-1][1:6] == ["Characterization Item", "char", ".NET", "Mug", "$12.34"]
    new_id = max(int(a.get("href").rsplit("/", 1)[1]) for a in soup(mvc.get("/Catalog/Index?pageSize=100"))
                 .find_all("a") if "/Catalog/Details/" in (a.get("href") or ""))
    assert new_id == 13
    r = form_post(mvc, f"/Catalog/Edit/{new_id}", {"Name": ""})
    assert r.status_code == 200 and "The Name field is required." in errors(r)
    r = form_post(mvc, f"/Catalog/Edit/{new_id}", {"Name": "Characterization Edited", "Price": "5.00"})
    assert r.status_code == 302
    assert "Characterization Edited" in mvc.get(f"/Catalog/Details/{new_id}").text
    r = form_post(mvc, f"/Catalog/Delete/{new_id}")
    assert r.status_code == 302
    assert mvc.get(f"/Catalog/Details/{new_id}").status_code == 404
    assert len(rows(mvc.get("/Catalog/Index?pageSize=100&pageIndex=0"))) == 12
