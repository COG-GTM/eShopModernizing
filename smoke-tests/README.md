# Canary smoke tests for the modernized eShop catalog

`smoke.py` exercises the critical paths of the modernized catalog app
(`eShopModernized/src/eShopCoreModernized`) over HTTP and returns a clear
promote / roll back verdict. Run it against any stage of the strangler-fig
cutover: the .NET Core instance directly, the nginx front door at 25/50/75/100%
(`nginx-*percent.conf`, `migrate-traffic.sh`), or production.

It uses only the Python 3.8+ standard library, so it runs from a jump box, a
pipeline agent, or a laptop with nothing to install.

```bash
python3 smoke-tests/smoke.py --list                        # checks and stage profiles
python3 smoke-tests/smoke.py --stage local                 # app on http://localhost:5002
python3 smoke-tests/smoke.py --stage canary-25 --junit smoke-canary-25.xml
python3 smoke-tests/smoke.py --stage production --base-url https://eshop.example.com
```

## What it checks

| Check | Severity | What it proves |
|---|---|---|
| `health.basic` | critical | `GET /api/health` answers `Healthy` |
| `health.detailed` | critical | Every dependency in `/api/health/detailed` is `Healthy`, and feature flags (`useMockData`, `useAzureStorage`, ...) match what the stage expects, so a canary on mock data or the wrong storage fails fast |
| `catalog.index` | critical | `/Catalog/Index` renders the catalog table with at least `min_catalog_items` rows |
| `catalog.pagination` | critical | A second page renders (`pageSize=2&pageIndex=1`) |
| `catalog.details` | critical | The Details page renders for an item discovered on the list page |
| `catalog.not_found` | critical | An unknown item returns 404, not a 5xx |
| `api.brands` | critical | `/api/Brands` and `/api/Brands/{id}` return brand JSON |
| `api.files` | critical | `/api/Files` brand export matches `/api/Brands` |
| `api.default_image` | critical | `/api/ImageUpload/default` returns an image URL |
| `auth.guard` | critical | Anonymous `GET /Catalog/Create` is redirected to sign-in (cookie login or Azure AD) instead of being served |
| `auth.login_page` | critical | `/Account/Login` renders the form (skipped when Azure AD is enabled) |
| `assets.static` | warn | Every CSS/JS/image referenced by the catalog page returns 200 |
| `catalog.crud_roundtrip` | critical, **writes** | Signs in, creates an item named `smoke-<stage>-<random>`, checks its Details page, deletes it, and confirms it is gone. Only runs when writes are allowed |

Checks marked *sampled* in `--list` run `samples` times. Behind the weighted
nginx upstreams, a single request only hits one backend, so sampling is what
catches the case where one upstream is broken. The default sample counts in
`stages.json` give roughly a 97% or better chance of hitting the .NET Core
upstream at least once at each stage (for example, at 25%: `1 - 0.75^12 ≈ 0.97`).
Sampling catches an upstream that answers with errors (5xx, wrong content,
flag drift). An upstream that refuses connections is usually hidden by nginx's
default `proxy_next_upstream error timeout` failover, so shared routes like
`/Catalog/` keep passing. That is why the `core-direct` pre-flight exists, and
why `/api/*`, which only routes to .NET Core, fails loudly in that case.

Any request slower than the stage's `max_latency_ms` downgrades that check to
a warning, so it is reported but does not block the stage on its own.

## Exit codes

| Code | Meaning | Cutover action |
|---|---|---|
| `0` | No critical failures (warnings may be printed) | Promote to the next stage |
| `1` | At least one critical check failed | Stop. Roll back this stage |
| `2` | Only warnings, and `--strict` was passed | Treat as a failed gate |
| `64` | Bad arguments or stages file | Fix the invocation |

## Stage profiles (`stages.json`)

Each stage sets `base_url`, `samples`, `allow_writes`, and `expect`:

```json
"canary-25": {
  "base_url": "http://localhost",
  "samples": 12,
  "allow_writes": false,
  "expect": { "min_catalog_items": 1, "max_latency_ms": 5000, "flags": { "useMockData": false } }
}
```

You can override any of these without editing the file. Precedence runs from
highest to lowest:

* Base URL: `--base-url`, then `SMOKE_BASE_URL`, then `SMOKE_BASE_URL_<STAGE>` (for example `SMOKE_BASE_URL_CANARY_25`), then the file.
* `--samples N` overrides the stage's sample count.
* `--allow-writes` or `--no-writes` overrides `allow_writes`.
* `--header 'Name: value'` can be repeated. Use it for routing or canary headers, a `Host` override, or gateway auth.
* `--insecure` skips TLS verification, for self-signed staging certificates.
* `--only <prefix>` and `--skip <prefix>` select checks, for example `--skip assets`.
* `SMOKE_USERNAME` and `SMOKE_PASSWORD` set the credentials the write round trip signs in with. The cookie login accepts any non-empty pair.
* `--junit <file>` and `--json <file>` write reports for CI dashboards.

Writes are off for every stage except `local`. Mock mode keeps data in memory,
but against a real database the round trip creates and deletes a real row.
Only enable writes on a stage whose database you are allowed to modify.

## Using it during a cutover

The cutover moves `/Catalog/` traffic from the modernized .NET Framework app
(port 5001) to .NET Core (port 5002) in steps, using `migrate-traffic.sh`.
Gate every step on the smoke suite:

1. **Pre-flight, before any traffic moves.** Run against the .NET Core
   instance directly, bypassing nginx:
   ```bash
   python3 smoke-tests/smoke.py --stage core-direct --base-url http://<core-host>:5002
   ```
   This catches configuration drift (mock data, wrong storage, unhealthy
   database) before any user can hit it. If you have a disposable database,
   add `--allow-writes` to prove the write path end to end.

2. **Shift traffic, then gate.** After each step, run the matching stage
   against the public front door:
   ```bash
   ./migrate-traffic.sh 25
   python3 smoke-tests/smoke.py --stage canary-25 --junit smoke-25.xml \
     || echo "GATE FAILED: roll back (step 4)"
   ```
   Repeat for `50`, `75`, and `100`. Use the exit code as the gate: `0`
   means promote, anything else means stop.

3. **Soak.** `migrate-traffic.sh` already runs its own 300-second
   `/api/health` loop. Run the suite once more at the end of the soak, since
   a failure there is often a leak or pool exhaustion that the first run
   missed. You can also loop it for longer:
   ```bash
   for i in $(seq 1 10); do python3 smoke-tests/smoke.py --stage canary-50 || break; sleep 60; done
   ```

4. **On failure, roll back.** A critical failure maps to the "Immediate
   rollback" rows of the decision matrix in `ROLLBACK-PROCEDURES.md` (5xx
   errors, database issues, authentication failure). Restore the previous
   `nginx-*percent.conf` from `nginx-backups/`, reload nginx, and then
   **re-run the suite at the previous stage** to confirm the rollback worked.

5. **After 100%.** Run the `production` stage against the public URL, then
   schedule it (cron or pipeline) as a lightweight synthetic monitor until the
   legacy backend is decommissioned.

Save the JUnit and JSON reports from every step with the change record. They
show which checks ran, how many samples were taken, and the slowest request
per check.

## Known findings on the current code

Running the suite against `eShopCoreModernized` in mock mode on Linux reports
`assets.static` as a warning:

* `_Layout.cshtml` links `/css/site.css`, but the file is `wwwroot/css/Site.css`.
  On case-sensitive file systems (Linux containers), the stylesheet returns 404.
* Catalog thumbnails point to `/pics/{id}/{file}.png`. Only `wwwroot/Pics/dummy.png`
  exists, and nothing serves that route, so every thumbnail returns 404.

Also note that `HealthController` marks the database `Healthy` without checking
the boolean result of `CanConnectAsync()`. `health.detailed` therefore cannot
detect a database outage until that is fixed, and `catalog.index`, which reads
real data when mock mode is off, is the effective database check.

## Developing the suite

```bash
python3 -m unittest discover -s smoke-tests -v   # self-tests against an in-process fake app
```

To add a check, write a function decorated with `@check(name, description,
severity=..., sampled=..., writes=...)` in `smoke.py`. Raise `CheckFailure` to
fail, raise `CheckSkipped` to skip, or return a short success message.
