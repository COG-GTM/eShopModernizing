#!/usr/bin/env python3
"""Audit every NuGet dependency in the repo for .NET 8 compatibility.

For each package referenced by a packages.config or <PackageReference>, this
queries nuget.org for (a) the pinned version, (b) the latest stable listed
version and (c) the newest stable version that ships a net8.0-loadable asset
(netstandard1.x/2.x, netcoreapp*, net5.0-net8.0), inspects the lib/ and ref/ folders of each to find the target
frameworks it ships, and records deprecation metadata.

Usage: python3 tools/nuget-net8-audit/audit.py [--out results.json]
"""
import argparse
import concurrent.futures
import gzip
import hashlib
import json
import os
import re
import sys
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
CACHE_DIR = Path(__file__).resolve().parent / ".cache"
REGISTRATION = "https://api.nuget.org/v3/registration5-gz-semver2/{id}/index.json"
MAX_VERSIONS_SCANNED = 80
SKIP_DIRS = {".git", "packages", "bin", "obj", "node_modules"}


def fetch_json(url):
    CACHE_DIR.mkdir(exist_ok=True)
    cache_file = CACHE_DIR / (hashlib.sha1(url.encode()).hexdigest() + ".json")
    if cache_file.exists():
        return json.loads(cache_file.read_text())
    req = urllib.request.Request(url, headers={"Accept-Encoding": "gzip", "User-Agent": "eshop-net8-audit"})
    with urllib.request.urlopen(req, timeout=60) as resp:
        raw = resp.read()
        if resp.headers.get("Content-Encoding") == "gzip" or raw[:2] == b"\x1f\x8b":
            raw = gzip.decompress(raw)
    cache_file.write_bytes(raw)
    return json.loads(raw)


def walk(root):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            yield Path(dirpath) / name


def project_tfm(csproj):
    text = csproj.read_text(encoding="utf-8-sig", errors="replace")
    m = re.search(r"<TargetFrameworks?>([^<]+)<", text) or re.search(r"<TargetFrameworkVersion>([^<]+)<", text)
    return m.group(1).strip() if m else "?"


def strip_ns(tag):
    return tag.split("}", 1)[-1]


def collect_references(root):
    """Return list of dicts: {id, version, file, project_tfm}."""
    refs = []
    for path in walk(root):
        rel = path.relative_to(root).as_posix()
        if path.name == "packages.config":
            csprojs = list(path.parent.glob("*.csproj")) + list(path.parent.glob("*.sfproj"))
            tfm = project_tfm(csprojs[0]) if csprojs else "?"
            for el in ET.parse(path).getroot():
                if strip_ns(el.tag) == "package":
                    refs.append({"id": el.get("id"), "version": el.get("version"), "file": rel,
                                 "project_tfm": el.get("targetFramework") or tfm})
        elif path.suffix in (".csproj", ".sfproj"):
            tfm = project_tfm(path)
            for el in ET.parse(path).getroot().iter():
                if strip_ns(el.tag) != "PackageReference":
                    continue
                version = el.get("Version")
                if version is None:
                    for child in el:
                        if strip_ns(child.tag) == "Version":
                            version = (child.text or "").strip()
                refs.append({"id": el.get("Include"), "version": version, "file": rel, "project_tfm": tfm})
    return refs


def parse_version(v):
    core, _, pre = v.partition("+")[0].partition("-")
    nums = [int(x) if x.isdigit() else 0 for x in core.split(".")]
    nums += [0] * (4 - len(nums))
    return tuple(nums[:4]), pre


def version_key(v):
    nums, pre = parse_version(v)
    return nums, 0 if pre else 1, pre


def same_version(a, b):
    return parse_version(a) == parse_version(b)


def registration_leaves(package_id):
    index = fetch_json(REGISTRATION.format(id=package_id.lower()))
    leaves = []
    for page in index["items"]:
        items = page.get("items") or fetch_json(page["@id"])["items"]
        leaves.extend(items)
    return leaves


TFM_RE = re.compile(r"^(?:runtimes/[^/]+/)?(lib|ref)/([^/]+)/", re.I)


