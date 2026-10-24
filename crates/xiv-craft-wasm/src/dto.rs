//! The JSON shapes `frontend/src/types/simulator.ts` and `solver.ts` declare, and the mappings
//! between them and the engine's types.
//! Field names follow System.Text.Json's camel case, which keeps an initial acronym upper case
//! (`maxCP`, `finalHQPercent`, `canBeHQ`); those are renamed explicitly.

use serde::{Deserialize, Serialize};
use xiv_craft::solver::{SolverAlgorithm, SolverConstraints, SolverMetrics, SolverResult};
use xiv_craft::{
    Action, ActionAvailability, ActionCategory, Condition, CraftStatus, CrafterStats,
    CraftingState, HqInput, Ingredient, MacroOptions, Recipe, SimulationOptions, SimulationResult,
    StepResult,
};

// ── Recipe and crafter ──

/// `RecipeParameters` as served by `GET /api/recipes/{id}/crafting-parameters`.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RecipeParametersDto {
    pub recipe_id: i32,
    pub recipe_level: i32,
    pub difficulty: i32,
    pub quality: i32,
    pub durability: i32,
    pub progress_divider: i32,
    pub progress_modifier: i32,
    pub quality_divider: i32,
    pub quality_modifier: i32,
    #[serde(default)]
    pub class_job_level: i32,
    #[serde(default)]
    pub stars: i32,
    #[serde(default)]
    pub is_expert: bool,
    #[serde(default)]
    pub conditions_flag: i32,
    #[serde(default)]
    pub suggested_craftsmanship: i32,
    #[serde(default)]
    pub suggested_control: i32,
    #[serde(default)]
    pub material_quality_factor: i32,
    #[serde(default)]
    pub ingredients: Vec<IngredientDto>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct IngredientDto {
    pub item_id: i32,
    pub quantity: i32,
    #[serde(rename = "canBeHQ")]
    pub can_be_hq: bool,
    /// Absent from data sets without item levels; 0 weighs HQ materials by count.
    #[serde(default)]
    pub item_level: i32,
}

impl RecipeParametersDto {
    pub fn to_recipe(&self, starting_quality: i32) -> Recipe {
        Recipe {
            recipe_id: self.recipe_id,
            recipe_level: self.recipe_level,
            difficulty: self.difficulty,
            quality: self.quality,
            durability: self.durability,
            progress_divider: self.progress_divider,
            progress_modifier: self.progress_modifier,
            quality_divider: self.quality_divider,
            quality_modifier: self.quality_modifier,
            class_job_level: self.class_job_level,
            stars: self.stars,
            is_expert: self.is_expert,
            conditions_flag: self.conditions_flag,
            suggested_craftsmanship: self.suggested_craftsmanship,
            suggested_control: self.suggested_control,
            starting_quality,
            material_quality_factor: self.material_quality_factor,
        }
    }

    pub fn ingredients(&self) -> Vec<Ingredient> {
        self.ingredients
            .iter()
            .map(|i| Ingredient {
                item_id: i.item_id,
                quantity: i.quantity,
                can_be_hq: i.can_be_hq,
                item_level: i.item_level,
            })
            .collect()
    }
}

/// `CrafterStatsInput`.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CrafterStatsDto {
    pub level: i32,
    pub craftsmanship: i32,
    pub control: i32,
    pub cp: i32,
    #[serde(default)]
    pub is_specialist: bool,
}

impl CrafterStatsDto {
    pub fn to_stats(self) -> CrafterStats {
        CrafterStats {
            level: self.level,
            craftsmanship: self.craftsmanship,
            control: self.control,
            cp: self.cp,
            is_specialist: self.is_specialist,
        }
    }
}

/// `HQIngredientInput`.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct HqIngredientDto {
    pub item_id: i32,
    pub hq_quantity: i32,
}

impl HqIngredientDto {
    pub fn to_input(self) -> HqInput {
        HqInput {
            item_id: self.item_id,
            hq_quantity: self.hq_quantity,
        }
    }
}

// ── Crafting state ──

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ActiveBuffDto {
    pub name: String,
    pub remaining_turns: i32,
    pub stacks: Option<i32>,
}

