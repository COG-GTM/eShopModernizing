import re
from urllib.parse import urljoin

from bs4 import BeautifulSoup


def soup(r):
    return BeautifulSoup(r.text, "html.parser")


def rows(r):
    out = []
    for tr in soup(r).find_all("tr"):
        cells = [re.sub(r"\s+", " ", td.get_text()).strip() for td in tr.find_all("td")]
        if len(cells) >= 10:
            out.append(cells)
    return out


def errors(r):
    s = soup(r)
    msgs = []
    for el in s.select(".field-validation-error, .text-danger"):
        style = (el.get("style") or "").replace(" ", "")
        txt = re.sub(r"\s+", " ", el.get_text()).strip()
        if txt and "visibility:hidden" not in style and "display:none" not in style:
            msgs.append(txt)
    return msgs


def form_post(client, page, fields=None, drop=(), submit=None):
    r = client.get(page)
    assert r.status_code == 200, (page, r.status_code)
    form = next(f for f in soup(r).find_all("form") if (f.get("method") or "").lower() == "post")
    data = {}
    for el in form.find_all(["input", "select", "textarea"]):
        n = el.get("name")
        if not n or (el.name == "input" and (el.get("type") or "").lower() in ("submit", "button")):
            continue
        if el.name == "select":
            o = el.find("option", selected=True) or el.find("option")
            data[n] = o.get("value") if o else ""
        else:
            data[n] = el.get("value", "")

    def key(k):
        return k if k in data else next((n for n in data if n.endswith("$" + k)), k)

    for k, v in (fields or {}).items():
        data[key(k)] = v
    for k in drop:
        data.pop(key(k), None)
    if submit:
        b = form.find("input", attrs={"type": "submit", "value": submit})
        data[b["name"]] = b["value"]
    return client.post(urljoin(r.url, form.get("action") or r.url), data=data)
