use crate::actions::Action;
use crate::state::{Condition, CrafterStats, CraftingState, Recipe};

// A recast replaces the running buff, so the old one must neither tick nor heal this step; the
// new duration lands in `apply_granted_buffs` after the tick.
pub(super) fn apply_effect(
    action: Action,
    state: CraftingState,
    _stats: &CrafterStats,
    _recipe: &Recipe,
) -> CraftingState {
    let mut next = state;
    match action {
        Action::InnerQuiet | Action::Observe | Action::QuickInnovation => {}
        Action::HeartAndSoul => {
            next.heart_and_soul_active = true;
            next.heart_and_soul_used = true;
        }
        Action::TrainedPerfection => {
            next.trained_perfection_active = true;
            next.trained_perfection_used = true;
        }
        _ => next = with_buff_turns(action, next, 0),
    }
    if action == Action::QuickInnovation {
        next.quick_innovation_used = true;
    }
    next
}

pub(super) fn apply_granted_buffs(action: Action, state: CraftingState) -> CraftingState {
    match action {
        Action::QuickInnovation => {
            let mut next = state;
            next.innovation_turns = 1;
            next
        }
        Action::WasteNot
        | Action::WasteNotII
        | Action::Veneration
        | Action::Innovation
        | Action::GreatStrides
        | Action::Manipulation
        | Action::FinalAppraisal => {
            let duration = effective_duration(&state, base_duration(action));
            with_buff_turns(action, state, duration)
        }
        _ => state,
    }
}

fn base_duration(action: Action) -> i32 {
    match action {
        Action::WasteNot | Action::Veneration | Action::Innovation => 4,
        Action::WasteNotII | Action::Manipulation => 8,
        Action::GreatStrides => 3,
        Action::FinalAppraisal => 5,
        _ => 0,
    }
}

fn with_buff_turns(action: Action, mut state: CraftingState, turns: i32) -> CraftingState {
    match action {
        // Waste Not and Waste Not II are one effect, so casting either replaces the other
        // (Teamcraft buff/waste-not.ts:35-37, waste-not-ii.ts:35-37).
        Action::WasteNot => {
            state.waste_not_turns = turns;
            state.waste_not_ii_turns = 0;
        }
        Action::WasteNotII => {
            state.waste_not_ii_turns = turns;
            state.waste_not_turns = 0;
        }
        Action::Veneration => state.veneration_turns = turns,
        Action::Innovation => state.innovation_turns = turns,
        Action::GreatStrides => state.great_strides_turns = turns,
        Action::Manipulation => state.manipulation_turns = turns,
        Action::FinalAppraisal => state.final_appraisal_turns = turns,
        _ => unreachable!("{action:?} has no timer"),
    }
    state
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
        let stats = CrafterStats {
            level: 100,
            craftsmanship: 4500,
            control: 4400,
            cp: 700,
            is_specialist: true,
        };
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
        (
            CraftingState::create_initial(&recipe, &stats),
            stats,
            recipe,
        )
    }

    #[test]
    fn buff_keeps_its_full_duration_on_the_cast_step() {
        let (state, stats, recipe) = fixture();
        let next = Action::Innovation.execute(state, &stats, &recipe, true);
        assert_eq!(next.innovation_turns, 4);
        assert_eq!(next.cp, 682);
    }

    #[test]
    fn waste_not_and_waste_not_ii_replace_each_other() {
        let (mut state, stats, recipe) = fixture();
        state.waste_not_ii_turns = 6;
        let waste_not = Action::WasteNot.execute(state, &stats, &recipe, true);
        assert_eq!(waste_not.waste_not_turns, 4);
        assert_eq!(waste_not.waste_not_ii_turns, 0);

        let waste_not_ii = Action::WasteNotII.execute(waste_not, &stats, &recipe, true);
        assert_eq!(waste_not_ii.waste_not_ii_turns, 8);
        assert_eq!(waste_not_ii.waste_not_turns, 0);
    }

    #[test]
    fn primed_extends_duration_by_two() {
        let (mut state, stats, recipe) = fixture();
        state.condition = Condition::Primed;
        let next = Action::Manipulation.execute(state, &stats, &recipe, true);
        assert_eq!(next.manipulation_turns, 10);
    }

    #[test]
    fn manipulation_does_not_heal_on_its_own_cast_or_recast() {
        let (mut state, stats, recipe) = fixture();
        state.durability = 40;
        let cast = Action::Manipulation.execute(state, &stats, &recipe, true);
        assert_eq!(cast.durability, 40);
        let after_action = Action::BasicSynthesis.execute(cast, &stats, &recipe, true);
        assert_eq!(after_action.durability, 35);
        assert_eq!(after_action.manipulation_turns, 7);
        let recast = Action::Manipulation.execute(after_action, &stats, &recipe, true);
        assert_eq!(recast.durability, 35);
        assert_eq!(recast.manipulation_turns, 8);
    }

    #[test]
    fn quick_innovation_and_heart_and_soul_take_no_step() {
        let (mut state, stats, recipe) = fixture();
        state.veneration_turns = 3;
        let quick = Action::QuickInnovation.execute(state, &stats, &recipe, true);
        assert_eq!(quick.innovation_turns, 1);
        assert_eq!(quick.step, 0);
        assert!(quick.is_first_step);
        assert_eq!(quick.veneration_turns, 3);
        let heart = Action::HeartAndSoul.execute(quick, &stats, &recipe, true);
        assert!(heart.heart_and_soul_active && heart.heart_and_soul_used);
        assert_eq!(heart.step, 0);
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
