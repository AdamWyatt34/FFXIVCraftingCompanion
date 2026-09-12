/// Discriminants are the C# `CraftCondition` ordinals; goldens carry them as integers.
#[repr(i32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum Condition {
    #[default]
    Normal = 0,
    Good = 1,
    Excellent = 2,
    Poor = 3,
    Centered = 4,
    Sturdy = 5,
    Pliant = 6,
    Malleable = 7,
    Primed = 8,
    GoodOmen = 9,
}

impl Condition {
    pub const ALL: [Condition; 10] = [
        Condition::Normal,
        Condition::Good,
        Condition::Excellent,
        Condition::Poor,
        Condition::Centered,
        Condition::Sturdy,
        Condition::Pliant,
        Condition::Malleable,
        Condition::Primed,
        Condition::GoodOmen,
    ];

    pub fn from_i32(value: i32) -> Option<Condition> {
        Condition::ALL.iter().copied().find(|c| *c as i32 == value)
    }
}

/// Discriminants are the C# `CraftStatus` ordinals.
#[repr(i32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq, Default)]
pub enum CraftStatus {
    #[default]
    InProgress = 0,
    Completed = 1,
    Failed = 2,
}

impl CraftStatus {
    pub fn from_i32(value: i32) -> Option<CraftStatus> {
        match value {
            0 => Some(CraftStatus::InProgress),
            1 => Some(CraftStatus::Completed),
            2 => Some(CraftStatus::Failed),
            _ => None,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Default)]
pub struct CrafterStats {
    pub level: i32,
    pub craftsmanship: i32,
    pub control: i32,
    pub cp: i32,
    /// Gates the specialist abilities (Heart and Soul, Careful Observation, Quick Innovation).
    pub is_specialist: bool,
}

impl CrafterStats {
    pub fn effective_control(&self, inner_quiet_stacks: i32) -> i32 {
        (self.control as f64 * (1.0 + 0.1 * inner_quiet_stacks as f64)) as i32
    }
}

/// The C# `RecipeParameters` without its ingredient list, which only the HQ calculator reads
/// (see `hq.rs`); keeping the recipe `Copy` keeps the simulation path allocation-free.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Default)]
pub struct Recipe {
    pub recipe_id: i32,
    /// The recipe level (rlvl), not the class job level; the formulas depend on the distinction.
    pub recipe_level: i32,
    pub difficulty: i32,
    pub quality: i32,
    pub durability: i32,
    pub progress_divider: i32,
    pub progress_modifier: i32,
    pub quality_divider: i32,
    pub quality_modifier: i32,
    pub class_job_level: i32,
    pub stars: i32,
    pub is_expert: bool,
    pub conditions_flag: i32,
    pub suggested_craftsmanship: i32,
    pub suggested_control: i32,
    pub starting_quality: i32,
    pub material_quality_factor: i32,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Default)]
pub struct CraftingState {
    pub progress: i32,
    pub max_progress: i32,
    pub quality: i32,
    pub max_quality: i32,
    pub durability: i32,
    pub max_durability: i32,
    pub cp: i32,
    pub max_cp: i32,

    pub step: i32,
    pub condition: Condition,
    pub status: CraftStatus,

    pub inner_quiet_stacks: i32,
    pub waste_not_turns: i32,
    pub waste_not_ii_turns: i32,
    pub veneration_turns: i32,
    pub innovation_turns: i32,
    pub great_strides_turns: i32,
    pub manipulation_turns: i32,
    pub final_appraisal_turns: i32,
    pub muscle_memory_turns: i32,
    pub heart_and_soul_active: bool,
    pub trained_perfection_active: bool,
    pub quick_innovation_used: bool,
    pub heart_and_soul_used: bool,
    pub trained_perfection_used: bool,
    pub careful_observation_count: i32,

    pub is_first_step: bool,
    /// Id of the touch that set up a combo discount, 0 when none.
    pub combo_action: i32,
    pub observe_active: bool,
}