/// The crafting state as the frontend holds it: buffs as a named list, plus the hidden fields
/// that let a state round-trip through step-by-step execution.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CraftingStateDto {
    pub progress: i32,
    pub max_progress: i32,
    pub progress_percent: f64,
    pub quality: i32,
    pub max_quality: i32,
    pub quality_percent: f64,
    pub durability: i32,
    pub max_durability: i32,
    pub cp: i32,
    #[serde(rename = "maxCP")]
    pub max_cp: i32,
    pub step: i32,
    pub condition: String,
    pub status: String,
    pub hq_percent: i32,
    #[serde(default)]
    pub active_buffs: Vec<ActiveBuffDto>,
    #[serde(default)]
    pub inner_quiet_stacks: i32,
    #[serde(default)]
    pub combo_action: i32,
    #[serde(default)]
    pub expedience_active: bool,
    #[serde(default)]
    pub is_first_step: bool,
    #[serde(default)]
    pub quick_innovation_used: bool,
    #[serde(default)]
    pub heart_and_soul_used: bool,
    #[serde(default)]
    pub trained_perfection_used: bool,
    #[serde(default)]
    pub careful_observation_count: i32,
}

const WASTE_NOT: &str = "Waste Not";
const WASTE_NOT_II: &str = "Waste Not II";
const VENERATION: &str = "Veneration";
const INNOVATION: &str = "Innovation";
const GREAT_STRIDES: &str = "Great Strides";
const MANIPULATION: &str = "Manipulation";
const FINAL_APPRAISAL: &str = "Final Appraisal";
const MUSCLE_MEMORY: &str = "Muscle Memory";
const HEART_AND_SOUL: &str = "Heart and Soul";
const TRAINED_PERFECTION: &str = "Trained Perfection";

pub fn condition_name(condition: Condition) -> &'static str {
    match condition {
        Condition::Normal => "Normal",
        Condition::Good => "Good",
        Condition::Excellent => "Excellent",
        Condition::Poor => "Poor",
        Condition::Centered => "Centered",
        Condition::Sturdy => "Sturdy",
        Condition::Pliant => "Pliant",
        Condition::Malleable => "Malleable",
        Condition::Primed => "Primed",
        Condition::GoodOmen => "GoodOmen",
    }
}

/// Case-insensitive; an unknown name yields the default condition.
pub fn parse_condition(name: &str) -> Condition {
    Condition::ALL
        .iter()
        .copied()
        .find(|c| condition_name(*c).eq_ignore_ascii_case(name))
        .unwrap_or_default()
}

pub fn status_name(status: CraftStatus) -> &'static str {
    match status {
        CraftStatus::InProgress => "InProgress",
        CraftStatus::Completed => "Completed",
        CraftStatus::Failed => "Failed",
    }
}

pub fn parse_status(name: &str) -> CraftStatus {
    [
        CraftStatus::InProgress,
        CraftStatus::Completed,
        CraftStatus::Failed,
    ]
    .iter()
    .copied()
    .find(|s| status_name(*s).eq_ignore_ascii_case(name))
    .unwrap_or_default()
}

impl CraftingStateDto {
    pub fn from_state(state: &CraftingState) -> CraftingStateDto {
        let mut active_buffs = Vec::new();
        let mut turns = |name: &str, remaining: i32| {
            if remaining > 0 {
                active_buffs.push(ActiveBuffDto {
                    name: name.to_string(),
                    remaining_turns: remaining,
                    stacks: None,
                });
            }
        };
        turns(WASTE_NOT, state.waste_not_turns);
        turns(WASTE_NOT_II, state.waste_not_ii_turns);
        turns(VENERATION, state.veneration_turns);
        turns(INNOVATION, state.innovation_turns);
        turns(GREAT_STRIDES, state.great_strides_turns);
        turns(MANIPULATION, state.manipulation_turns);
        turns(FINAL_APPRAISAL, state.final_appraisal_turns);
        turns(MUSCLE_MEMORY, state.muscle_memory_turns);
        turns(HEART_AND_SOUL, i32::from(state.heart_and_soul_active));
        turns(
            TRAINED_PERFECTION,
            i32::from(state.trained_perfection_active),
        );

        CraftingStateDto {
            progress: state.progress,
            max_progress: state.max_progress,
            progress_percent: state.progress_percent(),
            quality: state.quality,
            max_quality: state.max_quality,
            quality_percent: state.quality_percent(),
            durability: state.durability,
            max_durability: state.max_durability,
            cp: state.cp,
            max_cp: state.max_cp,
            step: state.step,
            condition: condition_name(state.condition).to_string(),
            status: status_name(state.status).to_string(),
            hq_percent: state.hq_percent(),
            active_buffs,
            inner_quiet_stacks: state.inner_quiet_stacks,
            combo_action: state.combo_action,
            expedience_active: state.expedience_active,
            is_first_step: state.is_first_step,
            quick_innovation_used: state.quick_innovation_used,
            heart_and_soul_used: state.heart_and_soul_used,
            trained_perfection_used: state.trained_perfection_used,
            careful_observation_count: state.careful_observation_count,
        }
    }

