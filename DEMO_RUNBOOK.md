# VF Corporation — Devin Executive Demo Runbook

**Audience:** Abhishek Dalmia, EVP & COO (economic buyer) · **When:** Mon 6/29, 11:00am PT · **Length:** ~20 min
**Business frame — Project Reinvent (~$600M cost-savings target):** cut SI/contractor spend and modernize legacy systems **without growing headcount.**
**Anchor repo:** [`COG-GTM/eShopModernizing`](https://github.com/COG-GTM/eShopModernizing) — Microsoft's legacy **.NET *Framework*** eShop reference app (ASP.NET WebForms + MVC, Windows Containers, Azure). It is simultaneously **legacy .NET Framework** *and* a **retail/e-commerce** app → a clean stand-in for a VF retail back-office commerce system.

> The narrative is one unified flow on one codebase: **Understand → Secure → Modernize → Scale.** Everything below is **pre-baked and real** (live PRs, real scan findings, real compiling/tested code, real parallel sessions). The live run just *opens and narrates* these artifacts, so it's reliable.

---

## At-a-glance: open these tabs before you start

| # | Beat | Open this | Link |
|---|---|---|---|
| 1 | UNDERSTAND | DeepWiki for the repo | https://app.devin.ai/wiki/COG-GTM/eShopModernizing |
| 2 | SECURE | Security remediation PR #28 | https://github.com/COG-GTM/eShopModernizing/pull/28 |
| 3 | MODERNIZE | .NET Framework → .NET 8 migration PR #26 | https://github.com/COG-GTM/eShopModernizing/pull/26 |
| 4 | SCALE | Vans PR #25 · Timberland PR #27 · the 2 parallel sessions | [#25](https://github.com/COG-GTM/eShopModernizing/pull/25) · [#27](https://github.com/COG-GTM/eShopModernizing/pull/27) |

**Parallel session links (Beat 4):**
- Vans Catalog .NET 8 — https://app.devin.ai/sessions/b513ec2b7bdc4d42b58c44b86e878cfe
- Timberland Catalog .NET 8 — https://app.devin.ai/sessions/88a4681eb6b44d7ea86700301e05a32c

**One-line setup:** have all five tabs above pre-loaded; pin the DeepWiki and the two PRs (#28, #26) front-and-center.

---

## Timing (≈20 min)
| Beat | Target | Running total |
|---|---|---|
| Open / framing | 1 min | 1:00 |
| 1 — UNDERSTAND | 4 min | 5:00 |
| 2 — SECURE | 5 min | 10:00 |
| 3 — MODERNIZE | 7 min | 17:00 |
| 4 — SCALE | 2.5 min | 19:30 |
| Close | 0.5 min | 20:00 |

---

## Opening (1 min) — set the frame
**Say:** "VF has a Project Reinvent mandate: ~$600M in cost savings, cut SI and contractor spend, modernize legacy systems — *without growing headcount*. The hard part of that mandate is the legacy .NET back-office and commerce estate: undocumented, full of old dependencies, expensive to touch. I'm going to show you, on one real legacy app, how Devin takes that from *understanding* → *securing* → *modernizing* → *scaling across brands* — as reviewed pull requests in your existing process."
**Do:** Show the repo root. Point out it's a real Microsoft .NET Framework reference app (WebForms + MVC + N-Tier variants).

---

## Beat 1 — UNDERSTAND (DeepWiki / Ask Devin) — 4 min
**Goal:** Devin already understands an undocumented legacy app, instantly.

**Click path:**
1. Open **https://app.devin.ai/wiki/COG-GTM/eShopModernizing**.
2. Show the auto-generated architecture overview + diagrams (no human wrote this).
3. Use **Ask Devin** on the wiki and ask the questions below verbatim.

### Ask-Devin Q&A (ask verbatim; grounded answers below)

**Q1 — "What .NET Framework version does this app target, and where are the Catalog and Ordering domains handled in the codebase?"**
*Expected answer:* Targets **.NET Framework 4.7.2** (the dominant `net472` TFM; a few helper libs are net461/462). The **Catalog** domain is the core of the app: legacy implementation at **`eShopLegacyMVCSolution/src/eShopLegacyMVC`** — `Models/CatalogItem|CatalogBrand|CatalogType`, `Services/ICatalogService` + `CatalogService` (Entity Framework 6 via `CatalogDBContext`) and a `CatalogServiceMock`, with both an MVC `Controllers/CatalogController` and an API `Controllers/Api/CatalogController`. Modernized variants live under `eShopModernized*`. **There is no Ordering domain in this codebase** — it's a catalog-management app; ordering/checkout would be a *new* bounded context (see Q3).
*Why it lands:* Devin is precise about what exists **and** honest about what doesn't — exactly what you want before touching legacy code.

**Q2 — "How is catalog data stored and configured, and how do I run this without a database?"**
*Expected answer:* Catalog data is persisted with **Entity Framework 6** through `CatalogDBContext` against **SQL Server**; product images can use **Azure Blob Storage** (`UseAzureStorage`). For local/dev runs there's a **mock mode** (`UseMockData`) backed by `CatalogServiceMock` + seed data (`Setup/CatalogItems.csv`, `CatalogBrands.csv`, `CatalogTypes.csv`), so it runs with no DB.
*Why it lands:* Devin surfaces the exact config seams (DB vs. mock, Azure vs. local) a migration team needs on day one.

**Q3 — "Where would a payments / checkout integration plug in?"**
*Expected answer:* There's no payments or basket code today, so a payments integration is a **new bounded context**, not an edit to existing code. The natural seam is the **API layer** (`Controllers/Api/CatalogController` pattern) — stand up a new Ordering/Checkout service that reads the Catalog API, and add a payments provider behind it; the catalog stays the source of truth for product/price/stock. In the modernized .NET 8 design (Beat 3) this is a sibling service calling the new `Catalog.Api`.
*Why it lands:* Devin reasons about *where new revenue features attach* to a legacy system — the modernization-to-growth bridge.

**Talk-track + proof point:** "This is the same 'understand-before-you-touch' capability **Itaú** uses across **17,000+** engineers, and that let **The Citation Group** compress a **.NET/AngularJS → .NET Core/React** rebuild from **~3 months to ~2 weeks**. Understanding is the bottleneck Devin removes first."

**Backup path:** If the wiki is slow or Ask Devin stalls live, read the grounded answers above directly off this runbook (they're verbatim from the code). Optionally show the file paths in the repo (`eShopLegacyMVCSolution/src/eShopLegacyMVC/...`) to prove they're real.

---

## Beat 2 — SECURE (vulnerability remediation) — 5 min
**Goal:** Real scanner findings → reviewed remediation PR in the normal flow.

**Click path:**
1. Open **PR #28** → https://github.com/COG-GTM/eShopModernizing/pull/28.
2. Show the PR body **before/after** table, then the **Files changed** tab.
3. Open `eShopModernizedMVCSolution/SECURITY_REMEDIATION.md` for the full finding list.
4. Point at the green **Snyk security + license CI checks** on the PR.

**What to show (all real Snyk findings):**
- **CSRF / CWE-352 (3 Snyk Code findings):** added `[ValidateAntiForgeryToken]` to the Catalog `Create`/`Edit`/`DeleteConfirmed` POST actions + `@Html.AntiForgeryToken()` in the matching Razor forms.
- **Newtonsoft.Json High (CVE-2024-21907):** `packages.config` said 13.0.2 but the csproj + `Web.config` binding redirect still pinned the **vulnerable 12.x** assembly at runtime — Devin caught and fixed the mismatch.
- **Before/after:** High **1 → 0**, CSRF Low **4 → 1**; remaining medium SCA items (log4net→3.3.0, IdentityModel/JWT→5.7.0) are **documented with exact upgrade commands** as a scoped follow-up rather than silently dropped.

**Talk-track + proof points:** "These are real Snyk findings, not a canned slide. **Itaú auto-remediated ~70%** of their SonarQube/Fortify/Veracode findings this way. On a security benchmark Devin is **~20x faster than senior engineers** on vulnerability fixes — and every fix is a reviewable PR through your existing CI and approvals."

**Live vs. pre-baked:** Pre-baked (PR open, CI green). Live action = open it and narrate. **Backup:** if GitHub is slow, show `SECURITY_REMEDIATION.md` locally / screenshot in this folder.

---

## Beat 3 — MODERNIZE (.NET Framework → .NET 8) — 7 min · **THE HEADLINE**
**Goal:** A real, compiling, **tested** migration of a bounded module off .NET Framework.

**Click path:**
1. Open **PR #26** → https://github.com/COG-GTM/eShopModernizing/pull/26.
2. Show `eShopOnNet8/MIGRATION_PLAN.md` — the legacy→.NET 8 mapping table + "done vs. remaining."
3. **Files changed:** show `eShopOnNet8/Catalog.Api` (Program.cs, CatalogController, EF Core `CatalogDbContext`, services) and the `Catalog.Api.Tests` project.
4. Land on the test story: **`dotnet build` clean + `dotnet test` → 14/14 passing on Linux.**

**The migration (legacy → .NET 8), say it as a table:**
- `packages.config` MSBuild/Windows-only → **SDK-style csproj, cross-platform**
- ASP.NET MVC 5 (`System.Web.Mvc`) → **ASP.NET Core `[ApiController]`** REST/JSON + Swagger
- Autofac + System.Web → **built-in DI** · **EF 6 → EF Core 8** · log4net → **`ILogger<T>`** · `Web.config` → **`appsettings.json`** · IIS/Windows containers → **Kestrel/Linux**

**"Done vs. remaining" (say this — it builds trust):** Done = project conversion, models, services (mock + EF Core), controller, pagination, seed data, 14 green tests. A full session would then swap EF Core InMemory → Azure SQL + migrations, port the Razor views to a modern frontend, port image upload + auth, and wire strangler-fig routing.

**Build constraint (own it):** "The legacy .NET Framework side only builds on Windows/MSBuild — so we target the cross-platform .NET 8 side that builds and tests on Linux and in CI. That mirrors a real migration: legacy stays put on Windows while new .NET 8 services stand up beside it."

**Talk-track + proof points:** "This is the **McDonald's** story — Devin driving **.NET 8 → 10** upgrades at **5–10x** the pace of manual work — and the **Citation Group** rebuild (**~3 months → ~2 weeks**). The headline for Project Reinvent: this migration capacity is **headcount-neutral** — **Goldman** runs the equivalent of **12,000 → 14,400** engineers of output with the same team."

**Live vs. pre-baked:** Pre-baked (PR open, 14 tests green, CI green). **Backup:** if GitHub is slow, open the code locally under `eShopOnNet8/` and run `dotnet test eShopOnNet8/eShopOnNet8.sln` to show 14/14 live.

---

## Beat 4 — SCALE (parallel / multi-brand) — 2.5 min
**Goal:** Make "3 brand stacks, built in parallel, headcount-neutral" tangible.

**Click path:**
1. Open the **two parallel session tabs** (Vans + Timberland) — show they ran **at the same time**.
2. Open their PRs: **Vans #25** and **Timberland #27**.
3. Tie it together: the same Catalog migration pattern from Beat 3 fanned out into **three brand stacks** simultaneously — the reference `Catalog.Api` (#26) + **Vans** `Vans.Catalog.Api` (#25) + **Timberland** `Timberland.Catalog.Api` (#27) — each a self-contained .NET 8 service with passing tests, **no extra engineers**.

**Talk-track + proof point:** "This is exactly how **Linktree** runs Devin — firing off **5 parallel sessions** and shipping same-day. For VF that means Vans, The North Face, and Timberland modernizing **in parallel on one pattern**, not one-after-another. Same team, 3x the brand throughput — that's the Project Reinvent math."

**Live vs. pre-baked:** Pre-baked (both sessions complete, both PRs open). **Backup:** if a session tab is slow, the two PRs (#25, #27) alone prove the parallel fan-out.

---

## Close (30 sec)
**Say:** "One legacy app, one unified flow: Devin **understood** it, **secured** it, **modernized** a module to .NET 8 with passing tests, and **scaled** that across three brand stacks in parallel — all as reviewed PRs in your existing process. That's how Project Reinvent hits its number: modernize the legacy estate and cut SI/contractor spend **without growing headcount**."

---

## What is LIVE vs. PRE-BAKED (summary)
| Beat | Pre-baked artifact (real, reviewable) | Live action on Monday |
|---|---|---|
| 1 UNDERSTAND | DeepWiki generated for the repo + 3 grounded Q&A | Open wiki, ask the 3 questions |
| 2 SECURE | PR #28 (real Snyk findings, before/after, CI green) | Open PR, narrate before/after |
| 3 MODERNIZE | PR #26 (.NET 8 Catalog API, 14 tests, CI green) | Open PR, show tests; optional `dotnet test` |
| 4 SCALE | PRs #25 + #27 from 2 parallel child sessions | Open both sessions + PRs |

## Proof-point cheat sheet (use the matching one per beat)
- **Goldman** — 12,000 → 14,400 engineers of output, headcount-neutral *(Beat 3 close)*
- **Itaú** — ~70% of scanner vulns auto-remediated; 17k+ engineers *(Beats 1–2)*
- **McDonald's** — .NET 8 → 10 upgrades at 5–10x *(Beat 3)*
- **The Citation Group** — .NET/AngularJS → .NET Core/React, ~3 months → ~2 weeks *(Beats 1, 3)*
- **Linktree** — 5 parallel sessions, same-day shipping *(Beat 4)*
- **Security benchmark** — ~20x faster than senior engineers on vuln fixes *(Beat 2)*

## Global backup paths
- **No internet / GitHub slow:** everything is in the local clone — open files under `eShopOnNet8/`, `eShopModernizedMVCSolution/`, and this runbook; run `dotnet test eShopOnNet8/eShopOnNet8.sln` for live green tests.
- **DeepWiki unavailable:** read the grounded Q&A answers in Beat 1 directly (verbatim from code) and show the real file paths.
- **A PR won't load:** the other PRs + this runbook's before/after tables carry the story; screenshots can be captured ahead of time into this folder.

## Open decisions to confirm before Monday
1. **Proof-point names:** this runbook uses the names from the brief (Goldman, McDonald's). Confirm each is referenceable *by name* in the room, or switch to the logo-only / anonymized phrasing per the Customer Proof Points knowledge note.
2. **Beat 2 scope:** confirm it's fine to present the medium SCA items (log4net 3.x, JWT 5.7.0) as a *documented follow-up* (they're major/co-versioned upgrades needing a Windows/MSBuild build to verify) rather than fixing them live.
3. **Windows CI:** the .NET Framework projects can't build on the Linux VM; if you want a green *build* (not just Snyk) on the Beat 2 PR, we'd need a Windows MSBuild runner.
