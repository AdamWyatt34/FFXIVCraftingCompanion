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

pub fn legal_actions(
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
) -> Vec<ActionId> {
    Action::ALL
        .iter()
        .filter(|a| a.fail_reason(state, stats, recipe).is_none())
        .map(|a| a.id())
        .collect()
}

/// `GetAvailableActions` for every action, in registry order.
pub fn action_availability(
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
) -> [ActionAvailability; 36] {
    let mut table = [Action::BasicSynthesis.availability(state, stats, recipe); 36];
    for (slot, action) in table.iter_mut().zip(Action::ALL) {
        *slot = action.availability(state, stats, recipe);
    }
    table
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
    } else if !options.assume_normal_condition {
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

    #[test]
    fn unknown_action_records_a_failed_step_and_continues() {
        let recipe = Recipe {
            difficulty: 100,
            quality: 100,
            durability: 40,
            ..Default::default()
        };
        let stats = CrafterStats {
            level: 1,
            cp: 10,
            ..Default::default()
        };
        let result = simulate(&recipe, &stats, &[999999, 999999]);
        assert_eq!(result.steps.len(), 2);
        assert_eq!(result.steps[0].fail_reason, Some(FailReason::UnknownAction));
        assert_eq!(result.steps[0].action_name(), "Unknown Action (999999)");
        assert_eq!(result.final_state, create_initial(&recipe, &stats));
    }

    #[test]
    fn legal_actions_follow_registry_order() {
        let recipe = Recipe {
            difficulty: 100,
            quality: 100,
            durability: 40,
            ..Default::default()
        };
        let stats = CrafterStats {
            level: 100,
            cp: 500,
            ..Default::default()
        };
        let state = create_initial(&recipe, &stats);
        let legal = legal_actions(&state, &stats, &recipe);
        assert_eq!(legal.first(), Some(&Action::BasicSynthesis.id()));
        assert!(!legal.contains(&Action::HeartAndSoul.id()));
    }
}