    /// The first buff of each name wins.
    pub fn to_state(&self) -> CraftingState {
        let turns = |name: &str| {
            self.active_buffs
                .iter()
                .find(|b| b.name == name)
                .map(|b| b.remaining_turns)
                .unwrap_or(0)
        };
        let active = |name: &str| self.active_buffs.iter().any(|b| b.name == name);

        CraftingState {
            progress: self.progress,
            max_progress: self.max_progress,
            quality: self.quality,
            max_quality: self.max_quality,
            durability: self.durability,
            max_durability: self.max_durability,
            cp: self.cp,
            max_cp: self.max_cp,
            step: self.step,
            condition: parse_condition(&self.condition),
            status: parse_status(&self.status),
            inner_quiet_stacks: self.inner_quiet_stacks,
            waste_not_turns: turns(WASTE_NOT),
            waste_not_ii_turns: turns(WASTE_NOT_II),
            veneration_turns: turns(VENERATION),
            innovation_turns: turns(INNOVATION),
            great_strides_turns: turns(GREAT_STRIDES),
            manipulation_turns: turns(MANIPULATION),
            final_appraisal_turns: turns(FINAL_APPRAISAL),
            muscle_memory_turns: turns(MUSCLE_MEMORY),
            heart_and_soul_active: active(HEART_AND_SOUL),
            trained_perfection_active: active(TRAINED_PERFECTION),
            expedience_active: self.expedience_active,
            quick_innovation_used: self.quick_innovation_used,
            heart_and_soul_used: self.heart_and_soul_used,
            trained_perfection_used: self.trained_perfection_used,
            careful_observation_count: self.careful_observation_count,
            is_first_step: self.is_first_step,
            combo_action: self.combo_action,
        }
    }
}

// ── Steps and simulations ──

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct StepDeltasDto {
    pub progress: i32,
    pub quality: i32,
    pub durability: i32,
    pub cp: i32,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct StepResultDto {
    pub step_number: i32,
    pub action_name: String,
    pub action_id: i32,
    pub action_category: String,
    pub succeeded: bool,
    pub fail_reason: Option<String>,
    pub deltas: StepDeltasDto,
    pub state_after: CraftingStateDto,
}

pub fn category_name(category: ActionCategory) -> &'static str {
    match category {
        ActionCategory::Progress => "Progress",
        ActionCategory::Quality => "Quality",
        ActionCategory::Buff => "Buff",
        ActionCategory::Repair => "Repair",
        ActionCategory::Other => "Other",
    }
}

impl StepResultDto {
    pub fn from_step(step: &StepResult) -> StepResultDto {
        StepResultDto {
            step_number: step.step_number,
            action_name: step.action_name(),
            action_id: step.action_id,
            action_category: step
                .action
                .map(|a| category_name(a.category()))
                .unwrap_or("Unknown")
                .to_string(),
            succeeded: step.succeeded,
            fail_reason: step.fail_reason.map(|r| r.to_string()),
            deltas: StepDeltasDto {
                progress: step.progress_delta(),
                quality: step.quality_delta(),
                durability: step.durability_delta(),
                cp: step.cp_delta(),
            },
            state_after: CraftingStateDto::from_state(&step.state_after),
        }
    }
}

