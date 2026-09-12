use crate::actions::Action;
use crate::state::{CrafterStats, CraftingState, Recipe};

pub(super) fn apply_effect(
    action: Action,
    state: CraftingState,
    _stats: &CrafterStats,
    _recipe: &Recipe,
) -> CraftingState {
    let mut next = state;
    match action {
        Action::MastersMend => next.durability = (state.durability + 30).min(state.max_durability),
        Action::ImmaculateMend => next.durability = state.max_durability,
        _ => unreachable!("{action:?} is not a repair action"),
    }
    next
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn masters_mend_restores_thirty_capped_at_max() {
        let stats = CrafterStats {
            level: 100,
            cp: 700,
            ..Default::default()
        };
        let recipe = Recipe {
            difficulty: 100,
            quality: 100,
            durability: 70,
            ..Default::default()
        };
        let mut state = CraftingState::create_initial(&recipe, &stats);
        state.durability = 25;
        assert_eq!(
            Action::MastersMend
                .execute(state, &stats, &recipe, true)
                .durability,
            55
        );
        state.durability = 50;
        assert_eq!(
            Action::MastersMend
                .execute(state, &stats, &recipe, true)
                .durability,
            70
        );
        state.durability = 5;
        assert_eq!(
            Action::ImmaculateMend
                .execute(state, &stats, &recipe, true)
                .durability,
            70
        );
    }
}
