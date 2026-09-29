#!/usr/bin/env python3
"""Compile legacy eShop sources against .NET 8 to measure real migration blockers.

Each probe generates a throwaway SDK-style project that links a slice of the
legacy source tree (nothing in the repo is modified), swaps .NET Framework-only
packages for their closest .NET 8 equivalent, runs `dotnet build`, and groups
the resulting compiler errors by code and by namespace/type.

Usage:
    python3 tools/net8-migration/probe_net8.py [--dotnet PATH] [--work DIR] [--out FILE]
"""
import argparse
import glob
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from collections import Counter
from xml.sax.saxutils import escape

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

WEBFORMS = "eShopLegacyWebFormsSolution/src/eShopLegacyWebForms"
MVC = "eShopLegacyMVCSolution/src/eShopLegacyMVC"
PORTED = "eShopLegacyMVCSolution/eShopPorted"
UTIL = "eShopLegacyMVCSolution/eShopLegacy.Utilities"
WCF = "eShopLegacyNTier/src/eShopWCFService"
WINFORMS = "eShopLegacyNTier/src/eShopWinForms"

EF6 = ("EntityFramework", "6.4.4")
CONFIG = ("System.Configuration.ConfigurationManager", "8.0.0")
LOG4NET = ("log4net", "2.0.17")
AUTOFAC = ("Autofac", "8.0.0")

