//! `MctsSolverTests`, `MCTSNodeTests`, `RewardFunctionTests` and `MctsSolverEarlyTerminationTests`,
//! ported case for case; the cancellation test has no counterpart (the budgets are the only stop
//! conditions). `same_seed_same_result` is new: reproducibility is a guarantee the C# solver does
//! not make.

mod common;

use common::{default_recipe, default_state, default_stats, frozen_clock, wall_clock};
use xiv_craft::solver::greedy;
use xiv_craft::solver::mcts::calculate_reward;
use xiv_craft::solver::mcts::tree::{Arena, NONE};
use xiv_craft::solver::ActionSet;
use xiv_craft::{
    simulate, Action, CraftStatus, CrafterStats, CraftingState, GreedySolverConfig, MctsConfig,
    MctsSolver, Recipe, SolverAlgorithm, SolverConstraints, SolverResult, SolverSettings,
    SolverStatus,
};

fn solver() -> MctsSolver {
    MctsSolver::new(MctsConfig::default(), GreedySolverConfig::default())
}

fn seeded(time_budget_ms: f64) -> SolverSettings {
    SolverSettings {
        time_budget_ms,
        max_iterations: None,
        random_seed: Some(42),
    }
}

fn solve(recipe: &Recipe, stats: &CrafterStats, settings: &SolverSettings) -> SolverResult {
    solver().solve(
        recipe,
        stats,
        &SolverConstraints::default(),
        settings,
        &mut wall_clock(),
    )
}

#[test]
fn simple_recipe_completes_successfully() {
    let stats = CrafterStats {
        level: 90,
        craftsmanship: 3800,
        control: 3600,
        cp: 600,
        is_specialist: false,
    };
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

    let result = solve(&recipe, &stats, &seeded(1000.0));

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(result.simulation.as_ref().expect("simulation").success());
    assert!(!result.action_ids.is_empty());
    assert!(result.metrics.craft_completed);
}

#[test]
fn level_90_recipe_completes_with_reasonable_quality() {
    let result = solve(&default_recipe(), &default_stats(), &seeded(2000.0));

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(result.simulation.as_ref().expect("simulation").success());
    assert!(result.metrics.final_quality_percent >= 10.0);
    assert!(result.action_ids.len() <= 50);
}

#[test]
fn respects_time_budget() {
    let config = MctsConfig {
        early_termination_threshold: u64::MAX,
        ..MctsConfig::default()
    };
    let mut solver = MctsSolver::new(config, GreedySolverConfig::default());
    let mut clock = wall_clock();

    let result = solver.solve(
        &default_recipe(),
        &default_stats(),
        &SolverConstraints::default(),
        &seeded(1000.0),
        &mut clock,
    );

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(
        result.metrics.solve_time_ms >= 1000.0,
        "spent {} ms of a 1000 ms budget",
        result.metrics.solve_time_ms
    );
    assert!(clock() < 3000.0);
    assert!(result.metrics.iterations_completed > 0);
}

#[test]
fn max_steps_respected() {
    let result = solver().solve(
        &default_recipe(),
        &default_stats(),
        &SolverConstraints {
            max_steps: 15,
            ..SolverConstraints::default()
        },
        &seeded(1000.0),
        &mut wall_clock(),
    );

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(result.action_ids.len() <= 15);
}

#[test]
fn generates_macros() {
    let result = solve(&default_recipe(), &default_stats(), &seeded(1000.0));

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(!result.macros.is_empty());
    assert!(result.macros[0].contains("/ac"));
}

#[test]
fn result_matches_an_independent_replay() {
    let recipe = default_recipe();
    let stats = default_stats();

    let result = solve(&recipe, &stats, &seeded(1000.0));
    let simulation = result.simulation.as_ref().expect("simulation");
    let replay = simulate(&recipe, &stats, &result.action_ids);

    assert_eq!(replay.success(), simulation.success());
    assert_eq!(replay.final_state.progress, simulation.final_state.progress);
    assert_eq!(replay.final_state.quality, simulation.final_state.quality);
    assert_eq!(replay.total_steps(), simulation.total_steps());
}

#[test]
fn metrics_are_populated() {
    let result = solve(&default_recipe(), &default_stats(), &seeded(1000.0));

    assert_eq!(result.metrics.algorithm_used, Some(SolverAlgorithm::Mcts));
    assert!(result.metrics.total_steps > 0);
    assert!(result.metrics.solve_time_ms >= 0.0);
    assert!(result.metrics.iterations_completed > 0);
    assert!(result.metrics.nodes_explored > 0);
}

