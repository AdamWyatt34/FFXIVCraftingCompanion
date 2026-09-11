# xiv-craft

Rust port of the crafting engine in `src/FFXIV.Domain/CraftingEngine`. The C# engine is the reference: this crate reproduces its results integer for integer, including behaviours that differ from the in-game rules. Do not fix a suspected engine bug here; fix it in C#, regenerate the goldens, then port the change.

- No runtime dependencies. Builds for the host and for `wasm32-unknown-unknown` (`rlib`, no bindings yet).
- `CraftingState` and `StepResult` are `Copy`; `execute_action` allocates nothing.
- Action ids equal the C# `ICraftingAction.Id` values, so saved rotations and macros are interchangeable.
- Randomness is explicit: `SimulationOptions.random_seed` drives a port of seeded `System.Random`, so seeded C# runs replay exactly.

## Parity

`tests/parity.rs` replays `tests/FFXIV.Tests/CraftingEngine/TestData/parity/*.jsonl` from the .NET test tree (this crate is monorepo-only). The C# side writes those files:

```powershell
$env:FFXIV_PARITY_GENERATE = "1"
dotnet test FFXIVCraftingCompanion.slnx --configuration Release --filter "FullyQualifiedName~Parity"
Remove-Item Env:FFXIV_PARITY_GENERATE
```

Without the variable the same tests fail on the first byte that differs from the committed goldens, so an engine change cannot land without regenerating them.

```powershell
cargo test
cargo build --target wasm32-unknown-unknown --release
```
