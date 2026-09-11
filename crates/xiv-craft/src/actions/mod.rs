//! The 36 crafting actions as one enum. Ids, names, level requirements and base costs are the
//! C# values; `execute` is `CraftingActionBase.Execute` step for step, and each category module
//! holds the `ApplyEffect` overrides.

mod buffs;
mod other;
mod progress;
mod quality;
mod repair;

use crate::fail_reason::FailReason;
use crate::state::{Condition, CrafterStats, CraftingState, CraftStatus, Recipe};

/// Persisted in saved rotations and macros; equal to the C# `ICraftingAction.Id`.
pub type ActionId = i32;

/// Discriminants are the C# `ActionCategory` ordinals.
#[repr(i32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ActionCategory {
    Progress = 0,
    Quality = 1,
    Buff = 2,
    Repair = 3,
    Other = 4,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum Action {
    BasicSynthesis,
    CarefulSynthesis,
    RapidSynthesis,
    FocusedSynthesis,
    Groundwork,
    IntensiveSynthesis,
    PrudentSynthesis,
    MuscleMemory,
    DelicateSynthesis,

    BasicTouch,
    StandardTouch,
    AdvancedTouch,
    HastyTouch,
    FocusedTouch,
    PreparatoryTouch,
    PrudentTouch,
    Reflect,
    ByregotsBlessing,
    TrainedFinesse,
    TrainedEye,

    InnerQuiet,
    WasteNot,
    WasteNotII,
    Veneration,
    Innovation,
    GreatStrides,
    Manipulation,
    FinalAppraisal,
    Observe,
    HeartAndSoul,
    TrainedPerfection,
    QuickInnovation,

    MastersMend,
    ImmaculateMend,

    TricksOfTheTrade,
    CarefulObservation,
}

/// Snapshot of one action against a state, as `ICraftingSimulator.GetAvailableActions` reports it.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct ActionAvailability {
    pub action: Action,
    pub fail_reason: Option<FailReason>,
    pub cp_cost: i32,
    pub durability_cost: i32,
    pub success_rate: i32,
}

impl ActionAvailability {
    pub fn is_available(&self) -> bool {
        self.fail_reason.is_none()
    }
}

struct Meta {
    id: ActionId,
    name: &'static str,
    in_game_name: &'static str,
    level: i32,
    category: ActionCategory,
    cp: i32,
    durability: i32,
    success: i32,
}

const fn meta(id: ActionId, name: &'static str, in_game_name: &'static str, level: i32, category: ActionCategory, cp: i32, durability: i32, success: i32) -> Meta {
    Meta { id, name, in_game_name, level, category, cp, durability, success }
}

impl Action {
    /// Every action in C# registry order.
    pub const ALL: [Action; 36] = [
        Action::BasicSynthesis,
        Action::CarefulSynthesis,
        Action::RapidSynthesis,
        Action::FocusedSynthesis,
        Action::Groundwork,
        Action::IntensiveSynthesis,
        Action::PrudentSynthesis,
        Action::MuscleMemory,
        Action::DelicateSynthesis,
        Action::BasicTouch,
        Action::StandardTouch,
        Action::AdvancedTouch,
        Action::HastyTouch,
        Action::FocusedTouch,
        Action::PreparatoryTouch,
        Action::PrudentTouch,
        Action::Reflect,
        Action::ByregotsBlessing,
        Action::TrainedFinesse,
        Action::TrainedEye,
        Action::InnerQuiet,
        Action::WasteNot,
        Action::WasteNotII,
        Action::Veneration,
        Action::Innovation,
        Action::GreatStrides,
        Action::Manipulation,
        Action::FinalAppraisal,
        Action::Observe,
        Action::HeartAndSoul,
        Action::TrainedPerfection,
        Action::QuickInnovation,
        Action::MastersMend,
        Action::ImmaculateMend,
        Action::TricksOfTheTrade,
        Action::CarefulObservation,
    ];

