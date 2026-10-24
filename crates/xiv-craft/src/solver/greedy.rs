//! `GreedySolver`: one pass, best-scoring action each step, three phases. The scores and every
//! threshold are the C# `GreedySolverConfig` values; the match tables keep the C# arm order because
//! the first matching arm wins in both languages.

use crate::actions::{Action, ActionAvailability, ActionCategory, ActionId};
use crate::formula::base_progress;
use crate::macro_export::{export_to_macro, MacroOptions};
use crate::simulator::{action_availability, create_initial, execute_action, simulate};
use crate::state::{Condition, CraftStatus, CrafterStats, CraftingState, Recipe};

use super::{Clock, SolverAlgorithm, SolverConstraints, SolverMetrics, SolverResult, SolverStatus};

#[derive(Clone, Debug, PartialEq)]
pub struct GreedySolverConfig {
    pub finishing_cp_buffer: i32,
    pub finishing_durability_buffer: i32,

    pub enable_durability_aware_scoring: bool,
    pub durability_aware_penalty: f64,

    pub cp_penalty_weight: f64,
    pub durability_penalty_weight: f64,

    pub tricks_of_the_trade_score: f64,

    pub trained_eye_score: f64,
    pub reflect_score: f64,
    pub muscle_memory_high_score: f64,
    pub muscle_memory_low_score: f64,
    pub muscle_memory_threshold: i32,
    pub opening_fallback_score: f64,

    pub byregots_high_iq_great_strides_score: f64,
    pub byregots_high_iq_threshold: i32,
    pub byregots_mid_iq_score: f64,
    pub byregots_mid_iq_threshold: i32,

    pub great_strides_high_iq_inno_score: f64,
    pub great_strides_high_iq_threshold: i32,
    pub great_strides_mid_iq_score: f64,
    pub great_strides_mid_iq_threshold: i32,

    pub innovation_score: f64,
    pub immaculate_mend_score: f64,
    pub immaculate_mend_dur_threshold: i32,
    pub immaculate_mend_cp_threshold: i32,
    pub manipulation_score: f64,
    pub manipulation_dur_threshold: i32,
    pub masters_mend_quality_score: f64,
    pub masters_mend_dur_threshold: i32,
    pub masters_mend_cp_threshold: i32,
    pub waste_not_ii_score: f64,
    pub waste_not_score: f64,

    pub preparatory_touch_score: f64,
    pub preparatory_touch_iq_cap: i32,
    pub trained_finesse_score: f64,
    pub combo_touch_score: f64,
    pub prudent_touch_score: f64,
    pub basic_touch_score: f64,
    pub innovation_active_score: f64,
    pub manipulation_active_score: f64,
    pub progress_in_quality_phase_score: f64,
    pub quality_fallback_score: f64,

    pub finish_manipulation_score: f64,
    pub finish_waste_not_ii_score: f64,
    pub finish_waste_not_ii_min_actions: i32,
    pub finish_waste_not_score: f64,
    pub finish_waste_not_min_actions: i32,
    pub finish_masters_mend_score: f64,
    pub finish_immaculate_mend_score: f64,

    pub finish_groundwork_veneration_score: f64,
    pub finish_veneration_score: f64,
    pub finish_groundwork_score: f64,
    pub finish_careful_synthesis_score: f64,
    pub finish_prudent_synthesis_score: f64,
    pub finish_basic_synthesis_score: f64,
    pub finish_groundwork_fallback_score: f64,
    pub finish_byregots_score: f64,
    pub finish_quality_penalty: f64,
    pub finish_buff_penalty: f64,
    pub finish_fallback_score: f64,
}

