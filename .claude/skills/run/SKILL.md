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
cd frontend && npm run lint && npm test
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
- `cargo test` and `cargo build --target wasm32-unknown-unknown --release` from `crates/xiv-craft`, and `cargo test` from `crates/xiv-craft-wasm`, are part of verify for changes under `crates/`. Any such change also needs `npm run wasm:build` in `frontend/` (rebuilds the committed `frontend/src/wasm/` artifact through a pinned `npx wasm-pack`) and `npm run wasm:check`; rust-ci's artifact check fails when the artifact is stale.
- API runs on http://localhost:5159 (Swagger at `/swagger`, health at `/health`); frontend on http://localhost:3000. The API needs the docker SQL Server + Redis containers.
- Engine snapshots in `crates/xiv-craft/tests/goldens/` regenerate only for an intended engine change: set `$env:XIV_CRAFT_GENERATE = "1"`, run `cargo test --release` in the crate, then `Remove-Item Env:XIV_CRAFT_GENERATE` and replay. Never set it on CI. Every rule change needs a Raphael/Teamcraft-cited case in `tests/reference_parity.rs`; `dotnet-random.json` is frozen .NET output.
- Static data export: `dotnet run --project src/FFXIV.Export --configuration Release -- --out ./out --skip-sync` (drop `--skip-sync` to sync XIVAPI first, about six minutes). The static-data goldens regenerate with `FFXIV_PARITY_GENERATE=1 dotnet test ... --filter "FullyQualifiedName~StaticDataGolden"`.
- Frontend builds run `scripts/data-fetch.mjs` first (`prebuild`): set `FFXIV_DATA_DIR=e2e/fixtures/data` for a quick local build, or a full export directory for all recipe pages; `npm run dev` needs `npm run data:fetch` once.
- Frontend `npm run build` uses `--webpack` (see `frontend/package.json`), not Turbopack.
