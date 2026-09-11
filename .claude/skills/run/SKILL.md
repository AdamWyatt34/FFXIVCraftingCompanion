---
name: run
description: Build, test, and run THIS repository using its recorded commands. Use when building, testing, or launching the project so the agent follows the known-good recipe instead of rediscovering it.
---

# Run / Verify — FFXIVCraftingCompanion

Recorded build/test/run recipe for this repo. Follow these commands; don't improvise.

## Build

```
dotnet build FFXIVCraftingCompanion.slnx --configuration Release
cd frontend && npm ci && npx tsc --noEmit && npm run build
```

## Test

```
dotnet test FFXIVCraftingCompanion.slnx --configuration Release
cd frontend && npm run lint
```

## Run

```
docker compose up -d
cd src/FFXIV.Api && dotnet run
cd frontend && npm run dev
```

## Notes

- Run from the repo root; the solution file is `FFXIVCraftingCompanion.slnx` (.NET 10 SDK, see `global.json`). Frontend needs Node 22 (`frontend/.nvmrc`).
- Verify scope: backend changes (`src/`, `tests/`) need the dotnet build + test; frontend changes need lint + typecheck + build. Run only the half the diff touches, both when it touches both.
- Once `crates/` exists: `cargo test` and `cargo build --target wasm32-unknown-unknown --release` from `crates/xiv-craft` are part of verify for changes there.
- API runs on http://localhost:5159 (Swagger at `/swagger`, health at `/health`); frontend on http://localhost:3000. The API needs the docker SQL Server + Redis containers.
- Solver benchmark and MCTS tuning tests are slow and time-budgeted; the full `dotnet test` run can take several minutes.
- Frontend `npm run build` uses `--webpack` (see `frontend/package.json`), not Turbopack.
