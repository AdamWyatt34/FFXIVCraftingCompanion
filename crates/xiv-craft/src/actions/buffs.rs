use crate::actions::Action;
use crate::state::{Condition, CrafterStats, CraftingState, Recipe};

pub(super) fn apply_effect(action: Action, state: CraftingState, _stats: &CrafterStats, _recipe: &Recipe) -> CraftingState {
    let mut next = state;
    match action {
        // Inner Quiet is automatic in the current game; the action is kept as a no-op.
        Action::InnerQuiet | Action::Observe => {}
        Action::WasteNot => next.waste_not_turns = effective_duration(&state, 4),
        Action::WasteNotII => next.waste_not_ii_turns = effective_duration(&state, 8),
        Action::Veneration => next.veneration_turns = effective_duration(&state, 4),
        Action::Innovation => next.innovation_turns = effective_duration(&state, 4),
        Action::GreatStrides => next.great_strides_turns = effective_duration(&state, 3),
        Action::Manipulation => next.manipulation_turns = effective_duration(&state, 8),
        Action::FinalAppraisal => next.final_appraisal_turns = effective_duration(&state, 5),
        Action::HeartAndSoul => {
            next.heart_and_soul_active = true;
            next.heart_and_soul_used = true;
        }
        Action::TrainedPerfection => {
            next.trained_perfection_active = true;
            next.trained_perfection_used = true;
        }
        // Quick Innovation grants a fixed single turn; Primed does not extend it.
        Action::QuickInnovation => {
            next.innovation_turns = 1;
            next.quick_innovation_used = true;
        }
        _ => unreachable!("{action:?} is not a buff"),
    }
    next
}

fn effective_duration(state: &CraftingState, duration: i32) -> i32 {
    if state.condition == Condition::Primed {
        duration + 2
    } else {
        duration
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn fixture() -> (CraftingState, CrafterStats, Recipe) {
        let stats = CrafterStats { level: 100, craftsmanship: 4500, control: 4400, cp: 700, is_specialist: false };
        let recipe = Recipe {
            recipe_level: 690,
            difficulty: 3800,
            quality: 12000,
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
    fn buff_ticks_on_the_step_it_is_applied() {
        let (state, stats, recipe) = fixture();
        let next = Action::Innovation.execute(state, &stats, &recipe, true);
        assert_eq!(next.innovation_turns, 3);
        assert_eq!(next.cp, 682);
        assert_eq!(next.durability, 70);
    }

    #[test]
    fn primed_extends_duration_by_two() {
        let (mut state, stats, recipe) = fixture();
        state.condition = Condition::Primed;
        let next = Action::Manipulation.execute(state, &stats, &recipe, true);
        assert_eq!(next.manipulation_turns, 9);
    }

    #[test]
    fn manipulation_heals_from_its_first_step() {
        let (mut state, stats, recipe) = fixture();
        state.durability = 40;
        let next = Action::Manipulation.execute(state, &stats, &recipe, true);
        assert_eq!(next.durability, 45);
    }

    #[test]
    fn trained_perfection_survives_zero_cost_actions_and_is_spent_once() {
        let (state, stats, recipe) = fixture();
        let armed = Action::TrainedPerfection.execute(state, &stats, &recipe, true);
        assert!(armed.trained_perfection_active && armed.trained_perfection_used);
        let still_armed = Action::Veneration.execute(armed, &stats, &recipe, true);
        assert!(still_armed.trained_perfection_active);
        let spent = Action::Groundwork.execute(still_armed, &stats, &recipe, true);
        assert!(!spent.trained_perfection_active);
        assert_eq!(spent.durability, 70);
    }
}
