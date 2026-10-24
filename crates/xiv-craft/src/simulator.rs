//! `CraftingSimulator`. `execute_action` is the public single-step call the solvers use (always
//! succeeds, never rolls a condition); `simulate_with` is the full run with options.

use crate::actions::{Action, ActionAvailability, ActionId};
use crate::condition::ConditionResolver;
use crate::fail_reason::FailReason;
use crate::rng::DotNetRandom;
use crate::state::{Condition, CraftStatus, CrafterStats, CraftingState, Recipe};

/// Seed used when a run needs randomness but none was supplied. C# seeds from the clock there,
/// which is neither reproducible nor available on wasm, so the engine pins a constant instead.
pub const DEFAULT_SEED: i32 = 0;

/// The defaults (every action succeeds, the condition stays Normal) are the path both solvers
/// run on.
///
/// With `assume_all_success` or `assume_normal_condition` off and `random_seed` unset, every run
/// uses [`DEFAULT_SEED`] and is therefore identical call after call. A caller that wants varied
/// rollouts (a solver sampling outcomes) must supply its own seed each time.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SimulationOptions {
    pub assume_all_success: bool,
    pub assume_normal_condition: bool,
    pub random_seed: Option<i32>,
    pub force_condition: Option<Condition>,
}

impl Default for SimulationOptions {
    fn default() -> SimulationOptions {
        SimulationOptions {
            assume_all_success: true,
            assume_normal_condition: true,
            random_seed: None,
            force_condition: None,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct StepResult {
    pub step_number: i32,
    pub action_id: ActionId,
    /// `None` when the id is not a known action.
    pub action: Option<Action>,
    pub state_before: CraftingState,
    pub state_after: CraftingState,
    pub succeeded: bool,
    pub fail_reason: Option<FailReason>,
}

impl StepResult {
    /// The C# `ActionName`: the in-game name, or `Unknown Action (<id>)`.
    pub fn action_name(&self) -> String {
        match self.action {
            Some(action) => action.in_game_name().to_string(),
            None => format!("Unknown Action ({})", self.action_id),
        }
    }

    pub fn progress_delta(&self) -> i32 {
        self.state_after.progress - self.state_before.progress
    }

    pub fn quality_delta(&self) -> i32 {
        self.state_after.quality - self.state_before.quality
    }

    pub fn durability_delta(&self) -> i32 {
        self.state_after.durability - self.state_before.durability
    }

    pub fn cp_delta(&self) -> i32 {
        self.state_after.cp - self.state_before.cp
    }

    fn unchanged(
        state: CraftingState,
        action_id: ActionId,
        action: Option<Action>,
        reason: FailReason,
    ) -> StepResult {
        StepResult {
            step_number: state.step + 1,
            action_id,
            action,
            state_before: state,
            state_after: state,
            succeeded: false,
            fail_reason: Some(reason),
        }
    }
}

#[derive(Clone, Debug, PartialEq)]
pub struct SimulationResult {
    pub steps: Vec<StepResult>,
    pub final_state: CraftingState,
}

impl SimulationResult {
    pub fn success(&self) -> bool {
        self.final_state.is_complete()
    }

    pub fn total_steps(&self) -> usize {
        self.steps.len()
    }

    pub fn final_quality_percent(&self) -> f64 {
        self.final_state.quality_percent()
    }

    pub fn final_hq_percent(&self) -> i32 {
        self.final_state.hq_percent()
    }
}

pub fn create_initial(recipe: &Recipe, stats: &CrafterStats) -> CraftingState {
    CraftingState::create_initial(recipe, stats)
}

pub fn execute_action(
    state: CraftingState,
    action_id: ActionId,
    stats: &CrafterStats,
    recipe: &Recipe,
) -> StepResult {
    let Some(action) = Action::from_id(action_id) else {
        return StepResult::unchanged(state, action_id, None, FailReason::UnknownAction);
    };
    if let Some(reason) = action.fail_reason(&state, stats, recipe) {
        return StepResult::unchanged(state, action_id, Some(action), reason);
    }

    StepResult {
        step_number: state.step + 1,
        action_id,
        action: Some(action),
        state_before: state,
        state_after: action.execute(state, stats, recipe, true),
        succeeded: true,
        fail_reason: None,
    }
}

pub fn simulate(
    recipe: &Recipe,
    stats: &CrafterStats,
    action_ids: &[ActionId],
) -> SimulationResult {
    simulate_with(recipe, stats, action_ids, &SimulationOptions::default())
}

pub fn simulate_with(
    recipe: &Recipe,
    stats: &CrafterStats,
    action_ids: &[ActionId],
    options: &SimulationOptions,
) -> SimulationResult {
    let mut state = create_initial(recipe, stats);
    let mut steps = Vec::with_capacity(action_ids.len());
    // Two streams, as in C#: conditions from the seed, success rolls from seed + 1.
    let seed = options.random_seed.unwrap_or(DEFAULT_SEED);
    let mut conditions = ConditionResolver::new(seed);
    let mut success_rolls = DotNetRandom::new(seed.wrapping_add(1));

    for &action_id in action_ids {
        if state.status != CraftStatus::InProgress {
            break;
        }

        let Some(action) = Action::from_id(action_id) else {
            steps.push(StepResult::unchanged(
                state,
                action_id,
                None,
                FailReason::UnknownAction,
            ));
            continue;
        };

        let step = execute_internal(
            state,
            action,
            stats,
            recipe,
            options,
            &mut conditions,
            &mut success_rolls,
        );
        state = step.state_after;
        steps.push(step);
    }

    SimulationResult {
        steps,
        final_state: state,
    }
}

/// The offered actions that can execute now, in registry order: the ids `GetAvailableActions`
/// marks available. Legacy actions are never listed.
pub fn legal_actions(
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
) -> Vec<ActionId> {
    Action::offered()
        .filter(|a| a.fail_reason(state, stats, recipe).is_none())
        .map(|a| a.id())
        .collect()
}

/// The availability row of every recognized action, legacy ones included, in registry order.
/// `GetAvailableActions` is the subset whose action is not [`Action::is_legacy`].
pub fn action_availability(
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
) -> [ActionAvailability; Action::COUNT] {
    Action::ALL.map(|action| action.availability(state, stats, recipe))
}

fn execute_internal(
    state: CraftingState,
    action: Action,
    stats: &CrafterStats,
    recipe: &Recipe,
    options: &SimulationOptions,
    conditions: &mut ConditionResolver,
    success_rolls: &mut DotNetRandom,
) -> StepResult {
    if let Some(reason) = action.fail_reason(&state, stats, recipe) {
        return StepResult::unchanged(state, action.id(), Some(action), reason);
    }

    let succeeded = if options.assume_all_success {
        true
    } else {
        success_rolls.next_double() * 100.0 < action.success_rate(&state) as f64
    };

    let mut after = action.execute(state, stats, recipe, succeeded);

    if let Some(forced) = options.force_condition {
        after.condition = forced;
    } else if !options.assume_normal_condition && action.advances_condition() {
        after.condition =
            conditions.next_condition(after.condition, recipe.is_expert, recipe.conditions_flag);
    }

    StepResult {
        step_number: state.step + 1,
        action_id: action.id(),
        action: Some(action),
        state_before: state,
        state_after: after,
        succeeded,
        fail_reason: if succeeded {
            None
        } else {
            Some(FailReason::SuccessRoll)
        },
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn fixture() -> (Recipe, CrafterStats) {
        let recipe = Recipe {
            difficulty: 1000,
            quality: 1000,
            durability: 40,
            progress_divider: 100,
            progress_modifier: 100,
            quality_divider: 100,
            quality_modifier: 100,
            ..Default::default()
        };
        let stats = CrafterStats {
            level: 100,
            craftsmanship: 1000,
            control: 1000,
            cp: 500,
            is_specialist: false,
        };
        (recipe, stats)
    }

    #[test]
    fn unknown_action_records_a_failed_step_and_continues() {
        let (recipe, stats) = fixture();
        let result = simulate(&recipe, &stats, &[999999, 999999]);
        assert_eq!(result.steps.len(), 2);
        assert_eq!(result.steps[0].fail_reason, Some(FailReason::UnknownAction));
        assert_eq!(result.steps[0].action_name(), "Unknown Action (999999)");
        assert_eq!(result.final_state, create_initial(&recipe, &stats));
    }

    #[test]
    fn legal_actions_follow_registry_order() {
        let (recipe, stats) = fixture();
        let state = create_initial(&recipe, &stats);
        let legal = legal_actions(&state, &stats, &recipe);
        assert_eq!(legal.first(), Some(&Action::BasicSynthesis.id()));
        assert!(!legal.contains(&Action::HeartAndSoul.id()));
    }

    #[test]
    fn legacy_actions_have_availability_rows_but_are_never_legal() {
        let (recipe, stats) = fixture();
        let state = create_initial(&recipe, &stats);
        let table = action_availability(&state, &stats, &recipe);
        assert_eq!(table.len(), Action::COUNT);
        for (row, action) in table.iter().zip(Action::ALL) {
            assert_eq!(row.action, action);
        }

        let legal = legal_actions(&state, &stats, &recipe);
        for legacy in [
            Action::FocusedSynthesis,
            Action::FocusedTouch,
            Action::InnerQuiet,
        ] {
            let row = table.iter().find(|r| r.action == legacy).unwrap();
            assert!(row.is_available(), "{legacy:?} still executes on replay");
            assert!(!legal.contains(&legacy.id()), "{legacy:?} is not offered");
        }

        let replayed = simulate(
            &recipe,
            &stats,
            &[Action::InnerQuiet.id(), Action::FocusedTouch.id()],
        );
        assert!(replayed.steps.iter().all(|s| s.succeeded));
    }

    #[test]
    fn unseeded_random_runs_are_deterministic() {
        let (recipe, stats) = fixture();
        let options = SimulationOptions {
            assume_all_success: false,
            assume_normal_condition: false,
            random_seed: None,
            force_condition: None,
        };
        let actions = [Action::RapidSynthesis.id(); 6];
        let first = simulate_with(&recipe, &stats, &actions, &options);
        let second = simulate_with(&recipe, &stats, &actions, &options);
        let pinned = simulate_with(
            &recipe,
            &stats,
            &actions,
            &SimulationOptions {
                random_seed: Some(DEFAULT_SEED),
                ..options
            },
        );
        assert_eq!(first, second);
        assert_eq!(first, pinned);
    }

    #[test]
    fn step_free_abilities_do_not_roll_a_new_condition() {
        let (recipe, stats) = fixture();
        let specialist = CrafterStats {
            is_specialist: true,
            ..stats
        };
        let options = SimulationOptions {
            assume_normal_condition: false,
            random_seed: Some(3),
            ..SimulationOptions::default()
        };
        let result = simulate_with(
            &recipe,
            &specialist,
            &[Action::HeartAndSoul.id(), Action::BasicSynthesis.id()],
            &options,
        );
        assert_eq!(result.steps[0].state_after.condition, Condition::Normal);
        assert_eq!(result.steps[0].state_after.step, 0);
        assert_eq!(result.steps[1].state_after.step, 1);
    }
}