/// `RecipeSummaryDto`, which only the simulate endpoint fills; the solver leaves it null and
/// so does this crate (the caller has the recipe).
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RecipeSummaryDto {
    pub recipe_id: i32,
    pub craft_type: String,
    pub craft_type_name: String,
    pub recipe_level: i32,
    pub required_craftsmanship: i32,
    pub required_control: i32,
    #[serde(rename = "yield")]
    pub yield_count: i32,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SimulationResultDto {
    pub success: bool,
    pub total_steps: i32,
    pub final_quality_percent: f64,
    #[serde(rename = "finalHQPercent")]
    pub final_hq_percent: i32,
    pub final_state: CraftingStateDto,
    pub steps: Vec<StepResultDto>,
    pub macros: Vec<String>,
    pub recipe: Option<RecipeSummaryDto>,
}

impl SimulationResultDto {
    pub fn from_simulation(result: &SimulationResult, macros: Vec<String>) -> SimulationResultDto {
        SimulationResultDto {
            success: result.success(),
            total_steps: result.total_steps() as i32,
            final_quality_percent: result.final_quality_percent(),
            final_hq_percent: result.final_hq_percent(),
            final_state: CraftingStateDto::from_state(&result.final_state),
            steps: result.steps.iter().map(StepResultDto::from_step).collect(),
            macros,
            recipe: None,
        }
    }
}

/// Absent fields take `SimulationOptions::default()`.
#[derive(Clone, Debug, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SimulationOptionsDto {
    pub assume_all_success: Option<bool>,
    pub assume_normal_condition: Option<bool>,
    pub random_seed: Option<i32>,
    pub force_condition: Option<String>,
}

impl SimulationOptionsDto {
    /// An unknown condition name is ignored.
    pub fn to_options(&self) -> SimulationOptions {
        let defaults = SimulationOptions::default();
        let force_condition = self
            .force_condition
            .as_deref()
            .filter(|name| !name.is_empty())
            .and_then(|name| {
                Condition::ALL
                    .iter()
                    .copied()
                    .find(|c| condition_name(*c).eq_ignore_ascii_case(name))
            });
        SimulationOptions {
            assume_all_success: self
                .assume_all_success
                .unwrap_or(defaults.assume_all_success),
            assume_normal_condition: self
                .assume_normal_condition
                .unwrap_or(defaults.assume_normal_condition),
            random_seed: self.random_seed,
            force_condition,
        }
    }
}

// ── Actions ──

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ActionInfoDto {
    pub id: i32,
    pub name: String,
    pub in_game_name: String,
    pub level_requirement: i32,
    pub category: String,
    #[serde(rename = "baseCPCost")]
    pub base_cp_cost: i32,
    pub base_durability_cost: i32,
    pub base_success_rate: i32,
    /// Removed from the game: a saved rotation still replays it, but a palette does not offer it.
    pub is_legacy: bool,
}

