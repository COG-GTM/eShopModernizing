#!/usr/bin/env python3
"""Static inventory of the three legacy eShop solutions for .NET 8 migration planning.

Parses the .sln/.csproj/packages.config/*.config files and scans C#/markup for
.NET Framework-only API usage. Pure Python 3 standard library, no build needed.

Usage:
    python3 tools/net8-migration/inventory.py [--out docs/net8-migration/inventory.md] [--json FILE]
"""
import argparse
import json
import os
import re
import xml.etree.ElementTree as ET
from collections import Counter, OrderedDict

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

SOLUTIONS = OrderedDict(
    [
        ("WebForms", "eShopLegacyWebFormsSolution/eShopLegacyWebForms.sln"),
        ("MVC", "eShopLegacyMVCSolution/eShopLegacyMVC.sln"),
        ("N-Tier (WCF + WinForms)", "eShopLegacyNTier/eShopLegacyNTier.sln"),
    ]
)

PROJECT_TYPE_GUIDS = {
    "349C5851-65DF-11DA-9384-00065B846F21": "ASP.NET Web Application",
    "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC": "C#",
    "E3E379DF-F4C6-4180-9B81-6769533ABE47": "ASP.NET MVC 4",
    "3D9AD99F-2412-4246-B90B-4EAA41C64699": "WCF",
    "60DC8134-EBA5-43B8-BCC9-BB4BC16C2548": "WPF",
}

