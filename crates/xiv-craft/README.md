# xiv-craft

Rust port of the crafting engine in `src/FFXIV.Domain/CraftingEngine`. The C# engine is the reference: this crate reproduces its results integer for integer, including behaviours that differ from the in-game rules. Do not fix a suspected engine bug here; fix it in C#, regenerate the goldens, then port the change.

- No runtime dependencies. Builds for the host and for `wasm32-unknown-unknown` (`rlib`, no bindings yet).
- `CraftingState` and `StepResult` are `Copy`; `execute_action` allocates nothing.
- Action ids equal the C# `ICraftingAction.Id` values, so saved rotations and macros are interchangeable.
- Randomness is explicit: `SimulationOptions.random_seed` drives a port of seeded `System.Random`, so seeded C# runs replay exactly.

## Solvers

`xiv_craft::solver` ports `FFXIV.Infrastructure/Solver`: `greedy::solve` (one pass, three phases) and `MctsSolver` (UCB1 search seeded with the greedy rotation). Same knobs as the C# `GreedySolverConfig` and `MCTSConfig`, same result shape (`SolverResult`: action ids, replayed simulation, macros, metrics).

- The wall clock is injected: `solve(..., now_ms: &mut dyn FnMut() -> f64)`. Native callers pass an `Instant`-based closure, a Web Worker passes `performance.now()`; the library never touches `std::time`, so it runs on wasm32.
- `SolverSettings.max_iterations` bounds the search without a clock. With a seed and an iteration budget a run is reproducible; with a time budget it is not (the iteration count depends on the machine).
- The tree is two chunked arenas with `u32` links: 48-byte nodes (a 24-byte packed state, untried actions as a `u64` bitset) and 16-byte edges holding the visit statistics, one contiguous slice per expanded node. A `MctsSolver` kept between calls allocates nothing per iteration after the first solve.
- `MctsConfig.early_termination_threshold` counts iterations without improvement, as in C#. At Rust iteration rates the default (5000) stops a search within ~50 ms; pass `u64::MAX` to spend the whole budget.
- `MctsConfig.max_nodes` caps the tree. A node is 48 bytes plus a 16-byte edge per legal action once it is expanded, about 130 bytes per node in practice; a 10 s search grows to 2-5 M nodes (250-700 MB, see `MctsSolver::tree_memory_bytes`). Past the cap the search keeps running rollouts from existing leaves. Unbounded by default, as in C#; a worker should set it (1 M nodes is roughly 130 MB).
- Randomness is xoshiro256** seeded from `SolverSettings.random_seed` (a fixed constant when unset); C# MCTS runs are not replayable across languages, only the engine is.

Benchmark over the ten recipes in `tests/FFXIV.Tests/CraftingEngine/TestData/solver-benchmarks.json`, with the C# numbers alongside:

```powershell
cargo run --release --example bench            # 10 s budget, seed 42
cargo run --release --example bench 2000 7     # 2 s budget, seed 7
cargo run --release --example bench 10000 42 lv100   # only ids containing "lv100"
```

`cargo test` runs the same theories at a 1 s budget.

## Parity

`tests/parity.rs` replays `tests/FFXIV.Tests/CraftingEngine/TestData/parity/*.jsonl` from the .NET test tree (this crate is monorepo-only). The C# side writes those files:

```powershell
$env:FFXIV_PARITY_GENERATE = "1"
dotnet test FFXIVCraftingCompanion.slnx --configuration Release --filter "FullyQualifiedName~Parity"
Remove-Item Env:FFXIV_PARITY_GENERATE
```

Without the variable the same tests fail on the first byte that differs from the committed goldens, so an engine change cannot land without regenerating them. Set the variable only in the shell that regenerates; a CI runner must never have it set, or every parity assertion passes vacuously.

```powershell
cargo test
cargo build --target wasm32-unknown-unknown --release
```
