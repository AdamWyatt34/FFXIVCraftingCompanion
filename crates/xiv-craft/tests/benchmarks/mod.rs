//! Loader for the hand-maintained `tests/benchmarks/solver-benchmarks.json`,
//! shared by the benchmark tests and the bench example (which includes this file by path).

#![allow(dead_code)]

use serde::Deserialize;
use std::path::PathBuf;
use xiv_craft::{Action, CrafterStats, Recipe};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Dataset {
    pub benchmarks: Vec<Benchmark>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Benchmark {
    pub id: String,
    pub recipe: RecipeJson,
    pub stats: StatsJson,
    pub community_rotation: CommunityRotation,
    pub solver_targets: SolverTargets,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RecipeJson {
    pub recipe_id: i32,
    pub recipe_level: i32,
    pub class_job_level: i32,
    pub difficulty: i32,
    pub quality: i32,
    pub durability: i32,
    pub stars: i32,
    pub progress_divider: i32,
    pub progress_modifier: i32,
    pub quality_divider: i32,
    pub quality_modifier: i32,
    pub suggested_craftsmanship: i32,
    pub suggested_control: i32,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct StatsJson {
    pub level: i32,
    pub craftsmanship: i32,
    pub control: i32,
    pub cp: i32,
    pub is_specialist: bool,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CommunityRotation {
    pub actions: Vec<String>,
    pub expected_completion: bool,
    pub expected_min_quality_percent: i32,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SolverTargets {
    pub greedy_must_complete: bool,
    pub mcts_min_quality_percent_of_community: i32,
    pub mcts_time_budget_ms: i32,
}

impl Benchmark {
    pub fn recipe(&self) -> Recipe {
        let r = &self.recipe;
        Recipe {
            recipe_id: r.recipe_id,
            recipe_level: r.recipe_level,
            class_job_level: r.class_job_level,
            difficulty: r.difficulty,
            quality: r.quality,
            durability: r.durability,
            stars: r.stars,
            progress_divider: r.progress_divider,
            progress_modifier: r.progress_modifier,
            quality_divider: r.quality_divider,
            quality_modifier: r.quality_modifier,
            suggested_craftsmanship: r.suggested_craftsmanship,
            suggested_control: r.suggested_control,
            ..Recipe::default()
        }
    }

    pub fn stats(&self) -> CrafterStats {
        CrafterStats {
            level: self.stats.level,
            craftsmanship: self.stats.craftsmanship,
            control: self.stats.control,
            cp: self.stats.cp,
            is_specialist: self.stats.is_specialist,
        }
    }

    pub fn community_action_ids(&self) -> Vec<i32> {
        self.community_rotation
            .actions
            .iter()
            .map(|name| {
                Action::from_name(name)
                    .unwrap_or_else(|| panic!("unknown action name in benchmark: '{name}'"))
                    .id()
            })
            .collect()
    }
}

pub fn load() -> Vec<Benchmark> {
    let path =
        PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("tests/benchmarks/solver-benchmarks.json");
    let json =
        std::fs::read_to_string(&path).unwrap_or_else(|e| panic!("read {}: {e}", path.display()));
    let dataset: Dataset = serde_json::from_str(&json).expect("benchmark json");
    assert_eq!(dataset.benchmarks.len(), 10);
    dataset.benchmarks
}
