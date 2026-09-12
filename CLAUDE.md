# FFXIV Crafting Companion

Crafting, gathering, gear, and market companion for FFXIV players, with a rotation simulator and solver. Charter (intent, no-go zones, gates, scoping): `.aios/charter.json`.

## Stack and layout

- Backend: .NET 10, Clean Architecture. `src/FFXIV.Domain` (crafting engine, formulas, entities; zero dependencies) -> `src/FFXIV.Application` (MediatR 14 handlers, DTOs) -> `src/FFXIV.Infrastructure` (EF Core SQL Server, Identity + JWT, caching, XIVAPI/Universalis/Lodestone clients, Greedy + MCTS solvers) -> `src/FFXIV.Api` (controllers, Swagger, Serilog, Sentry).
- Frontend: Next.js 16 App Router + React 19 PWA in `frontend/` (Tailwind 4, Recharts, Serwist). Node 22.
- Rust: `crates/xiv-craft`, a dependency-free port of the crafting engine and of the greedy and MCTS solvers (`solver` module) that builds for the host and `wasm32-unknown-unknown`. `cargo run --release --example bench` runs the solver benchmarks against the C# numbers. Parity with the C# engine is proven by the JSONL goldens in `tests/FFXIV.Tests/CraftingEngine/TestData/parity/`, written by `ParityGoldenTests` and replayed by the crate's tests. The C# engine is the reference; port changes, never invent them.
- WASM: `crates/xiv-craft-wasm` wraps the crate with wasm-bindgen using the API's DTO shapes. Its output is **committed** at `frontend/src/wasm/` (neither Vercel nor frontend CI has Rust); `npm run wasm:build` in `frontend/` regenerates it and records the `crates/` git tree hash in `BUILD.json`, and `npm run wasm:check` plus the `WasmArtifactTests` xUnit test fail when the artifact is stale. The frontend runs the simulator and solver from it by default (`src/lib/engine-mode.ts`: `localStorage["ffxiv_engine"]` or `NEXT_PUBLIC_ENGINE_MODE`, `api` falls back to the .NET endpoints); recipe parameters still come from `GET /api/recipes/{id}/crafting-parameters`.
- Deploy: Azure Container Apps + Azure SQL via `infra/` Bicep; frontend on Vercel. See `DEPLOYMENT.md`. End-to-end manual test flows: `TESTING_GUIDE.md`.

## Commands

Build/test/run recipe: `.claude/skills/run/SKILL.md`. Short form:

```powershell
dotnet build FFXIVCraftingCompanion.slnx --configuration Release
dotnet test FFXIVCraftingCompanion.slnx --configuration Release
cd frontend && npm ci && npm run lint && npx tsc --noEmit && npm run build
cd crates/xiv-craft && cargo test && cargo build --target wasm32-unknown-unknown --release
cd crates/xiv-craft-wasm && cargo test
cd frontend && npm run wasm:build && npm run wasm:check   # after any change under crates/; commit src/wasm/
```

Regenerating the parity goldens after an intended C# engine change (the generator test otherwise fails on the first differing byte):

```powershell
$env:FFXIV_PARITY_GENERATE = "1"; dotnet test FFXIVCraftingCompanion.slnx --configuration Release --filter "FullyQualifiedName~Parity"; Remove-Item Env:FFXIV_PARITY_GENERATE
```

## Conventions that differ from global defaults

- This repo uses **MediatR**, not the custom Result-aware mediator. Keep new handlers on MediatR.
- Tests use **xUnit + FluentAssertions + NSubstitute**, not Shouldly/Bogus.
- The crafting engine stays in `FFXIV.Domain/CraftingEngine`. Solver implementations stay in `FFXIV.Infrastructure/Solver` behind `IRotationSolver`. Do not move engine rules into Application or Infrastructure.
- Crafting action ids (`ICraftingAction.Id`) are persisted in saved rotations and macros. Never renumber them; any port must reuse the same ids.
- `Recipe.RecipeLevel` is the recipe level (rlvl), not the class job level. Formulas depend on the distinction (see commit bb6e8ad).

## Do not touch without a human

- `src/FFXIV.Infrastructure/Migrations/` (EF migrations)
- `infra/` and `.github/workflows/` (prod deploy and CI)
- `src/FFXIV.Api/appsettings.Production.json`
- `tests/FFXIV.Tests/CraftingEngine/TestData/solver-benchmarks.json` (hand-maintained solver benchmarks)
- `tests/FFXIV.Tests/CraftingEngine/TestData/parity/` (generator-only output of `ParityGoldenTests`; never hand-edit, regenerate only for an intended engine change)

## Gotchas

- Solver benchmark fixtures load from `AppContext.BaseDirectory/CraftingEngine/TestData/`, so new fixture files need `CopyToOutputDirectory` in `tests/FFXIV.Tests/FFXIV.Tests.csproj`.
- Local dev needs Docker for SQL Server + Redis (`docker compose up -d`); production uses in-memory caching, no Redis.
- First local run needs a data sync (`POST /api/admin/sync`) to populate items and recipes; it takes minutes.
- The root `package.json` (single dependency `claude`) and root `node_modules` are unrelated to the app. Do not add app dependencies there.
- Backend CI runs only when `src/**`, `tests/**`, or `*.slnx` change; frontend CI only on `frontend/**`.
