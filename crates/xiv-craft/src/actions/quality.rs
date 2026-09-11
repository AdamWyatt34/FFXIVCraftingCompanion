use crate::actions::Action;
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
    let _ = (action, state, stats, recipe);
    todo!("quality actions are ported in checkpoint 3c")
}
