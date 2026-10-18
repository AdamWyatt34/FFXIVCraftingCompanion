# FFXIV Crafting Companion — End-to-End Testing Guide & External Services Reference

## Context

This document is a comprehensive testing guide covering every feature, user flow, mobile/PWA behavior, and external service configuration for the FFXIV Crafting Companion application.

---

# Part 1: Local Development Setup

## Prerequisites

- **Node.js** >= 22 (see `frontend/.nvmrc`)
- **.NET 10 SDK**
- **Docker Desktop** (for SQL Server + Redis)

## Start Everything

```bash
# 1. Start infrastructure
docker compose up -d          # SQL Server 2022 (:1433) + Redis 7 (:6379)

# 2. Start backend
cd src/FFXIV.Api
dotnet run                    # Runs on http://localhost:5159

# 3. Start frontend
cd frontend
npm ci
npm run dev                   # Runs on http://localhost:3000
```

## Seed Data

After first startup, trigger a data sync to populate items/recipes/equipment from XIVAPI:

```
POST http://localhost:5159/api/admin/sync
```

This takes several minutes. Monitor with `GET /api/admin/sync/status`.

## Health Check

```
GET http://localhost:5159/health/live
```

Returns 200 with no checks run (the container liveness probe). The full readiness report, which
opens the database and calls XIVAPI, needs the admin key:

```
GET http://localhost:5159/health
X-Admin-Key: <Admin:ApiKey>
```

Returns JSON with database, XIVAPI, and Redis connectivity status; 401 without the key.

---

# Part 2: External Services — Registration & Configuration

## Required Services

### 1. Discord OAuth (Required for Discord Login)

**Register at:** https://discord.com/developers/applications

1. Create a New Application
2. Go to **OAuth2** tab
3. Copy **Client ID** and **Client Secret**
4. Add Redirect URL: `http://localhost:3000/auth/discord/callback`
5. Scopes needed: `identify`, `email`

**Where to configure:**

- **Development:** `src/FFXIV.Api/appsettings.Development.json`
  ```json
  "Discord": {
    "ClientId": "<your-client-id>",
    "ClientSecret": "<your-client-secret>",
    "RedirectUri": "http://localhost:3000/auth/discord/callback"
  }
  ```
- **Production (GitHub Secrets):**
  - `DISCORD_CLIENT_ID`
  - `DISCORD_CLIENT_SECRET`
  - `DISCORD_REDIRECT_URI` (e.g., `https://your-app.vercel.app/auth/discord/callback`)

**Options class:** `Infrastructure/Identity/DiscordOptions.cs` (section: `"Discord"`)

---

### 2. JWT Secret Key (Required for Auth)

No external registration needed — just generate a strong random string (minimum 32 bytes).

**Where to configure:**

- **Development:** Already set in `appsettings.Development.json`:
  ```json
  "Jwt": {
    "SecretKey": "DEVELOPMENT-ONLY-SECRET-KEY-MUST-BE-AT-LEAST-32-BYTES-LONG!!"
  }
  ```
- **Production (GitHub Secret):** `JWT_SECRET_KEY`

**Options class:** `Infrastructure/Identity/JwtOptions.cs` (section: `"Jwt"`)

---

### 3. SQL Server (Local via Docker)

No registration needed. Docker Compose provides SQL Server 2022.

- **Connection string:** Already in `appsettings.Development.json`
- **Credentials:** `sa` / `YourStrong!Passw0rd`
- **Port:** 1433

**Production:** Azure SQL Database (deployed via Bicep in `infra/main.bicep`)

- **GitHub Secret:** `SQL_ADMIN_PASSWORD`

---

### 4. Redis (Local via Docker)

No registration needed. Docker Compose provides Redis 7.

- **Port:** 6379
- **Development:** Enabled (`"Redis": { "Enabled": true }` in `appsettings.Development.json`)
- **Production:** Disabled (uses in-memory caching to stay on free tier)

---

## Optional Services

### 5. Sentry (Error Monitoring — Optional)

**Register at:** https://sentry.io

1. Create a new project -> Select **Next.js**
2. Copy the **DSN** string

**Where to configure:**

- **Frontend:** Set environment variable `NEXT_PUBLIC_SENTRY_DSN`
  - Local: create `frontend/.env.local` and add `NEXT_PUBLIC_SENTRY_DSN=https://xxx@xxx.ingest.sentry.io/xxx`
  - Vercel: Set in project environment variables
- **Backend:** `src/FFXIV.Api/appsettings.json`
  ```json
  "Sentry": {
    "Dsn": "https://xxx@xxx.ingest.sentry.io/xxx",
    "Enabled": true,
    "TracesSampleRate": 0.1
  }
  ```