# Legacy package -> (.NET 8 disposition, replacement / note)
PACKAGE_MAP = {
    "EntityFramework": ("upgrade", "EF 6.4.4+/6.5 runs on net8 (keep EDMX-free code-first); EF Core 8 is the long-term target"),
    "Autofac": ("upgrade", "Autofac 8.x (netstandard2.0/net8)"),
    "Autofac.Web": ("remove", "WebForms-only; use Autofac.Extensions.DependencyInjection or built-in DI"),
    "Autofac.Mvc5": ("remove", "MVC5-only; use Autofac.Extensions.DependencyInjection"),
    "Autofac.WebApi2": ("remove", "Web API 2-only; use Autofac.Extensions.DependencyInjection"),
    "Microsoft.AspNet.Mvc": ("replace", "ASP.NET Core MVC (shared framework Microsoft.AspNetCore.App)"),
    "Microsoft.AspNet.Razor": ("replace", "ASP.NET Core Razor (shared framework)"),
    "Microsoft.AspNet.WebPages": ("replace", "ASP.NET Core Razor / Tag Helpers"),
    "Microsoft.AspNet.WebApi": ("replace", "ASP.NET Core controllers ([ApiController])"),
    "Microsoft.AspNet.WebApi.Core": ("replace", "ASP.NET Core controllers ([ApiController])"),
    "Microsoft.AspNet.WebApi.WebHost": ("remove", "IIS System.Web host for Web API; Kestrel/IIS ANCM instead"),
    "Microsoft.AspNet.WebApi.Client": ("upgrade", "6.0.0 targets netstandard2.0; or System.Net.Http.Json"),
    "Microsoft.AspNet.Web.Optimization": ("replace", "Static bundling (LibMan/WebOptimizer/npm build); no System.Web bundling"),
    "Microsoft.AspNet.Web.Optimization.WebForms": ("remove", "WebForms bundling control"),
    "Microsoft.AspNet.FriendlyUrls": ("remove", "WebForms-only; ASP.NET Core routing"),
    "Microsoft.AspNet.FriendlyUrls.Core": ("remove", "WebForms-only; ASP.NET Core routing"),
    "Microsoft.AspNet.ScriptManager.MSAjax": ("remove", "WebForms ScriptManager/MS AJAX; no equivalent"),
    "Microsoft.AspNet.ScriptManager.WebForms": ("remove", "WebForms ScriptManager; no equivalent"),
    "AspNet.ScriptManager.bootstrap": ("remove", "ScriptManager mapping; reference static assets directly"),
    "AspNet.ScriptManager.jQuery": ("remove", "ScriptManager mapping; reference static assets directly"),
    "Microsoft.AspNet.SessionState.SessionStateModule": ("replace", "ASP.NET Core session middleware + IDistributedCache"),
    "Microsoft.AspNet.TelemetryCorrelation": ("remove", "Built into System.Diagnostics.Activity on .NET 8"),
    "Microsoft.ApplicationInsights": ("upgrade", "Microsoft.ApplicationInsights 2.22+ (or OpenTelemetry/Azure Monitor exporter)"),
    "Microsoft.ApplicationInsights.Web": ("replace", "Microsoft.ApplicationInsights.AspNetCore"),
    "Microsoft.ApplicationInsights.WindowsServer": ("replace", "Microsoft.ApplicationInsights.AspNetCore"),
    "Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel": ("replace", "Microsoft.ApplicationInsights.AspNetCore"),
    "Microsoft.ApplicationInsights.DependencyCollector": ("replace", "Included by Microsoft.ApplicationInsights.AspNetCore"),
    "Microsoft.ApplicationInsights.PerfCounterCollector": ("replace", "Included by Microsoft.ApplicationInsights.AspNetCore (EventCounters)"),
    "Microsoft.ApplicationInsights.Agent.Intercept": ("remove", ".NET Framework profiler shim"),
    "Microsoft.CodeDom.Providers.DotNetCompilerPlatform": ("remove", "ASP.NET runtime compilation of .aspx/.cshtml; not used by SDK projects"),
    "Microsoft.Net.Compilers": ("remove", "SDK ships Roslyn"),
    "Microsoft.Web.Infrastructure": ("remove", "System.Web dynamic module registration"),
    "WebGrease": ("remove", "System.Web.Optimization minifier"),
    "Antlr": ("remove", "Transitive of WebGrease"),
    "log4net": ("upgrade", "log4net 2.0.17+/3.x supports netstandard2.0; or Microsoft.Extensions.Logging provider"),
    "Newtonsoft.Json": ("upgrade", "13.0.3 (CVE-2024-21907 fixed in 13.0.1); or System.Text.Json"),
    "bootstrap": ("client-side", "Move to LibMan/npm; static files under wwwroot"),
    "jQuery": ("client-side", "Move to LibMan/npm; static files under wwwroot"),
    "jQuery.Validation": ("client-side", "Move to LibMan/npm"),
    "Microsoft.jQuery.Unobtrusive.Validation": ("client-side", "Move to LibMan/npm"),
    "Modernizr": ("client-side", "Likely drop"),
    "Respond": ("client-side", "IE8 polyfill; drop"),
    "popper.js": ("client-side", "Move to LibMan/npm"),
    "Pipelines.Sockets.Unofficial": ("remove", "Transitive (StackExchange.Redis); re-evaluate"),
    "Microsoft.AspNetCore": ("upgrade", "ASP.NET Core 2.2 metapackage -> FrameworkReference Microsoft.AspNetCore.App"),
    "Microsoft.AspNetCore.Mvc": ("upgrade", "Part of shared framework on net8"),
    "Microsoft.AspNetCore.StaticFiles": ("upgrade", "Part of shared framework on net8"),
    "Microsoft.EntityFrameworkCore": ("upgrade", "EF Core 2.2 -> 8.0 (breaking changes 3.0: client eval)"),
    "Microsoft.EntityFrameworkCore.Design": ("upgrade", "8.0"),
    "Microsoft.EntityFrameworkCore.Relational": ("upgrade", "8.0"),
    "Microsoft.EntityFrameworkCore.SqlServer": ("upgrade", "8.0 (Microsoft.Data.SqlClient; Encrypt=true default)"),
    "Autofac.Extensions.DependencyInjection": ("upgrade", "9.x"),
    "Microsoft.CSharp": ("remove", "In-box on net8"),
}

