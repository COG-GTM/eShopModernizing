"""Programme-wide intentional hardenings and documented differences: the ONLY differences replay.py accepts.

A rule matches a step when the app/step id match, every differing field is in `fields`, and the observed
legacy/modern statuses are in the rule's sets (None = any). Anything else that differs is FAIL.
"""

ANTIFORGERY = "Missing antiforgery token -> 400 with no data change (legacy 500)."

RULES = [
    # ---- programme-wide hardenings (story MBA-3849 parity policy) ----
    {"apps": ["mvc"], "steps": ["create-post-no-antiforgery", "edit-13-post-no-antiforgery",
                                "delete-13-post-no-antiforgery"],
     "fields": {"status", "content-type", "body"}, "legacy": {500}, "modern": {400}, "reason": ANTIFORGERY},
    {"apps": ["webforms"], "steps": ["create-post-no-viewstate"],
     "fields": {"status", "location", "content-type", "body"}, "legacy": None, "modern": {400},
     "reason": ANTIFORGERY + " WebForms: legacy has no antiforgery check on this post (ViewState is optional); "
                             "modern requires the page-wide token."},
    {"apps": ["webforms"], "steps": ["list-after-noaf"], "fields": {"body"}, "legacy": {200}, "modern": {200},
     "reason": "Consequence of the antiforgery hardening: legacy accepted the token-less post and created an item, "
               "modern rejected it (no data change)."},
    {"apps": ["mvc", "webforms"], "steps": ["create-post-markup"], "fields": {"status", "location", "content-type",
                                                                              "body"},
     "legacy": None, "modern": {302},
     "reason": "Markup in text fields never executes: modern accepts the value and renders it HTML-encoded "
               "(legacy MVC request validation answers 500)."},
    {"apps": ["mvc", "webforms"], "steps": ["details-last-markup"], "fields": {"status", "content-type", "body"},
     "legacy": None, "modern": {200},
     "reason": "Markup rendered HTML-encoded (&lt;script&gt;), never executed."},
    {"apps": ["mvc"], "steps": ["edit-1-overpost-picture", "pic-1-after-overpost", "details-1-after-overpost"],
     "fields": {"status", "location", "content-type", "body"}, "legacy": None, "modern": None,
     "reason": "PictureFileName cannot be over-posted or path-traversed: modern ignores the posted value and keeps "
               "serving the item's own picture."},
    {"apps": ["mvc"], "steps": ["api-files"], "fields": {"content-type", "body"}, "legacy": {200}, "modern": {200},
     "reason": "/api/files returns JSON instead of a BinaryFormatter payload (insecure deserialisation removed)."},
    # ---- documented in slice PRs ----
    {"apps": ["webforms"], "steps": ["about", "contact"], "fields": {"status", "content-type", "body"},
     "legacy": {404}, "modern": {200},
     "reason": "S6 (PR #111): extensionless /About and /Contact serve the pages (legacy only had the .aspx URL)."},
    {"apps": ["mvc", "webforms"], "steps": ["static-Images-brand-png", "static-images-brand-png",
                                            "static-Content-Site-css", "static-Content-site-css"],
     "fields": {"status", "content-type", "body"}, "legacy": {404}, "modern": {200},
     "reason": "S2/S6: static assets are served case-insensitively (legacy on a case-sensitive FS only matches the "
               "exact casing)."},
    {"apps": ["mvc", "webforms"], "steps": ["static-fonts-Montserrat-Regular-eot"], "fields": {"content-type", "body"},
     "legacy": {200}, "modern": {200},
     "reason": "S2/S6: .eot served as application/vnd.ms-fontobject (Mono serves its own fontobject variant)."},
    {"apps": ["mvc", "webforms"], "steps": ["create-post-long-name"], "fields": {"status", "location",
                                                                                 "content-type", "body"},
     "legacy": None, "modern": {200},
     "reason": "PR #117/#118: Name over 50 characters gets a validation message and nothing is saved (legacy: 500 "
               "DbEntityValidationException in SQL mode; the legacy mock has no length check and saves it)."},
    {"apps": ["mvc"], "steps": ["create-post-price-overflow"], "fields": {"status", "content-type", "body"},
     "legacy": {500}, "modern": {200},
     "reason": "PR #117: Price beyond Int32 gets the range validation message (legacy: 500 OverflowException)."},
    {"apps": ["webforms"], "steps": ["create-post-price-text", "create-post-price-unparseable"],
     "fields": {"status", "location", "content-type", "body"}, "legacy": {500}, "modern": {200},
     "reason": "PR #118: unparseable Price gets the validation message (legacy: 500 FormatException from "
               "decimal.Parse when client-side validation is bypassed)."},
    {"apps": ["webforms"], "steps": ["delete-13-post-no-button"], "fields": {"status", "location", "content-type",
                                                                             "body"},
     "legacy": None, "modern": None,
     "reason": "S7 (PR #109): Delete requires the Delete button field; a bare postback re-renders the page."},
]