def package_tfms(catalog_entry_url):
    leaf = fetch_json(catalog_entry_url)
    tfms, has_assets = set(), False
    for entry in leaf.get("packageEntries", []):
        full = entry["fullName"].replace("\\", "/")
        m = TFM_RE.match(full)
        if m:
            tfms.add(m.group(2).lower())
            has_assets = True
        elif re.match(r"^lib/[^/]+\.dll$", full, re.I):
            tfms.add("net (lib root)")
            has_assets = True
    return sorted(tfms), has_assets


NETCORE_RE = re.compile(r"^(netstandard(1\.\d|2\.[01])|netcoreapp\d|net([5-8])\.0)")


def net8_compatible(tfms):
    """Return 'yes', 'windows-only', or 'no' for a set of lib/ref TFMs."""
    plain, windows = False, False
    for tfm in tfms:
        if tfm.startswith("portable-") and "netstandard" in tfm:
            plain = True
        if not NETCORE_RE.match(tfm):
            continue
        if re.match(r"^net\d\.0-windows", tfm):
            windows = True
        elif re.match(r"^net\d\.0-", tfm):
            continue
        else:
            plain = True
    return "yes" if plain else "windows-only" if windows else "no"


def analyse(package_id, pinned):
    try:
        leaves = registration_leaves(package_id)
    except Exception as exc:  # noqa: BLE001 - reported in output
        return {"id": package_id, "error": str(exc)}
    entries = [leaf["catalogEntry"] for leaf in leaves]
    listed = [e for e in entries if e.get("listed", True)]
    pool = listed or entries
    stable = [e for e in pool if "-" not in e["version"]] or pool
    latest = max(stable, key=lambda e: version_key(e["version"]))
    pinned_entry = next((e for e in entries if pinned and same_version(e["version"], pinned)), None)

    def describe(entry):
        if entry is None:
            return None
        tfms, has_assets = package_tfms(entry["@id"])
        dep = entry.get("deprecation")
        return {
            "version": entry["version"],
            "tfms": tfms,
            "has_assemblies": has_assets,
            "net8": net8_compatible(tfms) if has_assets else "n/a (no assemblies)",
            "deprecated": bool(dep),
            "deprecation_reasons": dep.get("reasons", []) if dep else [],
            "alternate": (dep.get("alternatePackage") or {}).get("id") if dep else None,
        }

    newest_net8 = None
    for entry in sorted(stable, key=lambda e: version_key(e["version"]), reverse=True)[:MAX_VERSIONS_SCANNED]:
        info = describe(entry)
        if info["net8"] in ("yes", "windows-only"):
            newest_net8 = info
            break

    return {"id": package_id, "all_unlisted": not listed, "pinned": describe(pinned_entry),
            "latest": describe(latest), "newest_net8": newest_net8}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default=str(Path(__file__).resolve().parent / "results.json"))
    args = parser.parse_args()

    refs = collect_references(REPO_ROOT)
    by_id = {}
    for ref in refs:
        by_id.setdefault(ref["id"].lower(), []).append(ref)

    jobs = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=16) as pool:
        for key, usages in by_id.items():
            for version in sorted({u["version"] or "" for u in usages}):
                jobs[(key, version)] = pool.submit(analyse, usages[0]["id"], version)

    results = []
    for key, usages in sorted(by_id.items()):
        versions = []
        for version in sorted({u["version"] or "" for u in usages}):
            res = jobs[(key, version)].result()
            versions.append({"requested": version or None, **{k: v for k, v in res.items() if k != "id"}})
        results.append({
            "id": usages[0]["id"],
            "usages": [{"file": u["file"], "version": u["version"], "project_tfm": u["project_tfm"]} for u in usages],
            "versions": versions,
        })

    Path(args.out).write_text(json.dumps(results, indent=2) + "\n")
    no_net8 = [r["id"] for r in results
               if all(v.get("newest_net8") is None and (v.get("latest") or {}).get("has_assemblies") for v in r["versions"])]
    print(f"{len(refs)} references, {len(results)} unique packages -> {args.out}")
    print(f"{len(no_net8)} packages with no .NET 8-compatible version on nuget.org:")
    for pid in no_net8:
        print("  -", pid)


if __name__ == "__main__":
    sys.exit(main())