**Frontend files consuming this:**

- `frontend/sentry.client.config.ts`
- `frontend/sentry.server.config.ts`
- `frontend/sentry.edge.config.ts`
- All three only initialize if `NODE_ENV === "production"` AND `NEXT_PUBLIC_SENTRY_DSN` is set

**Package:** `@sentry/nextjs` ^10.38.0

---

### 6. Azure (Production Hosting — Optional)

**Register at:** https://portal.azure.com

**GitHub Secrets needed for deployment:**

| Secret | Purpose | How to get it |
|--------|---------|---------------|
| `AZURE_CREDENTIALS` | Service principal JSON | `az ad sp create-for-rbac --role contributor --scopes /subscriptions/{sub-id}` |
| `AZURE_RESOURCE_GROUP` | Resource group name | Choose any name (e.g., `rg-ffxiv-crafting`) |
| `SQL_ADMIN_PASSWORD` | Azure SQL admin password | Generate strong password |
| `JWT_SECRET_KEY` | JWT signing key | Generate 32+ byte string |
| `CORS_ORIGINS` | Frontend URL | e.g., `https://your-app.vercel.app` |
| `CONTAINER_IMAGE` | Docker image URI | Auto-set by backend-deploy workflow |

**Resources deployed (via `infra/main.bicep`):**

- Azure Container Apps (API hosting)
- Azure SQL Database (serverless, free tier, auto-pauses after 60 min)
- Log Analytics Workspace

---

### 7. Vercel (Frontend Hosting — Optional)

**Register at:** https://vercel.com

1. Import the repository
2. Set **Root Directory** to `frontend/`
3. Framework: Next.js (auto-detected)
4. Set environment variable: `NEXT_PUBLIC_API_URL` = Azure Container Apps FQDN

---

### 8. GitHub Container Registry (Docker Images)

No separate registration — uses `GITHUB_TOKEN` (automatic).

- Images pushed to: `ghcr.io/{owner}/{repo}/ffxiv-crafting-api`

---

## External APIs (No Registration Needed)

These are free, public APIs used by the data sync pipeline:

| Service | Base URL | Rate Limit | Config Section | Purpose |
|---------|----------|------------|----------------|---------|
| **XIVAPI v2** | `https://v2.xivapi.com` | 15 req/sec | `"XivApi"` | Game data (items, recipes, equipment) |
| **Garland Tools** | `https://garlandtools.org` | 2 req/sec | `"GarlandTools"` | Recipe tree validation |
| **Universalis** | `https://universalis.app/api/v2` | 20 req/sec | `"Universalis"` | Market board prices |

---

# Part 3: End-to-End Testing Guide

## A. Public Features (No Auth Required)

### A1. Recipe Search & Detail

**Route:** `/recipes` -> `/recipes/[id]`

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | Navigate to `/recipes` | Search page loads with search bar and craft type filters | — |
| 2 | Type a recipe name (e.g., "Iron Ingot") | Results appear after debounce (~300ms) | `GET /api/items/search?q=Iron+Ingot` |
| 3 | Filter by craft type (e.g., "BSM") | Results filter to blacksmith recipes only | `GET /api/items/search?q=...&craftType=BSM` |
| 4 | Scroll to bottom | Infinite scroll loads more results | Same endpoint with pagination |
| 5 | Click a recipe card | Navigates to `/recipes/[id]` with full detail | `GET /api/recipes/{id}` |
| 6 | View recipe detail page | Shows ingredients, difficulty, durability, quality target | — |
| 7 | Click "Material Tree" tab | Full tree of sub-recipes and raw materials renders | `GET /api/recipes/{id}/materials` |
| 8 | Click "Shopping List" tab | Flat list of all raw materials with quantities, grouped by source | `GET /api/recipes/{id}/materials` |
| 9 | Change quantity to 5 | Shopping list quantities multiply by 5 | Re-fetches with `?quantity=5` |

---

### A2. Gear Recommendations

**Route:** `/gear`

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | Navigate to `/gear` | Gear page loads with job selector | — |
| 2 | Select a DoH job (e.g., CRP) | Level input and stat weight profiles appear | `GET /api/gear/profiles?job=CRP` |
| 3 | Set level to 90 | Gear recommendations load for all slots | `GET /api/gear/recommendations?job=CRP&level=90&profile=CraftingBalanced` |
| 4 | Click a gear slot card | Expands to show alternatives and equipment details | — |
| 5 | Switch to "Search" mode | Equipment search form appears | — |
| 6 | Search for equipment name | Results with stats display | `GET /api/gear/search?q=...` |
| 7 | Filter by slot (e.g., "MainHand") | Results filter to that slot | `GET /api/gear/search?q=...&slot=MainHand` |
| 8 | Select DoL job (e.g., MIN) | Stat weights auto-switch to gathering profiles | — |
| 9 | Verify CUL/FSH have no offhand slot | Offhand row is absent | — |

