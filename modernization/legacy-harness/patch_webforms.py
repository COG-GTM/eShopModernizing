#!/usr/bin/env python3
"""Harness-only patches for the COPIED WebForms app (never the repo sources) so it runs on Mono 6.12.
Each patch only works around a missing Mono System.Web feature; page behaviour/markup is otherwise kept.
  1. ScriptResourceDefinition does not exist in Mono's System.Web.Extensions -> drop the "respond"
     ScriptResourceMapping registration and the named <asp:ScriptReference>s that rely on such mappings
     (script tags are emitted directly instead).
  2. Mono's page parser has no strongly typed data binding (ItemType / Item / <%#: ... %>) ->
     rewrite to Eval(...) + HttpUtility.HtmlEncode.
  3. Mono has no model-binding SelectMethod -> bind the Create dropdowns in code-behind.
  4. Mono's Page has no ModelState -> use Page.IsValid (the page only uses validator controls).
Usage: patch_webforms.py <app dir>"""
import pathlib, re, sys

root = pathlib.Path(sys.argv[1])

def edit(rel, fn):
    p = root / rel
    t = p.read_text(encoding="utf-8-sig")
    n = fn(t)
    if n == t:
        raise SystemExit(f"patch had no effect on {rel}")
    p.write_text(n, encoding="utf-8")
    print(f"patched {rel}")

def bundle(t):
    return re.sub(r"\s*ScriptManager\.ScriptResourceMapping\.AddDefinition\(.*?\}\);", "", t, flags=re.S)
edit("App_Start/BundleConfig.cs", bundle)

SCRIPTS = """<asp:ScriptManager runat="server" />
        <script src="/Scripts/jquery-3.3.1.js"></script>
        <script src="/Scripts/bootstrap.js"></script>
        <script src="/Scripts/respond.js"></script>
        <%: System.Web.Optimization.Scripts.Render("~/bundles/MsAjaxJs") %>
        <%: System.Web.Optimization.Scripts.Render("~/bundles/WebFormsJs") %>"""
def master(t):
    return re.sub(r"<asp:ScriptManager runat=\"server\">.*?</asp:ScriptManager>", lambda m: SCRIPTS, t, flags=re.S)
edit("Site.Master", master)

def typed_binding(t):
    t = re.sub(r'\s+ItemType="[^"]*"', "", t)
    t = re.sub(r"<%#:\s*Item\.([\w.]+)\s*%>", r'<%# HttpUtility.HtmlEncode(Eval("\1")) %>', t)
    t = re.sub(r"\bid\s*=\s*Item\.Id\b", 'id = Eval("Id")', t)
    return t
edit("Default.aspx", typed_binding)
edit("Catalog/Edit.aspx", typed_binding)

def create_markup(t):
    t = typed_binding(t)
    return re.sub(r'\s+SelectMethod="[^"]*"', "", t)
edit("Catalog/Create.aspx", create_markup)

def create_code(t):
    t = t.replace('_log.Info($"Now loading... /Catalog/Create.aspx");',
                  '_log.Info($"Now loading... /Catalog/Create.aspx");\n'
                  '            if (!IsPostBack) { Brand.DataSource = GetBrands(); Brand.DataBind(); '
                  'Type.DataSource = GetTypes(); Type.DataBind(); }')
    return t.replace("this.ModelState.IsValid", "this.IsValid")
edit("Catalog/Create.aspx.cs", create_code)
edit("Catalog/Edit.aspx.cs", lambda t: t.replace("this.ModelState.IsValid", "this.IsValid"))
