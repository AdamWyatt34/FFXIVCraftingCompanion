mod benchmarks;
mod common;

use benchmarks::load;
use common::wall_clock;
use xiv_craft::solver::greedy;
use xiv_craft::{
    simulate, GreedySolverConfig, MctsConfig, MctsSolver, SolverConstraints, SolverResult,
    SolverSettings,
};

/// The C# theories run MCTS at the JSON's 5-10 s budgets; 1 s gives the Rust solver more
/// iterations than C# completes in 10 s, and the assertions hold at any budget because the search
/// is seeded with the greedy rotation.
const MCTS_BUDGET_MS: f64 = 1000.0;

fn solve_mcts(entry: &benchmarks::Benchmark) -> SolverResult {
    let mut solver = MctsSolver::new(MctsConfig::default(), GreedySolverConfig::default());
    solver.solve(
        &entry.recipe(),
        &entry.stats(),
        &SolverConstraints::default(),
        &SolverSettings {
            time_budget_ms: MCTS_BUDGET_MS,
            max_iterations: None,
            random_seed: Some(42),
        },
        &mut wall_clock(),
    )
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

// Greedy is deterministic: any change to these qualities (one decimal) or step counts is a solver change.
const GREEDY_REFERENCE: [(&str, f64, usize); 10] = [
    ("lv100_4star_endgame", 21.3, 13),
    ("lv90_3star_standard", 39.7, 13),
    ("lv80_2star_overgeared", 100.0, 3),
    ("lv70_standard", 48.4, 16),
    ("lv60_standard", 96.1, 16),
    ("lv50_classic", 68.6, 12),
    ("lv90_35dur_food", 73.2, 13),
    ("lv100_40dur_potion", 100.0, 7),
    ("lv100_undergeared", 100.0, 7),
    ("lv100_4star_progress_heavy", 20.4, 14),
];

#[test]
fn greedy_reproduces_reference_metrics() {
    for entry in load() {
        let (_, expected_quality, expected_steps) = GREEDY_REFERENCE
            .iter()
            .find(|(id, _, _)| *id == entry.id)
            .unwrap_or_else(|| panic!("no greedy reference for {}", entry.id));
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
            "[{}] greedy differs from reference",
            entry.id
        );
    }
}

#[test]
fn mcts_achieves_target_quality_vs_community() {
    for entry in load() {
        let community = simulate(
            &entry.recipe(),
            &entry.stats(),
            &entry.community_action_ids(),
        )
        .final_quality_percent();
        let target =
            community * entry.solver_targets.mcts_min_quality_percent_of_community as f64 / 100.0;

        let result = solve_mcts(&entry);

        assert!(
            result.simulation.as_ref().expect("simulation").success(),
            "[{}] MCTS completes",
            entry.id
        );
        assert!(
            result.metrics.final_quality_percent >= target,
            "[{}] MCTS {:.1}% below {:.1}% ({}% of community {:.1}%)",
            entry.id,
            result.metrics.final_quality_percent,
            target,
            entry.solver_targets.mcts_min_quality_percent_of_community,
            community
        );
    }
}

#[test]
fn mcts_improves_over_greedy() {
    for entry in load() {
        let greedy = greedy::solve(
            &entry.recipe(),
            &entry.stats(),
            &SolverConstraints::default(),
            &GreedySolverConfig::default(),
            &mut wall_clock(),
        );
        let mcts = solve_mcts(&entry);

        assert!(
            mcts.metrics.final_quality_percent >= greedy.metrics.final_quality_percent,
            "[{}] MCTS {:.1}% below greedy {:.1}%",
            entry.id,
            mcts.metrics.final_quality_percent,
            greedy.metrics.final_quality_percent
        );
    }
}
