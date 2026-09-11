# Data flow — FFXIVCraftingCompanion

Seeded by onboard on 2026-09-11 (static path layer only, `.aios/data-flow.json`, seeded from `.aios/data-flow.config.json`). The telemetry overlay is attended and was not run.

## Operations traced

| Operation | Entry | Resolution | Steps | Unresolved hops |
|---|---|---|---|---|
| GetRecipe | `GET /api/recipes/{id:int}` | class fallback | 7 | 6 |
| SimulateRotation | `POST /api/crafting/simulate` | class fallback | 12 | 11 |
| SolveRotation | `POST /api/crafting/solve` | class fallback | 12 | 11 |
| DataSyncService | worker | worker class | 1 | 0 |

## What the trace shows and what it cannot

The regex-based tracer resolves the controller class but not the specific action method, so each controller operation lists every MediatR request type the controller sends rather than the one the route dispatches. It also cannot follow MediatR's `Send` into the handler, so every handler hop is unresolved and no entity touches are recorded. Read the tables above as "which requests this controller can dispatch", not as a per-route path.

The actual paths, read from source:

- **GetRecipe**: `RecipesController` → `GetRecipeByIdQuery` handler → recipe repository (EF, `Recipe` + `RecipeIngredient` + `RecipeLevelEntry`) → recipe DTO.
- **SimulateRotation**: `CraftingController` → `SimulateCraftCommand` handler → `RecipeParameterFactory` builds `RecipeParameters` from the recipe and rlvl table → `CraftingSimulator.Simulate` in Domain runs the action ids → per-step state DTOs. No database writes.
- **SolveRotation**: `CraftingController` → `StartSolveCommand` → `SolverJobService` queues a job → `SolverFactory` picks Greedy or MCTS (`FFXIV.Infrastructure/Solver`) → result cached via `SolverCacheService`; the client polls `GET /api/crafting/solve/{jobId}`.
- **DataSyncService**: Cronos-scheduled worker pulls XIVAPI rows through `IXivApiClient` and upserts items, recipes, equipment, gathering, and vendor data, recording a `SyncMetadata` row.

## Improving the trace

A handler-aware adapter (following `_mediator.Send(new X(...))` to `IRequestHandler<X,_>`) would resolve the 28 unresolved hops. Until then, the codebase-memory graph (`trace_path`) is the better tool for call chains here.