FRAMEWORK_ONLY_REFS = {
    "System.Web": "ASP.NET (System.Web) runtime — no .NET 8 equivalent",
    "System.Web.Abstractions": "HttpContextBase etc. — System.Web adapters only",
    "System.Web.ApplicationServices": "Membership/roles — ASP.NET Core Identity",
    "System.Web.DynamicData": "Dynamic Data — no equivalent",
    "System.Web.Entity": "EntityDataSource control — no equivalent",
    "System.Web.Extensions": "MS AJAX / JavaScriptSerializer — no equivalent",
    "System.Web.Services": "ASMX — no equivalent",
    "System.Web.Routing": "System.Web routing — ASP.NET Core endpoint routing",
    "System.Web.Mvc": "ASP.NET MVC 5",
    "System.Web.Helpers": "WebPages helpers",
    "System.Web.Optimization": "Bundling",
    "System.Web.Razor": "Razor v3",
    "System.Web.WebPages": "WebPages",
    "System.Web.WebPages.Deployment": "WebPages",
    "System.Web.WebPages.Razor": "WebPages",
    "System.ServiceModel": "WCF — client: System.ServiceModel.* 8.x packages; server: CoreWCF",
    "System.ServiceModel.Web": "WCF WebHttp — client-side unsupported; CoreWCF.WebHttp or ASP.NET Core",
    "System.EnterpriseServices": "COM+ — not supported",
    "System.Data.Entity.Design": "EF4/EDMX design-time — not supported",
    "System.Deployment": "ClickOnce runtime API — not supported (ClickOnce publish still works)",
    "System.Runtime.Remoting": "Remoting — not supported",
    "System.Configuration": "System.Configuration.ConfigurationManager package (compat) or Microsoft.Extensions.Configuration",
    "System.Windows.Forms": "Supported on net8.0-windows (UseWindowsForms)",
    "System.Drawing": "Supported on Windows only (System.Drawing.Common)",
}

SOURCE_PATTERNS = OrderedDict(
    [
        ("using System.Web*", r"^\s*using\s+System\.Web(\.[\w.]+)?\s*;"),
        ("System.Web.UI (WebForms controls)", r"\bSystem\.Web\.UI\b|:\s*(Page|MasterPage|UserControl)\b"),
        ("HttpContext.Current", r"\bHttpContext\.Current\b"),
        ("Session[...]", r"\bSession\s*\["),
        ("Server.MapPath", r"\bServer\.MapPath\b"),
        ("HostingEnvironment", r"\bHostingEnvironment\.\w+"),
        ("HttpApplication / Global.asax", r":\s*(System\.Web\.)?HttpApplication\b"),
        ("Page lifecycle (Page_Load/IsPostBack)", r"\bPage_Load\b|\bIsPostBack\b"),
        ("DataBind()", r"\.DataBind\s*\("),
        ("Response.Redirect", r"\bResponse\.Redirect\b"),
        ("RouteTable / MapPageRoute", r"\bRouteTable\b|\bMapPageRoute\b"),
        ("BundleTable / ScriptManager", r"\bBundleTable\b|\bScriptManager\b"),
        ("MVC `: Controller` base (MVC 5 or Core)", r":\s*Controller\b"),
        ("Web API 2 ApiController", r":\s*ApiController\b"),
        ("IHttpActionResult / HttpResponseMessage", r"\bIHttpActionResult\b|\bHttpResponseMessage\b"),
        ("GlobalConfiguration / DependencyResolver", r"\bGlobalConfiguration\b|\bDependencyResolver\.SetResolver\b"),
        ("ConfigurationManager", r"\bConfigurationManager\.\w+"),
        ("BinaryFormatter", r"\bBinaryFormatter\b"),
        ("System.Runtime.Remoting", r"\bSystem\.Runtime\.Remoting\b"),
        ("EF6 DbContext / System.Data.Entity", r"\bSystem\.Data\.Entity\b"),
        ("EF6 database initializer", r"\b(CreateDatabaseIfNotExists|DropCreateDatabase\w*|Database\.SetInitializer)\b"),
        ("Raw SQL / sequences", r"\bExecuteSqlCommand\b|\bSqlQuery\s*<|NEXT VALUE FOR"),
        ("log4net", r"\blog4net\b|\bLogManager\.GetLogger\b"),
        ("[ServiceContract]", r"\[\s*(System\.ServiceModel\.)?ServiceContract(Attribute)?\b"),
        ("[OperationContract]", r"\[\s*(System\.ServiceModel\.)?OperationContract(Attribute)?\b"),
        ("[DataContract]", r"\[\s*(System\.Runtime\.Serialization\.)?DataContract(Attribute)?\b"),
        ("[DataMember]", r"\[\s*(System\.Runtime\.Serialization\.)?DataMember(Attribute)?\b"),
        ("ClientBase<T> (WCF proxy)", r"\bClientBase\s*<"),
        ("WinForms Form / controls", r"\bSystem\.Windows\.Forms\b"),
        ("Image.FromFile / System.Drawing", r"\bImage\.FromFile\b|\bSystem\.Drawing\b"),
        ("Hard-coded Windows paths", r'"\\\\\.\.\\\\|Environment\.CurrentDirectory\s*\+'),
        ("UWP / WinRT (Windows.*)", r"^\s*using\s+Windows\.\w+"),
    ]
)

