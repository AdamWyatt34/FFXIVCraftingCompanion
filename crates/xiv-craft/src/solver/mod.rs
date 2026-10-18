//! Rotation solvers mirroring `FFXIV.Infrastructure/Solver`: the greedy heuristic and the MCTS
//! search seeded by it. Both run on wasm32: no threads, and the wall clock is a caller-supplied
//! callback (`now_ms`) so a Web Worker can pass `performance.now()`.

pub mod difficulty;
pub mod greedy;
pub mod mcts;
pub mod rng;

use crate::actions::{Action, ActionId};
use crate::fail_reason::FailReason;
use crate::simulator::SimulationResult;
use crate::state::{CrafterStats, CraftingState, Recipe};

pub use difficulty::{adaptive_time_budget_ms, classify, RecipeDifficulty};
pub use greedy::GreedySolverConfig;
pub use mcts::{MctsConfig, MctsSolver};

/// Milliseconds from an arbitrary origin; only differences are read.
pub type Clock<'a> = &'a mut dyn FnMut() -> f64;

/// Why a search could not start, or why a step of one was refused.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SolverError {
    /// `run`, `progress` or `finish` with no search begun.
    NoSearchInProgress,
    /// The initial state does not fit the tree's packed representation (`mcts::packed`): the
    /// search is refused before it touches the arena instead of aborting part-way through.
    StateDoesNotFit { field: &'static str, value: i32 },
}

impl core::fmt::Display for SolverError {
    fn fmt(&self, f: &mut core::fmt::Formatter<'_>) -> core::fmt::Result {
        match self {
            SolverError::NoSearchInProgress => {
                f.write_str("no search in progress: call begin first")
            }
            SolverError::StateDoesNotFit { field, value } => {
                write!(
                    f,
                    "{field} = {value} is outside the range the solver can represent"
                )
            }
        }
    }
}

impl std::error::Error for SolverError {}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SolverAlgorithm {
    Greedy,
    Mcts,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum SolverStatus {
    Completed,
    Failed,
}

/// The C# `SolverConstraints` fields the solvers read. `MustComplete`, `MaximizeQuality` and
/// `MinimizeSteps` exist there but nothing consumes them, so they are not carried.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SolverConstraints {
    pub target_quality_percent: Option<i32>,
    pub allow_unsafe_actions: bool,
    pub max_steps: usize,
}

impl Default for SolverConstraints {
    fn default() -> SolverConstraints {
        SolverConstraints {
            target_quality_percent: None,
            allow_unsafe_actions: false,
            max_steps: 50,
        }
    }
}

/// `max_iterations` has no C# counterpart: it bounds the MCTS loop without a clock, which is what
/// makes seeded runs reproducible. A run stops at whichever of the two budgets is hit first.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct SolverSettings {
    pub time_budget_ms: f64,
    pub max_iterations: Option<u64>,
    pub random_seed: Option<i32>,
}

impl Default for SolverSettings {
    fn default() -> SolverSettings {
        SolverSettings {
            time_budget_ms: 10_000.0,
            max_iterations: None,
            random_seed: None,
        }
    }
}

#[derive(Clone, Debug, PartialEq, Default)]
pub struct SolverMetrics {
    pub algorithm_used: Option<SolverAlgorithm>,
    pub solve_time_ms: f64,
    pub total_steps: usize,
    pub final_quality_percent: f64,
    pub final_hq_percent: i32,
    pub craft_completed: bool,
    /// Tree nodes allocated (MCTS) or actions chosen (greedy). C# reports the iteration count here.
    pub nodes_explored: u64,
    pub iterations_completed: u64,
}

#[derive(Clone, Debug, PartialEq)]
pub struct SolverResult {
    pub status: SolverStatus,
    pub action_ids: Vec<ActionId>,
    pub simulation: Option<SimulationResult>,
    pub macros: Vec<String>,
    pub metrics: SolverMetrics,
    pub error_message: Option<String>,
}

impl SolverResult {
    fn failed(algorithm: SolverAlgorithm, message: &str, metrics: SolverMetrics) -> SolverResult {
        SolverResult {
            status: SolverStatus::Failed,
            action_ids: Vec::new(),
            simulation: None,
            macros: Vec::new(),
            metrics: SolverMetrics {
                algorithm_used: Some(algorithm),
                ..metrics
            },
            error_message: Some(message.to_string()),
        }
    }
}

/// Set of actions keyed by [`Action::index`]; one bit per action, so membership and removal are
/// single instructions and a node's untried list never touches the heap.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Default)]
pub struct ActionSet(u64);

impl ActionSet {
    pub const EMPTY: ActionSet = ActionSet(0);

    pub fn insert(&mut self, action: Action) {
        self.0 |= 1u64 << action.index();
    }

    pub fn remove(&mut self, action: Action) {
        self.0 &= !(1u64 << action.index());
    }

