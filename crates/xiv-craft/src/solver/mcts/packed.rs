//! `CraftingState` squeezed into 24 bytes for tree nodes. The maxima (progress, quality,
//! durability, CP) are constant for a solve and live once in the arena; everything else fits a
//! `u16` or `u8` for any reachable state, and packing asserts that rather than truncating.

use crate::actions::Action;
use crate::state::{Condition, CraftStatus, CraftingState};

const HEART_AND_SOUL_ACTIVE: u8 = 1 << 0;
const TRAINED_PERFECTION_ACTIVE: u8 = 1 << 1;
const QUICK_INNOVATION_USED: u8 = 1 << 2;
const HEART_AND_SOUL_USED: u8 = 1 << 3;
const TRAINED_PERFECTION_USED: u8 = 1 << 4;
const IS_FIRST_STEP: u8 = 1 << 5;
const OBSERVE_ACTIVE: u8 = 1 << 6;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Default)]
pub struct PackedState {
    progress: u16,
    quality: u16,
    durability: i16,
    cp: i16,
    step: u16,
    condition: u8,
    status: u8,
    inner_quiet_stacks: u8,
    careful_observation_count: u8,
    flags: u8,
    combo: u8,
    turns: [u8; 8],
}

/// A state value outside the packed representation's range.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct PackError {
    pub field: &'static str,
    pub value: i32,
}

impl core::fmt::Display for PackError {
    fn fmt(&self, f: &mut core::fmt::Formatter<'_>) -> core::fmt::Result {
        write!(
            f,
            "{} = {} does not fit the packed state",
            self.field, self.value
        )
    }
}

impl std::error::Error for PackError {}

fn narrow<T: TryFrom<i32>>(value: i32, field: &'static str) -> Result<T, PackError> {
    T::try_from(value).map_err(|_| PackError { field, value })
}

impl PackedState {
    /// [`PackedState::try_pack`] for the states a begun search produces, which always fit (see
    /// `MctsSolver::begin`); a state that does not is a broken invariant, not an input error.
    pub fn pack(state: &CraftingState) -> PackedState {
        PackedState::try_pack(state).unwrap_or_else(|e| panic!("{e}"))
    }

    /// Packs a state, or says which field does not fit. Every state a search reaches from a
    /// packable initial state is packable too: the engine caps progress, quality, durability and
    /// CP at the recipe's and the crafter's maxima.
    pub fn try_pack(state: &CraftingState) -> Result<PackedState, PackError> {
        let mut flags = 0;
        for (on, bit) in [
            (state.heart_and_soul_active, HEART_AND_SOUL_ACTIVE),
            (state.trained_perfection_active, TRAINED_PERFECTION_ACTIVE),
            (state.quick_innovation_used, QUICK_INNOVATION_USED),
            (state.heart_and_soul_used, HEART_AND_SOUL_USED),
            (state.trained_perfection_used, TRAINED_PERFECTION_USED),
            (state.is_first_step, IS_FIRST_STEP),
            (state.observe_active, OBSERVE_ACTIVE),
        ] {
            if on {
                flags |= bit;
            }
        }
        let combo = match state.combo_action {
            0 => 0,
            id if id == Action::BasicTouch.id() => 1,
            id if id == Action::StandardTouch.id() => 2,
            other => panic!("combo_action {other} is not a combo touch"),
        };
        Ok(PackedState {
            progress: narrow(state.progress, "progress")?,
            quality: narrow(state.quality, "quality")?,
            durability: narrow(state.durability, "durability")?,
            cp: narrow(state.cp, "cp")?,
            step: narrow(state.step, "step")?,
            condition: state.condition as u8,
            status: state.status as u8,
            inner_quiet_stacks: narrow(state.inner_quiet_stacks, "inner_quiet_stacks")?,
            careful_observation_count: narrow(
                state.careful_observation_count,
                "careful_observation_count",
            )?,
            flags,
            combo,
            turns: [
                narrow(state.waste_not_turns, "waste_not_turns")?,
                narrow(state.waste_not_ii_turns, "waste_not_ii_turns")?,
                narrow(state.veneration_turns, "veneration_turns")?,
                narrow(state.innovation_turns, "innovation_turns")?,
                narrow(state.great_strides_turns, "great_strides_turns")?,
                narrow(state.manipulation_turns, "manipulation_turns")?,
                narrow(state.final_appraisal_turns, "final_appraisal_turns")?,
                narrow(state.muscle_memory_turns, "muscle_memory_turns")?,
            ],
        })
    }