impl ActionInfoDto {
    /// Costs are read on a fresh default state.
    pub fn from_action(action: Action) -> ActionInfoDto {
        let default_state = CraftingState {
            max_progress: 1000,
            max_quality: 1000,
            durability: 70,
            max_durability: 70,
            cp: 600,
            max_cp: 600,
            condition: Condition::Normal,
            status: CraftStatus::InProgress,
            is_first_step: true,
            ..CraftingState::default()
        };
        ActionInfoDto {
            id: action.id(),
            name: action.name().to_string(),
            in_game_name: action.in_game_name().to_string(),
            level_requirement: action.level_requirement(),
            category: category_name(action.category()).to_string(),
            base_cp_cost: action.cp_cost(&default_state),
            base_durability_cost: action.durability_cost(&default_state),
            base_success_rate: action.success_rate(&default_state),
            is_legacy: action.is_legacy(),
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ActionAvailabilityDto {
    pub action_id: i32,
    pub action_name: String,
    pub is_available: bool,
    pub unavailable_reason: Option<String>,
    pub cp_cost: i32,
    pub durability_cost: i32,
    pub success_rate: i32,
    pub category: String,
}

impl ActionAvailabilityDto {
    /// The name is the in-game one.
    pub fn from_availability(availability: &ActionAvailability) -> ActionAvailabilityDto {
        ActionAvailabilityDto {
            action_id: availability.action.id(),
            action_name: availability.action.in_game_name().to_string(),
            is_available: availability.is_available(),
            unavailable_reason: availability.fail_reason.map(|r| r.to_string()),
            cp_cost: availability.cp_cost,
            durability_cost: availability.durability_cost,
            success_rate: availability.success_rate,
            category: category_name(availability.action.category()).to_string(),
        }
    }
}

// ── Macros ──

/// Absent fields take `MacroOptions::default()`.
#[derive(Clone, Debug, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MacroOptionsDto {
    pub lines_per_macro: Option<usize>,
    pub sound_effect: Option<i32>,
    pub complete_sound_effect: Option<i32>,
    pub include_echo: Option<bool>,
}

impl MacroOptionsDto {
    pub fn to_options(&self) -> MacroOptions {
        let defaults = MacroOptions::default();
        MacroOptions {
            lines_per_macro: self.lines_per_macro.unwrap_or(defaults.lines_per_macro),
            sound_effect: self.sound_effect.unwrap_or(defaults.sound_effect),
            complete_sound_effect: self
                .complete_sound_effect
                .unwrap_or(defaults.complete_sound_effect),
            include_echo: self.include_echo.unwrap_or(defaults.include_echo),
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ExportMacroResultDto {
    pub macros: Vec<String>,
    pub total_actions: i32,
    pub macro_count: i32,
}

// ── Solver ──

/// `SolverConstraintsDto`; only the fields the solvers read (see `xiv_craft::solver`).
#[derive(Clone, Debug, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SolverConstraintsDto {
    pub target_quality_percent: Option<i32>,
    pub allow_unsafe_actions: Option<bool>,
    pub max_steps: Option<usize>,
}

impl SolverConstraintsDto {
    pub fn to_constraints(&self) -> SolverConstraints {
        let defaults = SolverConstraints::default();
        SolverConstraints {
            target_quality_percent: self.target_quality_percent,
            allow_unsafe_actions: self
                .allow_unsafe_actions
                .unwrap_or(defaults.allow_unsafe_actions),
            max_steps: self.max_steps.unwrap_or(defaults.max_steps),
        }
    }
}

/// The MCTS knobs a session takes: the time budget from `SolverSettingsDto`, the optional
/// iteration cap and seed the crate adds.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SolverSettingsDto {
    pub time_budget_ms: f64,
    pub max_iterations: Option<u64>,
    pub random_seed: Option<i32>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SolverMetricsDto {
    pub algorithm_used: String,
    pub solve_time_ms: i32,
    pub total_steps: i32,
    pub final_quality_percent: f64,
    #[serde(rename = "finalHQPercent")]
    pub final_hq_percent: i32,
    pub craft_completed: bool,
    pub nodes_explored: i32,
    pub iterations_completed: i32,
}

/// `SolverMapper.ToDto(SolverMetrics)`. A missing algorithm is the C# enum default, `Auto`.
pub fn algorithm_name(algorithm: Option<SolverAlgorithm>) -> &'static str {
    match algorithm {
        Some(SolverAlgorithm::Greedy) => "Greedy",
        Some(SolverAlgorithm::Mcts) => "MCTS",
        None => "Auto",
    }
}

impl SolverMetricsDto {
    pub fn from_metrics(metrics: &SolverMetrics) -> SolverMetricsDto {
        SolverMetricsDto {
            algorithm_used: algorithm_name(metrics.algorithm_used).to_string(),
            solve_time_ms: metrics.solve_time_ms as i32,
            total_steps: metrics.total_steps as i32,
            final_quality_percent: metrics.final_quality_percent,
            final_hq_percent: metrics.final_hq_percent,
            craft_completed: metrics.craft_completed,
            nodes_explored: metrics.nodes_explored.min(i32::MAX as u64) as i32,
            iterations_completed: metrics.iterations_completed.min(i32::MAX as u64) as i32,
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SolverResultDto {
    pub action_ids: Vec<i32>,
    pub action_names: Vec<String>,
    pub simulation: Option<SimulationResultDto>,
    pub macros: Vec<String>,
    pub metrics: SolverMetricsDto,
    pub error_message: Option<String>,
}

impl SolverResultDto {
    /// `SolverMapper.ToDto(SolverResult)`: names are the registry names, not the in-game ones.
    pub fn from_result(result: &SolverResult) -> SolverResultDto {
        SolverResultDto {
            action_ids: result.action_ids.clone(),
            action_names: result
                .action_ids
                .iter()
                .map(|id| match Action::from_id(*id) {
                    Some(action) => action.name().to_string(),
                    None => format!("Unknown({id})"),
                })
                .collect(),
            simulation: result
                .simulation
                .as_ref()
                .map(|s| SimulationResultDto::from_simulation(s, result.macros.clone())),
            macros: result.macros.clone(),
            metrics: SolverMetricsDto::from_metrics(&result.metrics),
            error_message: result.error_message.clone(),
        }
    }
}

/// `xiv_craft::solver::mcts::SearchProgress` for the worker's progress messages.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SearchProgressDto {
    pub iterations: f64,
    pub nodes_explored: f64,
    pub elapsed_ms: f64,
    pub best_steps: i32,
    pub best_quality_percent: f64,
    #[serde(rename = "bestHQPercent")]
    pub best_hq_percent: i32,
    pub done: bool,
}