MARKUP_EXT = {".aspx", ".ascx", ".master", ".asax", ".svc", ".cshtml", ".asmx", ".ashx"}


PACKAGE_MAP_CI = {k.lower(): v for k, v in PACKAGE_MAP.items()}


def classify_package(package_id):
    if package_id.lower() in PACKAGE_MAP_CI:
        disposition, note = PACKAGE_MAP_CI[package_id.lower()]
    elif package_id.startswith("System."):
        disposition, note = "remove", "System.* compat package; in-box on net8 (drop unless a netstandard lib needs it)"
    else:
        disposition, note = "review", "No mapping recorded; check NuGet for a net8/netstandard2.0 build"
    return {"disposition": disposition, "net8": note}


def rel(path):
    return os.path.relpath(path, REPO).replace(os.sep, "/")


def strip_ns(root):
    for el in root.iter():
        if "}" in el.tag:
            el.tag = el.tag.split("}", 1)[1]
    return root


def parse_xml(path):
    with open(path, "rb") as fh:
        data = fh.read().lstrip(b"\xef\xbb\xbf")
    return strip_ns(ET.fromstring(data))


def parse_sln(path):
    projects = []
    pattern = re.compile(r'^Project\("\{[^}]+\}"\)\s*=\s*"([^"]+)",\s*"([^"]+\.csproj)"', re.M)
    with open(path, encoding="utf-8-sig") as fh:
        for name, proj in pattern.findall(fh.read()):
            projects.append((name, os.path.normpath(os.path.join(os.path.dirname(path), proj.replace("\\", os.sep)))))
    return projects


def parse_csproj(path):
    root = parse_xml(path)
    sdk = root.get("Sdk")
    info = OrderedDict()
    info["path"] = rel(path)
    info["style"] = f"SDK ({sdk})" if sdk else "legacy (non-SDK)"

    def prop(name):
        for el in root.iter(name):
            if el.text and el.text.strip():
                return el.text.strip()
        return None

    info["target_framework"] = prop("TargetFrameworkVersion") or prop("TargetFramework") or prop("TargetFrameworks")
    info["output_type"] = prop("OutputType") or ("Exe" if sdk and sdk.endswith("Web") else "Library")
    info["assembly_name"] = prop("AssemblyName") or os.path.splitext(os.path.basename(path))[0]
    guids = prop("ProjectTypeGuids") or ""
    info["project_types"] = [PROJECT_TYPE_GUIDS.get(g.strip("{} ").upper(), g) for g in guids.split(";") if g.strip()]
    refs = []
    for el in root.iter("Reference"):
        refs.append(el.get("Include").split(",")[0].strip())
    info["framework_references"] = sorted(set(refs))
    info["framework_only_references"] = [r for r in info["framework_references"] if r in FRAMEWORK_ONLY_REFS]
    pkgs = OrderedDict()
    for el in root.iter("PackageReference"):
        version = el.get("Version")
        if version is None:
            v = el.find("Version")
            version = v.text.strip() if v is not None and v.text else None
        pkgs[el.get("Include")] = version
    info["package_references"] = pkgs
    info["project_references"] = [
        rel(os.path.normpath(os.path.join(os.path.dirname(path), el.get("Include").replace("\\", os.sep))))
        for el in root.iter("ProjectReference")
    ]
    compile_items = [el.get("Include").replace("\\", "/") for el in root.iter("Compile") if el.get("Include")]
    info["compile_items"] = compile_items
    info["imports"] = sorted(
        {el.get("Project") for el in root.iter("Import") if el.get("Project") and ("WebApplication" in el.get("Project") or "packages" in el.get("Project"))}
    )
    info["wcf_metadata"] = [el.get("Include") for el in root.iter("WCFMetadataStorage")]
    info["bootstrapper"] = [el.get("Include") for el in root.iter("BootstrapperPackage")]
    info["iis_express"] = prop("UseIISExpress") == "true" or prop("IISUrl") is not None
    info["iis_url"] = prop("IISUrl")
    return info


