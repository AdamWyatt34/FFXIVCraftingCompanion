//! Crafting engine mirroring `FFXIV.Domain/CraftingEngine`. Every rule here is a transcription
//! of the C# engine; parity is proven by the goldens replayed in `tests/parity.rs`.

pub mod actions;
pub mod condition;
pub mod fail_reason;
pub mod formula;
pub mod hq;
pub mod macro_export;
pub mod rng;
pub mod simulator;
pub mod solver;
pub mod state;

pub use actions::{Action, ActionAvailability, ActionCategory, ActionId};
pub use condition::ConditionResolver;
pub use fail_reason::FailReason;
pub use formula::{base_progress, base_quality};
pub use hq::{calculate_starting_quality, HqInput, Ingredient};
pub use macro_export::{export_to_macro, MacroOptions};
pub use rng::DotNetRandom;
pub use simulator::{
    action_availability, create_initial, execute_action, legal_actions, simulate, simulate_with,
    SimulationOptions, SimulationResult, StepResult, DEFAULT_SEED,
};
pub use solver::{
    GreedySolverConfig, MctsConfig, MctsSolver, SolverAlgorithm, SolverConstraints, SolverMetrics,
    SolverResult, SolverSettings, SolverStatus,
};
pub use state::{Condition, CraftStatus, CrafterStats, CraftingState, Recipe};