impl Default for GreedySolverConfig {
    fn default() -> GreedySolverConfig {
        GreedySolverConfig {
            finishing_cp_buffer: 30,
            finishing_durability_buffer: 15,

            enable_durability_aware_scoring: true,
            durability_aware_penalty: 15.0,

            cp_penalty_weight: 0.1,
            durability_penalty_weight: 0.05,

            tricks_of_the_trade_score: 200.0,

            trained_eye_score: 150.0,
            reflect_score: 100.0,
            muscle_memory_high_score: 110.0,
            muscle_memory_low_score: 90.0,
            muscle_memory_threshold: 4,
            opening_fallback_score: -50.0,

            byregots_high_iq_great_strides_score: 150.0,
            byregots_high_iq_threshold: 6,
            byregots_mid_iq_score: 60.0,
            byregots_mid_iq_threshold: 5,

            great_strides_high_iq_inno_score: 90.0,
            great_strides_high_iq_threshold: 8,
            great_strides_mid_iq_score: 70.0,
            great_strides_mid_iq_threshold: 6,

            innovation_score: 80.0,
            immaculate_mend_score: 85.0,
            immaculate_mend_dur_threshold: 15,
            immaculate_mend_cp_threshold: 200,
            manipulation_score: 90.0,
            manipulation_dur_threshold: 40,
            masters_mend_quality_score: 72.0,
            masters_mend_dur_threshold: 25,
            masters_mend_cp_threshold: 88,
            waste_not_ii_score: 70.0,
            waste_not_score: 55.0,

            preparatory_touch_score: 65.0,
            preparatory_touch_iq_cap: 8,
            trained_finesse_score: 60.0,
            combo_touch_score: 55.0,
            prudent_touch_score: 45.0,
            basic_touch_score: 40.0,
            innovation_active_score: -10.0,
            manipulation_active_score: -20.0,
            progress_in_quality_phase_score: -30.0,
            quality_fallback_score: -20.0,

            finish_manipulation_score: 120.0,
            finish_waste_not_ii_score: 105.0,
            finish_waste_not_ii_min_actions: 3,
            finish_waste_not_score: 95.0,
            finish_waste_not_min_actions: 2,
            finish_masters_mend_score: 85.0,
            finish_immaculate_mend_score: 85.0,

            finish_groundwork_veneration_score: 100.0,
            finish_veneration_score: 80.0,
            finish_groundwork_score: 60.0,
            finish_careful_synthesis_score: 50.0,
            finish_prudent_synthesis_score: 45.0,
            finish_basic_synthesis_score: 40.0,
            finish_groundwork_fallback_score: 35.0,
            finish_byregots_score: 90.0,
            finish_quality_penalty: -50.0,
            finish_buff_penalty: -30.0,
            finish_fallback_score: -20.0,
        }
    }
}

impl GreedySolverConfig {
    /// Low-durability recipes cannot afford the full buffer, or the solver would leave the quality
    /// phase before entering it.
    pub fn effective_durability_buffer(&self, max_durability: i32) -> i32 {
        if max_durability <= 40 {
            10
        } else {
            self.finishing_durability_buffer
        }
    }
}

pub fn solve(
    recipe: &Recipe,
    stats: &CrafterStats,
    constraints: &SolverConstraints,
    config: &GreedySolverConfig,
    now_ms: Clock<'_>,
) -> SolverResult {
    if recipe.is_expert {
        return SolverResult::failed(
            SolverAlgorithm::Greedy,
            "Expert recipes require the MCTS solver.",
            SolverMetrics::default(),
        );
    }

    let started = now_ms();
    let mut state = create_initial(recipe, stats);
    let mut action_ids: Vec<ActionId> = Vec::new();

    while state.status == CraftStatus::InProgress && action_ids.len() < constraints.max_steps {
        let Some(action) = select_best_action(&state, stats, recipe, constraints, config) else {
            break;
        };
        state = execute_action(state, action.id(), stats, recipe).state_after;
        action_ids.push(action.id());
    }

    let solve_time_ms = now_ms() - started;
    let simulation = simulate(recipe, stats, &action_ids);
    let macros = export_to_macro(&action_ids, &MacroOptions::default());

    SolverResult {
        status: SolverStatus::Completed,
        metrics: SolverMetrics {
            algorithm_used: Some(SolverAlgorithm::Greedy),
            solve_time_ms,
            total_steps: action_ids.len(),
            final_quality_percent: simulation.final_quality_percent(),
            final_hq_percent: simulation.final_hq_percent(),
            craft_completed: simulation.success(),
            nodes_explored: action_ids.len() as u64,
            iterations_completed: 1,
        },
        action_ids,
        simulation: Some(simulation),
        macros,
        error_message: None,
    }
}