---

### A3. Crafting Simulator (Manual Mode)

**Route:** `/simulator`

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | Navigate to `/simulator` | Simulator page with recipe selector | — |
| 2 | Select a recipe | Recipe parameters display (difficulty, quality, durability) | `GET /api/recipes/{id}` |
| 3 | Enter crafter stats (level, craftsmanship, control, CP) | Stats confirmed, "Start Craft" enabled | — |
| 4 | Click "Start Craft" | Simulation begins — progress/quality/durability/CP bars appear | `GET /api/crafting/initial-state?...` |
| 5 | Click an action (e.g., "Muscle Memory") | State updates: progress increases, CP decreases, buff applied | `POST /api/crafting/execute-action` |
| 6 | Execute unavailable action | Button is grayed out with tooltip showing reason | `POST /api/crafting/available-actions` |
| 7 | Verify combo chain (BasicTouch -> StandardTouch -> AdvancedTouch) | Each combo step costs 18 CP instead of normal cost | — |
| 8 | Click "Undo" | Last action reversed, state restored | — |
| 9 | Click "Reset" | Returns to initial state | — |
| 10 | Complete the craft (progress >= difficulty) | Success screen with quality percentage and HQ rate | — |
| 11 | Let durability reach 0 | Failure screen | — |
| 12 | Click "Export Macro" | Macro text appears with /ac lines and /wait delays | `POST /api/crafting/export-macro` |
| 13 | Click copy button | Macro copied to clipboard | — |

---

### A4. Rotation Solver

**Route:** `/simulator` (Auto-Solve section)

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | Set up recipe + stats in simulator | Solver button becomes available | — |
| 2 | Click "Quick Solve" | Greedy solver returns a rotation within a second | None in `wasm` engine mode (runs in the browser); `GET /api/crafting/solve/quick?...` with `?engine=api` |
| 3 | Click "Full Solve" (MCTS) | Progress overlay shows elapsed time, iterations and best quality | None in `wasm` mode (solver Web Worker); `POST /api/crafting/solve` -> `GET /api/crafting/solve/{jobId}` with `?engine=api` |
| 4 | Solver completes | RotationResultPanel shows: quality, HQ%, step list | — |
| 5 | Click "Replay" | Step-by-step replay with animations | — |
| 6 | Click "Load into Simulator" | Rotation loads into manual simulator | — |
| 7 | Click "Share" | URL with encoded rotation copied to clipboard | — |
| 8 | Click "Cancel" during solve | Job cancelled | `DELETE /api/crafting/solve/{jobId}` |

---

### A5. Rotation Library

**Route:** `/simulator/library` -> `/simulator/library/[id]`

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | Navigate to `/simulator/library` | List of shared rotations | `GET /api/rotations` |
| 2 | Search by recipe name | Filtered results | `GET /api/rotations?q=...` |
| 3 | Sort by votes / recent | Order changes | `GET /api/rotations?sort=votes` |
| 4 | Click rotation card | Detail page with full replay | `GET /api/rotations/{id}` |
| 5 | Click "Load into Simulator" | Opens simulator with rotation pre-loaded | — |

---

### A6. Craft Planning

**Route:** `/recipes/plan`

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | Navigate to `/recipes/plan` | Multi-recipe planner page | — |
| 2 | Add multiple recipes with quantities | Recipe list grows | — |
| 3 | Click "Resolve Plan" | Aggregated material list appears | `POST /api/craft-plans/resolve` |
| 4 | View combined shopping list | Materials deduplicated across recipes | — |
| 5 | Click "Export" | Text/JSON/Teamcraft export options | `POST /api/craft-plans/export?format=...` |
| 6 | Click "Gathering Guide" | Zone-based gathering locations appear | `POST /api/gathering/guide` |

---

### A7. Item Pages

**Route:** `/items/[id]`

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | Navigate to `/items/{id}` | Item detail with source info, contextual links | `GET /api/items/{id}`, `GET /api/items/{id}/context` |
| 2 | Click links to recipes/gear that use this item | Navigation works correctly | — |

---

### A8. Crafting Journey & Cost Estimate

