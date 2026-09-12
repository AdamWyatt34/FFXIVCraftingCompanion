//! `GreedySolverTests` and `GreedySolverTuningTests`, ported case for case. The cancellation test
//! has no counterpart: the step budget is the only stop condition here.

mod common;

use common::{default_recipe, default_stats, wall_clock};
use xiv_craft::solver::greedy;
use xiv_craft::{
    simulate, Action, CrafterStats, GreedySolverConfig, Recipe, SolverAlgorithm, SolverConstraints,
    SolverResult, SolverStatus,
};

fn solve(recipe: &Recipe, stats: &CrafterStats) -> SolverResult {
    solve_with(recipe, stats, &GreedySolverConfig::default())
}

fn solve_with(recipe: &Recipe, stats: &CrafterStats, config: &GreedySolverConfig) -> SolverResult {
    greedy::solve(
        recipe,
        stats,
        &SolverConstraints::default(),
        config,
        &mut wall_clock(),
    )
}

fn level_90_stats() -> CrafterStats {
    CrafterStats {
        level: 90,
        craftsmanship: 3800,
        control: 3600,
        cp: 600,
        is_specialist: false,
    }
}

fn level_100_stats() -> CrafterStats {
    CrafterStats {
        level: 100,
        craftsmanship: 4500,
        control: 4400,
        cp: 700,
        is_specialist: false,
    }
}

#[test]
fn simple_recipe_completes_successfully() {
    let recipe = Recipe {
        recipe_id: 1,
        recipe_level: 150,
        class_job_level: 50,
        difficulty: 1000,
        quality: 2000,
        durability: 60,
        progress_divider: 100,
        progress_modifier: 100,
        quality_divider: 100,
        quality_modifier: 100,
        ..Recipe::default()
    };

    let result = solve(&recipe, &level_90_stats());

    assert_eq!(result.status, SolverStatus::Completed);
    let simulation = result.simulation.as_ref().expect("simulation");
    assert!(simulation.success());
    assert!(!result.action_ids.is_empty());
    assert!(result.metrics.craft_completed);
}

#[test]
fn level_90_recipe_completes_with_reasonable_quality() {
    let result = solve(&default_recipe(), &default_stats());

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(result.simulation.as_ref().expect("simulation").success());
    assert!(result.metrics.final_quality_percent >= 10.0);
    assert!(result.action_ids.len() <= 50);
}

#[test]
fn high_end_recipe_completes_successfully() {
    let recipe = Recipe {
        recipe_id: 200,
        recipe_level: 690,
        class_job_level: 100,
        difficulty: 6600,
        quality: 12000,
        durability: 70,
        stars: 4,
        progress_divider: 150,
        progress_modifier: 80,
        quality_divider: 130,
        quality_modifier: 70,
        ..Recipe::default()
    };

    let result = solve(&recipe, &level_100_stats());

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(result.simulation.as_ref().expect("simulation").success());
    assert!(result.metrics.craft_completed);
}

#[test]
fn trained_eye_eligible_uses_trained_eye_and_maxes_quality() {
    let recipe = Recipe {
        recipe_id: 300,
        recipe_level: 300,
        class_job_level: 80,
        difficulty: 2000,
        quality: 5000,
        durability: 70,
        progress_divider: 110,
        progress_modifier: 100,
        quality_divider: 100,
        quality_modifier: 100,
        ..Recipe::default()
    };

    let result = solve(&recipe, &level_100_stats());

    assert_eq!(result.status, SolverStatus::Completed);
    let simulation = result.simulation.as_ref().expect("simulation");
    assert!(simulation.success());
    assert_eq!(result.action_ids.first(), Some(&Action::TrainedEye.id()));
    assert_eq!(simulation.final_state.quality, recipe.quality);
}

#[test]
fn very_low_cp_produces_a_valid_result() {
    let stats = CrafterStats {
        cp: 100,
        ..level_90_stats()
    };

    let result = solve(&default_recipe(), &stats);

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(result.simulation.is_some());
    assert!(!result.action_ids.is_empty());
}

