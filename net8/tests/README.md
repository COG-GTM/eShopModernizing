# EShop.Storefront.Tests

xUnit + `WebApplicationFactory` tests for both .NET 8 storefronts (`dotnet test net8/EShopStorefront.sln`).

- `Catalog/` — `ICatalogService` mock-mode unit tests (seed data, paging, page size 0, CRUD); a fresh host per test.
- `Mvc/`, `WebForms/` — endpoint tests mirroring `modernization/tools/tests` (77 cases) one-to-one. Each test class gets its own
  app fixture (fresh in-memory catalog = mock-mode reset) and one cookie jar per app; `*Roundtrip*` classes run last.
- Tests for a slice that is still a scaffold stub are reported as skipped. `ESHOP_TESTS_REQUIRE_ALL=1` turns those skips into
  failures (use once waves 1-2 are merged).
- `ESHOP_TEST_MVC_URL` / `ESHOP_TEST_WEBFORMS_URL` run the endpoint tests against a live server instead (e.g. legacy on
  `http://127.0.0.1:5100` / `:5101`, freshly started).