PROBES = [
    {
        "name": "utilities-net8",
        "project": "eShopLegacy.Utilities",
        "question": "Does the shared serialization helper compile on net8.0?",
        "sdk": "Microsoft.NET.Sdk",
        "tfm": "net8.0",
        "sources": [f"{UTIL}/Serializing.cs"],
        "packages": [],
    },
    {
        "name": "webforms-domain-net8",
        "project": "eShopLegacyWebForms (Models/Services/ViewModel only)",
        "question": "Is the WebForms data/service layer portable once the UI is removed?",
        "sdk": "Microsoft.NET.Sdk",
        "tfm": "net8.0",
        "sources": [f"{WEBFORMS}/Models/**/*.cs", f"{WEBFORMS}/Services/*.cs", f"{WEBFORMS}/ViewModel/*.cs"],
        "packages": [EF6, CONFIG],
    },
    {
        "name": "webforms-full-net8",
        "project": "eShopLegacyWebForms (all C#)",
        "question": "How much of the WebForms C# surface fails on ASP.NET Core?",
        "sdk": "Microsoft.NET.Sdk.Web",
        "tfm": "net8.0",
        "sources": [f"{WEBFORMS}/**/*.cs"],
        "packages": [EF6, CONFIG, LOG4NET, AUTOFAC],
    },
    {
        "name": "mvc-domain-net8",
        "project": "eShopLegacyMVC (Models/Services/ViewModel only)",
        "question": "Is the MVC data/service layer portable once the web layer is removed?",
        "sdk": "Microsoft.NET.Sdk",
        "tfm": "net8.0",
        "sources": [f"{MVC}/Models/**/*.cs", f"{MVC}/Services/*.cs", f"{MVC}/ViewModel/*.cs"],
        "packages": [EF6, CONFIG],
    },
    {
        "name": "mvc-full-net8",
        "project": "eShopLegacyMVC (all C#, Utilities linked)",
        "question": "How much of the MVC 5 / Web API 2 C# surface fails on ASP.NET Core?",
        "sdk": "Microsoft.NET.Sdk.Web",
        "tfm": "net8.0",
        "sources": [f"{MVC}/**/*.cs", f"{UTIL}/Serializing.cs"],
        "packages": [EF6, CONFIG, LOG4NET, AUTOFAC],
    },
    {
        "name": "ported-net8",
        "project": "eShopPorted (retargeted net461 -> net8.0)",
        "question": "How close is the existing ASP.NET Core 2.2 port to building on net8.0?",
        "sdk": "Microsoft.NET.Sdk.Web",
        "tfm": "net8.0",
        "sources": [f"{PORTED}/**/*.cs", f"{UTIL}/Serializing.cs"],
        "packages": [
            ("Microsoft.EntityFrameworkCore.SqlServer", "8.0.8"),
            ("Microsoft.EntityFrameworkCore.Design", "8.0.8"),
            ("Autofac.Extensions.DependencyInjection", "9.0.0"),
            LOG4NET,
            ("Newtonsoft.Json", "13.0.3"),
        ],
    },
    {
        "name": "wcf-service-corewcf-net8",
        "project": "eShopWCFService (csproj Compile items, CoreWCF)",
        "question": "Do the WCF contract, data contracts and EF6 service compile on net8.0 with CoreWCF?",
        "sdk": "Microsoft.NET.Sdk.Web",
        "tfm": "net8.0",
        "sources": [
            f"{WCF}/CatalogServiceMock.cs",
            f"{WCF}/EntityModel.cs",
            f"{WCF}/ICatalogService.cs",
            f"{WCF}/CatalogService.svc.cs",
            f"{WCF}/Models/*.cs",
            f"{WCF}/Models/Infrastructure/*.cs",
        ],
        "packages": [EF6, ("CoreWCF.Primitives", "1.5.2"), ("CoreWCF.Http", "1.5.2")],
    },
    {
        "name": "wcf-service-corewcf-nsswap-net8",
        "project": "eShopWCFService (same slice + `global using CoreWCF;`)",
        "question": "What is left after the mechanical System.ServiceModel -> CoreWCF namespace swap?",
        "sdk": "Microsoft.NET.Sdk.Web",
        "tfm": "net8.0",
        "sources": [
            f"{WCF}/CatalogServiceMock.cs",
            f"{WCF}/EntityModel.cs",
            f"{WCF}/ICatalogService.cs",
            f"{WCF}/CatalogService.svc.cs",
            f"{WCF}/Models/*.cs",
            f"{WCF}/Models/Infrastructure/*.cs",
        ],
        "extra_code": "global using CoreWCF;\n",
        "packages": [EF6, ("CoreWCF.Primitives", "1.5.2"), ("CoreWCF.Http", "1.5.2")],
    },
    {
        "name": "winforms-net8-windows",
        "project": "eShopWinForms (csproj Compile items, System.ServiceModel client)",
        "question": "Does the WinForms client + generated WCF proxy compile on net8.0-windows?",
        "sdk": "Microsoft.NET.Sdk",
        "tfm": "net8.0-windows",
        "props": {
            "OutputType": "WinExe",
            "UseWindowsForms": "true",
            "EnableWindowsTargeting": "true",
        },
        "sources": [
            f"{WINFORMS}/Connected Services/eShopServiceReference/Reference.cs",
            f"{WINFORMS}/Controllers/*.cs",
            f"{WINFORMS}/Program.cs",
            f"{WINFORMS}/Properties/*.cs",
            f"{WINFORMS}/Views/*.cs",
        ],
        "resources": [f"{WINFORMS}/Views/CatalogView.resx", f"{WINFORMS}/Properties/Resources.resx"],
        "packages": [
            ("System.ServiceModel.Http", "8.0.0"),
            ("System.ServiceModel.Primitives", "8.0.0"),
            ("System.Resources.Extensions", "8.0.0"),
        ],
    },
    {
        "name": "winforms-svcutil-net8-windows",
        "project": "eShopWinForms with proxy regenerated by dotnet-svcutil from the checked-in WSDL/XSD",
        "question": "Does regenerating the WCF proxy with `dotnet-svcutil --sync` clear the WinForms blockers?",
        "sdk": "Microsoft.NET.Sdk",
        "tfm": "net8.0-windows",
        "props": {
            "OutputType": "WinExe",
            "UseWindowsForms": "true",
            "EnableWindowsTargeting": "true",
        },
        "sources": [
            f"{WINFORMS}/Controllers/*.cs",
            f"{WINFORMS}/Program.cs",
            f"{WINFORMS}/Properties/*.cs",
            f"{WINFORMS}/Views/*.cs",
        ],
        "resources": [f"{WINFORMS}/Views/CatalogView.resx", f"{WINFORMS}/Properties/Resources.resx"],
        "svcutil": {
            "metadata": [
                f"{WINFORMS}/Connected Services/eShopServiceReference/CatalogService.wsdl",
                f"{WINFORMS}/Connected Services/eShopServiceReference/CatalogService*.xsd",
            ],
            "namespace": "*,eShopWinForms.eShopServiceReference",
        },
        "packages": [
            ("System.ServiceModel.Http", "8.0.0"),
            ("System.ServiceModel.Primitives", "8.0.0"),
            ("System.Resources.Extensions", "8.0.0"),
        ],
    },
]

SVCUTIL_VERSION = "2.1.0"

DIAG_RE = re.compile(r"^(?P<file>[^\n(]+?)\((?P<line>\d+),\d+\): (?P<sev>error|warning) (?P<code>[A-Z]+\d+): (?P<msg>.*?) \[[^\]]+\]$")
NAME_RE = re.compile(r"'([A-Za-z_][\w.]*)'")


def expand(patterns):
    files = []
    for pattern in patterns:
        matches = sorted(glob.glob(os.path.join(REPO, pattern), recursive=True))
        files.extend(m for m in matches if os.sep + "obj" + os.sep not in m and os.sep + "bin" + os.sep not in m)
    return sorted(dict.fromkeys(files))