**Route:** Accessed via `/recipes/[id]/journey`

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | Navigate to a recipe's journey page | Complete crafting journey with materials, gathering, costs | `GET /api/crafting/journey/{recipeId}` |
| 2 | View gathering guide within journey | Zone-based gathering locations | — |
| 3 | Expand "Cost Estimate" section | World selector and fetch button appear | — |
| 4 | Select a world from dropdown | Worlds grouped by region/data center | `GET /api/market/worlds` |
| 5 | Click "Fetch Prices" | Material prices load from Universalis, cost breakdown table shown | `GET /api/recipes/{id}/cost?world=...` |
| 6 | Toggle "Average" / "Cheapest" | Price mode switches between average and min listing price | Re-fetches with `?useMinPrice=true` |
| 7 | Check "Include gathered item prices" | Gathered items show market board opportunity cost | Re-fetches with `?includeGatheredItemPrices=true` |
| 8 | View "Craft vs Buy" comparison | Shows whether crafting or buying from MB is cheaper | — |

---

## B. Authenticated Features

### B1. Registration & Login

**Route:** `/auth/register`, `/auth/login`

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | Navigate to `/auth/register` | Registration form with email, password, display name | — |
| 2 | Submit with valid data | Account created, tokens stored, redirect to profile | `POST /api/auth/register` |
| 3 | Submit with weak password (< 8 chars, no digit/upper) | Validation error shown | — |
| 4 | Navigate to `/auth/login` | Login form | — |
| 5 | Login with valid credentials | Tokens returned, redirect to profile | `POST /api/auth/login` |
| 6 | Login with wrong password | Error message shown | — |
| 7 | Click "Sign in with Discord" | Redirected to Discord OAuth | `GET /api/auth/discord/url` |
| 8 | Authorize on Discord | Redirected to `/auth/discord/callback`, tokens stored | `POST /api/auth/discord/callback` |
| 9 | Wait >1 hour (token expiry) | Auto-refresh via refresh token, seamless | `POST /api/auth/refresh` |
| 10 | Click Logout | Tokens cleared, redirect to home | `POST /api/auth/logout` |

---

### B2. Profile Management

**Route:** `/profile`

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | First visit (no profile) | Profile Setup Wizard appears | `GET /api/profile` -> 404 |
| 2 | Complete wizard (name, job, stats) | Profile created | `POST /api/profile` |
| 3 | View profile dashboard | Character name, stats, job levels, saved content | `GET /api/profile` |
| 4 | Edit job levels | Update specific jobs, others preserved | `PUT /api/profile/jobs` |
| 5 | Edit gear assignments | Update equipped gear by slot | `PUT /api/profile/gear` |
| 6 | View calculated stats | Stats derived from equipped gear | `GET /api/profile/stats` |
| 7 | Click "Import from Lodestone" | Enter character name + server | — |
| 8 | Submit Lodestone import | Stats/gear auto-populated from Lodestone | `POST /api/profile/import-lodestone` |
| 9 | Try import again within 15 minutes | Rate limit error shown | — |
| 10 | Click "Refresh from Lodestone" | Data refreshed | `POST /api/profile/refresh-lodestone` |
| 11 | Preview Lodestone data (anonymous) | Character data shown without saving | `GET /api/profile/lodestone-preview` |
| 12 | Delete profile | Profile removed, wizard appears again | `DELETE /api/profile` |

---

### B3. Saved Lists & Favorites

**Route:** `/profile` -> `/profile/lists/[id]`

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | On recipe detail, click heart icon | Recipe added to favorites | `POST /api/favorites/recipes/{recipeId}` |
| 2 | Click heart again | Unfavorited (toggle) | `POST /api/favorites/recipes/{recipeId}` |
| 3 | Check favorite status | Returns whether recipe is favorited | `GET /api/favorites/recipes/{recipeId}/status` |
| 4 | View all favorites | List of favorited recipes | `GET /api/favorites/recipes` |
| 5 | On recipe detail, click "Save Shopping List" | Named list created | `POST /api/saved-lists/shopping-list` |
| 6 | On gear page, click "Save Gear Set" | Gear set saved | `POST /api/saved-lists/gear-set` |
| 7 | Save a crafting plan | Plan saved as list | `POST /api/saved-lists/crafting-plan` |
| 8 | Navigate to profile -> Saved Content | All lists shown with tabs by type | `GET /api/saved-lists` |
| 9 | Click a list | Detail page with items | `GET /api/saved-lists/{id}` |
| 10 | Add item to list | Item added | `POST /api/saved-lists/{id}/items` |
| 11 | Remove item from list | Item removed | `DELETE /api/saved-lists/{id}/items/{itemId}` |
| 12 | Update a list (rename, pin) | List updated | `PUT /api/saved-lists/{id}` |
| 13 | Delete a list | List removed | `DELETE /api/saved-lists/{id}` |

---

### B4. Gear Comparison (Authenticated)

