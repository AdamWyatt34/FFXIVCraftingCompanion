# Plan: serve game data statically; the API becomes accounts-only

Status: draft for review, 2026-10-17. Facts below are from the repo at `8ee70ba` (PR #7 merged), XIVAPI row counts on 2026-10-17, and the Vercel limits page (updated 2026-09-16).

## 1. End state

- Signed-out use reads prebuilt JSON from Vercel's CDN and computes everything in the browser. Zero `/api/*` requests except the two exceptions below.
- The .NET API serves accounts only: auth, profile (incl. Lodestone import), favorites, saved lists, craft plans (saved), rotations, notifications, gear compare, feedback.
- Accepted exceptions that still reach the API signed-out: the rotation library (`/simulator/library`, community content, cannot be static) and feedback submissions (user-initiated, rare). Both load only when used.
- Production DB keeps its game-data tables. Lodestone import matches gear by name against `Equipment`, and favorites/profile/rotations join names; dropping tables would need migrations (no-go). The DB is re-synced once per game patch (section 5), not daily.
- Everything stays on free tiers: Vercel Hobby, GitHub Actions on a public repo, Azure as today.

## 2. Inventory as verified

Verified against `frontend/src` callers, not the controllers. "Static" = prebuilt file. "Client" = ported computation. "API" = stays.

| Endpoint | Today | Target |
|---|---|---|
| `GET /api/items/search` | browser, signed-out | Client search over a static index |
| `GET /api/items/{id}`, `{id}/context` | browser | Static item file (context embedded) |
| `GET /api/recipes/{id}` (+materials), `{id}/crafting-parameters` | browser + server (`server-api.ts`) | Static recipe file; recipe page SSG |
| `GET /api/recipes/{id}/materials?quantity` | browser | Client: tree at qty 1 is precomputed in the recipe file; quantity scaling ported |
| `GET /api/recipes/{id}/cost` | browser | Client: cost estimate ported, Universalis called directly (CORS `*` confirmed) |
| `GET /api/crafting/journey/{id}` | browser | Client composition of recipe + tree + item context; sufficiency via WASM when signed in |
| `POST /api/gathering/guide`, `GET eorzean-time` | browser | Client: guide ported; gathering rows embedded in item files |
| `GET /api/gear/recommendations`, `/profiles`, `/search` | browser | Client: scoring ported; per-job static equipment files |
| `GET /api/gear/recommendations/compare` | browser, `[Authorize]` | API (unchanged) |
| `POST /api/craft-plans/resolve`, `/export`, `GET {id}/resolved` | browser, anonymous | Client aggregation over recipe trees; `/resolved` becomes client-side over the saved plan's ids |
| `GET /api/market/worlds` | browser | Static list already in `server-data.ts`; call removed |
| `GET /api/market/prices` | no caller | Removed |
| `/api/crafting/*` simulate, solve, actions, initial-state, execute-action, export-macro | browser, api engine mode only | WASM only; `api` engine mode deleted (section 7) |
| `POST /api/analytics/event` | browser, signed-out | Vercel Web Analytics (50k events/month on Hobby) |
| `POST /api/feedback` | browser | API (exception) |
| `GET /api/rotations`, `{id}` | browser, signed-out | API (exception) |
| shoppinglist, sufficiency, tree, slot recommendation, calculate-stats | no caller | Not ported; deleted with the dead controllers in PR 6 |
| `/api/admin/*` | unauthenticated | API key required (PR 1a); the export workflow calls `sync` |

Account endpoints that join game data: favorites (ItemName, IconUrl, CraftType, RecipeLevel), profile gear (EquipmentName, IconUrl), rotations (RecipeName, ActionNames), notifications (item name in title), craft plans `/resolved` (full tree). All could return ids for the client to resolve from static data, but only craft-plan resolution moves (it is the one that walks the recipe graph). The rest keep their joins since the tables stay.

## 3. Export pipeline

- New console project `src/FFXIV.Export` (outer ring, references Infrastructure). It boots the existing DI against a SQL Server given by connection string, runs `MigrateAsync`, runs `DataSyncOrchestrator.RunFullSyncAsync`, then enumerates every recipe, item, and job and writes JSON by calling the existing MediatR query handlers (`GetRecipeByIdQuery` with materials, `GetRecipeCraftingParametersQuery`, `GetMaterialTreeQuery` at qty 1, `GetItemByIdQuery`, `GetItemContextQuery`, equipment per job, gathering rows). Serialization uses the API's `JsonOptions`, so file shapes equal today's API responses. No sync or mapping code is refactored.
- Workflow `.github/workflows/data-export.yml` (no-go zone: written in the PR, applied by Adam): `mcr.microsoft.com/mssql/server:2022-latest` as a service container, `dotnet run --project src/FFXIV.Export`, then publish. Weekly schedule plus `workflow_dispatch`. Skips publishing when XIVAPI's game version equals the last export's. Public repo, so minutes are free; expected 10 to 15 min per run (sync at 15 req/s over ~250 XIVAPI pages).
- Publishing: the output is committed to an orphan `data` branch and force-pushed, so the branch is always one commit and `main`'s history never carries data. The workflow then commits `frontend/data.lock.json` (`{ version, gameVersion, dataSha }`) to `main`, which is what triggers the Vercel deploy. Rollback = revert the lock bump.
- Vercel build: `prebuild` runs `frontend/scripts/data-fetch.mjs`, which downloads the `data` branch tarball from `codeload.github.com` at the locked sha, verifies its manifest, and extracts into `frontend/public/data/v/<version>/` (gitignored). Build output has no file-count limit; the 15,000-file cap is for source. Local dev runs the same script.
- After a publish with a changed game version, the workflow calls `POST /api/admin/sync` on production with the new API key. One DB wake per patch (about 1,800 vCore-seconds), instead of the daily cron that only fires when a replica happens to be awake.

Rejected: GitHub release assets (same mechanics, worse diffing); jsDelivr over the data branch (keeps Vercel out entirely but adds a third-party origin to the service worker and offline story); refactoring the sync off EF (large change for no runtime benefit).

## 4. File layout and measured sizes

Counts from XIVAPI on 2026-10-17: 12,264 recipes, 44,400 item rows (the sync keeps rows with a name; expect 35k to 40k), 1,817 gathering items, 5,011 gathering points. A rich recipe row measures about 1 KB raw.

Layout under `/data/v/<version>/`:

| File | Count | Est. raw size | Contents |
|---|---|---|---|
| `recipes/<bucket>.json` (100 ids per bucket) | ~125 | ~40 MB total (~3 KB/recipe) | `RecipeDto` + `RecipeCraftingParametersDto` + `MaterialTreeResultDto` at qty 1 |
| `items/<bucket>.json` (100 ids) | ~450 | ~20 MB total | `ItemDto` + `ItemContextDto` + all gathering rows + vendor rows |
| `gear/<job>.json` | ~30 | ~5 MB total | `EquipmentDto` list with the 21 stats, per job category |
| `search/items.json` | 1 | ~2.5 MB (~0.5 MB gzipped) | `{id, name, icon, ilvl, cat, hq, recipeId, craftType, isEquip}` for every named item |
| `manifest.json` | 1 | <1 KB | version, gameVersion, exportedAt, counts |

Buckets of 100 ids instead of one file per entity: ~600 files instead of ~55,000, faster deploy upload, one fetch caches 100 neighbours in the service worker, and an id-to-bucket mapping is a division. Switching to per-entity files is an exporter flag if a page shows a bucket is too heavy; a 100-recipe bucket is ~300 KB raw, ~50 KB gzipped.

Headroom on Vercel Hobby: output ~70 MB raw (no cap on build output; the 100 MB cap is for CLI source uploads); a recipe page visit transfers ~60 KB gzipped of data, so 100 GB/month covers over a million visits; SSG pages use no functions; build time measured in PR 1b, cap is 45 min. Vercel Web Analytics: 50,000 events/month, with the five custom events kept.

## 5. Freshness and versioning

- The version is baked into the bundle from `data.lock.json` (`NEXT_PUBLIC_DATA_VERSION`). Every data URL contains it, so a deployment references exactly one version and the app never mixes.
- `next.config.ts` adds `Cache-Control: public, max-age=31536000, immutable` for `/data/v/*`.
- Serwist: `/data/v/*` becomes CacheFirst with a large entry cap; the `game-data-api` and `market-data` rules go. A page cached by the service worker from an older bundle keeps referencing its own version, which stays in the cache until expiry. Offline falls back to whatever version was last cached.
- A patch flows as: scheduled or manual export, data branch force-push, lock bump on `main`, Vercel deploy, prod DB re-sync. Manual trigger covers a patch day.

## 6. Recipe pages and sitemap

- `/recipes/[id]` gets `generateStaticParams` over all recipe ids and reads the recipe bucket from `public/data` on disk at build (no fetch, no `server-api.ts`). `generateMetadata` and the JSON-LD block read the same object. `dynamicParams = false`, unknown ids 404.
- Decision: SSG all 12.3k pages. Build time is measured in PR 1b; the fallback if it exceeds 20 min is ISR with the top-N by popularity prebuilt and the rest on demand (each miss is one function invocation; Hobby includes 1M).
- Sitemap: `generateSitemaps` in chunks of 5,000 recipe URLs (3 files plus the static pages), so the `sitemap.ts` TODO closes in the same PR.

## 7. Engine mode

Deleted entirely, for signed-in users too. The two paths that still force the API in WASM mode (`useProfileStats`, missing stats) are replaced by resolving stats client-side from `CharacterProfileDto.JobLevels[job]` (Craftsmanship, Control, CP are already there). Crafting parameters come from the static recipe file. Verify in the Vercel project that `NEXT_PUBLIC_ENGINE_MODE` is unset or `wasm` (not readable from this session).

## 8. Parity

Same pattern as the engine goldens, but the frontend needs a unit runner: add `vitest` (dev dependency, flagged).

- A fixture dataset (`tests/FFXIV.Tests/StaticData/TestData/fixture.json`) is produced once by the exporter: about 25 recipes with their full ingredient closure, items with context, gathering rows, and equipment for two jobs. Committed; small (well under 1 MB).
- C# generator tests load the fixture into NSubstitute repositories, run the real handlers, and write JSONL goldens: material tree at quantities {1, 2, 7, 99, 999}, cost estimate against a recorded Universalis response, gathering guide at fixed Eorzean times, gear sets per profile, craft-plan aggregation, item search queries. `FFXIV_PARITY_GENERATE=1` regenerates, otherwise the test fails on the first differing byte, as `ParityGoldenTests` does.
- Vitest replays the goldens against the TypeScript ports.
- Two C# sorts are unstable and must get a deterministic tie-break before goldens exist: intermediate-craft ordering in `RecipeTreeResolver` (depth sort) and the top-10-by-item-level candidate pool in `EquipmentRepository`. Both are one-line changes and are the only behaviour changes to the reference.
- Stat sufficiency runs a time-budgeted solver, so its golden covers the deterministic parts (minimum checks, recommendation thresholds) and not the solver output.

## 9. PR breakdown

Each leaves the site working. Backend PRs keep MediatR, xUnit + FluentAssertions + NSubstitute; no engine rules move out of Domain; no action ids change; `crates/` is untouched.

0. **Health and migrations** (separate; touches `infra/` and `appsettings.Production.json`, Adam applies): `/health/live` with `Predicate = _ => false`, liveness probe pointed at it, `/health` stays the full check, `ApplyMigrationsOnStartup: false` in production with a migration step in `backend-deploy.yml`. `freeLimitExhaustionBehavior` untouched.
1a. **Exporter and fixtures** (backend only): `src/FFXIV.Export`, the two deterministic sorts, API key on `AdminController`, fixture dataset and golden generator scaffolding, `data-export.yml` written for Adam to apply. Site unchanged.
1b. **Static recipes and items in the frontend**: `data-fetch.mjs` + `data.lock.json`, a `src/lib/static-data/` client (bucket lookup, search index in a worker), item search, item detail, recipe detail, simulator recipe loading and crafting parameters, SSG recipe pages, sitemap, Serwist rules, vitest. API still serves materials/cost/journey/gear/gathering.
2. **Material tree, shopping list, craft plans on the client**: quantity scaling, `categorizeShoppingList` fed from the static tree, craft-plan resolve/export/resolved, journey composition; goldens.
3. **Gear on the client**: per-job gear files, profiles and scoring port, equipment search; compare stays on the API.
4. **Gathering and market**: gathering guide and Eorzean time, cost estimate with direct Universalis calls, static worlds; signed-in sufficiency via WASM.
5. **Remove the rest**: engine mode deletion, analytics to Vercel Web Analytics, remaining signed-out calls, and a Playwright test that walks the signed-out flows against the mock API and asserts it recorded zero `/api/` requests (the mock already records every request).
6. **Delete dead API surface** (optional, after a week on static data): Items, Recipes, Crafting, Gear (except compare), Gathering, Market, Analytics controllers and their routes; the XIVAPI health check; the in-app sync cron. Handlers stay because the exporter uses them.

## 10. Open items for Adam

- Confirm the exceptions (rotation library, feedback) and the admin API key approach for the per-patch DB sync.
- Confirm `vitest` as a frontend dev dependency.
- Apply PR 0 and the `data-export.yml` workflow when they come up; both are no-go zones for me.
- Check the Vercel project's `NEXT_PUBLIC_ENGINE_MODE` and `INTERNAL_API_URL` values; `INTERNAL_API_URL` becomes unused after PR 1b.
