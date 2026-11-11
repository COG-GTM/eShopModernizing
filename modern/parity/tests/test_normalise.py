"""Unit tests for the parity normaliser: python3 -m unittest discover -s modern/parity/tests -v"""
import pathlib
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))

from intentional import classify  # noqa: E402
from normalise import (HOST_ERROR_LINE, MASK, host_error_detail, json_shape, normalise_body,  # noqa: E402
                       normalise_content_type, normalise_location, visible_text)

HTML = "text/html; charset=utf-8"
LEGACY = "http://127.0.0.1:5100"
MODERN = "http://127.0.0.1:5200"


def page(inner):
    return ("<html><head><title>t</title></head><body>%s</body></html>" % inner).encode()


class BodyNormalisation(unittest.TestCase):
    def assertSameAfterNormalising(self, legacy, modern, ctype=HTML):
        self.assertEqual(normalise_body(ctype, legacy, LEGACY), normalise_body(ctype, modern, MODERN))

    def test_whitespace_and_line_layout_do_not_matter(self):
        self.assertSameAfterNormalising(page("<div>\n   <p>Name</p>\n</div>"), page("<div><p>Name</p></div>"))

    def test_visible_text_difference_is_kept(self):
        self.assertNotEqual(normalise_body(HTML, page("<p>Name</p>"), LEGACY),
                            normalise_body(HTML, page("<p>Nombre</p>"), MODERN))

    def test_css_class_difference_is_kept(self):
        self.assertNotEqual(normalise_body(HTML, page('<p class="esh-a">x</p>'), LEGACY),
                            normalise_body(HTML, page('<p class="esh-b">x</p>'), MODERN))

    def test_antiforgery_token_value_is_masked(self):
        self.assertSameAfterNormalising(
            page('<input name="__RequestVerificationToken" type="hidden" value="CfDJ8AAA" />'),
            page('<input name="__RequestVerificationToken" type="hidden" value="CfDJ8ZZZ" />'))

    def test_viewstate_and_modern_antiforgery_are_the_same_page_state_slot(self):
        legacy = page('<div class="aspNetHidden"><input type="hidden" name="__VIEWSTATE" id="__VIEWSTATE" value="/wEPDw" />'
                      '<input type="hidden" name="__VIEWSTATEGENERATOR" id="__VIEWSTATEGENERATOR" value="C2EE9ABB" />'
                      '<input type="hidden" name="__EVENTVALIDATION" id="__EVENTVALIDATION" value="/wEdAA" /></div><p>x</p>')
        modern = page('<input name="__RequestVerificationToken" type="hidden" value="CfDJ8" /><p>x</p>')
        self.assertSameAfterNormalising(legacy, modern)
        self.assertIn("{page-state}", normalise_body(HTML, modern, MODERN))

    def test_session_and_origin_are_masked(self):
        self.assertSameAfterNormalising(page('<a href="%s/Catalog/Details/1">d</a>' % LEGACY),
                                        page('<a href="%s/Catalog/Details/1">d</a>' % MODERN))

    def test_asset_cache_busting_hash_is_masked(self):
        self.assertSameAfterNormalising(page('<link href="/Content/site.css?v=abc123" rel="stylesheet" />'),
                                        page('<link href="/Content/site.css?v=zzz999" rel="stylesheet" />'))
        self.assertIn("?v=" + MASK, normalise_body(HTML, page('<img src="/images/a.png?v=1" />'), LEGACY))

    def test_hex_and_decimal_character_references_are_equal(self):
        self.assertSameAfterNormalising(page("<span>can&#39;t</span>"), page("<span>can&#x27;t</span>"))

    def test_different_encoded_characters_stay_different(self):
        self.assertNotEqual(normalise_body(HTML, page("<span>&#39;</span>"), LEGACY),
                            normalise_body(HTML, page("<span>&#x22;</span>"), MODERN))

    def test_markup_encoded_vs_raw_is_kept(self):
        self.assertNotEqual(normalise_body(HTML, page("<td>&lt;b&gt;x&lt;/b&gt;</td>"), LEGACY),
                            normalise_body(HTML, page("<td><b>x</b></td>"), MODERN))

    def test_host_error_pages_collapse_to_one_marker(self):
        mono = page("<h1>Server Error in '/' Application.</h1><h2><i>Object reference not set</i></h2>"
                    "<p>Mono 6.12.0.200 stack trace...</p>")
        self.assertEqual(HOST_ERROR_LINE, normalise_body(HTML, mono, LEGACY))
        self.assertIn("Object reference", host_error_detail(HTML, mono, LEGACY))
        self.assertEqual("", host_error_detail(HTML, page("<p>fine</p>"), LEGACY))

    def test_json_whitespace_does_not_matter_but_property_order_does(self):
        self.assertSameAfterNormalising(b'{"b": 1, "a": [1, 2]}', b'{"b":1,"a":[1,2]}', "application/json")
        self.assertNotEqual(normalise_body("application/json", b'{"b":1,"a":2}', LEGACY),
                            normalise_body("application/json", b'{"a":2,"b":1}', MODERN))

    def test_binary_bodies_compare_by_content(self):
        same = normalise_body("image/png", b"\x89PNG\x00\x01", LEGACY)
        self.assertEqual(same, normalise_body("image/png", b"\x89PNG\x00\x01", MODERN))
        self.assertNotEqual(same, normalise_body("image/png", b"\x89PNG\x00\x02", MODERN))

    def test_identical_logic_for_both_sides(self):
        body = page('<form><input name="__RequestVerificationToken" value="x" /><p>it&#x27;s</p></form>')
        self.assertEqual(normalise_body(HTML, body, LEGACY), normalise_body(HTML, body, MODERN))