**Route:** `/gear` (when logged in with profile)

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | Log in with profile that has current gear | UpgradeSummaryBanner appears on gear page | — |
| 2 | View gear recommendations | Comparison column shows current vs. recommended | `GET /api/gear/recommendations/compare?...` |
| 3 | Click "Apply to Profile" on a slot | Profile gear updated | `PUT /api/profile/gear` |
| 4 | View stats summary | Before/after stat totals shown | — |

---

### B5. Rotation Sharing (Authenticated)

**Route:** `/simulator/library`

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | Complete a solve in simulator | "Share to Library" button available | — |
| 2 | Click "Share to Library" | Rotation published | `POST /api/rotations` |
| 3 | View in library | Your rotation appears | `GET /api/rotations` |
| 4 | Upvote another rotation | Vote toggled | `POST /api/rotations/{id}/upvote` |
| 5 | Delete your own rotation | Removed from library | `DELETE /api/rotations/{id}` |

---

### B6. Craft Plans (Authenticated CRUD)

**Route:** `/recipes/plan`

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | Create a saved plan | Plan persisted to account | `POST /api/craft-plans` |
| 2 | View all saved plans | List of plans | `GET /api/craft-plans` |
| 3 | View a specific plan | Plan details (owner only) | `GET /api/craft-plans/{id}` |
| 4 | View resolved plan (public) | Resolved materials | `GET /api/craft-plans/{id}/resolved` |
| 5 | Update a plan | Plan modified | `PUT /api/craft-plans/{id}` |
| 6 | Get cost estimate | Market prices for plan | `POST /api/craft-plans/{id}/cost` |
| 7 | Delete a plan | Plan removed | `DELETE /api/craft-plans/{id}` |

---

### B7. Notifications (Authenticated)

**Route:** Accessed via notification bell in header

| # | Test Step | Expected Result | API Call |
|---|-----------|-----------------|----------|
| 1 | View notifications | List of user notifications | `GET /api/notifications` |
| 2 | Dismiss a notification | Single notification marked as read | `POST /api/notifications/{id}/read` |
| 3 | Dismiss all | All notifications cleared | `POST /api/notifications/read-all` |

---

## C. Mobile & Responsive Testing

### C1. Breakpoints to Test

| Breakpoint | Width | Devices |
|------------|-------|---------|
| Mobile | < 640px | iPhone SE, iPhone 14, Pixel 7 |
| Tablet | 640px-1024px | iPad Mini, iPad Air |
| Desktop | > 1024px | Laptop, Desktop |

### C2. Mobile-Specific Behaviors

| # | Feature | Mobile Behavior | Where to Test |
|---|---------|-----------------|---------------|
| 1 | **Navigation** | Hamburger menu / bottom tabs | Header component |
| 2 | **Simulator step history** | Horizontal strip (`StepHistoryMobile`) instead of vertical sidebar | `/simulator` on mobile |
| 3 | **Gear slot expansion** | Bottom sheet instead of inline expand | `/gear` on mobile |
| 4 | **Action buttons** | Compact grid with smaller buttons | `/simulator` on mobile |
| 5 | **Recipe cards** | Single column layout | `/recipes` on mobile |
| 6 | **Gear grid** | Stacked slots instead of 2-3 column grid | `/gear` on mobile |
| 7 | **Touch targets** | All interactive elements >= 44x44px | All pages |
| 8 | **Tables/data** | Horizontal scroll or card layout | Shopping lists, material trees |

### C3. Mobile Testing Checklist

- [ ] Test with Chrome DevTools device emulation (iPhone SE, iPhone 14 Pro, Pixel 7, iPad)
- [ ] Verify no horizontal overflow on any page
- [ ] Verify all modals/dialogs are scrollable and dismissable
- [ ] Test keyboard doesn't cover input fields
- [ ] Test landscape orientation on all pages
- [ ] Verify swipe gestures don't conflict with browser gestures

---

## D. PWA Testing

### D1. Service Worker & Caching

The service worker (`frontend/src/sw.ts`) is **only active in production builds**. To test PWA locally:

```bash
cd frontend
npm run build            # Must use --webpack (already in build script)
npm run start            # Serves production build on :3000
```

### D2. Caching Strategy Tests

| # | Test | Steps | Expected |
|---|------|-------|----------|
| 1 | **Game data caching** | Visit `/recipes`, search for items. Go offline. Search again. | Cached results return from `game-data-api` cache (StaleWhileRevalidate, 7-day TTL) |
| 2 | **Icon caching** | View recipes with icons. Go offline. Revisit. | Icons load from `xivapi-icons` cache (CacheFirst, 30-day TTL) |
| 3 | **Market data** | View market prices. Go offline within 15 min. Revisit. | Cached prices shown (NetworkFirst, 15-min TTL, 5s timeout) |
| 4 | **Solver offline** | Open a recipe in the simulator online. Go offline. Reload and run Auto-Solve. | Solves locally from the precached WASM engine; no network request (`npm run test:e2e` automates this) |
| 5 | **Simulator offline** | Same as above, then Start Manual Craft and execute actions | Every step computes locally; the recipe and its crafting parameters come from the `game-data-api` cache |
| 6 | **API engine mode** | Add `?engine=api` to any URL (or set `localStorage.ffxiv_engine = "api"`), then solve | Solver and simulate requests reach the API again (NetworkOnly, never cached); `?engine=wasm` switches back |