#[test]
fn produces_at_least_greedy_quality() {
    let recipe = default_recipe();
    let stats = default_stats();

    let greedy = greedy::solve(
        &recipe,
        &stats,
        &SolverConstraints::default(),
        &GreedySolverConfig::default(),
        &mut wall_clock(),
    );
    let mcts = solve(&recipe, &stats, &seeded(2000.0));

    assert_eq!(mcts.status, SolverStatus::Completed);
    assert_eq!(greedy.status, SolverStatus::Completed);
    assert!(mcts.metrics.final_quality_percent >= greedy.metrics.final_quality_percent);
}

#[test]
fn same_seed_same_result() {
    let settings = SolverSettings {
        time_budget_ms: f64::INFINITY,
        max_iterations: Some(20_000),
        random_seed: Some(7),
    };
    let mut solver = solver();

    let first = solver.solve(
        &default_recipe(),
        &default_stats(),
        &SolverConstraints::default(),
        &settings,
        &mut frozen_clock(),
    );
    let second = solver.solve(
        &default_recipe(),
        &default_stats(),
        &SolverConstraints::default(),
        &settings,
        &mut frozen_clock(),
    );

    assert_eq!(first.action_ids, second.action_ids);
    assert_eq!(first.metrics, second.metrics);
    assert!(first.metrics.iterations_completed <= 20_000);
}

#[test]
fn max_nodes_caps_the_tree_and_the_search_still_completes() {
    let config = MctsConfig {
        max_nodes: 500,
        early_termination_threshold: u64::MAX,
        ..MctsConfig::default()
    };
    let mut solver = MctsSolver::new(config, GreedySolverConfig::default());

    let result = solver.solve(
        &default_recipe(),
        &default_stats(),
        &SolverConstraints::default(),
        &SolverSettings {
            time_budget_ms: f64::INFINITY,
            max_iterations: Some(5_000),
            random_seed: Some(42),
        },
        &mut frozen_clock(),
    );

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(result.simulation.as_ref().expect("simulation").success());
    assert_eq!(result.metrics.nodes_explored, 500);
    assert_eq!(result.metrics.iterations_completed, 5_000);
}

// MCTSNodeTests. Statistics live on edges and are only written by backpropagation, so the
// fixtures build them by backpropagating instead of setting fields.

fn actions(list: &[Action]) -> ActionSet {
    let mut set = ActionSet::EMPTY;
    for action in list {
        set.insert(*action);
    }
    set
}

fn root_with_two_children(root_visits: u32) -> (Arena, u32, u32, u32) {
    let mut arena = Arena::default();
    let root = arena.push_root(
        default_state(),
        actions(&[Action::BasicSynthesis, Action::BasicTouch]),
    );
    let first = arena.push_child(
        root,
        Action::BasicSynthesis,
        default_state(),
        ActionSet::EMPTY,
    );
    let second = arena.push_child(root, Action::BasicTouch, default_state(), ActionSet::EMPTY);
    for _ in 0..root_visits {
        arena.backpropagate(root, 0.5);
    }
    (arena, root, first, second)
}

fn visit(arena: &mut Arena, node: u32, times: u32, reward: f64) {
    for _ in 0..times {
        arena.backpropagate(node, reward);
    }
}

#[test]
fn ucb1_unvisited_node_returns_max_value() {
    let (arena, _, child, _) = root_with_two_children(10);
    assert_eq!(arena.ucb1(child, 1.41), f64::MAX);
}

#[test]
fn ucb1_visited_node_returns_finite_value() {
    let (mut arena, _, child, _) = root_with_two_children(90);
    visit(&mut arena, child, 10, 0.7);

    let score = arena.ucb1(child, 1.41);
    assert!(score > 0.0);
    assert!(score < 10.0);
}

#[test]
fn ucb1_higher_reward_higher_score() {
    let (mut arena, _, good, bad) = root_with_two_children(80);
    visit(&mut arena, good, 10, 0.8);
    visit(&mut arena, bad, 10, 0.2);

    assert!(arena.ucb1(good, 1.41) > arena.ucb1(bad, 1.41));
}

#[test]
fn ucb1_less_visited_higher_exploration_bonus() {
    let (mut arena, _, less, more) = root_with_two_children(48);
    visit(&mut arena, less, 2, 0.5);
    visit(&mut arena, more, 50, 0.5);

    assert!(arena.ucb1(less, 1.41) > arena.ucb1(more, 1.41));
}

