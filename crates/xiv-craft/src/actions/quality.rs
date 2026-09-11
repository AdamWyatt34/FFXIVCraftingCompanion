use crate::actions::Action;
use crate::formula;
use crate::state::{Condition, CrafterStats, CraftingState, Recipe};

/// The condition multiplier on a quality gain, truncating as the C# casts do.
pub(super) fn condition_multiplier(quality_increase: i32, condition: Condition) -> i32 {
    match condition {
        Condition::Good => (quality_increase as f64 * 1.5) as i32,
        Condition::Excellent => quality_increase * 4,
        Condition::Poor => (quality_increase as f64 * 0.5) as i32,
        _ => quality_increase,
    }
}

pub(super) fn apply_effect(action: Action, state: CraftingState, stats: &CrafterStats, recipe: &Recipe) -> CraftingState {
    match action {
        // Trained Eye replaces the whole effect: quality jumps to max and nothing else changes.
        Action::TrainedEye => {
            let mut next = state;
            next.quality = state.max_quality;
            next
        }
        Action::ByregotsBlessing => {
            let potency = 100 + state.inner_quiet_stacks * 20;
            let mut next = state;
            next.quality = (state.quality + calculate_quality(&state, stats, recipe, potency)).min(state.max_quality);
            next.great_strides_turns = 0;
            next.inner_quiet_stacks = 0;
            next
        }
        _ => {
            let (potency, inner_quiet_gain) = match action {
                Action::BasicTouch => (100, 1),
                Action::StandardTouch => (125, 1),
                Action::AdvancedTouch => (150, 1),
                Action::HastyTouch => (100, 1),
                Action::FocusedTouch => (150, 1),
                Action::PreparatoryTouch => (200, 2),
                Action::PrudentTouch => (100, 1),
                Action::Reflect => (300, 1),
                Action::TrainedFinesse => (100, 1),
                _ => unreachable!("{action:?} is not a quality action"),
            };
            let mut next = state;
            next.quality = (state.quality + calculate_quality(&state, stats, recipe, potency)).min(state.max_quality);
            next.great_strides_turns = 0;
            next.inner_quiet_stacks = (state.inner_quiet_stacks + inner_quiet_gain).min(10);
            next
        }
    }
}

fn calculate_quality(state: &CraftingState, stats: &CrafterStats, recipe: &Recipe, potency: i32) -> i32 {
    let effective_control = stats.effective_control(state.inner_quiet_stacks);
    let base = formula::base_quality(
        effective_control,
        recipe.quality_divider,
        recipe.quality_modifier,
        stats.level,
        recipe.recipe_level,
    );
    let mut efficiency = potency;
    if state.innovation_turns > 0 {
        efficiency += 50;
    }
    if state.great_strides_turns > 0 {
        efficiency += 100;
    }
    condition_multiplier(((base * efficiency) as f64 / 100.0) as i32, state.condition)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn fixture() -> (CraftingState, CrafterStats, Recipe) {
        let stats = CrafterStats { level: 90, craftsmanship: 3800, control: 3600, cp: 600, is_specialist: false };
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
        (CraftingState::create_initial(&recipe, &stats), stats, recipe)
    }

    #[test]
    fn basic_touch_adds_quality_and_one_inner_quiet_stack() {
        let (state, stats, recipe) = fixture();
        let next = Action::BasicTouch.execute(state, &stats, &recipe, true);
        assert_eq!(next.quality, 243);
        assert_eq!(next.inner_quiet_stacks, 1);
        assert_eq!(next.combo_action, Action::BasicTouch.id());
    }

    #[test]
    fn inner_quiet_raises_control_and_great_strides_is_consumed() {
        let (mut state, stats, recipe) = fixture();
        state.inner_quiet_stacks = 5;
        state.great_strides_turns = 2;
        state.innovation_turns = 2;
        let next = Action::PreparatoryTouch.execute(state, &stats, &recipe, true);
        assert_eq!(next.quality, 1232);
        assert_eq!(next.great_strides_turns, 0);
        assert_eq!(next.inner_quiet_stacks, 7);
    }

    #[test]
    fn byregots_scales_with_stacks_then_clears_them() {
        let (mut state, stats, recipe) = fixture();
        state.inner_quiet_stacks = 10;
        let next = Action::ByregotsBlessing.execute(state, &stats, &recipe, true);
        assert_eq!(next.quality, 1386);
        assert_eq!(next.inner_quiet_stacks, 0);
    }

    #[test]
    fn condition_multipliers_truncate() {
        assert_eq!(condition_multiplier(243, Condition::Good), 364);
        assert_eq!(condition_multiplier(243, Condition::Excellent), 972);
        assert_eq!(condition_multiplier(243, Condition::Poor), 121);
        assert_eq!(condition_multiplier(243, Condition::Pliant), 243);
    }
}