    const fn meta(self) -> Meta {
        use ActionCategory::*;
        match self {
            Action::BasicSynthesis => meta(100001, "BasicSynthesis", "Basic Synthesis", 1, Progress, 0, 10, 100),
            Action::CarefulSynthesis => meta(100203, "CarefulSynthesis", "Careful Synthesis", 62, Progress, 7, 10, 100),
            Action::RapidSynthesis => meta(100033, "RapidSynthesis", "Rapid Synthesis", 9, Progress, 0, 10, 50),
            Action::FocusedSynthesis => meta(100235, "FocusedSynthesis", "Focused Synthesis", 67, Progress, 5, 10, 50),
            Action::Groundwork => meta(100403, "Groundwork", "Groundwork", 72, Progress, 18, 20, 100),
            Action::IntensiveSynthesis => meta(100315, "IntensiveSynthesis", "Intensive Synthesis", 78, Progress, 6, 10, 100),
            Action::PrudentSynthesis => meta(100427, "PrudentSynthesis", "Prudent Synthesis", 88, Progress, 18, 5, 100),
            Action::MuscleMemory => meta(100379, "MuscleMemory", "Muscle Memory", 54, Progress, 6, 10, 100),
            Action::DelicateSynthesis => meta(100323, "DelicateSynthesis", "Delicate Synthesis", 76, Progress, 32, 10, 100),

            Action::BasicTouch => meta(100002, "BasicTouch", "Basic Touch", 5, Quality, 18, 10, 100),
            Action::StandardTouch => meta(100004, "StandardTouch", "Standard Touch", 18, Quality, 32, 10, 100),
            Action::AdvancedTouch => meta(100411, "AdvancedTouch", "Advanced Touch", 68, Quality, 46, 10, 100),
            Action::HastyTouch => meta(100355, "HastyTouch", "Hasty Touch", 9, Quality, 0, 10, 60),
            Action::FocusedTouch => meta(100243, "FocusedTouch", "Focused Touch", 68, Quality, 18, 10, 50),
            Action::PreparatoryTouch => meta(100299, "PreparatoryTouch", "Preparatory Touch", 71, Quality, 40, 20, 100),
            Action::PrudentTouch => meta(100227, "PrudentTouch", "Prudent Touch", 66, Quality, 25, 5, 100),
            Action::Reflect => meta(100387, "Reflect", "Reflect", 69, Quality, 6, 10, 100),
            Action::ByregotsBlessing => meta(100339, "ByregotsBlessing", "Byregot's Blessing", 50, Quality, 24, 10, 100),
            Action::TrainedFinesse => meta(100435, "TrainedFinesse", "Trained Finesse", 90, Quality, 32, 0, 100),
            Action::TrainedEye => meta(100283, "TrainedEye", "Trained Eye", 80, Quality, 250, 0, 100),

            Action::InnerQuiet => meta(100252, "InnerQuiet", "Inner Quiet", 11, Buff, 18, 0, 100),
            Action::WasteNot => meta(100279, "WasteNot", "Waste Not", 15, Buff, 56, 0, 100),
            Action::WasteNotII => meta(100285, "WasteNotII", "Waste Not II", 47, Buff, 98, 0, 100),
            Action::Veneration => meta(100297, "Veneration", "Veneration", 15, Buff, 18, 0, 100),
            Action::Innovation => meta(100209, "Innovation", "Innovation", 26, Buff, 18, 0, 100),
            Action::GreatStrides => meta(100260, "GreatStrides", "Great Strides", 21, Buff, 32, 0, 100),
            Action::Manipulation => meta(100278, "Manipulation", "Manipulation", 65, Buff, 96, 0, 100),
            Action::FinalAppraisal => meta(100364, "FinalAppraisal", "Final Appraisal", 42, Buff, 1, 0, 100),
            Action::Observe => meta(100010, "Observe", "Observe", 13, Buff, 7, 0, 100),
            Action::HeartAndSoul => meta(100317, "HeartAndSoul", "Heart and Soul", 86, Buff, 0, 0, 100),
            Action::TrainedPerfection => meta(100451, "TrainedPerfection", "Trained Perfection", 100, Buff, 0, 0, 100),
            Action::QuickInnovation => meta(100443, "QuickInnovation", "Quick Innovation", 96, Buff, 0, 0, 100),

            Action::MastersMend => meta(100003, "MastersMend", "Master's Mend", 7, Repair, 88, 0, 100),
            Action::ImmaculateMend => meta(100439, "ImmaculateMend", "Immaculate Mend", 98, Repair, 112, 0, 100),

            Action::TricksOfTheTrade => meta(100371, "TricksOfTheTrade", "Tricks of the Trade", 13, Other, 0, 0, 100),
            Action::CarefulObservation => meta(100395, "CarefulObservation", "Careful Observation", 55, Other, 0, 0, 100),
        }
    }

    pub fn from_id(id: ActionId) -> Option<Action> {
        Action::ALL.iter().copied().find(|a| a.id() == id)
    }

    /// Case-insensitive lookup by the C# `Name` (the identifier form, e.g. `ByregotsBlessing`).
    pub fn from_name(name: &str) -> Option<Action> {
        Action::ALL.iter().copied().find(|a| a.name().eq_ignore_ascii_case(name))
    }

    pub const fn id(self) -> ActionId {
        self.meta().id
    }

