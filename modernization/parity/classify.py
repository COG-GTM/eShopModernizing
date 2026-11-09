#!/usr/bin/env python3
"""Explains every strict legacy-vs-migrated difference in side-by-side results with a rule from classifications.json.

  classify.py results/side-by-side-mock.json [results/side-by-side-sql.json ...]   (rewrites the files in place)

Bodies are split into tokens (tags / text runs, whitespace collapsed); tags are canonicalised (attribute order, class
token order, self-closing slash, attribute-name case, HTML character references decoded) before a token diff, so pure ordering/whitespace changes are
attributed to rule FW-MARKUP-CANONICAL. Each remaining diff hunk, header difference and runtime-header set difference
must match exactly one rule (first match wins); anything that matches none is counted as unexplained. Each case gets
"classified": [{rule, field, legacy, migrated}] and the file gets a "classification" summary.
"""
import difflib
import html
import json
import pathlib
import re
import sys

PARITY = pathlib.Path(__file__).resolve().parent
RULES = json.loads((PARITY / "classifications.json").read_text())["rules"]
CANONICAL_RULE = "FW-MARKUP-CANONICAL"
ATTR = re.compile(r'([^\s=/>]+)(?:\s*=\s*("[^"]*"|\'[^\']*\'|[^\s>]+))?')


def canon_tag(t):
    m = re.match(r"<(/?)([A-Za-z][\w:-]*)(.*?)/?>$", t, re.S)
    if not m:
        return t
    close, name, rest = m.groups()
    attrs = []
    for k, v in ATTR.findall(rest):
        v = v.strip("\"'") if v else ""
        k = k.lower()
        if k == "class":
            v = " ".join(sorted(v.split()))
        attrs.append(f'{k}="{v}"')
    return f"<{close}{name.lower()}{''.join(' ' + a for a in sorted(attrs))}>"


def tokens(s, canonical):
    out = []
    for t in re.findall(r"<[^>]+>|[^<]+", s or ""):
        t = re.sub(r"\s+", " ", t).strip()
        if t:
            out.append((canon_tag(html.unescape(t)) if t.startswith("<") else html.unescape(t)) if canonical else t)
    return out


def hunks(a, b):
    sm = difflib.SequenceMatcher(None, a, b, autojunk=False)
    return [(" ‖ ".join(a[i1:i2]), " ‖ ".join(b[j1:j2])) for op, i1, i2, j1, j2 in sm.get_opcodes() if op != "equal"]


def match(rule, app, mode, case, field, legacy, migrated):
    if app not in rule.get("apps", ["mvc", "webforms"]) or mode not in rule.get("modes", ["mock", "sql"]):
        return False
    if not re.fullmatch(rule.get("field", "body"), field):
        return False
    status = case.get("legacy_status") or 0
    if "status" in rule and not re.fullmatch(rule["status"], str(status)):
        return False
    if "case" in rule and not re.search(rule["case"], case["case"]):
        return False
    if field == "runtime-headers":  # set rule: every header present on one side only must be allowed for that side
        lset, mset = set(legacy.split()), set(migrated.split())
        return (lset - mset <= set(rule.get("legacy_only", []))) and (mset - lset <= set(rule.get("migrated_only", [])))
    for side, text in (("legacy", legacy), ("migrated", migrated)):
        if side in rule and not re.search(rule[side], "" if text is None else str(text), re.S):
            return False
    return True


def classify_case(app, mode, case, raw_dir):
    out = []

    def add(field, legacy, migrated):
        rule = next((r for r in RULES if match(r, app, mode, case, field, legacy, migrated)), None)
        out.append({"rule": rule["id"] if rule else None, "field": field, "legacy": legacy, "migrated": migrated})

    for d in case["strict_diffs"]:
        if d["field"] != "body":
            add(d["field"], d["legacy"] if d["field"] != "runtime-headers" else " ".join(d["legacy"]),
                d["migrated"] if d["field"] != "runtime-headers" else " ".join(d["migrated"]))
            continue
        lf, mf = (raw_dir / case["case"].replace(".json", f".{side}.txt") for side in ("legacy", "migrated"))
        lt = lf.read_text(errors="replace") if lf.exists() else d["legacy"]
        mt = mf.read_text(errors="replace") if mf.exists() else d["migrated"]
        status = case.get("legacy_status") or 0
        if 300 <= status < 400 or status >= 400:
            add("body", f"[{status} body]", f"[{case.get('migrated_status')} body]")
            continue
        if tokens(lt, True) != tokens(lt, False) or tokens(mt, True) != tokens(mt, False) or tokens(lt, False) != tokens(mt, False):
            raw_h, can_h = hunks(tokens(lt, False), tokens(mt, False)), hunks(tokens(lt, True), tokens(mt, True))
            if len(can_h) < len(raw_h) or not raw_h:
                out.append({"rule": CANONICAL_RULE, "field": "body", "legacy": f"{len(raw_h) - len(can_h)} hunk(s)", "migrated": ""})
            for a, b in can_h:
                add("body", a[:400], b[:400])
    case["classified"] = out
    return out


def summarise(result):
    by_rule, unexplained = {}, []
    for app in result["apps"]:
        for case in app["cases"]:
            for c in case.get("classified", []):
                if c["rule"] is None:
                    unexplained.append({"app": app["app"], "case": case["case"], **c})
                else:
                    by_rule.setdefault(c["rule"], {"occurrences": 0, "cases": set()})
                    by_rule[c["rule"]]["occurrences"] += 1
                    by_rule[c["rule"]]["cases"].add(f"{app['app']}/{case['case']}")
    return {"rules": {k: {"occurrences": v["occurrences"], "cases": sorted(v["cases"])} for k, v in sorted(by_rule.items())},
            "unexplained": unexplained}


def main(paths):
    total_unexplained = 0
    for p in map(pathlib.Path, paths):
        result = json.loads(p.read_text())
        for app in result["apps"]:
            raw_dir = p.parent / "raw" / result["mode"] / app["app"]
            for case in app["cases"]:
                classify_case(app["app"], result["mode"], case, raw_dir)
        result["classification"] = summarise(result)
        p.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n")
        u = result["classification"]["unexplained"]
        total_unexplained += len(u)
        print(f"{p.name}: {sum(r['occurrences'] for r in result['classification']['rules'].values())} classified, {len(u)} unexplained")
        for x in u:
            print(f"  UNEXPLAINED {x['app']}/{x['case']} {x['field']}\n    L: {str(x['legacy'])[:300]}\n    M: {str(x['migrated'])[:300]}")
    return 1 if total_unexplained else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