def parse_packages_config(project_dir):
    path = os.path.join(project_dir, "packages.config")
    if not os.path.exists(path):
        return None, OrderedDict()
    root = parse_xml(path)
    return rel(path), OrderedDict((p.get("id"), p.get("version")) for p in root.iter("package"))


def parse_config(path):
    root = parse_xml(path)
    cfg = OrderedDict()
    cfg["path"] = rel(path)
    comp = root.find("./system.web/compilation")
    rt = root.find("./system.web/httpRuntime")
    cfg["compilation_targetFramework"] = comp.get("targetFramework") if comp is not None else None
    cfg["httpRuntime_targetFramework"] = rt.get("targetFramework") if rt is not None else None
    ss = root.find("./system.web/sessionState")
    cfg["sessionState"] = ss.get("mode") if ss is not None else None
    cfg["connection_strings"] = [
        {"name": c.get("name"), "provider": c.get("providerName"), "localdb": "(localdb)" in (c.get("connectionString") or "").lower()}
        for c in root.iter("add")
        if c.get("connectionString") is not None
    ]
    app = root.find("./appSettings")
    cfg["app_settings"] = [a.get("key") for a in app.iter("add")] if app is not None else []
    modules = []
    for sect in ("./system.web/httpModules", "./system.webServer/modules"):
        node = root.find(sect)
        if node is not None:
            modules += [m.get("name") for m in node.iter("add")]
    cfg["http_modules"] = sorted(set(modules))
    handlers = root.find("./system.webServer/handlers")
    cfg["http_handlers"] = sorted({h.get("name") for h in handlers.iter("add")}) if handlers is not None else []
    cfg["binding_redirects"] = len(list(root.iter("dependentAssembly")))
    ef = root.find("./entityFramework")
    cfg["ef6_config_section"] = ef is not None
    sm = root.find("./system.serviceModel")
    if sm is not None:
        wcf = OrderedDict()
        wcf["services"] = [
            {
                "name": s.get("name"),
                "endpoints": [
                    {"address": e.get("address"), "binding": e.get("binding"), "contract": e.get("contract")} for e in s.iter("endpoint")
                ],
            }
            for s in sm.iter("service")
        ]
        client = sm.find("client")
        wcf["client_endpoints"] = (
            [{"name": e.get("name"), "address": e.get("address"), "binding": e.get("binding"), "contract": e.get("contract")} for e in client.iter("endpoint")]
            if client is not None
            else []
        )
        bindings = sm.find("bindings")
        wcf["binding_configs"] = [f"{b.tag}/{c.get('name')}" for b in bindings for c in b] if bindings is not None else []
        pm = sm.find("protocolMapping")
        wcf["protocol_mapping"] = [f"{a.get('scheme')}->{a.get('binding')}" for a in pm.iter("add")] if pm is not None else []
        she = sm.find("serviceHostingEnvironment")
        wcf["aspNetCompatibilityEnabled"] = she.get("aspNetCompatibilityEnabled") if she is not None else None
        wcf["behaviors"] = sorted({el.tag for el in sm.iter() if el.tag in ("serviceMetadata", "serviceDebug", "serviceCredentials", "serviceAuthorization")})
        cfg["wcf"] = wcf
    return cfg


