//! `RecipeDifficultyClassifier`: the bucket a recipe falls in for a crafter, and the time budget
//! the C# API picks for it.

use crate::state::{CrafterStats, Recipe};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum RecipeDifficulty {
    Trivial,
    Easy,
    Standard,
    Hard,
    Expert,
}

pub fn classify(recipe: &Recipe, stats: &CrafterStats) -> RecipeDifficulty {
    if recipe.is_expert {
        return RecipeDifficulty::Expert;
    }
    if stats.level >= recipe.class_job_level + 10 && recipe.stars == 0 {
        return RecipeDifficulty::Trivial;
    }
    if stats.level >= recipe.class_job_level + 5 || recipe.stars == 0 {
        return RecipeDifficulty::Easy;
    }
    if recipe.stars >= 3 || recipe.durability <= 40 {
        return RecipeDifficulty::Hard;
    }
    RecipeDifficulty::Standard
}

pub fn adaptive_time_budget_ms(difficulty: RecipeDifficulty) -> f64 {
    match difficulty {
        RecipeDifficulty::Trivial => 1_000.0,
        RecipeDifficulty::Easy => 3_000.0,
        RecipeDifficulty::Standard => 5_000.0,
        RecipeDifficulty::Hard => 10_000.0,
        RecipeDifficulty::Expert => 15_000.0,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn recipe(class_job_level: i32, stars: i32, durability: i32) -> Recipe {
        Recipe {
            class_job_level,
            stars,
            durability,
            ..Recipe::default()
        }
    }

    fn crafter(level: i32) -> CrafterStats {
        CrafterStats {
            level,
            ..CrafterStats::default()
        }
    }

    #[test]
    fn buckets_follow_the_csharp_order() {
        let expert = Recipe {
            is_expert: true,
            ..recipe(90, 0, 70)
        };
        assert_eq!(classify(&expert, &crafter(100)), RecipeDifficulty::Expert);
        assert_eq!(
            classify(&recipe(80, 0, 70), &crafter(90)),
            RecipeDifficulty::Trivial
        );
        assert_eq!(
            classify(&recipe(80, 2, 70), &crafter(90)),
            RecipeDifficulty::Easy
        );
        assert_eq!(
            classify(&recipe(100, 0, 70), &crafter(100)),
            RecipeDifficulty::Easy
        );
        assert_eq!(
            classify(&recipe(100, 3, 70), &crafter(100)),
            RecipeDifficulty::Hard
        );
        assert_eq!(
            classify(&recipe(100, 1, 40), &crafter(100)),
            RecipeDifficulty::Hard
        );
        assert_eq!(
            classify(&recipe(100, 2, 70), &crafter(100)),
            RecipeDifficulty::Standard
        );
    }

    #[test]
    fn budgets_match_the_csharp_table() {
        assert_eq!(adaptive_time_budget_ms(RecipeDifficulty::Trivial), 1_000.0);
        assert_eq!(adaptive_time_budget_ms(RecipeDifficulty::Expert), 15_000.0);
    }
}
