#!/usr/bin/env python3
"""Mono-only shims for a SCRATCH copy of eShopLegacyWebForms (never the repo checkout).

Each edit keeps the rendered output equivalent to what IIS/.NET Framework renders;
see LEGACY-RUN.md ("WebForms shims") for the reasoning behind each one.
Usage: shim_webforms.py <path-to-scratch-copy-of-eShopLegacyWebForms>
"""
import pathlib
import re
import sys

root = pathlib.Path(sys.argv[1]).resolve()
if (root / ".git").exists() or any((p / ".git").exists() for p in root.parents):
    sys.exit("refusing to shim a path inside a git checkout: %s" % root)


def edit(rel, fn):
    p = root / rel
    src = p.read_text(encoding="utf-8-sig")
    out = fn(src)
    if out == src:
        print("  (no change) " + rel)
    else:
        p.write_text(out, encoding="utf-8")
        print("  shimmed " + rel)


# W1: Mono's System.Web.Extensions has no ScriptResourceDefinition / ScriptResourceMapping.
edit("App_Start/BundleConfig.cs", lambda s: re.sub(
    r"\s*ScriptManager\.ScriptResourceMapping\.AddDefinition\(.*?\}\);", "", s, flags=re.S))

# W2: replace <asp:ScriptManager> (its named ScriptReferences need ScriptResourceMapping) with the
# plain <script> tags IIS renders for the same references in debug mode.
SCRIPTS = [
    "/Scripts/WebForms/MsAjax/MicrosoftAjax.js",
    "/Scripts/WebForms/MsAjax/MicrosoftAjaxApplicationServices.js",
    "/Scripts/WebForms/MsAjax/MicrosoftAjaxTimer.js",
    "/Scripts/WebForms/MsAjax/MicrosoftAjaxWebForms.js",
    "/Scripts/jquery-3.3.1.js",
    "/Scripts/bootstrap.js",
    "/Scripts/respond.js",
    "/Scripts/WebForms/WebForms.js",
    "/Scripts/WebForms/WebUIValidation.js",
    "/Scripts/WebForms/MenuStandards.js",
    "/Scripts/WebForms/GridView.js",
    "/Scripts/WebForms/DetailsView.js",
    "/Scripts/WebForms/TreeView.js",
    "/Scripts/WebForms/WebParts.js",
    "/Scripts/WebForms/Focus.js",
]
tags = "\n".join('<script src="%s" type="text/javascript"></script>' % s for s in SCRIPTS)
edit("Site.Master", lambda s: re.sub(
    r"<asp:ScriptManager runat=\"server\">.*?</asp:ScriptManager>", lambda m: tags, s, flags=re.S))


# W3: strongly typed binding (ItemType + <%#: Item.X %>) is not supported by Mono's page parser.
def untype(s):
    s = re.sub(r'\s+ItemType="[^"]*"', "", s)
    s = re.sub(r"<%#:\s*Item\.([\w.]+)\s*%>", r'<%# HttpUtility.HtmlEncode(Eval("\1")) %>', s)
    s = re.sub(r"=\s*Item\.Id\b", '= Eval("Id")', s)
    return s


for page in ["Default.aspx", "Catalog/Create.aspx", "Catalog/Edit.aspx"]:
    edit(page, untype)

# W4: SelectMethod model binding is unsupported -> bind the same data in Page_Load on first load
# (ViewState keeps the items on postback, as SelectMethod does).
edit("Catalog/Create.aspx", lambda s: re.sub(r'\s+SelectMethod="[^"]*"', "", s))
edit("Catalog/Create.aspx.cs", lambda s: s.replace(
    '_log.Info($"Now loading... /Catalog/Create.aspx");',
    '_log.Info($"Now loading... /Catalog/Create.aspx");\n'
    "            if (!IsPostBack) { Brand.DataSource = GetBrands(); Brand.DataBind(); Type.DataSource = GetTypes(); Type.DataBind(); }"))

# W5: Page.ModelState does not exist on Mono. These pages never add model errors, so on IIS
# ModelState.IsValid is always true; keep that semantics.
for cs in ["Catalog/Create.aspx.cs", "Catalog/Edit.aspx.cs"]:
    edit(cs, lambda s: s.replace("this.ModelState.IsValid", "true /* shim: Page.ModelState is always valid here */"))