    /// `base` supplies the four maxima, which the pack does not carry.
    pub fn unpack(&self, base: &CraftingState) -> CraftingState {
        let flag = |bit: u8| self.flags & bit != 0;
        CraftingState {
            progress: self.progress as i32,
            max_progress: base.max_progress,
            quality: self.quality as i32,
            max_quality: base.max_quality,
            durability: self.durability as i32,
            max_durability: base.max_durability,
            cp: self.cp as i32,
            max_cp: base.max_cp,
            step: self.step as i32,
            condition: Condition::from_i32(self.condition as i32).expect("packed condition"),
            status: CraftStatus::from_i32(self.status as i32).expect("packed status"),
            inner_quiet_stacks: self.inner_quiet_stacks as i32,
            waste_not_turns: self.turns[0] as i32,
            waste_not_ii_turns: self.turns[1] as i32,
            veneration_turns: self.turns[2] as i32,
            innovation_turns: self.turns[3] as i32,
            great_strides_turns: self.turns[4] as i32,
            manipulation_turns: self.turns[5] as i32,
            final_appraisal_turns: self.turns[6] as i32,
            muscle_memory_turns: self.turns[7] as i32,
            heart_and_soul_active: flag(HEART_AND_SOUL_ACTIVE),
            trained_perfection_active: flag(TRAINED_PERFECTION_ACTIVE),
            quick_innovation_used: flag(QUICK_INNOVATION_USED),
            heart_and_soul_used: flag(HEART_AND_SOUL_USED),
            trained_perfection_used: flag(TRAINED_PERFECTION_USED),
            careful_observation_count: self.careful_observation_count as i32,
            is_first_step: flag(IS_FIRST_STEP),
            combo_action: match self.combo {
                0 => 0,
                1 => Action::BasicTouch.id(),
                _ => Action::StandardTouch.id(),
            },
            observe_active: flag(OBSERVE_ACTIVE),
        }
    }

    pub fn is_in_progress(&self) -> bool {
        self.status == CraftStatus::InProgress as u8
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::state::{CrafterStats, Recipe};

    #[test]
    fn packed_state_is_24_bytes() {
        assert_eq!(core::mem::size_of::<PackedState>(), 24);
    }

    #[test]
    fn round_trips_every_state_of_a_specialist_walk() {
        let recipe = Recipe {
            recipe_level: 690,
            class_job_level: 100,
            difficulty: 3800,
            quality: 12000,
            durability: 70,
            progress_divider: 130,
            progress_modifier: 80,
            quality_divider: 115,
            quality_modifier: 70,
            ..Recipe::default()
        };
        let stats = CrafterStats {
            level: 100,
            craftsmanship: 4500,
            control: 4400,
            cp: 700,
            is_specialist: true,
        };
        let base = CraftingState::create_initial(&recipe, &stats);
        let walk = [
            Action::Reflect,
            Action::HeartAndSoul,
            Action::Manipulation,
            Action::WasteNotII,
            Action::Innovation,
            Action::PreparatoryTouch,
            Action::BasicTouch,
            Action::StandardTouch,
            Action::AdvancedTouch,
            Action::TrainedPerfection,
            Action::GreatStrides,
            Action::ByregotsBlessing,
            Action::Observe,
            Action::FocusedSynthesis,
            Action::CarefulObservation,
            Action::QuickInnovation,
            Action::Veneration,
            Action::Groundwork,
            Action::Groundwork,
            Action::Groundwork,
        ];
        let mut state = base;
        for action in walk {
            let packed = PackedState::pack(&state);
            assert_eq!(packed.unpack(&base), state, "before {action:?}");
            let mut forced_good = state;
            forced_good.condition = Condition::Good;
            assert_eq!(PackedState::pack(&forced_good).unpack(&base), forced_good);
            if action.fail_reason(&state, &stats, &recipe).is_none() {
                state = action.execute(state, &stats, &recipe, true);
            }
        }
        assert_eq!(PackedState::pack(&state).unpack(&base), state);
    }

    #[test]
    fn refuses_values_that_do_not_fit() {
        let error = PackedState::try_pack(&CraftingState {
            progress: 70_000,
            ..CraftingState::default()
        })
        .unwrap_err();
        assert_eq!(
            error,
            PackError {
                field: "progress",
                value: 70_000
            }
        );
        assert_eq!(
            error.to_string(),
            "progress = 70000 does not fit the packed state"
        );
    }
}