### D3. Offline Behavior

| # | Test | Expected |
|---|------|----------|
| 1 | Go offline and navigate to uncached page | Redirected to `/offline` page |
| 2 | `/offline` page content | Shows two cards: "Available Offline" (cached recipes, items, gear, simulator, solver for cached recipes) and "Requires Internet" (market, new searches, account sync) |
| 4 | Previously visited page offline | A page opened online (e.g. the simulator for a recipe) opens again offline from the `pages` cache instead of `/offline` |
| 3 | Come back online | Can navigate away from offline page normally |

### D4. Install Prompt (PWA Add to Home Screen)

| # | Test | Expected |
|---|------|----------|
| 1 | Visit site in Chrome on mobile | Install banner appears (via `useInstallPrompt` hook) |
| 2 | Click "Install" | App installs to home screen |
| 3 | Open installed app | Standalone display (no browser chrome), theme color `#e6b422`, bg `#1a1a2e` |
| 4 | Check manifest | Name: "FFXIV Crafting Companion", Short: "FFXIV Craft" |
| 5 | Check icons | 192x192 and 512x512 icons load correctly |

### D5. PWA Lighthouse Audit

```bash
# In Chrome DevTools:
# 1. Open Lighthouse tab
# 2. Select "Progressive Web App" category
# 3. Run audit on production build
# Expected: PWA installable, works offline, fast loading
```

---

## E. Cross-Feature Navigation Tests

These flows test links between different features:

| # | Start | Action | Destination |
|---|-------|--------|-------------|
| 1 | Recipe Detail | Click ingredient that has a recipe | Recipe detail for that sub-recipe |
| 2 | Recipe Detail | Click "Simulate this recipe" | Simulator pre-filled with recipe |
| 3 | Recipe Detail | Click "View Gear for this craft" | Gear page filtered to relevant job |
| 4 | Gear Recommendation | Click crafted equipment source | Recipe detail for that equipment |
| 5 | Simulator | Click recipe name | Recipe detail page |
| 6 | Profile Dashboard | Click a saved shopping list | Saved list detail page |
| 7 | Gear Comparison | Click "Apply" on recommended gear | Profile updated, stats recalculated |

---

## F. Backend Unit Tests

### Running Tests

```bash
dotnet test FFXIVCraftingCompanion.slnx --configuration Release
```

### Test Organization

| Directory | What It Tests | Key Files |
|-----------|---------------|-----------|
| `CraftingEngine/Actions/` | All 36 crafting actions individually | Progress, Quality, Buffs, Repair, Registry tests |
| `CraftingEngine/Solver/` | Greedy solver, MCTS solver, time budgets | `SolverJobServiceTests`, `GreedySolverTuningTests`, `MctsSolverEarlyTerminationTests`, `AdaptiveTimeBudgetTests` |
| `CraftingEngine/` | Simulator, state, conditions, formulas, macros, HQ calc | `CraftingSimulatorTests`, `HQCalculatorTests` |
| `Gear/` | Gear scoring, stat weights, ring dedup, combat gear, batch loading | `GearRecommendationServiceTests`, `CombatGearRecommendationTests`, `GearBatchLoadingTests` |
| `CraftPlans/` | Multi-recipe planning | — |
| `DataSync/` | Data synchronization | — |
| `Gathering/` | Gathering guide generation | — |
| `Journey/` | Crafting journey | — |
| `Market/` | Market board integration | — |
| `Notifications/` | Notification logic | — |
| `Profile/` | Profile CRUD, Lodestone import | — |
| `SavedLists/` | Saved list operations | — |

### Test Framework Details

- **Framework:** xUnit 2.9.3
- **Mocking:** NSubstitute 5.3.0
- **Assertions:** FluentAssertions 8.2.0 (use `BeGreaterThanOrEqualTo`, not `BeGreaterOrEqualTo`)
- **Project:** `tests/FFXIV.Tests/FFXIV.Tests.csproj`

---

## G. Frontend Validation (No Test Framework Yet)

The frontend currently has **no automated test suite** (no Jest, Vitest, Playwright, or Cypress). Current validation is limited to:

```bash
cd frontend
npm run lint          # ESLint (flat config)
npx tsc --noEmit      # TypeScript type checking
npm run build         # Full production build (catches runtime issues)
```

### Recommended Future Testing Stack

If you want to add frontend tests:

- **Unit/Component:** Vitest + React Testing Library
- **E2E:** Playwright (cross-browser, mobile emulation, PWA testing)
- **Visual Regression:** Playwright screenshots or Chromatic

---

## H. CI/CD Pipeline Tests

### GitHub Actions Workflows

| Workflow | File | Trigger | What It Does |
|----------|------|---------|--------------|
| **Backend CI** | `.github/workflows/backend-ci.yml` | Push/PR on `src/**`, `tests/**`, `*.slnx` | Restore -> Build -> Test |
| **Frontend CI** | `.github/workflows/frontend-ci.yml` | Push/PR on `frontend/**` | Install -> Lint -> Type Check -> Build |
| **Backend Deploy** | `.github/workflows/backend-deploy.yml` | Push to main on `src/**`, `Dockerfile` | Test -> Docker build -> Push to ghcr.io -> Deploy to Azure Container Apps |
| **Infra Deploy** | `.github/workflows/infra-deploy.yml` | Manual dispatch | Create resource group -> Bicep deployment to Azure |

---

## I. API Rate Limits to Test

Every policy is partitioned per client: the signed-in user, otherwise the client address forwarded by the ingress. Budgets live in the `RateLimiting` configuration section; 429 responses carry a `Retry-After` header.

| Endpoint | Limit | Window | Policy |
|----------|-------|--------|--------|
| `POST /api/auth/login`, `POST /api/auth/discord/callback` | 20 | 5 minutes | `auth` (sliding window) |
| `POST /api/auth/register` | 10 | 1 hour | `register` |
| `POST /api/auth/refresh`, `GET /api/auth/discord/url` | 120 | 1 minute | `public-read` (sliding window) |
| `POST /api/feedback` | 5 | 1 hour | `feedback` |
| Lodestone import/refresh | 15-min cooldown | Per user | Application-level (LastLodestoneSync field) |

Test by exceeding limits and verifying 429 Too Many Requests responses with `{ "error": "Rate limit exceeded. Try again later." }`.

---

## J. Error Handling Tests

| # | Scenario | Expected |
|---|----------|----------|
| 1 | Backend down, frontend loaded | API calls fail gracefully, error states shown (ErrorState component) |
| 2 | Invalid recipe ID in URL | 404 or error message on recipe detail page |
| 3 | Expired JWT | 401 -> auto-refresh -> retry (transparent to user) |
| 4 | Expired refresh token | Redirect to login page |
| 5 | XIVAPI down | Health check reports unhealthy, data sync fails gracefully |
| 6 | Network timeout | Loading states + timeout errors shown |
| 7 | React error boundary | `error.tsx` / `global-error.tsx` catch and display error UI |

---

## K. Accessibility Testing

| # | Test | Tool/Method |
|---|------|-------------|
| 1 | Keyboard navigation through all pages | Tab through all interactive elements |
| 2 | Screen reader compatibility | Test with NVDA/VoiceOver on key flows |
| 3 | Color contrast | Chrome DevTools Accessibility audit |
| 4 | Focus indicators visible | Tab through forms and buttons |
| 5 | ARIA labels on icon-only buttons | Inspect gear slot cards, action buttons |
| 6 | Reduced motion | Test with `prefers-reduced-motion` media query |

---

# Part 4: Configuration Reference Summary

## All Config Sections at a Glance

| Section | File | Options Class | Secrets? |
|---------|------|---------------|----------|
| `ConnectionStrings:DefaultConnection` | appsettings.*.json | — | Yes (password) |
| `Jwt` | appsettings.json | `JwtOptions` | Yes (`SecretKey`) |
| `Discord` | appsettings.json | `DiscordOptions` | Yes (`ClientId`, `ClientSecret`) |
| `Sentry` | appsettings.json | — | Optional (`Dsn`) |
| `Redis` | appsettings.json | `RedisOptions` | No |
| `XivApi` | appsettings.json | `XivApiOptions` | No |
| `GarlandTools` | appsettings.json | `GarlandToolsOptions` | No |
| `Universalis` | appsettings.json | `UniversalisOptions` | No |
| `DataSync` | appsettings.json | `DataSyncOptions` | No |
| `Cors:AllowedOrigins` | appsettings.json | — | No |

## Frontend Environment Variables

| Variable | Required | Where Set | Purpose |
|----------|----------|-----------|---------|
| `NEXT_PUBLIC_API_URL` | Yes (prod) | `.env.local` / Vercel | Backend API base URL |
| `NEXT_PUBLIC_SENTRY_DSN` | No | `.env.local` / Vercel | Sentry error tracking |

