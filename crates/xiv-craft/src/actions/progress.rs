use crate::actions::quality::condition_multiplier;
use crate::actions::Action;
use crate::formula;
use crate::state::{Condition, CrafterStats, CraftingState, Recipe};

pub(super) fn apply_effect(
    action: Action,
    state: CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
) -> CraftingState {
    if action == Action::DelicateSynthesis {
        return delicate_synthesis(state, stats, recipe);
    }

    let potency = match action {
        // Mastery traits raise the potency at 31, 82 and 86.
        Action::BasicSynthesis => {
            if stats.level >= 31 {
                120
            } else {
                100
            }
        }
        Action::CarefulSynthesis => {
            if stats.level >= 82 {
                180
            } else {
                150
            }
        }
        Action::RapidSynthesis => 500,
        Action::FocusedSynthesis => 200,
        // Groundwork loses half its potency when durability cannot cover its full cost.
        Action::Groundwork => {
            let base = if stats.level >= 86 { 360 } else { 300 };
            if state.durability < action.durability_cost(&state) {
                base / 2
            } else {
                base
            }
        }
        Action::IntensiveSynthesis => 400,
        Action::PrudentSynthesis => 180,
        Action::MuscleMemory => 300,
        _ => unreachable!("{action:?} is not a progress action"),
    };

    let mut next = progress_effect(state, stats, recipe, potency);

    // Heart and Soul is spent only when it substituted for the missing Good condition.
    if action == Action::IntensiveSynthesis
        && state.heart_and_soul_active
        && !matches!(state.condition, Condition::Good | Condition::Excellent)
    {
        next.heart_and_soul_active = false;
    }
    next
}

pub(super) fn apply_granted_buffs(action: Action, state: CraftingState) -> CraftingState {
    match action {
        Action::MuscleMemory => {
            let mut next = state;
            next.muscle_memory_turns = if state.condition == Condition::Primed {
                7
            } else {
                5
            };
            next
        }
        _ => state,
    }
}

// Final Appraisal holds progress one short of completion and is consumed; Muscle Memory is
// consumed by any progress action.
fn progress_effect(
    state: CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
    potency: i32,
) -> CraftingState {
    let new_progress = state.progress + calculate_progress(&state, stats, recipe, potency);
    let mut next = state;

    if state.final_appraisal_turns > 0 && new_progress >= state.max_progress {
        next.progress = state.max_progress - 1;
        next.final_appraisal_turns = 0;
        next.muscle_memory_turns = 0;
        return next;
    }

    next.progress = new_progress.min(state.max_progress);
    next.muscle_memory_turns = 0;
    next
}

pub(super) fn calculate_progress(
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
    potency: i32,
) -> i32 {
    let base = formula::base_progress(
        stats.craftsmanship,
        recipe.progress_divider,
        recipe.progress_modifier,
        stats.level,
        recipe.recipe_level,
    );
    let mut efficiency = potency;
    if state.veneration_turns > 0 {
        efficiency += 50;
    }
    if state.muscle_memory_turns > 0 {
        efficiency += 100;
    }
    ((base * efficiency) as f64 / 100.0) as i32
}

fn delicate_synthesis(
    state: CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
) -> CraftingState {
    // Delicate Synthesis Mastery (94) raises only the progress half.
    let progress_potency = if stats.level >= 94 { 150 } else { 100 };
    let progress_increase = calculate_progress(&state, stats, recipe, progress_potency);

    let effective_control = stats.effective_control(state.inner_quiet_stacks);
    let base_quality = formula::base_quality(
        effective_control,
        recipe.quality_divider,
        recipe.quality_modifier,
        stats.level,
        recipe.recipe_level,
    );
    let mut quality_efficiency = 100;
    if state.innovation_turns > 0 {
        quality_efficiency += 50;
    }
    if state.great_strides_turns > 0 {
        quality_efficiency += 100;
    }
    let quality_increase = condition_multiplier(
        ((base_quality * quality_efficiency) as f64 / 100.0) as i32,
        state.condition,
    );

    let mut new_progress = state.progress + progress_increase;
    let mut final_appraisal_turns = state.final_appraisal_turns;
    if final_appraisal_turns > 0 && new_progress >= state.max_progress {
        new_progress = state.max_progress - 1;
        final_appraisal_turns = 0;
    } else {
        new_progress = new_progress.min(state.max_progress);
    }

    let mut next = state;
    next.progress = new_progress;
    next.quality = (state.quality + quality_increase).min(state.max_quality);
    next.final_appraisal_turns = final_appraisal_turns;
    next.muscle_memory_turns = 0;
    next.great_strides_turns = 0;
    next.inner_quiet_stacks = (state.inner_quiet_stacks + 1).min(10);
    next
}

#[cfg(test)]
mod tests {
    use super::*;

    fn fixture() -> (CraftingState, CrafterStats, Recipe) {
        let stats = CrafterStats {
            level: 90,
            craftsmanship: 3800,
            control: 3600,
            cp: 600,
            is_specialist: false,
        };
        let recipe = Recipe {
            recipe_level: 580,
            difficulty: 3500,
            quality: 7200,
            durability: 70,
            progress_divider: 130,
            progress_modifier: 80,
            quality_divider: 115,
            quality_modifier: 70,
            ..Default::default()
        };
        (
            CraftingState::create_initial(&recipe, &stats),
            stats,
            recipe,
        )
    }

    #[test]
    fn basic_synthesis_adds_potency_scaled_progress() {
        let (state, stats, recipe) = fixture();
        let next = Action::BasicSynthesis.execute(state, &stats, &recipe, true);
        assert_eq!(next.progress, 282);
        assert_eq!(next.durability, 60);
    }

    #[test]
    fn traits_change_potency_with_crafter_level() {
        let (state, stats, recipe) = fixture();
        let low = CrafterStats { level: 30, ..stats };
        assert_eq!(
            Action::BasicSynthesis
                .execute(state, &low, &recipe, true)
                .progress,
            235
        );
        let high = CrafterStats { level: 90, ..stats };
        assert_eq!(
            Action::Groundwork
                .execute(state, &high, &recipe, true)
                .progress,
            846
        );
    }

    #[test]
    fn veneration_and_muscle_memory_stack_and_muscle_memory_is_consumed() {
        let (mut state, stats, recipe) = fixture();
        state.veneration_turns = 2;
        state.muscle_memory_turns = 3;
        let next = Action::CarefulSynthesis.execute(state, &stats, &recipe, true);
        assert_eq!(next.progress, 775);
        assert_eq!(next.muscle_memory_turns, 0);
    }

    #[test]
    fn muscle_memory_lasts_five_steps_from_the_next_action() {
        let (state, stats, recipe) = fixture();
        let next = Action::MuscleMemory.execute(state, &stats, &recipe, true);
        assert_eq!(next.muscle_memory_turns, 5);
    }

    #[test]
    fn final_appraisal_holds_progress_one_short() {
        let (mut state, stats, recipe) = fixture();
        state.progress = 3400;
        state.final_appraisal_turns = 2;
        let next = Action::Groundwork.execute(state, &stats, &recipe, true);
        assert_eq!(next.progress, 3499);
        assert_eq!(next.final_appraisal_turns, 0);
    }

    #[test]
    fn groundwork_halves_when_durability_is_short() {
        let (mut state, stats, recipe) = fixture();
        state.durability = 15;
        let next = Action::Groundwork.execute(state, &stats, &recipe, true);
        assert_eq!(next.progress, 423);
    }
}
