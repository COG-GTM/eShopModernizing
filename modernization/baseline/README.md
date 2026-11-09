# Legacy baseline (recorded on Mono 6.12 / xsp4, mock mode)

| Artifact | Content |
|---|---|
| `transcripts/mvc/` | 86 transcripts (`NNN-<case>.json`): list incl. every page/page size + edge params, details (1, 5, 12, missing, no id, non-numeric), create GET + 8 validation failures + missing antiforgery + valid POST, edit GET/missing/no-id/2 invalid/valid, delete GET/missing/no-id/POST, data-state after create/edit/delete, pics, `/api/brands` (JSON, XML, default Accept, by id, missing, DELETE, POST/PUT 405), `/api/types`, `/api/files`, `/api`, `/api/catalog`, 5 bundles + static files, error pages. |
| `transcripts/webforms/` | 52 transcripts: Default list (`/`, `/Default`, `/Default.aspx`, every index/size route, malformed route), details, create GET + 7 validation failures + valid POST, edit GET/missing/2 invalid/valid, delete GET/missing/POST, data-state snapshots, static + bundle, error pages. |
| `determinism/` | Each app reset and replayed twice with `--strict` (full normalised body): **mvc 86/86, 86/86; webforms 52/52, 52/52**. |
| `legacy-test-results.txt` / `.xml` | Characterization suite `modernization/tools/tests` against legacy mock mode: **77 passed / 77** (mvc 48, webforms 29). |
| `sql-mode/` | Mock transcripts replayed against legacy **SQL mode** (SQL Server 2022 container): mvc 85/86 (only `/api/files` bytes differ), webforms 29/52 (all 23 differences are price formatting `19.50` vs `19.5`). |

Each transcript stores the request (method, path, headers, form data with volatile fields as placeholders), the response status, key
headers (`content-type`, `location`), normalised body, semantic view and (for `data-state` cases) catalog rows.
Re-record: `python3 modernization/tools/transcript.py record --app mvc --base-url http://127.0.0.1:5100 --reset-cmd "modernization/legacy-harness/reset.sh mvc"`.
Normalisation and comparison rules: `../CONTRACT.md`.