def write_project(probe, workdir):
    sources = expand(probe["sources"]) + probe.get("extra_files", [])
    resources = expand(probe.get("resources", []))
    props = {
        "TargetFramework": probe["tfm"],
        "EnableDefaultItems": "false",
        "Nullable": "disable",
        "ImplicitUsings": "disable",
        "TreatWarningsAsErrors": "false",
        "NoWarn": "CS0105;CS0168;CS0219;CS0414;CS0649;CS8632",
        "GenerateResourceUsePreserializedResources": "true",
        "GenerateAssemblyInfo": "false",
    }
    props.update(probe.get("props", {}))
    lines = [f'<Project Sdk="{probe["sdk"]}">', "  <PropertyGroup>"]
    lines += [f"    <{k}>{escape(v)}</{k}>" for k, v in props.items()]
    lines += ["  </PropertyGroup>", "  <ItemGroup>"]
    for src in sources:
        rel = os.path.relpath(src, REPO)
        lines.append(f'    <Compile Include="{escape(src)}" Link="{escape(rel)}" />')
    for res in resources:
        rel = os.path.relpath(res, REPO)
        lines.append(f'    <EmbeddedResource Include="{escape(res)}" Link="{escape(rel)}" />')
    for pkg, ver in probe["packages"]:
        lines.append(f'    <PackageReference Include="{pkg}" Version="{ver}" />')
    lines += ["  </ItemGroup>", "</Project>", ""]
    os.makedirs(workdir, exist_ok=True)
    if probe.get("extra_code"):
        with open(os.path.join(workdir, "ProbeExtra.cs"), "w", encoding="utf-8") as fh:
            fh.write(probe["extra_code"])
        lines.insert(-2, '  <ItemGroup><Compile Include="ProbeExtra.cs" /></ItemGroup>')
    with open(os.path.join(workdir, "probe.csproj"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines))
    return sources


def generate_proxy(probe, dotnet, workroot, workdir, env):
    """Regenerate the WCF client proxy from checked-in metadata with dotnet-svcutil."""
    tool_dir = os.path.join(workroot, "_tools")
    exe = os.path.join(tool_dir, "dotnet-svcutil")
    if not os.path.exists(exe):
        subprocess.run([dotnet, "tool", "install", "dotnet-svcutil", "--version", SVCUTIL_VERSION, "--tool-path", tool_dir], check=True, capture_output=True, env=env)
    os.makedirs(workdir, exist_ok=True)
    metadata = expand(probe["svcutil"]["metadata"])
    dotnet_root = os.path.dirname(os.path.realpath(dotnet))
    tool_env = {**env, "DOTNET_ROOT": dotnet_root, "DOTNET_ROLL_FORWARD": "Major", "PATH": dotnet_root + os.pathsep + env.get("PATH", "")}
    subprocess.run(
        [exe, *metadata, "-n", probe["svcutil"]["namespace"], "-d", workdir, "-o", "GeneratedReference.cs", "--noLogo", "-ntr", "--sync"],
        cwd=workdir,
        check=True,
        capture_output=True,
        env=tool_env,
    )
    probe.setdefault("extra_files", []).append(os.path.join(workdir, "GeneratedReference.cs"))


