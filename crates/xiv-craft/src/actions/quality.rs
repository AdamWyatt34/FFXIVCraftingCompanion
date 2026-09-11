use crate::actions::Action;
use crate::state::{CrafterStats, CraftingState, Recipe};

pub(super) fn apply_effect(action: Action, state: CraftingState, stats: &CrafterStats, recipe: &Recipe) -> CraftingState {
    let _ = (action, state, stats, recipe);
    todo!("quality actions are ported in checkpoint 3c")
}
