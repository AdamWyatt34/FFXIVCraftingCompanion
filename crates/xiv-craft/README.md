# xiv-craft

The FFXIV crafting engine and solvers. Correctness is pinned by Raphael/Teamcraft arithmetic in `tests/reference_parity.rs`, regression snapshots in `tests/goldens/`, and solver benchmarks in `tests/benchmarks/solver-benchmarks.json`. For suspect behaviour, Teamcraft and Raphael agreeing is ground truth; disagreement needs independent evidence before changing a rule.

- No runtime dependencies. Builds for the host and for `wasm32-unknown-unknown` (`rlib`). The bindings live in the sibling crate `xiv-craft-wasm` (wasm-bindgen, API DTO shapes), built into `frontend/src/wasm` by `npm run wasm:build`.
- `CraftingState` and `StepResult` are `Copy`; `execute_action` allocates nothing.
- Action ids are stable because saved rotations and macros persist them. `tests/goldens/actions.json` pins the catalog, including legacy actions.
- Randomness is explicit: `SimulationOptions.random_seed` drives a port of seeded `System.Random`, with its independent frozen .NET output pinned in `tests/goldens/dotnet-random.json`; this fixture has no Rust generator.

## Solvers

`xiv_craft::solver` provides `greedy::solve` (one pass, three phases) and `MctsSolver` (UCB1 search seeded with the greedy rotation). Configuration lives in `GreedySolverConfig` and `MctsConfig`, with a shared result shape (`SolverResult`: action ids, replayed simulation, macros, metrics).

- The wall clock is injected: `solve(..., now_ms: &mut dyn FnMut() -> f64)`. Native callers pass an `Instant`-based closure, the wasm bindings pass `Date.now()`; the library never touches `std::time`, so it runs on wasm32.
- `solve` is `begin` + `run` + `finish`. A caller that needs to yield (a Web Worker reading cancel messages and posting progress) calls `run(max_iterations, now)` in slices and `progress()` / `finish()` when it likes; a sliced run walks the same iterations as the one-shot call, so a seed and an iteration budget give the same rotation either way.
- `SolverSettings.max_iterations` bounds the search without a clock. With a seed and an iteration budget a run is reproducible; with a time budget it is not (the iteration count depends on the machine).
- The tree is two chunked arenas with `u32` links: 48-byte nodes (a 24-byte packed state, untried actions as a `u64` bitset) and 16-byte edges holding the visit statistics, one contiguous slice per expanded node. A `MctsSolver` kept between calls allocates nothing per iteration after the first solve.
- `MctsConfig.early_termination_threshold` counts iterations without improvement. At Rust iteration rates the default (5000) stops a search within ~50 ms; pass `u64::MAX` to spend the whole budget. The browser worker does exactly that and applies its own wall-clock stall rule instead (see `frontend/src/workers/solver.worker.ts`).
- `MctsConfig.max_nodes` caps the tree. A node is 48 bytes plus a 16-byte edge per legal action once it is expanded, about 130 bytes per node in practice; a 10 s search grows to 2-5 M nodes (250-700 MB, see `MctsSolver::tree_memory_bytes`). Past the cap the search keeps running rollouts from existing leaves. Unbounded by default; a worker should set it (1 M nodes is roughly 130 MB).
- Randomness is xoshiro256** seeded from `SolverSettings.random_seed` (a fixed constant when unset).

Run the ten solver benchmarks in `tests/benchmarks/solver-benchmarks.json`:

```powershell
cargo run --release --example bench            # 10 s budget, seed 42
cargo run --release --example bench 2000 7     # 2 s budget, seed 7
cargo run --release --example bench 10000 42 lv100   # only ids containing "lv100"
```

`cargo test` runs the same theories at a 1 s budget.

## Correctness and snapshots

The cited expectations in `tests/reference_parity.rs` are independent of this engine's output. `tests/snapshot.rs` generates and replays the regression cases, and `tests/engine_units.rs` pins HQ material arithmetic and macro output. The solver benchmarks also exercise community rotations and deterministic greedy metrics.

Regenerate snapshots only for an intended engine change, and add a Raphael/Teamcraft-cited reference case for every rule change:

```powershell
$env:XIV_CRAFT_GENERATE = "1"; cargo test --release
Remove-Item Env:XIV_CRAFT_GENERATE
```

Never set `XIV_CRAFT_GENERATE` on CI: generation replaces the expected output and bypasses snapshot comparisons. Without it, tests fail on the first differing line and column or a missing file. Review the regenerated files before committing them.

```powershell
cargo test --release
cargo build --target wasm32-unknown-unknown --release
```
