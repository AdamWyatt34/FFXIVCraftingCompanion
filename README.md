# FFXIV Crafting Companion

Recipes, material trees, shopping lists, gear recommendations and a crafting simulator with a
rotation solver for Final Fantasy XIV crafters. Live at **[ffxivcrafting.app](https://ffxivcrafting.app)**.

Everything a visitor sees works without an account and without a server round trip: the game
data is a static export served with the site, and the simulator and solver run in the browser
from a WebAssembly build of the crafting engine. Signing in adds character profiles (with
Lodestone import), favourites, saved shopping lists, craft plans and the community rotation
library, which an ASP.NET Core API backs.

## Features

- **Recipes**: every recipe by job and level, with its ingredient tree, a shopping list grouped
  by source, the crafting order for intermediate crafts, and a craft plan that aggregates
  several recipes.
- **Simulator**: step through a craft action by action with the real formulas, buffs and
  conditions, replay a rotation, and export it as in-game macros.
- **Solver**: a greedy solver for quick answers and a Monte Carlo tree search for the best
  rotation your stats allow, both running in a Web Worker.
- **Gear**: best-in-slot recommendations for every crafter and gatherer job at any level,
  weighted for craftsmanship, control and CP, with the recipes to make the gear.
- **Accounts**: profiles with job levels and current gear, Lodestone import, favourites, saved
  lists, craft plans, shared rotations with votes, and notifications.

## How it is built

| Part | Where | What |
|---|---|---|
| Crafting engine | `crates/xiv-craft` | The reference implementation of formulas, actions, buffs, conditions and both solvers; zero runtime dependencies. |
| API | `src/FFXIV.Application`, `src/FFXIV.Infrastructure`, `src/FFXIV.Api` | Clean Architecture over MediatR: accounts, profiles, lists, rotations and feedback on EF Core (SQL Server) with Identity and JWT. |
| Static game data | `src/FFXIV.Export` | Syncs [XIVAPI](https://v2.xivapi.com) into SQL Server and writes the JSON the frontend serves (recipes, items, gear, a search index). A weekly workflow publishes it to the `data` branch and pins the commit in `frontend/data.lock.json`. |
| Rust engine | `crates/xiv-craft`, `crates/xiv-craft-wasm` | Correctness pinned by Raphael/Teamcraft reference tests, snapshot goldens and solver benchmarks; built to WebAssembly and committed at `frontend/src/wasm`. |
| Frontend | `frontend` | Next.js (App Router) and React, a PWA with an offline service worker, deployed on Vercel. Recipe pages are rendered on the server from the static export. |
| Infrastructure | `infra` | Bicep for Azure Container Apps and Azure SQL. See [DEPLOYMENT.md](DEPLOYMENT.md). |

Prices come from [Universalis](https://universalis.app) directly from the browser. Signed-out
pages never call the API.

## Running it locally

Prerequisites: the [.NET 10 SDK](https://dotnet.microsoft.com/download), Node.js 22, Docker (for
SQL Server and Redis) and, only for changes under `crates/`, a Rust toolchain
(`crates/rust-toolchain.toml` names the version).

```bash
# Backend: SQL Server and Redis, then the API on http://localhost:5159
docker compose up -d
dotnet run --project src/FFXIV.Api

# Game data: the first run needs a sync (minutes). The admin endpoints require the
# X-Admin-Key header to match Admin:ApiKey in the API's configuration.
curl -X POST http://localhost:5159/api/admin/sync -H "X-Admin-Key: <your key>" -H "Content-Length: 0"

# Frontend on http://localhost:3000, with the published data set placed under public/data
cd frontend
npm ci
npm run data:fetch      # from frontend/data.lock.json, or set FFXIV_DATA_DIR to a local export
npm run dev
```

The frontend reads game data from the export, not from the API, so it runs without the backend;
the API is only needed for accounts. `frontend/.env.local` with `NEXT_PUBLIC_API_URL=http://localhost:5159`
points the account features at the local API.

## Tests and checks

```bash
dotnet build FFXIVCraftingCompanion.slnx --configuration Release
dotnet test FFXIVCraftingCompanion.slnx --configuration Release

cd frontend && npm run lint && npx tsc --noEmit && npm test && npm run build && npm run test:e2e

cd crates/xiv-craft && cargo test --release && cargo run --release --example bench
cd crates/xiv-craft-wasm && cargo test
cd frontend && npm run wasm:build && npm run wasm:check   # after any change under crates/; commit src/wasm/
```

`crates/xiv-craft` is the engine reference. Its `tests/reference_parity.rs` pins Raphael/Teamcraft
arithmetic; `tests/goldens/` snapshots and solver benchmarks pin regressions. Regenerate snapshots
only for an intended engine change with `XIV_CRAFT_GENERATE=1`, then replay with it unset; never set
it on CI. Every rule change needs a Raphael/Teamcraft-cited reference case. The frozen
`dotnet-random.json` fixture is not regenerated.

The static-data computations stay C#-generated: `tests/FFXIV.Tests/StaticData/TestData/goldens/`
is written by `StaticDataGoldenTests` with `FFXIV_PARITY_GENERATE=1`. Never hand-edit either set
of goldens. See `CLAUDE.md` for regeneration commands.

[TESTING_GUIDE.md](TESTING_GUIDE.md) walks through the manual end-to-end flows.

## Contributing

Issues and pull requests are welcome. CI runs the backend, frontend and Rust suites on every pull
request. A few paths are deliberately hands-off for contributors: EF Core migrations, the
`infra/` templates, the GitHub workflows and the generated golden files; open an issue first if a
change needs them. `CLAUDE.md` describes the layout and conventions in more detail.

Security issues: see [SECURITY.md](SECURITY.md).

## Licence and attribution

[MIT](LICENSE). FINAL FANTASY is a registered trademark of Square Enix Holdings Co., Ltd.
FINAL FANTASY XIV © SQUARE ENIX CO., LTD. All Rights Reserved. This app is not affiliated with
or endorsed by Square Enix. Game data is read from XIVAPI, prices from Universalis, and
character data from the Lodestone.
