# FFXIV Crafting Companion

Crafting, gathering, gear, and market companion for FFXIV players, with a rotation simulator and solver. Charter (intent, no-go zones, gates, scoping): `.aios/charter.json`.

## Stack and layout

- Backend: .NET 10, Clean Architecture. `src/FFXIV.Domain` (crafting engine, formulas, entities; zero dependencies) -> `src/FFXIV.Application` (MediatR 14 handlers, DTOs) -> `src/FFXIV.Infrastructure` (EF Core SQL Server, Identity + JWT, caching, XIVAPI/Universalis/Lodestone clients, Greedy + MCTS solvers) -> `src/FFXIV.Api` (controllers, Swagger, Serilog, Sentry).
- Frontend: Next.js 16 App Router + React 19 PWA in `frontend/` (Tailwind 4, Recharts, Serwist). Node 22.
- Planned: `crates/xiv-craft`, a Rust port of the crafting engine targeting wasm32, with parity against the C# engine proven by committed JSONL golden files.
- Deploy: Azure Container Apps + Azure SQL via `infra/` Bicep; frontend on Vercel. See `DEPLOYMENT.md`. End-to-end manual test flows: `TESTING_GUIDE.md`.

## Commands

Build/test/run recipe: `.claude/skills/run/SKILL.md`. Short form:

```powershell
dotnet build FFXIVCraftingCompanion.slnx --configuration Release
dotnet test FFXIVCraftingCompanion.slnx --configuration Release
cd frontend && npm ci && npm run lint && npx tsc --noEmit && npm run build
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
- `tests/FFXIV.Tests/CraftingEngine/TestData/` (solver benchmarks and parity golden files; regenerate only through the designated generator test with approval)

## Gotchas

- Solver benchmark fixtures load from `AppContext.BaseDirectory/CraftingEngine/TestData/`, so new fixture files need `CopyToOutputDirectory` in `tests/FFXIV.Tests/FFXIV.Tests.csproj`.
- Local dev needs Docker for SQL Server + Redis (`docker compose up -d`); production uses in-memory caching, no Redis.
- First local run needs a data sync (`POST /api/admin/sync`) to populate items and recipes; it takes minutes.
- The root `package.json` (single dependency `claude`) and root `node_modules` are unrelated to the app. Do not add app dependencies there.
- Backend CI runs only when `src/**`, `tests/**`, or `*.slnx` change; frontend CI only on `frontend/**`.
