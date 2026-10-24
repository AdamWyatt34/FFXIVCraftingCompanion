//! The crafting actions as one enum. Ids, names and costs are the C# values; `execute` mirrors `CraftingActionBase.Execute`.

mod buffs;
mod other;
mod progress;
mod quality;
mod repair;

use crate::fail_reason::FailReason;
use crate::state::{Condition, CraftStatus, CrafterStats, CraftingState, Recipe};

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
    PreciseTouch,
    RefinedTouch,
    DaringTouch,

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

/// Mirrors the row shape of the C# `ICraftingSimulator.GetAvailableActions`.
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

#[allow(clippy::too_many_arguments)]
const fn meta(
    id: ActionId,
    name: &'static str,
    in_game_name: &'static str,
    level: i32,
    category: ActionCategory,
    cp: i32,
    durability: i32,
    success: i32,
) -> Meta {
    Meta {
        id,
        name,
        in_game_name,
        level,
        category,
        cp,
        durability,
        success,
    }
}

impl Action {
    pub const COUNT: usize = 39;

    /// The recognized set in C# registry order, legacy ones included: every action a saved rotation may hold.
    /// `offered` is the subset a palette or solver picks.
    pub const ALL: [Action; Action::COUNT] = [
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
        Action::PreciseTouch,
        Action::RefinedTouch,
        Action::DaringTouch,
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

    #[rustfmt::skip]
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
            // Ids in our own range, not the game's 100128/100443/100451; see CLAUDE.md.
            Action::PreciseTouch => meta(100501, "PreciseTouch", "Precise Touch", 53, Quality, 18, 10, 100),
            Action::RefinedTouch => meta(100502, "RefinedTouch", "Refined Touch", 92, Quality, 24, 10, 100),
            Action::DaringTouch => meta(100503, "DaringTouch", "Daring Touch", 96, Quality, 0, 10, 60),

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

    pub fn offered() -> impl Iterator<Item = Action> {
        Action::ALL.into_iter().filter(|a| !a.is_legacy())
    }

    /// Resolves every recognized id, legacy ones included, so saved rotations still load.
    pub fn from_id(id: ActionId) -> Option<Action> {
        Action::ALL.iter().copied().find(|a| a.id() == id)
    }

    /// Position in [`Action::ALL`], for per-action tables and bitsets. The enum is declared in
    /// registry order, so this is the discriminant.
    pub const fn index(self) -> usize {
        self as usize
    }

    pub const fn from_index(index: usize) -> Action {
        Action::ALL[index]
    }

    /// Case-insensitive lookup by the C# `Name` (the identifier form, e.g. `ByregotsBlessing`).
    pub fn from_name(name: &str) -> Option<Action> {
        Action::ALL
            .iter()
            .copied()
            .find(|a| a.name().eq_ignore_ascii_case(name))
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

    /// Removed from the game but kept resolvable by id so saved rotations and macros still load; never offered or picked by a solver.
    pub const fn is_legacy(self) -> bool {
        matches!(
            self,
            Action::FocusedSynthesis | Action::FocusedTouch | Action::InnerQuiet
        )
    }

    /// The name a macro's `/ac` line uses: under Expedience the Hasty Touch slot becomes Daring Touch, which the game
    /// resolves by Hasty Touch's name (Raphael `raphael-data/src/locales.rs` macro_name; Teamcraft commit a21776a).
    pub const fn macro_name(self) -> &'static str {
        match self {
            Action::DaringTouch => Action::HastyTouch.in_game_name(),
            _ => self.in_game_name(),
        }
    }

    /// False for the abilities the game resolves without advancing the craft: no buff tick, no
    /// step count, first-step actions stay available afterwards.
    pub const fn consumes_step(self) -> bool {
        !matches!(
            self,
            Action::HeartAndSoul
                | Action::QuickInnovation
                | Action::CarefulObservation
                | Action::FinalAppraisal
        )
    }

    /// Whether the condition rolls again after this action. Careful Observation rerolls without
    /// consuming a step; that is its entire purpose.
    pub const fn advances_condition(self) -> bool {
        self.consumes_step() || matches!(self, Action::CarefulObservation)
    }

    pub const fn macro_wait_seconds(self) -> i32 {
        match self {
            Action::InnerQuiet
            | Action::WasteNot
            | Action::WasteNotII
            | Action::Veneration
            | Action::Innovation
            | Action::GreatStrides
            | Action::Manipulation
            | Action::FinalAppraisal => 2,
            _ => 3,
        }
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
        let mut rate = self.base_success_rate();
        if state.condition == Condition::Centered {
            rate += 25;
        }
        rate.min(100)
    }

    /// The action-specific gate first, then the shared checks, in the C# override order.
    pub fn fail_reason(
        self,
        state: &CraftingState,
        stats: &CrafterStats,
        recipe: &Recipe,
    ) -> Option<FailReason> {
        let good_or_excellent = matches!(state.condition, Condition::Good | Condition::Excellent);
        let waste_not_active = state.waste_not_turns > 0 || state.waste_not_ii_turns > 0;

        let specific = match self {
            Action::IntensiveSynthesis | Action::TricksOfTheTrade | Action::PreciseTouch
                if !good_or_excellent && !state.heart_and_soul_active =>
            {
                Some(FailReason::RequiresGoodOrExcellent)
            }
            Action::DaringTouch if !state.expedience_active => Some(FailReason::RequiresExpedience),
            // The slot is Daring Touch under Expedience (Raphael raphael-sim/src/actions.rs:1120-1123).
            Action::HastyTouch if state.expedience_active => {
                Some(FailReason::HastyTouchUnderExpedience)
            }
            Action::MuscleMemory | Action::Reflect if !state.is_first_step => {
                Some(FailReason::FirstActionOnly)
            }
            Action::PrudentSynthesis | Action::PrudentTouch if waste_not_active => {
                Some(FailReason::WasteNotActive)
            }
            Action::ByregotsBlessing if state.inner_quiet_stacks <= 0 => {
                Some(FailReason::RequiresInnerQuiet)
            }
            Action::TrainedFinesse if state.inner_quiet_stacks < 10 => {
                Some(FailReason::RequiresTenInnerQuiet)
            }
            Action::TrainedEye => {
                if !state.is_first_step {
                    Some(FailReason::FirstActionOnly)
                } else if recipe.is_expert {
                    Some(FailReason::NotOnExpert)
                } else if recipe.class_job_level <= 0 {
                    Some(FailReason::RecipeLevelUnavailable)
                } else if stats.level < recipe.class_job_level + 10 {
                    Some(FailReason::RequiresCrafterLevel(
                        recipe.class_job_level + 10,
                    ))
                } else {
                    None
                }
            }
            Action::HeartAndSoul => {
                if !stats.is_specialist {
                    Some(FailReason::RequiresSpecialist)
                } else if state.heart_and_soul_used {
                    Some(FailReason::HeartAndSoulOnce)
                } else {
                    None
                }
            }
            Action::QuickInnovation => {
                if !stats.is_specialist {
                    Some(FailReason::RequiresSpecialist)
                } else if state.quick_innovation_used {
                    Some(FailReason::QuickInnovationOnce)
                } else if state.innovation_turns > 0 {
                    Some(FailReason::InnovationActive)
                } else {
                    None
                }
            }
            Action::TrainedPerfection if state.trained_perfection_used => {
                Some(FailReason::TrainedPerfectionOnce)
            }
            Action::CarefulObservation => {
                if !stats.is_specialist {
                    Some(FailReason::RequiresSpecialist)
                } else if state.careful_observation_count >= 3 {
                    Some(FailReason::CarefulObservationLimit)
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

    pub fn availability(
        self,
        state: &CraftingState,
        stats: &CrafterStats,
        recipe: &Recipe,
    ) -> ActionAvailability {
        ActionAvailability {
            action: self,
            fail_reason: self.fail_reason(state, stats, recipe),
            cp_cost: self.cp_cost(state),
            durability_cost: self.durability_cost(state),
            success_rate: self.success_rate(state),
        }
    }

    /// `CraftingActionBase.Execute`. Gating is the caller's job; this only applies the action.
    pub fn execute(
        self,
        mut state: CraftingState,
        stats: &CrafterStats,
        recipe: &Recipe,
        succeeded: bool,
    ) -> CraftingState {
        // Costs and the combo token are read from the state the action was used on, before its
        // own effects land.
        let combo_before = state.combo_action;
        let durability_cost = self.durability_cost(&state);
        state.cp -= self.cp_cost(&state);

        if succeeded {
            state = self.apply_effect(state, stats, recipe);
        }

        state.durability -= durability_cost;

        if state.trained_perfection_active && self.base_durability_cost() > 0 {
            state.trained_perfection_active = false;
        }

        if self.consumes_step() {
            // Manipulation is judged before the tick so its final turn still restores durability.
            let manipulation_before_tick = state.manipulation_turns;
            state = state.with_buff_tick();
            if manipulation_before_tick > 0 && state.durability > 0 {
                state.durability = (state.durability + 5).min(state.max_durability);
            }

            state.step += 1;
            state.is_first_step = false;
            state.combo_action = if succeeded {
                self.combo_action_after(combo_before)
            } else {
                0
            };
            // Expedience lasts the one step after the Hasty Touch that granted it; step-free abilities keep it
            // (Raphael raphael-sim/src/effects.rs tick_down; Teamcraft simulation.ts tickBuffs/skipsBuffTicks).
            state.expedience_active = succeeded && self.grants_expedience(stats);
        } else {
            state.combo_action = 0;
        }

        // Granted buffs land after the tick so they last their full duration.
        if succeeded {
            state = self.apply_granted_buffs(state);
        }

        if state.progress >= state.max_progress {
            state.status = CraftStatus::Completed;
        } else if state.durability <= 0 {
            state.status = CraftStatus::Failed;
        }

        state
    }

    // Only a comboed Standard Touch or an Observe opens Advanced Touch's discount (Raphael
    // raphael-sim/src/actions.rs:158-172,236-280; Teamcraft quality/advanced-touch.ts hasCombo).
    fn combo_action_after(self, combo_before: i32) -> i32 {
        match self {
            Action::BasicTouch => self.id(),
            Action::StandardTouch if combo_before == Action::BasicTouch.id() => self.id(),
            Action::Observe => Action::StandardTouch.id(),
            _ => 0,
        }
    }

    // Raphael raphael-sim/src/actions.rs HastyTouch transform; Teamcraft quality/hasty-touch.ts execute.
    fn grants_expedience(self, stats: &CrafterStats) -> bool {
        self == Action::HastyTouch && stats.level >= Action::DaringTouch.level_requirement()
    }

    fn apply_effect(
        self,
        state: CraftingState,
        stats: &CrafterStats,
        recipe: &Recipe,
    ) -> CraftingState {
        match self.category() {
            ActionCategory::Progress => progress::apply_effect(self, state, stats, recipe),
            ActionCategory::Quality => quality::apply_effect(self, state, stats, recipe),
            ActionCategory::Buff => buffs::apply_effect(self, state, stats, recipe),
            ActionCategory::Repair => repair::apply_effect(self, state, stats, recipe),
            ActionCategory::Other => other::apply_effect(self, state, stats, recipe),
        }
    }

    fn apply_granted_buffs(self, state: CraftingState) -> CraftingState {
        match self.category() {
            ActionCategory::Progress => progress::apply_granted_buffs(self, state),
            ActionCategory::Buff => buffs::apply_granted_buffs(self, state),
            _ => state,
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
            assert_eq!(
                Action::from_name(&action.name().to_ascii_lowercase()),
                Some(action)
            );
        }
        assert_eq!(Action::from_id(999999), None);
    }

    #[test]
    fn index_is_the_registry_position() {
        for (position, action) in Action::ALL.iter().enumerate() {
            assert_eq!(action.index(), position);
            assert_eq!(Action::from_index(position), *action);
        }
    }

    #[test]
    fn pliant_halves_cp_rounding_up() {
        let state = CraftingState {
            condition: Condition::Pliant,
            ..Default::default()
        };
        assert_eq!(Action::CarefulSynthesis.cp_cost(&state), 4);
        assert_eq!(Action::BasicSynthesis.cp_cost(&state), 0);
    }

    #[test]
    fn combo_discounts_apply_before_pliant() {
        let mut state = CraftingState {
            combo_action: Action::BasicTouch.id(),
            ..Default::default()
        };
        assert_eq!(Action::StandardTouch.cp_cost(&state), 18);
        state.condition = Condition::Pliant;
        assert_eq!(Action::StandardTouch.cp_cost(&state), 9);
        state.combo_action = 0;
        assert_eq!(Action::StandardTouch.cp_cost(&state), 16);
    }

    #[test]
    fn advanced_touch_is_discounted_after_a_comboed_standard_touch_or_observe() {
        let stats = CrafterStats {
            level: 100,
            craftsmanship: 4000,
            control: 4000,
            cp: 900,
            is_specialist: false,
        };
        let recipe = Recipe {
            recipe_level: 690,
            class_job_level: 100,
            difficulty: 6600,
            quality: 12000,
            durability: 80,
            progress_divider: 170,
            progress_modifier: 90,
            quality_divider: 150,
            quality_modifier: 75,
            ..Default::default()
        };
        let costs = |actions: &[Action]| {
            let mut state = CraftingState::create_initial(&recipe, &stats);
            actions
                .iter()
                .map(|action| {
                    let before = state.cp;
                    state = action.execute(state, &stats, &recipe, true);
                    before - state.cp
                })
                .collect::<Vec<_>>()
        };
        assert_eq!(
            costs(&[Action::StandardTouch, Action::AdvancedTouch]),
            [32, 46]
        );
        assert_eq!(costs(&[Action::Observe, Action::AdvancedTouch]), [7, 18]);
        assert_eq!(
            costs(&[
                Action::BasicTouch,
                Action::StandardTouch,
                Action::AdvancedTouch
            ]),
            [18, 18, 18]
        );
        assert_eq!(
            costs(&[
                Action::Observe,
                Action::StandardTouch,
                Action::AdvancedTouch
            ]),
            [7, 32, 46]
        );

        let observed = Action::Observe.execute(
            CraftingState::create_initial(&recipe, &stats),
            &stats,
            &recipe,
            true,
        );
        assert_eq!(observed.combo_action, Action::StandardTouch.id());
    }

    #[test]
    fn durability_halves_stack_and_round_up() {
        let mut state = CraftingState {
            waste_not_turns: 1,
            ..Default::default()
        };
        assert_eq!(Action::PrudentTouch.durability_cost(&state), 3);
        state.condition = Condition::Sturdy;
        assert_eq!(Action::PrudentTouch.durability_cost(&state), 2);
        state.trained_perfection_active = true;
        assert_eq!(Action::Groundwork.durability_cost(&state), 0);
    }

    #[test]
    fn centered_caps_success_at_100() {
        let state = CraftingState {
            condition: Condition::Centered,
            ..Default::default()
        };
        assert_eq!(Action::RapidSynthesis.success_rate(&state), 75);
        assert_eq!(Action::DaringTouch.success_rate(&state), 85);
        assert_eq!(Action::BasicTouch.success_rate(&state), 100);
    }

    #[test]
    fn legacy_focused_actions_keep_their_base_rate_after_observe() {
        let stats = CrafterStats {
            level: 90,
            cp: 600,
            ..Default::default()
        };
        let recipe = Recipe {
            difficulty: 3500,
            quality: 7200,
            durability: 70,
            ..Default::default()
        };
        let observed = Action::Observe.execute(
            CraftingState::create_initial(&recipe, &stats),
            &stats,
            &recipe,
            true,
        );
        assert_eq!(Action::FocusedSynthesis.success_rate(&observed), 50);
        assert_eq!(Action::FocusedTouch.success_rate(&observed), 50);
    }

    #[test]
    fn legacy_actions_are_recognized_but_not_offered() {
        let legacy: Vec<Action> = Action::ALL.into_iter().filter(|a| a.is_legacy()).collect();
        assert_eq!(
            legacy,
            [
                Action::FocusedSynthesis,
                Action::FocusedTouch,
                Action::InnerQuiet
            ]
        );
        for action in &legacy {
            assert_eq!(Action::from_id(action.id()), Some(*action));
        }
        let offered: Vec<Action> = Action::offered().collect();
        assert_eq!(offered.len(), Action::COUNT - 3);
        assert!(offered.iter().all(|a| !a.is_legacy()));
    }

    #[test]
    fn new_touches_sit_after_trained_eye_with_our_ids() {
        let position = |action: Action| Action::ALL.iter().position(|a| *a == action);
        let trained_eye = position(Action::TrainedEye).unwrap();
        assert_eq!(position(Action::PreciseTouch), Some(trained_eye + 1));
        assert_eq!(position(Action::RefinedTouch), Some(trained_eye + 2));
        assert_eq!(position(Action::DaringTouch), Some(trained_eye + 3));
        assert_eq!(Action::PreciseTouch.id(), 100501);
        assert_eq!(Action::RefinedTouch.id(), 100502);
        assert_eq!(Action::DaringTouch.id(), 100503);
    }

    #[test]
    fn macro_name_is_the_in_game_name_except_daring_touch() {
        assert_eq!(Action::DaringTouch.macro_name(), "Hasty Touch");
        assert_eq!(Action::PreciseTouch.macro_name(), "Precise Touch");
        for action in Action::ALL {
            if action != Action::DaringTouch {
                assert_eq!(action.macro_name(), action.in_game_name());
            }
        }
    }

    #[test]
    fn specific_gate_precedes_level_and_cp() {
        let state = CraftingState {
            status: CraftStatus::InProgress,
            cp: 0,
            is_first_step: false,
            ..Default::default()
        };
        let stats = CrafterStats {
            level: 10,
            ..Default::default()
        };
        let recipe = Recipe::default();
        assert_eq!(
            Action::MuscleMemory.fail_reason(&state, &stats, &recipe),
            Some(FailReason::FirstActionOnly)
        );
        assert_eq!(
            Action::CarefulSynthesis.fail_reason(&state, &stats, &recipe),
            Some(FailReason::RequiresLevel(62))
        );
        assert_eq!(
            Action::BasicTouch.fail_reason(&state, &stats, &recipe),
            Some(FailReason::NotEnoughCp)
        );
        assert_eq!(
            Action::BasicSynthesis.fail_reason(&state, &stats, &recipe),
            None
        );
        assert_eq!(
            Action::PreciseTouch.fail_reason(&state, &stats, &recipe),
            Some(FailReason::RequiresGoodOrExcellent)
        );
        assert_eq!(
            Action::DaringTouch.fail_reason(&state, &stats, &recipe),
            Some(FailReason::RequiresExpedience)
        );
        assert_eq!(
            Action::RefinedTouch.fail_reason(&state, &stats, &recipe),
            Some(FailReason::RequiresLevel(92))
        );

        let gates_open = CraftingState {
            heart_and_soul_active: true,
            expedience_active: true,
            ..state
        };
        assert_eq!(
            Action::PreciseTouch.fail_reason(&gates_open, &stats, &recipe),
            Some(FailReason::RequiresLevel(53))
        );
        assert_eq!(
            Action::DaringTouch.fail_reason(&gates_open, &stats, &recipe),
            Some(FailReason::RequiresLevel(96))
        );
        assert_eq!(
            Action::HastyTouch.fail_reason(&gates_open, &stats, &recipe),
            Some(FailReason::HastyTouchUnderExpedience)
        );
    }

    #[test]
    fn step_free_abilities_are_exactly_the_four_instant_ones() {
        let step_free: Vec<Action> = Action::ALL
            .iter()
            .copied()
            .filter(|a| !a.consumes_step())
            .collect();
        assert_eq!(
            step_free,
            [
                Action::FinalAppraisal,
                Action::HeartAndSoul,
                Action::QuickInnovation,
                Action::CarefulObservation
            ]
        );
        assert!(Action::CarefulObservation.advances_condition());
        assert!(!Action::HeartAndSoul.advances_condition());
    }
}
