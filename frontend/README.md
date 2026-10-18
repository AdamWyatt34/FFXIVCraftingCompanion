# Frontend

The Next.js (App Router) and React PWA for [ffxivcrafting.app](https://ffxivcrafting.app). The
repository's [README](../README.md) describes the whole system; this file is the frontend's own
notes.

## Game data

Pages read game data from the static export, never from the API. `npm run data:fetch` (also the
`prebuild` step) places the data set under `public/data/v/<version>/`, from the commit pinned in
`data.lock.json` (a tarball of the `data` branch) or from a local export in `FFXIV_DATA_DIR`.
Without a data set the build has no recipe pages and no search. `src/lib/static-data/` holds the
client, the search port and the DTO rebuilds; `src/lib/game-data.ts` keeps the API signatures
over it, and `src/lib/api-client.ts` is the account API.

## Engine

The simulator, the solver and the signed-in stat check run from `src/wasm/`, a committed
WebAssembly build of `crates/xiv-craft-wasm`. After any change under `crates/`, run
`npm run wasm:build` here and commit `src/wasm/`; `npm run wasm:check` and the backend's
`WasmArtifactTests` fail when it is stale.

## Commands

```bash
npm ci
npm run data:fetch
npm run dev            # http://localhost:3000
npm run lint && npx tsc --noEmit
npm test               # vitest, replays the C# goldens
npm run build          # production build (runs data:fetch first)
npm run test:e2e       # production build on the fixture data set + Playwright
npm run analyze        # bundle analyzer report
```

`NEXT_PUBLIC_API_URL` points account features at the API (unset: `http://localhost:5159`).
`NEXT_PUBLIC_SITE_URL` is the canonical origin and is required for a production build.
`NEXT_PUBLIC_SENTRY_DSN` turns on error reporting.