    pub const fn name(self) -> &'static str {
        self.meta().name
    }

    pub const fn in_game_name(self) -> &'static str {
        self.meta().in_game_name
    }

    pub const fn level_requirement(self) -> i32 {
        self.meta().level
    }

    pub const fn category(self) -> ActionCategory {
        self.meta().category
    }

    pub const fn base_cp_cost(self) -> i32 {
        self.meta().cp
    }

    pub const fn base_durability_cost(self) -> i32 {
        self.meta().durability
    }

    pub const fn base_success_rate(self) -> i32 {
        self.meta().success
    }

    pub fn cp_cost(self, state: &CraftingState) -> i32 {
        let base = match self {
            Action::StandardTouch if state.combo_action == Action::BasicTouch.id() => 18,
            Action::AdvancedTouch if state.combo_action == Action::StandardTouch.id() => 18,
            _ => self.base_cp_cost(),
        };
        if state.condition == Condition::Pliant {
            (base as f64 / 2.0).ceil() as i32
        } else {
            base
        }
    }

    pub fn durability_cost(self, state: &CraftingState) -> i32 {
        let base = self.base_durability_cost();
        if base == 0 || state.trained_perfection_active {
            return 0;
        }
        let mut cost = base as f64;
        if state.waste_not_turns > 0 || state.waste_not_ii_turns > 0 {
            cost /= 2.0;
        }
        if state.condition == Condition::Sturdy {
            cost /= 2.0;
        }
        cost.ceil() as i32
    }

    pub fn success_rate(self, state: &CraftingState) -> i32 {
        if matches!(self, Action::FocusedSynthesis | Action::FocusedTouch) && state.observe_active {
            return 100;
        }
        let mut rate = self.base_success_rate();
        if state.condition == Condition::Centered {
            rate += 25;
        }
        rate.min(100)
    }

    /// The action-specific gate first, then the shared checks, in the C# override order.
    pub fn fail_reason(self, state: &CraftingState, stats: &CrafterStats, recipe: &Recipe) -> Option<FailReason> {
        let good_or_excellent = matches!(state.condition, Condition::Good | Condition::Excellent);
        let waste_not_active = state.waste_not_turns > 0 || state.waste_not_ii_turns > 0;

        let specific = match self {
            Action::IntensiveSynthesis | Action::TricksOfTheTrade
                if !good_or_excellent && !state.heart_and_soul_active =>
            {
                Some(FailReason::RequiresGoodOrExcellent)
            }
            Action::MuscleMemory | Action::Reflect if !state.is_first_step => Some(FailReason::FirstActionOnly),
            Action::PrudentSynthesis | Action::PrudentTouch if waste_not_active => Some(FailReason::WasteNotActive),
            Action::ByregotsBlessing if state.inner_quiet_stacks <= 0 => Some(FailReason::RequiresInnerQuiet),
            Action::TrainedFinesse if state.inner_quiet_stacks < 10 => Some(FailReason::RequiresTenInnerQuiet),
            Action::TrainedEye => {
                if !state.is_first_step {
                    Some(FailReason::FirstActionOnly)
                } else if recipe.is_expert {
                    Some(FailReason::NotOnExpert)
                } else if recipe.class_job_level <= 0 {
                    Some(FailReason::RecipeLevelUnavailable)
                } else if stats.level < recipe.class_job_level + 10 {
                    Some(FailReason::RequiresCrafterLevel(recipe.class_job_level + 10))
                } else {
                    None
                }
            }
            Action::HeartAndSoul => {
                if state.heart_and_soul_used {
                    Some(FailReason::HeartAndSoulOnce)
                } else if !recipe.is_expert {
                    Some(FailReason::ExpertOnly)
                } else {
                    None
                }
            }
            Action::QuickInnovation => {
                if state.quick_innovation_used {
                    Some(FailReason::QuickInnovationOnce)
                } else if state.innovation_turns > 0 {
                    Some(FailReason::InnovationActive)
                } else {
                    None
                }
            }
            Action::TrainedPerfection if state.trained_perfection_used => Some(FailReason::TrainedPerfectionOnce),
            Action::CarefulObservation => {
                if state.careful_observation_count >= 3 {
                    Some(FailReason::CarefulObservationLimit)
                } else if !recipe.is_expert {
                    Some(FailReason::ExpertOnly)
                } else {
                    None
                }
            }
            _ => None,
        };
        if specific.is_some() {
            return specific;
        }

        if state.status != CraftStatus::InProgress {
            return Some(FailReason::NotInProgress);
        }
        if stats.level < self.level_requirement() {
            return Some(FailReason::RequiresLevel(self.level_requirement()));
        }
        if state.cp < self.cp_cost(state) {
            return Some(FailReason::NotEnoughCp);
        }
        None
    }

    pub fn availability(self, state: &CraftingState, stats: &CrafterStats, recipe: &Recipe) -> ActionAvailability {
        ActionAvailability {
            action: self,
            fail_reason: self.fail_reason(state, stats, recipe),
            cp_cost: self.cp_cost(state),
            durability_cost: self.durability_cost(state),
            success_rate: self.success_rate(state),
        }
    }

    /// `CraftingActionBase.Execute`. Gating is the caller's job; this only applies the action.
    pub fn execute(self, mut state: CraftingState, stats: &CrafterStats, recipe: &Recipe, succeeded: bool) -> CraftingState {
        state.cp -= self.cp_cost(&state);

        if succeeded {
            state = self.apply_effect(state, stats, recipe);
        }

        state.durability -= self.durability_cost(&state);

        if state.trained_perfection_active && self.base_durability_cost() > 0 {
            state.trained_perfection_active = false;
        }

        // Manipulation heals after the tick but is judged on the pre-tick count, so its last turn
        // still restores durability.
        let manipulation_before_tick = state.manipulation_turns;
        state = state.with_buff_tick();
        if manipulation_before_tick > 0 && state.durability > 0 {
            state.durability = (state.durability + 5).min(state.max_durability);
        }

        state.step += 1;
        state.is_first_step = false;
        state.combo_action = if succeeded { self.combo_action_after() } else { 0 };
        state.observe_active = succeeded && self == Action::Observe;

        if state.progress >= state.max_progress {
            state.status = CraftStatus::Completed;
        } else if state.durability <= 0 {
            state.status = CraftStatus::Failed;
        }

        state
    }

    fn combo_action_after(self) -> i32 {
        match self {
            Action::BasicTouch | Action::StandardTouch => self.id(),
            _ => 0,
        }
    }

    fn apply_effect(self, state: CraftingState, stats: &CrafterStats, recipe: &Recipe) -> CraftingState {
        match self.category() {
            ActionCategory::Progress => progress::apply_effect(self, state, stats, recipe),
            ActionCategory::Quality => quality::apply_effect(self, state, stats, recipe),
            ActionCategory::Buff => buffs::apply_effect(self, state, stats, recipe),
            ActionCategory::Repair => repair::apply_effect(self, state, stats, recipe),
            ActionCategory::Other => other::apply_effect(self, state, stats, recipe),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn ids_are_unique_and_round_trip() {
        for action in Action::ALL {
            assert_eq!(Action::from_id(action.id()), Some(action));
            assert_eq!(Action::from_name(action.name()), Some(action));
            assert_eq!(Action::from_name(&action.name().to_ascii_lowercase()), Some(action));
        }
        assert_eq!(Action::from_id(999999), None);
    }

    #[test]
    fn pliant_halves_cp_rounding_up() {
        let state = CraftingState { condition: Condition::Pliant, ..Default::default() };
        assert_eq!(Action::CarefulSynthesis.cp_cost(&state), 4);
        assert_eq!(Action::BasicSynthesis.cp_cost(&state), 0);
    }

    #[test]
    fn combo_discounts_apply_before_pliant() {
        let mut state = CraftingState { combo_action: Action::BasicTouch.id(), ..Default::default() };
        assert_eq!(Action::StandardTouch.cp_cost(&state), 18);
        state.condition = Condition::Pliant;
        assert_eq!(Action::StandardTouch.cp_cost(&state), 9);
        state.combo_action = 0;
        assert_eq!(Action::StandardTouch.cp_cost(&state), 16);
    }

    #[test]
    fn durability_halves_stack_and_round_up() {
        let mut state = CraftingState { waste_not_turns: 1, ..Default::default() };
        assert_eq!(Action::PrudentTouch.durability_cost(&state), 3);
        state.condition = Condition::Sturdy;
        assert_eq!(Action::PrudentTouch.durability_cost(&state), 2);
        state.trained_perfection_active = true;
        assert_eq!(Action::Groundwork.durability_cost(&state), 0);
    }

    #[test]
    fn centered_caps_success_at_100_and_observe_guarantees_focused() {
        let mut state = CraftingState { condition: Condition::Centered, ..Default::default() };
        assert_eq!(Action::RapidSynthesis.success_rate(&state), 75);
        assert_eq!(Action::BasicTouch.success_rate(&state), 100);
        state.condition = Condition::Normal;
        state.observe_active = true;
        assert_eq!(Action::FocusedTouch.success_rate(&state), 100);
    }

    #[test]
    fn specific_gate_precedes_level_and_cp() {
        let state = CraftingState { status: CraftStatus::InProgress, cp: 0, is_first_step: false, ..Default::default() };
        let stats = CrafterStats { level: 10, ..Default::default() };
        let recipe = Recipe::default();
        assert_eq!(Action::MuscleMemory.fail_reason(&state, &stats, &recipe), Some(FailReason::FirstActionOnly));
        assert_eq!(Action::CarefulSynthesis.fail_reason(&state, &stats, &recipe), Some(FailReason::RequiresLevel(62)));
        assert_eq!(Action::BasicTouch.fail_reason(&state, &stats, &recipe), Some(FailReason::NotEnoughCp));
        assert_eq!(Action::BasicSynthesis.fail_reason(&state, &stats, &recipe), None);
    }
}