    pub fn contains(self, action: Action) -> bool {
        self.0 & (1u64 << action.index()) != 0
    }

    pub fn is_empty(self) -> bool {
        self.0 == 0
    }

    pub fn len(self) -> usize {
        self.0.count_ones() as usize
    }

    /// Members in registry order.
    pub fn iter(self) -> impl Iterator<Item = Action> {
        let mut bits = self.0;
        core::iter::from_fn(move || {
            if bits == 0 {
                return None;
            }
            let index = bits.trailing_zeros() as usize;
            bits &= bits - 1;
            Some(Action::from_index(index))
        })
    }

    /// The `n`th member in registry order; `n` must be below `len()`.
    pub fn nth(self, n: usize) -> Action {
        self.iter().nth(n).expect("index within the set")
    }

    /// Every action.
    pub fn all() -> ActionSet {
        ActionSet((1u64 << Action::ALL.len()) - 1)
    }

    /// Actions that can execute now, dropping unreliable ones unless `allow_unsafe_actions`. This
    /// is the C# `GetLegalActions` filter shared by expansion and rollout.
    pub fn legal(
        state: &CraftingState,
        stats: &CrafterStats,
        recipe: &Recipe,
        allow_unsafe_actions: bool,
    ) -> ActionSet {
        ActionSet::legal_from(ActionSet::all(), state, stats, recipe, allow_unsafe_actions)
    }

    /// [`ActionSet::legal`] over `candidates` only; the result is the same when `candidates`
    /// contains every action that could be legal, which is what [`ActionSet::eligible`] gives.
    pub fn legal_from(
        candidates: ActionSet,
        state: &CraftingState,
        stats: &CrafterStats,
        recipe: &Recipe,
        allow_unsafe_actions: bool,
    ) -> ActionSet {
        let mut set = ActionSet::EMPTY;
        for action in candidates.iter() {
            if action.fail_reason(state, stats, recipe).is_some() {
                continue;
            }
            if !allow_unsafe_actions && action.success_rate(state) < 100 {
                continue;
            }
            set.insert(action);
        }
        set
    }

    /// Actions this crafter can ever use on this recipe: everything except those the initial
    /// state rejects for a reason that depends only on the stats or the recipe. Evaluating the
    /// rest per step is what the per-step checks are for; this only trims the loop.
    pub fn eligible(stats: &CrafterStats, recipe: &Recipe) -> ActionSet {
        let initial = CraftingState::create_initial(recipe, stats);
        let mut set = ActionSet::EMPTY;
        for action in Action::ALL {
            let static_reason = matches!(
                action.fail_reason(&initial, stats, recipe),
                Some(
                    FailReason::RequiresLevel(_)
                        | FailReason::RequiresSpecialist
                        | FailReason::NotOnExpert
                        | FailReason::RecipeLevelUnavailable
                        | FailReason::RequiresCrafterLevel(_)
                )
            );
            if !static_reason {
                set.insert(action);
            }
        }
        set
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn action_set_round_trips_in_registry_order() {
        let mut set = ActionSet::EMPTY;
        set.insert(Action::CarefulObservation);
        set.insert(Action::BasicSynthesis);
        set.insert(Action::Innovation);
        assert_eq!(set.len(), 3);
        assert_eq!(
            set.iter().collect::<Vec<_>>(),
            [
                Action::BasicSynthesis,
                Action::Innovation,
                Action::CarefulObservation
            ]
        );
        assert_eq!(set.nth(1), Action::Innovation);
        set.remove(Action::Innovation);
        assert!(!set.contains(Action::Innovation));
        assert!(set.contains(Action::BasicSynthesis));
        assert_eq!(ActionSet::all().len(), Action::ALL.len());
    }

    #[test]
    fn eligible_drops_only_static_exclusions() {
        let recipe = Recipe {
            class_job_level: 50,
            ..Recipe::default()
        };
        let stats = CrafterStats {
            level: 50,
            cp: 0,
            ..CrafterStats::default()
        };
        let eligible = ActionSet::eligible(&stats, &recipe);
        assert!(!eligible.contains(Action::Groundwork), "level 72");
        assert!(!eligible.contains(Action::HeartAndSoul), "specialist");
        assert!(
            !eligible.contains(Action::TrainedEye),
            "crafter level 60 needed"
        );
        assert!(
            eligible.contains(Action::ByregotsBlessing),
            "IQ gate is dynamic"
        );
        assert!(eligible.contains(Action::BasicTouch), "CP gate is dynamic");

        let initial = CraftingState::create_initial(&recipe, &stats);
        let all = ActionSet::legal(&initial, &stats, &recipe, true);
        let trimmed = ActionSet::legal_from(eligible, &initial, &stats, &recipe, true);
        assert_eq!(all, trimmed);
    }
}