#[test]
fn completes_within_time_budget() {
    let result = solve(&default_recipe(), &default_stats());

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(
        result.metrics.solve_time_ms < 100.0,
        "greedy took {} ms",
        result.metrics.solve_time_ms
    );
}

#[test]
fn result_matches_an_independent_replay() {
    let recipe = default_recipe();
    let stats = default_stats();

    let result = solve(&recipe, &stats);
    let simulation = result.simulation.as_ref().expect("simulation");
    let replay = simulate(&recipe, &stats, &result.action_ids);

    assert_eq!(replay.success(), simulation.success());
    assert_eq!(replay.final_state.progress, simulation.final_state.progress);
    assert_eq!(replay.final_state.quality, simulation.final_state.quality);
    assert_eq!(replay.total_steps(), simulation.total_steps());
}

#[test]
fn expert_recipe_fails_with_message() {
    let recipe = Recipe {
        recipe_id: 400,
        recipe_level: 580,
        class_job_level: 90,
        difficulty: 5000,
        quality: 10000,
        durability: 70,
        is_expert: true,
        conditions_flag: 0x3F,
        progress_divider: 130,
        progress_modifier: 80,
        quality_divider: 115,
        quality_modifier: 70,
        ..Recipe::default()
    };

    let result = solve(&recipe, &default_stats());

    assert_eq!(result.status, SolverStatus::Failed);
    assert!(result
        .error_message
        .as_deref()
        .is_some_and(|m| m.contains("Expert recipes")));
    assert!(result.action_ids.is_empty());
}

#[test]
fn generates_macros() {
    let result = solve(&default_recipe(), &default_stats());

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(!result.macros.is_empty());
    assert!(result.macros[0].contains("/ac"));
}

#[test]
fn metrics_are_populated() {
    let result = solve(&default_recipe(), &default_stats());

    assert_eq!(result.metrics.algorithm_used, Some(SolverAlgorithm::Greedy));
    assert!(result.metrics.total_steps > 0);
    assert!(result.metrics.solve_time_ms >= 0.0);
    assert_eq!(result.metrics.iterations_completed, 1);
    assert_eq!(
        result.metrics.nodes_explored,
        result.action_ids.len() as u64
    );
}

// GreedySolverTuningTests

#[test]
fn default_config_values_match_csharp() {
    let config = GreedySolverConfig::default();
    assert_eq!(config.byregots_high_iq_threshold, 6);
    assert_eq!(config.manipulation_score, 90.0);
    assert_eq!(config.finishing_cp_buffer, 30);
    assert_eq!(config.finishing_durability_buffer, 15);
    assert_eq!(config.effective_durability_buffer(35), 10);
    assert_eq!(config.effective_durability_buffer(40), 10);
    assert_eq!(
        config.effective_durability_buffer(60),
        config.finishing_durability_buffer
    );
    assert_eq!(
        config.effective_durability_buffer(80),
        config.finishing_durability_buffer
    );
}

fn low_durability_recipe(recipe_id: i32, durability: i32) -> Recipe {
    Recipe {
        recipe_id,
        recipe_level: 580,
        class_job_level: 90,
        difficulty: 3500,
        quality: 7200,
        durability,
        progress_divider: 130,
        progress_modifier: 80,
        quality_divider: 115,
        quality_modifier: 70,
        stars: 2,
        ..Recipe::default()
    }
}

#[test]
fn low_durability_recipe_completes_successfully() {
    let result = solve(&low_durability_recipe(1, 35), &level_90_stats());

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(result.simulation.as_ref().expect("simulation").success());
}

#[test]
fn low_durability_recipe_completes_with_and_without_durability_aware_scoring() {
    let recipe = low_durability_recipe(2, 40);
    let stats = level_90_stats();

    let aware = solve_with(&recipe, &stats, &GreedySolverConfig::default());
    let unaware = solve_with(
        &recipe,
        &stats,
        &GreedySolverConfig {
            enable_durability_aware_scoring: false,
            ..GreedySolverConfig::default()
        },
    );

    assert_eq!(aware.status, SolverStatus::Completed);
    assert_eq!(unaware.status, SolverStatus::Completed);
    assert!(aware.simulation.as_ref().expect("simulation").success());
}