#[test]
fn ucb1_root_node_returns_average_reward() {
    let mut arena = Arena::default();
    let root = arena.push_root(default_state(), ActionSet::EMPTY);
    visit(&mut arena, root, 10, 0.5);

    assert_eq!(arena.ucb1(root, 1.41), 0.5);
}

#[test]
fn is_terminal_follows_status() {
    let mut arena = Arena::default();
    let in_progress = arena.push_root(default_state(), ActionSet::EMPTY);
    let completed = arena.push_root(
        CraftingState {
            status: CraftStatus::Completed,
            ..default_state()
        },
        ActionSet::EMPTY,
    );
    let failed = arena.push_root(
        CraftingState {
            status: CraftStatus::Failed,
            ..default_state()
        },
        ActionSet::EMPTY,
    );

    assert!(!arena.get(in_progress).is_terminal());
    assert!(arena.get(completed).is_terminal());
    assert!(arena.get(failed).is_terminal());
    assert_eq!(arena.state(in_progress), default_state());
}

#[test]
fn is_fully_expanded_when_no_untried_actions() {
    let mut arena = Arena::default();
    let pending = arena.push_root(
        default_state(),
        actions(&[Action::BasicSynthesis, Action::BasicTouch, Action::Observe]),
    );
    let done = arena.push_root(default_state(), ActionSet::EMPTY);

    assert!(!arena.get(pending).is_fully_expanded());
    assert!(arena.get(done).is_fully_expanded());
}

#[test]
fn average_reward_calculation() {
    let mut arena = Arena::default();
    let node = arena.push_root(default_state(), ActionSet::EMPTY);
    assert_eq!(arena.stats(node).average_reward(), 0.0);

    visit(&mut arena, node, 3, 1.0);
    arena.backpropagate(node, 0.0);
    assert_eq!(arena.stats(node).average_reward(), 0.75);
}

#[test]
fn backpropagate_updates_all_ancestors() {
    let mut arena = Arena::default();
    let root = arena.push_root(default_state(), actions(&[Action::BasicSynthesis]));
    let child = arena.push_child(
        root,
        Action::BasicSynthesis,
        default_state(),
        actions(&[Action::BasicTouch]),
    );
    let grandchild = arena.push_child(child, Action::BasicTouch, default_state(), ActionSet::EMPTY);

    arena.backpropagate(grandchild, 0.8);

    for index in [grandchild, child, root] {
        let stats = arena.stats(index);
        assert_eq!(stats.visits, 1);
        assert_eq!(stats.total_reward, 0.8);
    }
    assert_eq!(arena.get(root).parent, NONE);
}

#[test]
fn backpropagate_multiple_updates_accumulates() {
    let mut arena = Arena::default();
    let root = arena.push_root(default_state(), actions(&[Action::BasicSynthesis]));
    let child = arena.push_child(
        root,
        Action::BasicSynthesis,
        default_state(),
        ActionSet::EMPTY,
    );

    arena.backpropagate(child, 0.6);
    arena.backpropagate(child, 0.9);

    let child_stats = arena.stats(child);
    assert_eq!(child_stats.visits, 2);
    assert!((child_stats.total_reward - 1.5).abs() < 0.001);
    assert!((child_stats.average_reward() - 0.75).abs() < 0.001);
    let root_stats = arena.stats(root);
    assert_eq!(root_stats.visits, 2);
    assert!((root_stats.total_reward - 1.5).abs() < 0.001);
}

#[test]
fn path_actions_lists_root_to_node_and_children_keep_insertion_order() {
    let mut arena = Arena::default();
    let root = arena.push_root(
        default_state(),
        actions(&[Action::MuscleMemory, Action::Reflect]),
    );
    let first = arena.push_child(
        root,
        Action::MuscleMemory,
        default_state(),
        actions(&[Action::Manipulation]),
    );
    let second = arena.push_child(root, Action::Reflect, default_state(), ActionSet::EMPTY);
    let leaf = arena.push_child(
        first,
        Action::Manipulation,
        default_state(),
        ActionSet::EMPTY,
    );

    let mut path = Vec::new();
    arena.path_actions(leaf, &mut path);
    assert_eq!(path, [Action::MuscleMemory.id(), Action::Manipulation.id()]);
    assert_eq!(arena.children(root).collect::<Vec<_>>(), [first, second]);
}

// RewardFunctionTests

fn reward(state: CraftingState, constraints: SolverConstraints) -> f64 {
    calculate_reward(&state, &constraints, &MctsConfig::default())
}