---

# Part 5: Complete API Endpoint Reference

The API serves accounts, the community rotation library and feedback. Game data, simulation, solving, market prices and gathering are served to the browser from the static export, WASM and Universalis directly, so there are no API endpoints for them.

## Auth (`/api/auth`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/register` | No | Register new account |
| POST | `/api/auth/login` | No | Login with email/password |
| POST | `/api/auth/refresh` | No | Refresh access token |
| POST | `/api/auth/logout` | Yes | Logout (revoke refresh token) |
| GET | `/api/auth/me` | Yes | Get current user info |
| GET | `/api/auth/discord/url` | No | Get Discord OAuth URL |
| POST | `/api/auth/discord/callback` | No | Handle Discord OAuth callback |

## Profile (`/api/profile`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/profile` | Yes | Get current user's profile |
| POST | `/api/profile` | Yes | Create profile |
| PUT | `/api/profile` | Yes | Update profile |
| PUT | `/api/profile/jobs` | Yes | Update job levels |
| PUT | `/api/profile/gear` | Yes | Update equipped gear |
| GET | `/api/profile/stats` | Yes | Get calculated stats |
| DELETE | `/api/profile` | Yes | Delete profile |
| POST | `/api/profile/import-lodestone` | Yes | Import from Lodestone |
| POST | `/api/profile/refresh-lodestone` | Yes | Refresh Lodestone data |
| GET | `/api/profile/lodestone-preview` | No | Preview Lodestone character |

## Gear (`/api/gear`)

Anonymous recommendations, profiles and equipment search come from the static game-data export; only the profile comparison stays on the API.

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/gear/recommendations/compare` | Yes | Compare with profile gear |

## Rotations (`/api/rotations`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/rotations` | No | Browse/search rotations |
| GET | `/api/rotations/{id}` | No | Get rotation detail |
| POST | `/api/rotations` | Yes | Share rotation |
| POST | `/api/rotations/{id}/upvote` | Yes | Toggle upvote |
| DELETE | `/api/rotations/{id}` | Yes | Delete rotation (author only) |

## Favorites (`/api/favorites`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/favorites/recipes` | Yes | Get all favorite recipes |
| POST | `/api/favorites/recipes/{recipeId}` | Yes | Toggle favorite |
| GET | `/api/favorites/recipes/{recipeId}/status` | Yes | Check favorite status |

## Saved Lists (`/api/saved-lists`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/saved-lists` | Yes | Get all lists |
| GET | `/api/saved-lists/{id}` | Yes | Get list detail |
| POST | `/api/saved-lists/shopping-list` | Yes | Save shopping list |
| POST | `/api/saved-lists/gear-set` | Yes | Save gear set |
| POST | `/api/saved-lists/crafting-plan` | Yes | Save crafting plan |
| POST | `/api/saved-lists/{id}/items` | Yes | Add item to list |
| DELETE | `/api/saved-lists/{id}/items/{itemId}` | Yes | Remove item from list |
| PUT | `/api/saved-lists/{id}` | Yes | Update list |
| DELETE | `/api/saved-lists/{id}` | Yes | Delete list |

## Craft Plans (`/api/craft-plans`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/craft-plans` | Yes | Create saved plan |
| GET | `/api/craft-plans` | Yes | Get all plans |
| GET | `/api/craft-plans/{id}` | Yes | Get plan (owner only) |
| GET | `/api/craft-plans/{id}/items` | No | Shared plan name and items (the id is the share link) |
| PUT | `/api/craft-plans/{id}` | Yes | Update plan |
| DELETE | `/api/craft-plans/{id}` | Yes | Delete plan |

## Notifications (`/api/notifications`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/notifications` | Yes | Get notifications |
| POST | `/api/notifications/{id}/read` | Yes | Dismiss notification |
| POST | `/api/notifications/read-all` | Yes | Dismiss all |

## Admin (`/api/admin`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/admin/sync` | No* | Trigger data sync |
| GET | `/api/admin/sync/status` | No* | Get sync status |
| POST | `/api/admin/validate-tree/{recipeId}` | No* | Validate recipe tree |
| POST | `/api/admin/validate-batch` | No* | Batch validate trees |

## Feedback (`/api/feedback`)

| Method | Path | Auth | Rate Limit | Description |
|--------|------|------|------------|-------------|
| POST | `/api/feedback` | No | feedback | Submit feedback |

## Health

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/health/live` | No | Liveness probe (no checks run) |
| GET | `/health` | `X-Admin-Key` | Readiness report (DB, XIVAPI, Redis) |
