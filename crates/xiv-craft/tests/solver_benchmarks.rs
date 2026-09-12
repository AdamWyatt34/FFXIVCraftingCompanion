//! The C# `SolverBenchmarks` theories over the hand-maintained
//! `tests/FFXIV.Tests/CraftingEngine/TestData/solver-benchmarks.json`. Greedy is deterministic, so
//! its quality and step count are asserted against the values the C# suite prints; that is the
//! solver-level parity check the goldens do not cover.

mod common;

use common::wall_clock;
use serde::Deserialize;
use std::path::PathBuf;
use xiv_craft::solver::greedy;
use xiv_craft::{simulate, Action, CrafterStats, GreedySolverConfig, Recipe, SolverConstraints};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Dataset {
    benchmarks: Vec<Benchmark>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Benchmark {
    id: String,
    recipe: RecipeJson,
    stats: StatsJson,
    community_rotation: CommunityRotation,
    solver_targets: SolverTargets,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct RecipeJson {
    recipe_id: i32,
    recipe_level: i32,
    class_job_level: i32,
    difficulty: i32,
    quality: i32,
    durability: i32,
    stars: i32,
    progress_divider: i32,
    progress_modifier: i32,
    quality_divider: i32,
    quality_modifier: i32,
    suggested_craftsmanship: i32,
    suggested_control: i32,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct StatsJson {
    level: i32,
    craftsmanship: i32,
    control: i32,
    cp: i32,
    is_specialist: bool,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct CommunityRotation {
    actions: Vec<String>,
    expected_completion: bool,
    expected_min_quality_percent: i32,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct SolverTargets {
    greedy_must_complete: bool,
}

impl Benchmark {
    fn recipe(&self) -> Recipe {
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

    fn stats(&self) -> CrafterStats {
        CrafterStats {
            level: self.stats.level,
            craftsmanship: self.stats.craftsmanship,
            control: self.stats.control,
            cp: self.stats.cp,
            is_specialist: self.stats.is_specialist,
        }
    }

    fn community_action_ids(&self) -> Vec<i32> {
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

fn load() -> Vec<Benchmark> {
    let path = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../../tests/FFXIV.Tests/CraftingEngine/TestData/solver-benchmarks.json");
    let json =
        std::fs::read_to_string(&path).unwrap_or_else(|e| panic!("read {}: {e}", path.display()));
    let dataset: Dataset = serde_json::from_str(&json).expect("benchmark json");
    assert_eq!(dataset.benchmarks.len(), 10);
    dataset.benchmarks
}

#[test]
fn community_rotation_completes_with_expected_quality() {
    for entry in load() {
        let result = simulate(
            &entry.recipe(),
            &entry.stats(),
            &entry.community_action_ids(),
        );
        if entry.community_rotation.expected_completion {
            assert!(
                result.success(),
                "[{}] community rotation completes",
                entry.id
            );
        }
        assert!(
            result.final_quality_percent()
                >= entry.community_rotation.expected_min_quality_percent as f64,
            "[{}] community quality {:.1}% below {}%",
            entry.id,
            result.final_quality_percent(),
            entry.community_rotation.expected_min_quality_percent
        );
    }
}

#[test]
fn greedy_completes_all_benchmarks() {
    for entry in load() {
        let result = greedy::solve(
            &entry.recipe(),
            &entry.stats(),
            &SolverConstraints::default(),
            &GreedySolverConfig::default(),
            &mut wall_clock(),
        );
        assert_eq!(
            result.status,
            xiv_craft::SolverStatus::Completed,
            "[{}]",
            entry.id
        );
        if entry.solver_targets.greedy_must_complete {
            assert!(
                result.simulation.as_ref().expect("simulation").success(),
                "[{}] greedy completes",
                entry.id
            );
        }
    }
}

/// Quality (to one decimal, as the C# suite prints it) and step count of the C# greedy solver on
/// each benchmark. Greedy has no randomness, so any drift here is a porting bug.
const CSHARP_GREEDY: [(&str, f64, usize); 10] = [
    ("lv100_4star_endgame", 41.3, 17),
    ("lv90_3star_standard", 57.3, 18),
    ("lv80_2star_overgeared", 100.0, 3),
    ("lv70_standard", 45.7, 16),
    ("lv60_standard", 86.2, 16),
    ("lv50_classic", 64.4, 12),
    ("lv90_35dur_food", 64.3, 13),
    ("lv100_40dur_potion", 48.3, 19),
    ("lv100_undergeared", 39.0, 16),
    ("lv100_4star_progress_heavy", 39.7, 17),
];

#[test]
fn greedy_reproduces_the_csharp_rotations() {
    for entry in load() {
        let (_, expected_quality, expected_steps) = CSHARP_GREEDY
            .iter()
            .find(|(id, _, _)| *id == entry.id)
            .unwrap_or_else(|| panic!("no C# reference for {}", entry.id));
        let result = greedy::solve(
            &entry.recipe(),
            &entry.stats(),
            &SolverConstraints::default(),
            &GreedySolverConfig::default(),
            &mut wall_clock(),
        );
        let quality = (result.metrics.final_quality_percent * 10.0).round() / 10.0;
        assert_eq!(
            (quality, result.metrics.total_steps),
            (*expected_quality, *expected_steps),
            "[{}] greedy differs from C#",
            entry.id
        );
    }
}