class HeaderNormalisation(unittest.TestCase):
    def test_relative_and_absolute_location_are_equal(self):
        self.assertEqual(normalise_location("/", LEGACY + "/Catalog/Create"),
                         normalise_location(MODERN + "/", MODERN + "/Catalog/Create"))

    def test_location_is_resolved_against_the_request(self):
        self.assertEqual("/Catalog/Index", normalise_location("Index", LEGACY + "/Catalog/Create"))

    def test_location_path_and_query_differences_are_kept(self):
        self.assertNotEqual(normalise_location("/?a=1", LEGACY + "/x"), normalise_location("/", MODERN + "/x"))

    def test_external_location_is_kept_absolute(self):
        self.assertEqual("http://example.com/a", normalise_location("http://example.com/a", LEGACY + "/"))

    def test_empty_location(self):
        self.assertEqual("", normalise_location(None, LEGACY))

    def test_content_type_charset_case_and_default(self):
        self.assertEqual("text/html; charset=utf-8", normalise_content_type("text/html"))
        self.assertEqual("text/html; charset=utf-8", normalise_content_type('Text/HTML; Charset="UTF-8"'))
        self.assertEqual("image/png", normalise_content_type("image/png"))
        self.assertNotEqual(normalise_content_type("application/json"), normalise_content_type("text/html"))


class ShapeAndText(unittest.TestCase):
    def test_json_shape_ignores_values_but_not_keys_or_types(self):
        self.assertEqual(json_shape(b'[{"Id":1,"Brand":"a"}]'), json_shape(b'[{"Brand":"z","Id":9}]'))
        self.assertNotEqual(json_shape(b'[{"Id":1}]'), json_shape(b'[{"id":1}]'))
        self.assertNotEqual(json_shape(b'{"Id":1}'), json_shape(b'{"Id":"1"}'))

    def test_json_shape_of_non_json_is_none(self):
        self.assertIsNone(json_shape(b"<html></html>"))

    def test_visible_text_drops_tags(self):
        self.assertEqual("Name Price", visible_text("<th>Name</th>\n<th>Price</th>"))


class Classification(unittest.TestCase):
    def test_no_diff_is_pass(self):
        self.assertEqual("PASS", classify("mvc", "mock", "root", [], {}, {})[0])

    def test_undocumented_difference_is_fail(self):
        verdict, reason = classify("mvc", "mock", "root", ["body"], {"status": 200}, {"status": 200})
        self.assertEqual("FAIL", verdict)
        self.assertIn("body", reason)

    def test_missing_antiforgery_400_is_intentional(self):
        verdict, _ = classify("mvc", "mock", "create-post-no-antiforgery", ["status", "content-type", "body"],
                              {"status": 500}, {"status": 400})
        self.assertEqual("INTENTIONAL", verdict)

    def test_documented_step_with_unexpected_status_is_fail(self):
        verdict, _ = classify("mvc", "mock", "create-post-no-antiforgery", ["status", "body"],
                              {"status": 500}, {"status": 200})
        self.assertEqual("FAIL", verdict)

    def test_server_side_validation_decision_is_intentional(self):
        verdict, reason = classify("webforms", "sql", "create-post-noname", ["status", "location", "body"],
                                   {"status": 302}, {"status": 200})
        self.assertEqual("INTENTIONAL", verdict)
        self.assertIn("Decision A", reason)

    def test_validation_decision_requires_modern_rerender(self):
        verdict, _ = classify("webforms", "sql", "create-post-noname", ["status", "location", "body"],
                              {"status": 302}, {"status": 302})
        self.assertEqual("FAIL", verdict)

    def test_legacy_mock_decision_covers_harness_error_in_mock(self):
        verdict, reason = classify("webforms", "mock", "delete-13-post", ["status", "body", "harness"],
                                   {"status": None, "error": "no POST form"}, {"status": 302})
        self.assertEqual("INTENTIONAL", verdict)
        self.assertIn("Decision B", reason)
        self.assertIn("harness error", reason)

    def test_mock_only_rule_does_not_cover_sql(self):
        verdict, reason = classify("mvc", "sql", "details-13-edited", ["body"], {"status": 200}, {"status": 200})
        self.assertEqual("FAIL", verdict)
        self.assertNotIn("Decision", reason)

    def test_harness_error_is_fail(self):
        self.assertEqual("FAIL", classify("mvc", "mock", "root", ["harness"], {"error": "x"}, {})[0])


if __name__ == "__main__":
    unittest.main()
