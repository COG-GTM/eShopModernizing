"""WebForms catalog (legacy eShopLegacyWebForms) characterization. Mutating tests run last and clean up."""
import pytest

from helpers import errors, form_post, rows, soup

VALID = {"Name": "Characterization Item", "Description": "char", "Brand": "2", "Type": "1", "Price": "12.34",
         "Stock": "10", "Restock": "2", "Maxstock": "50"}


def test_list_default(wf):
    r = wf.get("/")
    assert r.status_code == 200
    rs = rows(r)
    assert len(rs) == 10 and rs[0][1] == ".NET Bot Black Hoodie"


@pytest.mark.parametrize("path", ["/Default", "/Default.aspx"])
def test_list_aliases(wf, path):
    assert len(rows(wf.get(path))) == 10


@pytest.mark.parametrize("size,index,count", [(10, 0, 10), (10, 1, 2), (5, 0, 5), (5, 2, 2), (12, 0, 12),
                                              (20, 0, 12), (1, 11, 1), (3, 3, 3)])
def test_list_paging_route(wf, size, index, count):
    r = wf.get(f"/Default/index/{index}/size/{size}")
    assert r.status_code == 200 and len(rows(r)) == count


@pytest.mark.parametrize("item,name", [(1, ".NET Bot Black Hoodie"), (12, "Prism White TShirt")])
def test_details(wf, item, name):
    r = wf.get(f"/Catalog/Details/{item}")
    assert r.status_code == 200 and name in r.text


@pytest.mark.parametrize("path,status", [("/Catalog/Details/999", 500), ("/Catalog/Edit/999", 500),
                                         ("/Catalog/Delete/999", 500), ("/does-not-exist", 404)])
def test_missing(wf, path, status):
    assert wf.get(path).status_code == status


def test_create_get_form(wf):
    r = wf.get("/Catalog/Create")
    assert r.status_code == 200
    names = {(i.get("name") or "").rsplit("$", 1)[-1] for i in soup(r).find_all(["input", "select"])}
    assert {"Name", "Description", "Brand", "Type", "Price", "Stock", "Restock", "Maxstock"} <= names


@pytest.mark.parametrize("field,value,message", [
    ("Name", "", "The Name field is required."),
    ("Price", "-5", "The Price must be a positive number with maximum two decimals between 0 and 1 million."),
    ("Price", "abc", "The Price must be a positive number with maximum two decimals between 0 and 1 million."),
    ("Stock", "-1", "The field Stock must be between 0 and 10 million."),
    ("Restock", "abc", "The field Restock must be between 0 and 10 million."),
    ("Maxstock", "20000000", "The field Max stock must be between 0 and 10 million."),
])
def test_create_validation(wf, field, value, message):
    r = form_post(wf, "/Catalog/Create", {**VALID, field: value}, submit="[ Create ]")
    assert r.status_code == 200 and message in errors(r)


@pytest.mark.parametrize("path,ctype", [("/Content/site.css", "text/css"), ("/images/brand.png", "image/png"),
                                        ("/Pics/1.png", "image/png"), ("/Scripts/WebForms/WebForms.js", "javascript")])
def test_static(wf, path, ctype):
    r = wf.get(path)
    assert r.status_code == 200 and ctype in r.headers["content-type"]


def test_zz_create_edit_delete_roundtrip(wf):
    r = form_post(wf, "/Catalog/Create", VALID, submit="[ Create ]")
    assert r.status_code == 302 and r.headers["location"].endswith("/")
    all_rows = rows(wf.get("/Default/index/0/size/100"))
    assert len(all_rows) == 13 and all_rows[-1][1:4] == ["Characterization Item", "char", ".NET"]
    new_id = max(int(a.get("href").rsplit("/", 1)[1]) for a in soup(wf.get("/Default/index/0/size/100")).find_all("a")
                 if "/Catalog/Details/" in (a.get("href") or ""))
    r = form_post(wf, f"/Catalog/Edit/{new_id}", {"Name": ""}, submit="[ Save ]")
    assert r.status_code == 200 and "The Name field is required." in errors(r)
    r = form_post(wf, f"/Catalog/Edit/{new_id}", {"Name": "Characterization Edited"}, submit="[ Save ]")
    assert r.status_code == 302
    # Legacy quirk (mock mode): Details of an item that was created AND then edited returns 500 on Mono
    # (NullReferenceException while data-binding); the list reflects the edit, so assert there.
    assert any(r[1] == "Characterization Edited" for r in rows(wf.get("/Default/index/0/size/100")))
    r = form_post(wf, f"/Catalog/Delete/{new_id}", submit="[ Delete ]")
    assert r.status_code == 302
    assert len(rows(wf.get("/Default/index/0/size/100"))) == 12