def scan_sources(project_dir):
    counts = Counter()
    hits = {}
    file_types = Counter()
    for dirpath, dirnames, filenames in os.walk(project_dir):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "packages", "node_modules", "Scripts", "Content", "fonts", "wwwroot")]
        for fn in filenames:
            ext = os.path.splitext(fn)[1].lower()
            file_types[ext] += 1
            if ext not in (".cs",) and ext not in MARKUP_EXT:
                continue
            path = os.path.join(dirpath, fn)
            with open(path, encoding="utf-8-sig", errors="replace") as fh:
                text = fh.read()
            for label, pattern in SOURCE_PATTERNS.items():
                n = len(re.findall(pattern, text, re.M))
                if n:
                    counts[label] += n
                    hits.setdefault(label, []).append(rel(path))
    return counts, hits, file_types


def scan_wcf_contracts(project_dir):
    contracts = []
    for dirpath, dirnames, filenames in os.walk(project_dir):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj")]
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            with open(path, encoding="utf-8-sig", errors="replace") as fh:
                text = fh.read()
            if "ServiceContract" not in text:
                continue
            for m in re.finditer(r"\[\s*(?:System\.ServiceModel\.)?ServiceContract(?:Attribute)?[^\]]*\]\s*public\s+interface\s+(\w+)([^{]*)\{", text):
                body_start = m.end()
                depth, i = 1, body_start
                while depth and i < len(text):
                    depth += {"{": 1, "}": -1}.get(text[i], 0)
                    i += 1
                body = text[body_start:i]
                ops = re.findall(r"OperationContract[^\]]*\]\s*(?:\[[^\]]*\]\s*)*([\w.<>\[\], ]+?)\s+(\w+)\s*\(([^)]*)\)", body)
                contracts.append(
                    {
                        "file": rel(path),
                        "interface": m.group(1),
                        "inherits": m.group(2).strip(": ").strip() or None,
                        "operations": [{"returns": r.strip(), "name": n, "params": p.strip()} for r, n, p in ops],
                    }
                )
    return contracts


def uncompiled_sources(project_dir, info):
    if info["style"].startswith("SDK"):
        return []
    included = {os.path.normpath(os.path.join(project_dir, c)) for c in info["compile_items"]}
    missing = []
    for dirpath, dirnames, filenames in os.walk(project_dir):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj")]
        for fn in filenames:
            p = os.path.normpath(os.path.join(dirpath, fn))
            if fn.endswith(".cs") and p not in included:
                missing.append(rel(p))
    return sorted(missing)


def build_inventory():
    inventory = OrderedDict()
    for label, sln in SOLUTIONS.items():
        sln_path = os.path.join(REPO, sln)
        entry = OrderedDict(solution=sln, projects=[])
        for name, proj_path in parse_sln(sln_path):
            project_dir = os.path.dirname(proj_path)
            info = parse_csproj(proj_path)
            info["name"] = name
            pc_path, pc = parse_packages_config(project_dir)
            info["packages_config"] = pc_path
            packages = OrderedDict(pc)
            for k, v in info["package_references"].items():
                packages.setdefault(k, v)
            info["packages"] = [dict(id=k, version=v, **classify_package(k)) for k, v in packages.items()]
            info["configs"] = [
                parse_config(os.path.join(project_dir, fn))
                for fn in sorted(os.listdir(project_dir))
                if fn.lower() in ("web.config", "app.config")
            ]
            counts, hits, file_types = scan_sources(project_dir)
            info["api_usage"] = OrderedDict((k, {"count": counts[k], "files": sorted(set(hits[k]))}) for k in SOURCE_PATTERNS if counts[k])
            info["file_types"] = OrderedDict(sorted(((k, v) for k, v in file_types.items() if k in MARKUP_EXT | {".cs", ".resx", ".config", ".sql", ".wsdl", ".xsd", ".svcmap"}), key=lambda kv: -kv[1]))
            info["wcf_contracts"] = scan_wcf_contracts(project_dir)
            info["uncompiled_cs_files"] = uncompiled_sources(project_dir, info)
            del info["compile_items"]
            entry["projects"].append(info)
        inventory[label] = entry
    return inventory