fn select_best_action(
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
    constraints: &SolverConstraints,
    config: &GreedySolverConfig,
) -> Option<Action> {
    let phase = determine_phase(state, stats, recipe, config);

    let mut best = None;
    let mut best_score = f64::MIN;
    for availability in action_availability(state, stats, recipe) {
        // Legacy actions replay but are never picked: the solver draws only from the offered set.
        if !availability.is_available() || availability.action.is_legacy() {
            continue;
        }
        // Step-free abilities never move the craft out of its phase, so a greedy pick would
        // re-select them until the step budget runs out.
        if !availability.action.consumes_step() {
            continue;
        }
        let score = score_action(
            &availability,
            state,
            stats,
            recipe,
            phase,
            constraints,
            config,
        );
        if score > best_score {
            best_score = score;
            best = Some(availability.action);
        }
    }
    best
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum CraftPhase {
    Opening,
    Quality,
    Finishing,
}

fn determine_phase(
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
    config: &GreedySolverConfig,
) -> CraftPhase {
    if state.is_first_step {
        return CraftPhase::Opening;
    }
    if state.quality >= state.max_quality {
        return CraftPhase::Finishing;
    }

    let finishing = estimate_finishing_resources(state, stats, recipe);
    let cp_tight = state.cp <= finishing.cp_needed + config.finishing_cp_buffer;
    let durability_tight = state.durability
        <= finishing.durability_needed + config.effective_durability_buffer(recipe.durability);

    if cp_tight || durability_tight {
        CraftPhase::Finishing
    } else {
        CraftPhase::Quality
    }
}

struct FinishingEstimate {
    cp_needed: i32,
    durability_needed: i32,
}

fn estimate_finishing_resources(
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
) -> FinishingEstimate {
    let actions_to_finish = actions_to_finish(state.max_progress - state.progress, stats, recipe);

    // Budgets Groundwork at 20 durability each, less the Manipulation ticks still to come (one
    // extra for the Veneration turn).
    let raw_durability = actions_to_finish * 20;
    let manip_active = state.manipulation_turns > 0;
    let manip_healing = if manip_active {
        state.manipulation_turns.min(actions_to_finish + 1) * 5
    } else {
        0
    };
    let net_durability = (raw_durability - manip_healing).max(actions_to_finish * 5);

    let mut cp_needed = 18 + actions_to_finish * 18;
    let needs_manipulation = !manip_active && state.durability < raw_durability;
    if needs_manipulation {
        cp_needed += Action::Manipulation.base_cp_cost();
    }

    FinishingEstimate {
        cp_needed,
        durability_needed: net_durability,
    }
}

fn actions_to_finish(remaining_progress: i32, stats: &CrafterStats, recipe: &Recipe) -> i32 {
    let progress_per_action = estimate_progress_per_action(stats, recipe);
    if progress_per_action > 0 {
        (remaining_progress as f64 / progress_per_action as f64).ceil() as i32
    } else {
        3
    }
}

/// Groundwork (300 potency) under Veneration (+50%).
fn estimate_progress_per_action(stats: &CrafterStats, recipe: &Recipe) -> i32 {
    let base = base_progress(
        stats.craftsmanship,
        recipe.progress_divider,
        recipe.progress_modifier,
        stats.level,
        recipe.class_job_level,
    );
    (base as f64 * 450.0 / 100.0).floor() as i32
}

fn estimate_actions_to_finish_progress(stats: &CrafterStats, recipe: &Recipe) -> i32 {
    let progress_per_action = estimate_progress_per_action(stats, recipe);
    if progress_per_action <= 0 {
        return 10;
    }
    (recipe.difficulty as f64 / progress_per_action as f64).ceil() as i32
}

fn score_action(
    availability: &ActionAvailability,
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
    phase: CraftPhase,
    constraints: &SolverConstraints,
    config: &GreedySolverConfig,
) -> f64 {
    if availability.success_rate < 100 && !constraints.allow_unsafe_actions {
        return f64::MIN;
    }

    let action = availability.action;
    if action == Action::TricksOfTheTrade
        && matches!(state.condition, Condition::Good | Condition::Excellent)
    {
        return config.tricks_of_the_trade_score;
    }

    let mut score = 0.0;
    if availability.cp_cost > 0 {
        score -= availability.cp_cost as f64 * config.cp_penalty_weight;
    }
    if availability.durability_cost > 0 {
        score -= availability.durability_cost as f64 * config.durability_penalty_weight;
    }

    score
        + match phase {
            CraftPhase::Opening => score_opening_action(action, stats, recipe, config),
            CraftPhase::Quality => score_quality_action(action, state, recipe, config),
            CraftPhase::Finishing => score_finishing_action(action, state, stats, recipe, config),
        }
}

fn score_opening_action(
    action: Action,
    stats: &CrafterStats,
    recipe: &Recipe,
    config: &GreedySolverConfig,
) -> f64 {
    match action {
        Action::TrainedEye if stats.level >= recipe.class_job_level + 10 => {
            config.trained_eye_score
        }
        Action::Reflect => config.reflect_score,
        Action::MuscleMemory => {
            if estimate_actions_to_finish_progress(stats, recipe) >= config.muscle_memory_threshold
            {
                config.muscle_memory_high_score
            } else {
                config.muscle_memory_low_score
            }
        }
        _ => config.opening_fallback_score,
    }
}

fn score_quality_action(
    action: Action,
    state: &CraftingState,
    recipe: &Recipe,
    config: &GreedySolverConfig,
) -> f64 {
    let innovation_active = state.innovation_turns > 0;
    let great_strides_active = state.great_strides_turns > 0;
    let waste_not_active = state.waste_not_turns > 0 || state.waste_not_ii_turns > 0;
    let manipulation_active = state.manipulation_turns > 0;
    let iq = state.inner_quiet_stacks;
    let category = action.category();

    let durability_penalty = if config.enable_durability_aware_scoring
        && recipe.durability > 0
        && (state.durability as f64) < recipe.durability as f64 * 0.3
        && category == ActionCategory::Quality
    {
        config.durability_aware_penalty
    } else {
        0.0
    };

    let score = match action {
        Action::ByregotsBlessing
            if iq >= config.byregots_high_iq_threshold && great_strides_active =>
        {
            config.byregots_high_iq_great_strides_score
        }
        Action::ByregotsBlessing if iq >= config.byregots_mid_iq_threshold => {
            config.byregots_mid_iq_score
        }

        Action::GreatStrides
            if iq >= config.great_strides_high_iq_threshold && innovation_active =>
        {
            config.great_strides_high_iq_inno_score
        }
        Action::GreatStrides if iq >= config.great_strides_mid_iq_threshold => {
            config.great_strides_mid_iq_score
        }

        Action::Innovation if !innovation_active => config.innovation_score,

        Action::ImmaculateMend
            if state.durability <= config.immaculate_mend_dur_threshold
                && state.cp >= config.immaculate_mend_cp_threshold =>
        {
            config.immaculate_mend_score
        }
        Action::Manipulation
            if !manipulation_active && state.durability <= config.manipulation_dur_threshold =>
        {
            config.manipulation_score
        }
        Action::MastersMend
            if state.durability <= config.masters_mend_dur_threshold
                && state.cp >= config.masters_mend_cp_threshold =>
        {
            config.masters_mend_quality_score
        }
        Action::WasteNotII if !waste_not_active => config.waste_not_ii_score,
        Action::WasteNot if !waste_not_active => config.waste_not_score,

        Action::PreparatoryTouch if iq < config.preparatory_touch_iq_cap => {
            config.preparatory_touch_score
        }
        Action::TrainedFinesse if iq == 10 => config.trained_finesse_score,

        Action::AdvancedTouch if state.combo_action == Action::StandardTouch.id() => {
            config.combo_touch_score
        }
        Action::StandardTouch if state.combo_action == Action::BasicTouch.id() => {
            config.combo_touch_score
        }

        Action::PrudentTouch if !waste_not_active => config.prudent_touch_score,
        Action::BasicTouch => config.basic_touch_score,

        Action::Innovation if innovation_active => config.innovation_active_score,
        Action::Manipulation if manipulation_active => config.manipulation_active_score,

        _ if category == ActionCategory::Progress => config.progress_in_quality_phase_score,

        _ => config.quality_fallback_score,
    };

    score - durability_penalty
}

fn score_finishing_action(
    action: Action,
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
    config: &GreedySolverConfig,
) -> f64 {
    let veneration_active = state.veneration_turns > 0;
    let manipulation_active = state.manipulation_turns > 0;
    let waste_not_active = state.waste_not_turns > 0 || state.waste_not_ii_turns > 0;
    let iq = state.inner_quiet_stacks;

    let actions_remaining = actions_to_finish(state.max_progress - state.progress, stats, recipe);
    let dur_per_action = if waste_not_active { 10 } else { 20 };
    let dur_needed = actions_remaining * dur_per_action;
    let durability_short = state.durability < dur_needed + 5;

    match action {
        Action::Manipulation if !manipulation_active && durability_short => {
            config.finish_manipulation_score
        }
        Action::WasteNotII
            if !waste_not_active
                && durability_short
                && actions_remaining >= config.finish_waste_not_ii_min_actions =>
        {
            config.finish_waste_not_ii_score
        }
        Action::WasteNot
            if !waste_not_active
                && durability_short
                && actions_remaining >= config.finish_waste_not_min_actions =>
        {
            config.finish_waste_not_score
        }
        Action::MastersMend
            if state.durability <= config.masters_mend_dur_threshold
                && state.cp >= config.masters_mend_cp_threshold =>
        {
            config.finish_masters_mend_score
        }
        Action::ImmaculateMend
            if state.durability <= config.immaculate_mend_dur_threshold
                && state.cp >= config.immaculate_mend_cp_threshold =>
        {
            config.finish_immaculate_mend_score
        }

        Action::Groundwork if veneration_active => config.finish_groundwork_veneration_score,
        Action::Veneration if !veneration_active && state.durability > dur_per_action => {
            config.finish_veneration_score
        }
        Action::Groundwork if !durability_short => config.finish_groundwork_score,
        Action::CarefulSynthesis => config.finish_careful_synthesis_score,
        Action::PrudentSynthesis if !waste_not_active => config.finish_prudent_synthesis_score,
        Action::BasicSynthesis => config.finish_basic_synthesis_score,

        Action::Groundwork => config.finish_groundwork_fallback_score,

        Action::ByregotsBlessing if iq > 0 && !durability_short => config.finish_byregots_score,

        _ if action.category() == ActionCategory::Quality => config.finish_quality_penalty,

        Action::Innovation | Action::GreatStrides => config.finish_buff_penalty,

        _ => config.finish_fallback_score,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn effective_buffer_drops_to_ten_at_or_below_forty_durability() {
        let config = GreedySolverConfig::default();
        assert_eq!(config.effective_durability_buffer(35), 10);
        assert_eq!(config.effective_durability_buffer(40), 10);
        assert_eq!(config.effective_durability_buffer(60), 15);
        assert_eq!(config.effective_durability_buffer(80), 15);
    }

    #[test]
    fn progress_per_action_is_groundwork_under_veneration() {
        let stats = CrafterStats {
            level: 90,
            craftsmanship: 3800,
            ..CrafterStats::default()
        };
        let recipe = Recipe {
            recipe_level: 580,
            class_job_level: 90,
            progress_divider: 130,
            progress_modifier: 80,
            difficulty: 3500,
            ..Recipe::default()
        };
        // base_progress = (3800*10/130 + 2) * 80 / 100 = 235.4 → 235; 235 * 4.5 floored.
        assert_eq!(estimate_progress_per_action(&stats, &recipe), 1057);
        assert_eq!(estimate_actions_to_finish_progress(&stats, &recipe), 4);
    }
}