def run_probe(probe, dotnet, workroot):
    workdir = os.path.join(workroot, probe["name"])
    shutil.rmtree(workdir, ignore_errors=True)
    env = {**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1", "DOTNET_SVCUTIL_TELEMETRY_OPTOUT": "1"}
    if probe.get("svcutil"):
        generate_proxy(probe, dotnet, workroot, workdir, env)
    sources = write_project(probe, workdir)
    proc = subprocess.run(
        [dotnet, "build", "probe.csproj", "-nologo", "-v:q", "-clp:NoSummary", "/p:RunAnalyzers=false"],
        cwd=workdir,
        capture_output=True,
        text=True,
        env=env,
    )
    diags = {}
    for raw in (proc.stdout + proc.stderr).splitlines():
        m = DIAG_RE.match(raw.strip())
        if not m:
            continue
        rel = os.path.relpath(m["file"], REPO) if m["file"].startswith(REPO) else m["file"]
        key = (rel, m["line"], m["code"], m["msg"])
        diags[key] = {"file": rel, "line": int(m["line"]), "severity": m["sev"], "code": m["code"], "message": m["msg"]}
    diags = list(diags.values())
    errors = [d for d in diags if d["severity"] == "error"]
    syslib = [d for d in diags if d["code"].startswith("SYSLIB")]
    missing = Counter()
    for d in errors:
        if d["code"] in ("CS0234", "CS0246", "CS0103", "CS0117", "CS1061", "CS0115", "CS0012"):
            names = NAME_RE.findall(d["message"])
            if names:
                missing[names[0]] += 1
    files_with_errors = Counter(d["file"] for d in errors)
    return {
        "name": probe["name"],
        "project": probe["project"],
        "question": probe["question"],
        "tfm": probe["tfm"],
        "packages": [f"{p} {v}" for p, v in probe["packages"]],
        "source_files": len(sources),
        "exit_code": proc.returncode,
        "succeeded": proc.returncode == 0,
        "error_count": len(errors),
        "files_with_errors": len(files_with_errors),
        "errors_by_code": dict(Counter(d["code"] for d in errors).most_common()),
        "top_missing_symbols": dict(missing.most_common(15)),
        "top_files": dict(files_with_errors.most_common(10)),
        "syslib_diagnostics": sorted({f'{d["code"]} {d["file"]}:{d["line"]}' for d in syslib}),
        "sample_errors": [f'{d["file"]}:{d["line"]} {d["code"]} {d["message"]}' for d in errors[:8]],
        "restore_failed": "error NU" in proc.stdout,
        "raw_tail": "" if diags else (proc.stdout + proc.stderr)[-2000:],
    }


def to_markdown(results, sdk_version):
    out = [
        "# .NET 8 compile probes (generated)",
        "",
        "Generated by `python3 tools/net8-migration/probe_net8.py`. Do not edit by hand.",
        "",
        f"- .NET SDK: `{sdk_version}`",
        "- Each probe links legacy sources into a throwaway SDK-style project; the repo is not modified.",
        "- .NET Framework-only packages are swapped for the closest .NET 8 package (listed per probe).",
        "- Compiler errors cascade: counts measure breadth of coupling, not hours of work.",
        "",
        "| Probe | Project slice | TFM | Files | Result | Errors | Files w/ errors |",
        "|---|---|---|---|---|---|---|",
    ]
    for r in results:
        status = "builds" if r["succeeded"] else ("restore failed" if r["restore_failed"] else "fails")
        out.append(f'| `{r["name"]}` | {r["project"]} | `{r["tfm"]}` | {r["source_files"]} | {status} | {r["error_count"]} | {r["files_with_errors"]} |')
    for r in results:
        out += ["", f'## `{r["name"]}`', "", f'**Question:** {r["question"]}', ""]
        out.append("Packages: " + (", ".join(f"`{p}`" for p in r["packages"]) or "none (BCL only)"))
        out.append("")
        if r["errors_by_code"]:
            out.append("Errors by code: " + ", ".join(f"`{c}` x{n}" for c, n in r["errors_by_code"].items()))
            out.append("")
        if r["top_missing_symbols"]:
            out.append("Most frequent unresolved symbols: " + ", ".join(f"`{s}` x{n}" for s, n in r["top_missing_symbols"].items()))
            out.append("")
        if r["top_files"]:
            out.append("Files with most errors:")
            out.append("")
            out += [f"- `{f}` ({n})" for f, n in r["top_files"].items()]
            out.append("")
        if r["syslib_diagnostics"]:
            out.append("SYSLIB (obsoletion) diagnostics:")
            out.append("")
            out += [f"- `{s}`" for s in r["syslib_diagnostics"]]
            out.append("")
        if r["sample_errors"]:
            out.append("Sample errors:")
            out.append("")
            out.append("```text")
            out += r["sample_errors"]
            out.append("```")
        if r["raw_tail"] and not r["succeeded"]:
            out += ["", "```text", r["raw_tail"].strip(), "```"]
    return "\n".join(out) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--dotnet", default=shutil.which("dotnet") or os.path.expanduser("~/dotnet/dotnet"))
    parser.add_argument("--work", default=os.path.join(tempfile.gettempdir(), "eshop-net8-probes"))
    parser.add_argument("--out", default=os.path.join(REPO, "docs", "net8-migration", "net8-probe-results.md"))
    parser.add_argument("--json", default=None, help="optional path for machine-readable results")
    parser.add_argument("--only", nargs="*", help="run only the named probes")
    args = parser.parse_args()

    if not args.dotnet or not os.path.exists(args.dotnet) and not shutil.which(args.dotnet):
        sys.exit("dotnet SDK 8.x not found; pass --dotnet")
    sdk_version = subprocess.run([args.dotnet, "--version"], capture_output=True, text=True).stdout.strip()
    probes = [p for p in PROBES if not args.only or p["name"] in args.only]
    results = []
    for probe in probes:
        print(f"[probe] {probe['name']} ...", file=sys.stderr, flush=True)
        result = run_probe(probe, args.dotnet, args.work)
        print(f"        exit={result['exit_code']} errors={result['error_count']}", file=sys.stderr, flush=True)
        results.append(result)
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as fh:
        fh.write(to_markdown(results, sdk_version))
    if args.json:
        with open(args.json, "w", encoding="utf-8") as fh:
            json.dump(results, fh, indent=2)
    print(f"wrote {os.path.relpath(args.out, REPO)}", file=sys.stderr)


if __name__ == "__main__":
    main()