#[test]
fn reward_failed_craft_returns_zero() {
    let state = CraftingState {
        status: CraftStatus::Failed,
        durability: 0,
        ..default_state()
    };
    assert_eq!(reward(state, SolverConstraints::default()), 0.0);
}

#[test]
fn reward_in_progress_returns_partial_credit() {
    let state = CraftingState {
        progress: 1750,
        max_progress: 3500,
        status: CraftStatus::InProgress,
        ..default_state()
    };
    let r = reward(state, SolverConstraints::default());
    assert!(r > 0.0);
    assert!(r <= 0.1);
}

#[test]
fn reward_completed_no_quality_returns_base_reward() {
    let state = CraftingState {
        progress: 3500,
        max_progress: 3500,
        quality: 0,
        max_quality: 7200,
        status: CraftStatus::Completed,
        ..default_state()
    };
    let r = reward(state, SolverConstraints::default());
    assert!(r >= 0.15);
    assert!(r < 0.3);
}

#[test]
fn reward_completed_half_quality_returns_mid_reward() {
    let state = CraftingState {
        progress: 3500,
        max_progress: 3500,
        quality: 3600,
        max_quality: 7200,
        status: CraftStatus::Completed,
        ..default_state()
    };
    let r = reward(state, SolverConstraints::default());
    assert!(r > 0.5);
    assert!(r < 0.7);
}

#[test]
fn reward_completed_max_quality_returns_near_one() {
    let state = CraftingState {
        progress: 3500,
        max_progress: 3500,
        quality: 7200,
        max_quality: 7200,
        status: CraftStatus::Completed,
        ..default_state()
    };
    let r = reward(state, SolverConstraints::default());
    assert!(r >= 0.95);
    assert!(r <= 1.0);
}

#[test]
fn reward_target_quality_met_returns_full_quality_reward() {
    let state = CraftingState {
        progress: 3500,
        max_progress: 3500,
        quality: 5040,
        max_quality: 7200,
        status: CraftStatus::Completed,
        ..default_state()
    };
    let r = reward(
        state,
        SolverConstraints {
            target_quality_percent: Some(70),
            ..SolverConstraints::default()
        },
    );
    assert!(r >= 0.95);
}

#[test]
fn reward_zero_max_quality_returns_full_reward() {
    let state = CraftingState {
        progress: 3500,
        max_progress: 3500,
        quality: 0,
        max_quality: 0,
        status: CraftStatus::Completed,
        ..default_state()
    };
    assert!(reward(state, SolverConstraints::default()) >= 0.95);
}

// MctsSolverEarlyTerminationTests

#[test]
fn easy_recipe_stops_early_when_perfect_quality() {
    let stats = CrafterStats {
        level: 100,
        craftsmanship: 4500,
        control: 4400,
        cp: 700,
        is_specialist: false,
    };
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
    let config = MctsConfig {
        perfect_reward_threshold: 0.999,
        ..MctsConfig::default()
    };
    let mut solver = MctsSolver::new(config, GreedySolverConfig::default());

    let result = solver.solve(
        &recipe,
        &stats,
        &SolverConstraints::default(),
        &seeded(30_000.0),
        &mut wall_clock(),
    );

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(result.simulation.as_ref().expect("simulation").success());
    assert!(result.metrics.solve_time_ms < 15_000.0);
}

#[test]
fn convergence_threshold_stops_after_no_improvement() {
    let config = MctsConfig {
        early_termination_threshold: 100,
        ..MctsConfig::default()
    };
    let mut solver = MctsSolver::new(config, GreedySolverConfig::default());

    let result = solver.solve(
        &default_recipe(),
        &default_stats(),
        &SolverConstraints::default(),
        &seeded(60_000.0),
        &mut wall_clock(),
    );

    assert_eq!(result.status, SolverStatus::Completed);
    assert!(result.metrics.solve_time_ms < 30_000.0);
}

#[test]
fn default_config_values_match_csharp() {
    let config = MctsConfig::default();
    assert_eq!(config.early_termination_threshold, 5000);
    assert_eq!(config.perfect_reward_threshold, 0.999);
    assert_eq!(config.exploration_constant, 1.41);
    assert_eq!(config.rollout_temperature, 0.5);
    assert_eq!(config.heuristic_expansion_probability, 0.7);
    assert_eq!(config.completed_quality_weight, 0.8);
    assert_eq!(config.step_penalty_per_step, 0.001);
}
