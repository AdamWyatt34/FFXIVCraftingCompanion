use crate::actions::Action;
use crate::state::{Condition, CrafterStats, CraftingState, Recipe};

pub(super) fn apply_effect(
    action: Action,
    state: CraftingState,
    _stats: &CrafterStats,
    _recipe: &Recipe,
) -> CraftingState {
    let mut next = state;
    match action {
        Action::TricksOfTheTrade => {
            next.cp = (state.cp + 20).min(state.max_cp);
            // Heart and Soul is spent only when it substituted for the missing Good condition.
            if state.heart_and_soul_active
                && !matches!(state.condition, Condition::Good | Condition::Excellent)
            {
                next.heart_and_soul_active = false;
            }
        }
        Action::CarefulObservation => {
            next.careful_observation_count = state.careful_observation_count + 1
        }
        _ => unreachable!("{action:?} is not a special action"),
    }
    next
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
            is_specialist: false,
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
            is_expert: true,
            ..Default::default()
        };
        (
            CraftingState::create_initial(&recipe, &stats),
            stats,
            recipe,
        )
    }

    #[test]
    fn tricks_restores_cp_capped_and_spends_heart_and_soul_off_good() {
        let (mut state, stats, recipe) = fixture();
        state.cp = 690;
        state.heart_and_soul_active = true;
        let next = Action::TricksOfTheTrade.execute(state, &stats, &recipe, true);
        assert_eq!(next.cp, 700);
        assert!(!next.heart_and_soul_active);

        state.condition = Condition::Good;
        state.cp = 100;
        let kept = Action::TricksOfTheTrade.execute(state, &stats, &recipe, true);
        assert_eq!(kept.cp, 120);
        assert!(kept.heart_and_soul_active);
    }

    #[test]
    fn careful_observation_counts_uses_and_costs_nothing() {
        let (state, stats, recipe) = fixture();
        let next = Action::CarefulObservation.execute(state, &stats, &recipe, true);
        assert_eq!(next.careful_observation_count, 1);
        assert_eq!(next.cp, 700);
        assert_eq!(next.durability, 70);
        assert_eq!(next.step, 1);
    }
}