impl CraftingState {
    pub fn create_initial(recipe: &Recipe, stats: &CrafterStats) -> CraftingState {
        CraftingState {
            progress: 0,
            max_progress: recipe.difficulty,
            quality: recipe.starting_quality,
            max_quality: recipe.quality,
            durability: recipe.durability,
            max_durability: recipe.durability,
            cp: stats.cp,
            max_cp: stats.cp,
            step: 0,
            condition: Condition::Normal,
            status: CraftStatus::InProgress,
            is_first_step: true,
            ..CraftingState::default()
        }
    }

    pub fn is_complete(&self) -> bool {
        self.progress >= self.max_progress
    }

    pub fn is_failed(&self) -> bool {
        self.durability <= 0 && !self.is_complete()
    }

    pub fn quality_percent(&self) -> f64 {
        if self.max_quality > 0 {
            (self.quality as f64 / self.max_quality as f64 * 100.0).min(100.0)
        } else {
            0.0
        }
    }

    pub fn progress_percent(&self) -> f64 {
        if self.max_progress > 0 {
            (self.progress as f64 / self.max_progress as f64 * 100.0).min(100.0)
        } else {
            0.0
        }
    }

    pub fn hq_percent(&self) -> i32 {
        hq_percent(self.quality_percent())
    }

    pub fn with_buff_tick(mut self) -> CraftingState {
        self.waste_not_turns = (self.waste_not_turns - 1).max(0);
        self.waste_not_ii_turns = (self.waste_not_ii_turns - 1).max(0);
        self.veneration_turns = (self.veneration_turns - 1).max(0);
        self.innovation_turns = (self.innovation_turns - 1).max(0);
        self.great_strides_turns = (self.great_strides_turns - 1).max(0);
        self.manipulation_turns = (self.manipulation_turns - 1).max(0);
        self.final_appraisal_turns = (self.final_appraisal_turns - 1).max(0);
        self.muscle_memory_turns = (self.muscle_memory_turns - 1).max(0);
        self
    }
}

// The in-game HQ chance per whole percent of quality (0..100), as datamined by the community;
// it is a table, not a formula.
#[rustfmt::skip]
const HQ_CHANCE_BY_QUALITY_PERCENT: [i32; 101] = [
    1, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8,
    9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 15, 16, 16, 17, 17,
    17, 18, 18, 18, 19, 19, 20, 20, 21, 22, 23, 24, 26, 28, 31, 34, 38, 42, 47, 52, 58, 64, 68, 71,
    74, 76, 78, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 94, 96, 98, 100,
];

fn hq_percent(quality_percent: f64) -> i32 {
    let index = (quality_percent.floor() as i32).clamp(0, 100);
    HQ_CHANCE_BY_QUALITY_PERCENT[index as usize]
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn hq_table_endpoints_and_steep_middle() {
        assert_eq!(hq_percent(0.0), 1);
        assert_eq!(hq_percent(4.9), 1);
        assert_eq!(hq_percent(5.0), 2);
        assert_eq!(hq_percent(70.0), 28);
        assert_eq!(hq_percent(99.9), 98);
        assert_eq!(hq_percent(100.0), 100);
    }

    #[test]
    fn buff_tick_floors_at_zero() {
        let state = CraftingState {
            innovation_turns: 1,
            veneration_turns: 0,
            ..Default::default()
        };
        let ticked = state.with_buff_tick();
        assert_eq!(ticked.innovation_turns, 0);
        assert_eq!(ticked.veneration_turns, 0);
    }

    #[test]
    fn effective_control_truncates() {
        let stats = CrafterStats {
            control: 3600,
            ..Default::default()
        };
        assert_eq!(stats.effective_control(0), 3600);
        assert_eq!(stats.effective_control(3), 4680);
        assert_eq!(stats.effective_control(10), 7200);
    }
}
