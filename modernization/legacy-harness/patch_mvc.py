#!/usr/bin/env python3
"""Harness-only patches for the COPIED MVC app (never the repo sources).
Views/Web.config names System.Web.Mvc 5.2.3.0 for the Razor host factory; Mono resolves that type
string without applying the root bindingRedirect, so point it at the shipped 5.2.7.0.
Usage: patch_mvc.py <app dir>"""
import pathlib, sys
p = pathlib.Path(sys.argv[1]) / "Views" / "Web.config"
t = p.read_text(encoding="utf-8-sig")
p.write_text(t.replace("System.Web.Mvc, Version=5.2.3.0", "System.Web.Mvc, Version=5.2.7.0"), encoding="utf-8")
print(f"patched {p}")