# Story-owner decisions on MBA-3849 (both INTENTIONAL).
VALIDATION_ENFORCED = (
    "Decision A: WebForms server-side validation is enforced even when client-side JS is bypassed (same hardening "
    "family as #117/#118). Legacy code-behind checks Page.ModelState.IsValid, not Page.IsValid, so without the JS "
    "validators it saves the invalid item (302) or fails in EF (500); modern (S8, PR #114) re-renders with the "
    "legacy validator message and saves nothing. With JS on, both block the submit identically.")
LEGACY_MOCK_LOOKUPS = (
    "Decision B: legacy in-memory mock artefact. Legacy's mock service leaves CatalogBrand/CatalogType null on "
    "created/edited items until the list page renders, so legacy MVC shows blank Brand/Type and legacy WebForms "
    "throws a NullReferenceException (500) on Details/Delete; the later steps on item 13 inherit that state. "
    "Modern resolves the lookups (S4). SQL mode matches.")
RULES += [
    {"apps": ["webforms"], "steps": ["create-post-price-unparseable", "create-post-noname", "create-post-price-range",
                                     "create-post-stock-range", "edit-1-post-price-range"],
     "fields": {"status", "location", "body"}, "legacy": {302, 500}, "modern": {200}, "reason": VALIDATION_ENFORCED},
    {"apps": ["mvc"], "modes": ["mock"], "steps": ["details-13-edited", "delete-13-get", "details-13-after-noaf-delete"],
     "fields": {"body"}, "legacy": {200}, "modern": {200}, "reason": LEGACY_MOCK_LOOKUPS},
    # legacy's 500 on GET Delete/13 leaves no form to post, hence the "harness" field on the two Delete posts
    {"apps": ["webforms"], "modes": ["mock"],
     "steps": ["details-13", "details-13-edited", "delete-13-get", "delete-13-post-no-button",
               "details-13-after-nobutton", "delete-13-post", "list-after-delete", "details-13-deleted"],
     "fields": {"status", "body", "harness"}, "legacy": None, "modern": None, "reason": LEGACY_MOCK_LOOKUPS},
]


def _match(rule, app, mode, step_id, diffs, legacy, modern):
    return (app in rule["apps"] and step_id in rule["steps"]
            and (not rule.get("modes") or mode in rule["modes"])
            and set(diffs) <= rule["fields"]
            and (rule["legacy"] is None or legacy.get("status") in rule["legacy"])
            and (rule["modern"] is None or modern.get("status") in rule["modern"]))


def classify(app, mode, step_id, diffs, legacy, modern):
    if not diffs:
        return "PASS", ""
    rule = next((r for r in RULES if _match(r, app, mode, step_id, diffs, legacy, modern)), None)
    if "harness" in diffs:
        error = "harness error: %s / %s" % (legacy.get("error"), modern.get("error"))
        return ("INTENTIONAL", rule["reason"] + " | " + error) if rule else ("FAIL", error)
    if rule:
        return "INTENTIONAL", rule["reason"]
    return "FAIL", "differs in: " + ", ".join(diffs)
