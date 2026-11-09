#!/usr/bin/env python3
"""Config transform for the COPIED run dir: sets UseMockData / UseCustomizationData and (sql mode)
the CatalogDBContext connection string. Usage: patch_config.py <Web.config> <mock|sql>"""
import os, re, sys

path, mode = sys.argv[1], sys.argv[2]
text = open(path, encoding="utf-8-sig").read()

def set_key(text, key, value):
    pat = re.compile(r'(<add\s+key="%s"\s+value=")[^"]*(")' % re.escape(key))
    if not pat.search(text):
        raise SystemExit(f"{key} not found in {path}")
    return pat.sub(lambda m: m.group(1) + value + m.group(2), text)

text = set_key(text, "UseMockData", "true" if mode == "mock" else "false")
text = set_key(text, "UseCustomizationData", os.environ.get("USE_CUSTOMIZATION_DATA", "false"))
if mode == "sql":
    cs = os.environ.get("LEGACY_SQL_CONNECTION")
    if not cs:
        raise SystemExit("sql mode needs LEGACY_SQL_CONNECTION")
    text = re.sub(r'(<add name="CatalogDBContext" connectionString=")[^"]*(")',
                  lambda m: m.group(1) + cs.replace("&", "&amp;") + m.group(2), text)
open(path, "w", encoding="utf-8").write(text)
print(f"patched {path}: mode={mode}")
