# .NET 8 storefront parity report

Generated 2026-11-09T18:37:25+00:00 at commit `1706eff` by `modernization/parity/run-parity.sh`. Each transcript set is replayed against a freshly started .NET 8 app in mock mode (`UseMockData=true`; MVC `127.0.0.1:5200`, Razor Pages `127.0.0.1:5201`), compared per CONTRACT.md §3.

**Overall: NOT AT PARITY** — transcripts 14/138 (legacy 138/138), characterization tests 6/77 (legacy 77/77).

## Transcripts (`modernization/tools/transcript.py replay`)

| App | .NET 8 pass/total | Legacy pass/total | Accepted differences | Report |
|---|---|---|---|---|
| mvc | 11/86 | 86/86 | 0 | `results/mvc.json` |
| webforms | 3/52 | 52/52 | 0 | `results/webforms.json` |

## Characterization suite (`modernization/tools/tests`, pytest)

| Suite | .NET 8 pass/total | Legacy pass/total |
|---|---|---|
| mvc | 5/48 | 48/48 |
| webforms | 1/29 | 29/29 |
| **total** | **6/77** | **77/77** |

## Accepted differences

None.

## Failures

- mvc `001-list-root.json`: fail (status, content-type, semantic)
- mvc `002-list-catalog-index.json`: fail (status, content-type, semantic)
- mvc `003-list-catalog-default-route.json`: fail (status, content-type, semantic)
- mvc `004-list-size10-page0.json`: fail (status, content-type, semantic)
- mvc `005-list-size10-page1.json`: fail (status, content-type, semantic)
- mvc `006-list-size10-page2.json`: fail (status, content-type, semantic)
- mvc `007-list-size5-page0.json`: fail (status, content-type, semantic)
- mvc `008-list-size5-page1.json`: fail (status, content-type, semantic)
- mvc `009-list-size5-page2.json`: fail (status, content-type, semantic)
- mvc `010-list-size5-page3.json`: fail (status, content-type, semantic)
- mvc `011-list-size20-page0.json`: fail (status, content-type, semantic)
- mvc `012-list-size12-page0.json`: fail (status, content-type, semantic)
- mvc `013-list-size1-page0.json`: fail (status, content-type, semantic)
- mvc `014-list-size1-page11.json`: fail (status, content-type, semantic)
- mvc `015-list-size3-page3.json`: fail (status, content-type, semantic)
- mvc `016-list-size100-page0.json`: fail (status, content-type, semantic)
- mvc `017-list-page-only.json`: fail (status, content-type, semantic)
- mvc `018-list-size-only.json`: fail (status, content-type, semantic)
- mvc `019-list-size0.json`: fail (status)
- mvc `020-list-nonnumeric-params.json`: fail (status, content-type, semantic)
- mvc `021-details-1.json`: fail (status, content-type, semantic)
- mvc `022-details-5.json`: fail (status, content-type, semantic)
- mvc `023-details-12.json`: fail (status, content-type, semantic)
- mvc `025-details-no-id.json`: fail (status)
- mvc `026-details-nonnumeric-id.json`: fail (status)
- mvc `027-create-get.json`: fail (status, content-type, semantic)
- mvc `028-create-post-invalid-missing-name.json`: fail (error)
- mvc `029-create-post-invalid-price-three-decimals.json`: fail (error)
- mvc `030-create-post-invalid-price-too-large.json`: fail (error)
- mvc `031-create-post-invalid-price-not-number.json`: fail (error)
- mvc `032-create-post-invalid-price-empty.json`: fail (error)
- mvc `033-create-post-invalid-stock-negative.json`: fail (error)
- mvc `034-create-post-invalid-restock-too-large.json`: fail (error)
- mvc `035-create-post-invalid-maxstock-not-number.json`: fail (error)
- mvc `036-create-post-missing-antiforgery.json`: fail (error)
- mvc `037-create-post-valid.json`: fail (error)
- mvc `038-state-after-create.json`: fail (status, state_rows)
- mvc `039-details-created-13.json`: fail (status, content-type, semantic)
- mvc `040-edit-get-1.json`: fail (status, content-type, semantic)
- mvc `042-edit-get-no-id.json`: fail (status)
- mvc `043-edit-post-invalid-missing-name.json`: fail (error)
- mvc `044-edit-post-invalid-price-too-large.json`: fail (error)
- mvc `045-edit-post-valid.json`: fail (error)
- mvc `046-state-after-edit.json`: fail (status, state_rows)
- mvc `047-details-edited-1.json`: fail (status, content-type, semantic)
- mvc `048-delete-get-2.json`: fail (status, content-type, semantic)
- mvc `050-delete-get-no-id.json`: fail (status)
- mvc `051-delete-post-2.json`: fail (error)
- mvc `052-state-after-delete.json`: fail (status, state_rows)
- mvc `054-pic-item-1.json`: fail (status, content-type, semantic)
- mvc `055-pic-item-12.json`: fail (status, content-type, semantic)
- mvc `056-pic-item-created-13.json`: fail (status, content-type, semantic)
- mvc `059-api-brands-json.json`: fail (status, content-type, semantic)
- mvc `060-api-brands-xml.json`: fail (status, content-type, semantic)
- mvc `061-api-brands-default-accept.json`: fail (status, content-type, semantic)
- mvc `062-api-brand-1.json`: fail (status, content-type, semantic)
- mvc `063-api-brand-1-xml.json`: fail (status, content-type, semantic)
- mvc `065-api-brand-delete-1.json`: fail (status, content-type)
- mvc `067-api-brands-after-delete.json`: fail (status, content-type, semantic)
- mvc `068-api-brands-post-not-allowed.json`: fail (status, semantic)
- mvc `069-api-brands-put-not-allowed.json`: fail (status, semantic)
- mvc `070-api-types.json`: fail (semantic)
- mvc `071-api-files.json`: fail (status, content-type, semantic)
- mvc `073-api-catalog-hello.json`: fail (semantic)
- mvc `074-static-bundle-css.json`: fail (status, content-type, semantic)
- mvc `075-static-bundle-jquery.json`: fail (status, content-type, semantic)
- mvc `076-static-bundle-jqueryval.json`: fail (status, content-type, semantic)
- mvc `077-static-bundle-modernizr.json`: fail (status, content-type, semantic)
- mvc `078-static-bundle-bootstrap.json`: fail (status, content-type, semantic)
- mvc `079-static-site-css.json`: fail (status, content-type, semantic)
- mvc `080-static-custom-css.json`: fail (status, content-type, semantic)
- mvc `081-static-brand-image.json`: fail (status, content-type, semantic)
- mvc `082-static-pic-file.json`: fail (status, content-type, semantic)
- mvc `083-static-favicon.json`: fail (status, content-type, semantic)
- mvc `084-static-jquery-file.json`: fail (status, content-type, semantic)
- webforms `001-list-root.json`: fail (status, content-type, semantic)
- webforms `002-list-default.json`: fail (status, content-type, semantic)
- webforms `003-list-default-aspx.json`: fail (status, content-type, semantic)
- webforms `004-list-size10-page0.json`: fail (status, content-type, semantic)
- webforms `005-list-size10-page1.json`: fail (status, content-type, semantic)
- webforms `006-list-size10-page2.json`: fail (status, content-type, semantic)
- webforms `007-list-size5-page0.json`: fail (status, content-type, semantic)
- webforms `008-list-size5-page1.json`: fail (status, content-type, semantic)
- webforms `009-list-size5-page2.json`: fail (status, content-type, semantic)
- webforms `010-list-size20-page0.json`: fail (status, content-type, semantic)
- webforms `011-list-size12-page0.json`: fail (status, content-type, semantic)
- webforms `012-list-size1-page0.json`: fail (status, content-type, semantic)
- webforms `013-list-size1-page11.json`: fail (status, content-type, semantic)
- webforms `014-list-size3-page3.json`: fail (status, content-type, semantic)
- webforms `015-list-size100-page0.json`: fail (status, content-type, semantic)
- webforms `016-list-nonnumeric-route.json`: fail (status)
- webforms `017-details-1.json`: fail (status, content-type, semantic)
- webforms `018-details-5.json`: fail (status, content-type, semantic)
- webforms `019-details-12.json`: fail (status, content-type, semantic)
- webforms `020-details-missing-999.json`: fail (status)
- webforms `021-details-nonnumeric-id.json`: fail (status)
- webforms `022-create-get.json`: fail (status, content-type, semantic)
- webforms `023-create-post-invalid-missing-name.json`: fail (error)
- webforms `024-create-post-invalid-price-negative.json`: fail (error)
- webforms `025-create-post-invalid-price-not-number.json`: fail (error)
- webforms `026-create-post-invalid-price-too-large.json`: fail (error)
- webforms `027-create-post-invalid-stock-negative.json`: fail (error)
- webforms `028-create-post-invalid-restock-not-number.json`: fail (error)
- webforms `029-create-post-invalid-maxstock-too-large.json`: fail (error)
- webforms `030-create-post-valid.json`: fail (error)
- webforms `031-state-after-create.json`: fail (status, state_rows)
- webforms `032-details-created-13.json`: fail (status, content-type, semantic)
- webforms `033-edit-get-1.json`: fail (status, content-type, semantic)
- webforms `034-edit-get-missing-999.json`: fail (status)
- webforms `035-edit-post-invalid-missing-name.json`: fail (error)
- webforms `036-edit-post-invalid-stock-negative.json`: fail (error)
- webforms `037-edit-post-valid.json`: fail (error)
- webforms `038-state-after-edit.json`: fail (status, state_rows)
- webforms `039-details-edited-1.json`: fail (status, content-type, semantic)
- webforms `040-delete-get-2.json`: fail (status, content-type, semantic)
- webforms `041-delete-get-missing-999.json`: fail (status)
- webforms `042-delete-post-2.json`: fail (error)
- webforms `043-state-after-delete.json`: fail (status, state_rows)
- webforms `044-details-deleted-2.json`: fail (status)
- webforms `046-static-site-css.json`: fail (status, content-type, semantic)
- webforms `047-static-brand-image.json`: fail (status, content-type, semantic)
- webforms `048-static-pic-file.json`: fail (status, content-type, semantic)
- webforms `049-static-webforms-js.json`: fail (status, content-type, semantic)
- webforms `050-static-favicon.json`: fail (status, content-type, semantic)
- `test_mvc::test_list_default_shows_first_10_ordered_by_id`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_list_paging[10-0-10]`: failed (legacy: passed) assert 404 == 200
- `test_mvc::test_list_paging[10-1-2]`: failed (legacy: passed) assert 404 == 200
- `test_mvc::test_list_paging[10-2-0]`: failed (legacy: passed) assert 404 == 200
- `test_mvc::test_list_paging[5-0-5]`: failed (legacy: passed) assert 404 == 200
- `test_mvc::test_list_paging[5-2-2]`: failed (legacy: passed) assert 404 == 200
- `test_mvc::test_list_paging[12-0-12]`: failed (legacy: passed) assert 404 == 200
- `test_mvc::test_list_paging[20-0-12]`: failed (legacy: passed) assert 404 == 200
- `test_mvc::test_list_paging[1-11-1]`: failed (legacy: passed) assert 404 == 200
- `test_mvc::test_list_paging[3-3-3]`: failed (legacy: passed) assert 404 == 200
- `test_mvc::test_list_page_size_zero_is_server_error`: failed (legacy: passed) AssertionError: assert 404 == 500
- `test_mvc::test_list_nonnumeric_params_fall_back_to_defaults`: failed (legacy: passed) AssertionError: assert 0 == 10
- `test_mvc::test_details[1-.NET Bot Black Hoodie]`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_details[5-Roslyn Red Sheet]`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_missing_and_bad_requests[/Catalog/Details-400]`: failed (legacy: passed) AssertionError: assert 404 == 400
- `test_mvc::test_missing_and_bad_requests[/Catalog/Edit-400]`: failed (legacy: passed) AssertionError: assert 404 == 400
- `test_mvc::test_missing_and_bad_requests[/Catalog/Delete-400]`: failed (legacy: passed) AssertionError: assert 404 == 400
- `test_mvc::test_create_get_form`: failed (legacy: passed) assert 404 == 200
- `test_mvc::test_create_validation[Name--The Name field is required.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_mvc::test_create_validation[Price-1.234-The field Price must be a positive number with maximum two decimals.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_mvc::test_create_validation[Price-2000000-The field Price must be between 0 and 1000000.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_mvc::test_create_validation[Price-abc-The value 'abc' is not valid for Price.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_mvc::test_create_validation[Price--The Price field is required.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_mvc::test_create_validation[AvailableStock--1-The field Stock must be between 0 and 10 million.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_mvc::test_create_validation[RestockThreshold-20000000-The field Restock must be between 0 and 10 million.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_mvc::test_create_validation[MaxStockThreshold-abc-The value 'abc' is not valid for Max stock.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_mvc::test_create_without_antiforgery_token_is_rejected`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_mvc::test_pic_endpoint`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_api_brands_json`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_api_brands_xml_content_negotiation`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_api_brand_by_id`: failed (legacy: passed) requests.exceptions.JSONDecodeError: Expecting value: line 1 column 1 (char 0)
- `test_mvc::test_api_brand_delete_is_non_mutating`: failed (legacy: passed) AssertionError: assert 404 == 200
- `test_mvc::test_api_brands_unsupported_verbs`: failed (legacy: passed) AssertionError: assert 404 == 405
- `test_mvc::test_api_files_returns_binary_brand_list`: failed (legacy: passed) assert 404 == 200
- `test_mvc::test_static_content[/Content/css-text/css]`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_static_content[/bundles/jquery-javascript]`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_static_content[/bundles/jqueryval-javascript]`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_static_content[/bundles/modernizr-javascript]`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_static_content[/bundles/bootstrap-javascript]`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_static_content[/images/brand.png-image/png]`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_static_content[/Pics/1.png-image/png]`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_static_content[/favicon.ico-image]`: failed (legacy: passed) assert (404 == 200)
- `test_mvc::test_zz_create_edit_delete_roundtrip`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_webforms::test_list_default`: failed (legacy: passed) assert 404 == 200
- `test_webforms::test_list_aliases[/Default]`: failed (legacy: passed) AssertionError: assert 0 == 10
- `test_webforms::test_list_aliases[/Default.aspx]`: failed (legacy: passed) AssertionError: assert 0 == 10
- `test_webforms::test_list_paging_route[10-0-10]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_list_paging_route[10-1-2]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_list_paging_route[5-0-5]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_list_paging_route[5-2-2]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_list_paging_route[12-0-12]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_list_paging_route[20-0-12]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_list_paging_route[1-11-1]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_list_paging_route[3-3-3]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_details[1-.NET Bot Black Hoodie]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_details[12-Prism White TShirt]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_missing[/Catalog/Details/999-500]`: failed (legacy: passed) AssertionError: assert 404 == 500
- `test_webforms::test_missing[/Catalog/Edit/999-500]`: failed (legacy: passed) AssertionError: assert 404 == 500
- `test_webforms::test_missing[/Catalog/Delete/999-500]`: failed (legacy: passed) AssertionError: assert 404 == 500
- `test_webforms::test_create_get_form`: failed (legacy: passed) assert 404 == 200
- `test_webforms::test_create_validation[Name--The Name field is required.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_webforms::test_create_validation[Price--5-The Price must be a positive number with maximum two decimals between 0 and 1 million.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_webforms::test_create_validation[Price-abc-The Price must be a positive number with maximum two decimals between 0 and 1 million.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_webforms::test_create_validation[Stock--1-The field Stock must be between 0 and 10 million.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_webforms::test_create_validation[Restock-abc-The field Restock must be between 0 and 10 million.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_webforms::test_create_validation[Maxstock-20000000-The field Max stock must be between 0 and 10 million.]`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)
- `test_webforms::test_static[/Content/site.css-text/css]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_static[/images/brand.png-image/png]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_static[/Pics/1.png-image/png]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_static[/Scripts/WebForms/WebForms.js-javascript]`: failed (legacy: passed) assert (404 == 200)
- `test_webforms::test_zz_create_edit_delete_roundtrip`: failed (legacy: passed) AssertionError: ('/Catalog/Create', 404)

Re-run: `modernization/parity/run-parity.sh` (see `modernization/parity/README.md`).