def md_table(rows, headers):
    out = ["| " + " | ".join(headers) + " |", "|" + "---|" * len(headers)]
    out += ["| " + " | ".join(str(c) for c in r) + " |" for r in rows]
    return out


def to_markdown(inv):
    out = [
        "# Legacy solution inventory (generated)",
        "",
        "Generated by `python3 tools/net8-migration/inventory.py`. Do not edit by hand; re-run the script.",
        "",
        "## Summary",
        "",
    ]
    rows = []
    for label, sol in inv.items():
        for p in sol["projects"]:
            sysweb = sum(1 for r in p["framework_only_references"] if r.startswith("System.Web"))
            wcf_ops = sum(len(c["operations"]) for c in p["wcf_contracts"])
            rows.append(
                [
                    label,
                    f'`{p["name"]}`',
                    p["style"],
                    f'`{p["target_framework"]}`',
                    p["output_type"],
                    len(p["packages"]),
                    sysweb,
                    p["api_usage"].get("using System.Web*", {}).get("count", 0),
                    wcf_ops,
                ]
            )
    out += md_table(rows, ["Solution", "Project", "Project style", "TFM", "Output", "Packages", "System.Web* refs", "`using System.Web*`", "WCF ops"])
    for label, sol in inv.items():
        out += ["", f"## {label} — `{sol['solution']}`"]
        for p in sol["projects"]:
            out += ["", f'### `{p["name"]}`', "", f'- Project file: `{p["path"]}`', f'- Style: {p["style"]}; TFM `{p["target_framework"]}`; output `{p["output_type"]}`; assembly `{p["assembly_name"]}`']
            if p["project_types"]:
                out.append(f'- Project type GUIDs: {", ".join(p["project_types"])}')
            if p["iis_url"]:
                out.append(f'- IIS / IIS Express URL: `{p["iis_url"]}`')
            if p["project_references"]:
                out.append("- Project references: " + ", ".join(f"`{r}`" for r in p["project_references"]))
            if p["packages_config"]:
                out.append(f'- Uses `packages.config`: `{p["packages_config"]}`')
            if p["imports"]:
                out.append("- Legacy MSBuild imports: " + ", ".join(f"`{i}`" for i in p["imports"]))
            if p["wcf_metadata"]:
                out.append("- WCF service reference (Connected Services): " + ", ".join(f"`{w}`" for w in p["wcf_metadata"]))
            if p["file_types"]:
                out.append("- File types: " + ", ".join(f"`{k}` x{v}" for k, v in p["file_types"].items()))
            if p["framework_only_references"]:
                out += ["", "**.NET Framework assembly references with no/partial .NET 8 equivalent**", ""]
                out += md_table([[f"`{r}`", FRAMEWORK_ONLY_REFS[r]] for r in p["framework_only_references"]], ["Reference", ".NET 8 status"])
            if p["packages"]:
                out += ["", "**NuGet packages**", ""]
                out += md_table([[f'`{k["id"]}`', k["version"] or "", k["disposition"], k["net8"]] for k in p["packages"]], ["Package", "Version", "Disposition", ".NET 8 path"])
            if p["api_usage"]:
                out += ["", "**Framework-coupled API usage (source scan)**", ""]
                rows = []
                for k, v in p["api_usage"].items():
                    files = v["files"]
                    shown = ", ".join(f"`{f.split('/')[-1]}`" for f in files[:6]) + (f" (+{len(files) - 6} more)" if len(files) > 6 else "")
                    rows.append([k, v["count"], shown])
                out += md_table(rows, ["Pattern", "Hits", "Files"])
            for c in p["wcf_contracts"]:
                out += ["", f'**WCF contract `{c["interface"]}`** (`{c["file"]}`{", inherits " + c["inherits"] if c["inherits"] else ""})', ""]
                out += md_table([[f'`{o["name"]}`', f'`{o["returns"]}`', f'`{o["params"]}`' if o["params"] else ""] for o in c["operations"]], ["Operation", "Returns", "Parameters"])
            for cfg in p["configs"]:
                out += ["", f'**Config `{cfg["path"]}`**', ""]
                if cfg["compilation_targetFramework"] or cfg["httpRuntime_targetFramework"]:
                    out.append(f'- `compilation targetFramework={cfg["compilation_targetFramework"]}`, `httpRuntime targetFramework={cfg["httpRuntime_targetFramework"]}`')
                if cfg["sessionState"]:
                    out.append(f'- Session state: `{cfg["sessionState"]}`')
                if cfg["connection_strings"]:
                    out.append("- Connection strings: " + ", ".join(f'`{c["name"]}`' + (" (LocalDB)" if c["localdb"] else "") for c in cfg["connection_strings"]))
                if cfg["app_settings"]:
                    out.append("- appSettings keys: " + ", ".join(f"`{k}`" for k in cfg["app_settings"]))
                if cfg["http_modules"]:
                    out.append("- HTTP modules: " + ", ".join(f"`{m}`" for m in cfg["http_modules"]))
                if cfg["http_handlers"]:
                    out.append("- HTTP handlers: " + ", ".join(f"`{h}`" for h in cfg["http_handlers"]))
                if cfg["binding_redirects"]:
                    out.append(f'- Binding redirects: {cfg["binding_redirects"]}')
                if cfg["ef6_config_section"]:
                    out.append("- EF6 `<entityFramework>` config section present")
                wcf = cfg.get("wcf")
                if wcf:
                    for s in wcf["services"]:
                        for e in s["endpoints"]:
                            out.append(f'- WCF service `{s["name"]}` endpoint `{e["address"] or "(base)"}` binding `{e["binding"]}` contract `{e["contract"]}`')
                    for e in wcf["client_endpoints"]:
                        out.append(f'- WCF client endpoint `{e["name"]}` -> `{e["address"]}` binding `{e["binding"]}` contract `{e["contract"]}`')
                    if wcf["binding_configs"]:
                        out.append("- WCF binding configurations: " + ", ".join(f"`{b}`" for b in wcf["binding_configs"]))
                    if wcf["protocol_mapping"]:
                        out.append("- WCF protocol mapping: " + ", ".join(f"`{b}`" for b in wcf["protocol_mapping"]))
                    if wcf["aspNetCompatibilityEnabled"]:
                        out.append(f'- `aspNetCompatibilityEnabled={wcf["aspNetCompatibilityEnabled"]}`')
                    if wcf["behaviors"]:
                        out.append("- WCF behaviors: " + ", ".join(f"`{b}`" for b in wcf["behaviors"]))
            if p["uncompiled_cs_files"]:
                out += ["", "**`.cs` files on disk that the legacy csproj does not compile** (dead code; do not port blindly)", ""]
                out += [f"- `{f}`" for f in p["uncompiled_cs_files"]]
    return "\n".join(out) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=os.path.join(REPO, "docs", "net8-migration", "inventory.md"))
    parser.add_argument("--json", default=os.path.join(REPO, "docs", "net8-migration", "inventory.json"))
    args = parser.parse_args()
    inv = build_inventory()
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as fh:
        fh.write(to_markdown(inv))
    with open(args.json, "w", encoding="utf-8") as fh:
        json.dump(inv, fh, indent=2)
        fh.write("\n")
    print(f"wrote {rel(args.out)} and {rel(args.json)}")


if __name__ == "__main__":
    main()
