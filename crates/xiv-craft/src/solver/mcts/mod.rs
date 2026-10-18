//! `MCTSSolver`: UCB1 tree search seeded with the greedy rotation, softmax rollouts, and the C#
//! reward. The solver owns its arena and scratch buffers, so a caller that keeps one instance
//! allocates nothing per iteration after the first solve.

pub mod packed;
pub mod tree;

use crate::actions::{Action, ActionCategory, ActionId};
use crate::macro_export::{export_to_macro, MacroOptions};
use crate::simulator::{create_initial, simulate, DEFAULT_SEED};
use crate::state::{CraftStatus, CrafterStats, CraftingState, Recipe};

use super::greedy::{self, GreedySolverConfig};
use super::rng::Xoshiro256;
use super::{
    ActionSet, Clock, SolverAlgorithm, SolverConstraints, SolverError, SolverMetrics, SolverResult,
    SolverSettings, SolverStatus,
};
use packed::PackedState;
use tree::Arena;

#[derive(Clone, Debug, PartialEq)]
pub struct MctsConfig {
    pub exploration_constant: f64,
    pub rollout_temperature: f64,
    pub heuristic_expansion_probability: f64,

    pub failed_reward: f64,
    pub in_progress_progress_weight: f64,
    pub completed_base_reward: f64,
    pub completed_quality_weight: f64,
    pub step_penalty_per_step: f64,

    pub emergency_cp_ratio: f64,
    pub emergency_durability_threshold: i32,

    pub rollout_cp_penalty: f64,
    pub rollout_durability_penalty: f64,

    /// Iterations without a better rotation before the search gives up; iterations, not time, so
    /// a faster solver stops sooner on the wall clock.
    pub early_termination_threshold: u64,
    pub perfect_reward_threshold: f64,

    /// Ceiling on tree nodes (48 bytes each, plus 16 bytes per edge of an expanded node). Once reached, the search keeps running
    /// rollouts from the existing leaves instead of expanding. No C# counterpart: the C# tree is
    /// unbounded, so the default is too; a Web Worker should set one.
    pub max_nodes: usize,
}

impl Default for MctsConfig {
    fn default() -> MctsConfig {
        MctsConfig {
            exploration_constant: 1.41,
            rollout_temperature: 0.5,
            heuristic_expansion_probability: 0.7,

            failed_reward: 0.0,
            in_progress_progress_weight: 0.1,
            completed_base_reward: 0.2,
            completed_quality_weight: 0.8,
            step_penalty_per_step: 0.001,

            emergency_cp_ratio: 0.15,
            emergency_durability_threshold: 15,

            rollout_cp_penalty: 0.05,
            rollout_durability_penalty: 0.03,

            early_termination_threshold: 5000,
            perfect_reward_threshold: 0.999,

            max_nodes: usize::MAX,
        }
    }
}

pub struct MctsSolver {
    config: MctsConfig,
    greedy_config: GreedySolverConfig,
    arena: Arena,
    /// Actions the current crafter and recipe can ever use; see [`ActionSet::eligible`].
    eligible: ActionSet,
    rollout_actions: Vec<ActionId>,
    candidate: Vec<ActionId>,
    best_sequence: Vec<ActionId>,
    session: Option<Session>,
}

/// The loop state of a search between [`MctsSolver::begin`] and [`MctsSolver::finish`]. Kept
/// out of the solver's scratch fields so a chunked run and a one-shot [`MctsSolver::solve`] walk
/// the same iterations in the same order.
struct Session {
    recipe: Recipe,
    stats: CrafterStats,
    constraints: SolverConstraints,
    settings: SolverSettings,
    root: u32,
    rng: Xoshiro256,
    started_ms: f64,
    iterations: u64,
    best_reward: f64,
    without_improvement: u64,
    done: bool,
}

/// What a search has found so far. `best_*` replay the best sequence with the engine, so they are
/// the numbers [`MctsSolver::finish`] will report if the search stopped now.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct SearchProgress {
    pub iterations: u64,
    pub nodes_explored: u64,
    pub elapsed_ms: f64,
    pub best_steps: usize,
    pub best_quality_percent: f64,
    pub best_hq_percent: i32,
    /// A stop condition was hit (budget, iteration cap, perfect reward or early termination);
    /// further [`MctsSolver::run`] calls do nothing.
    pub done: bool,
}

impl MctsSolver {
    pub fn new(config: MctsConfig, greedy_config: GreedySolverConfig) -> MctsSolver {
        MctsSolver {
            config,
            greedy_config,
            arena: Arena::default(),
            eligible: ActionSet::EMPTY,
            rollout_actions: Vec::with_capacity(64),
            candidate: Vec::with_capacity(64),
            best_sequence: Vec::with_capacity(64),
            session: None,
        }
    }

    pub fn config(&self) -> &MctsConfig {
        &self.config
    }

    /// Bytes the tree of the last solve occupies; the arena keeps them for the next solve.
    pub fn tree_memory_bytes(&self) -> usize {
        self.arena.memory_bytes()
    }

    /// One-shot search: [`MctsSolver::begin`], [`MctsSolver::run`] to a stop condition,
    /// [`MctsSolver::finish`].
    pub fn solve(
        &mut self,
        recipe: &Recipe,
        stats: &CrafterStats,
        constraints: &SolverConstraints,
        settings: &SolverSettings,
        now_ms: Clock<'_>,
    ) -> SolverResult {
        let failed = |error: SolverError| {
            SolverResult::failed(
                SolverAlgorithm::Mcts,
                &error.to_string(),
                SolverMetrics::default(),
            )
        };
        if let Err(error) = self.begin(recipe, stats, constraints, settings, &mut *now_ms) {
            return failed(error);
        }
        // Begun just above, so neither step can report a missing search.
        let _ = self.run(u64::MAX, &mut *now_ms);
        self.finish(now_ms).unwrap_or_else(failed)
    }

    /// Seeds the search with the greedy rotation and builds the root. Drops any search in
    /// progress.
    ///
    /// # Errors
    ///
    /// [`SolverError::StateDoesNotFit`] when the initial state cannot be held by the tree, in
    /// which case nothing has been touched: a search already in progress is still there.
    pub fn begin(
        &mut self,
        recipe: &Recipe,
        stats: &CrafterStats,
        constraints: &SolverConstraints,
        settings: &SolverSettings,
        now_ms: Clock<'_>,
    ) -> Result<(), SolverError> {
        let started_ms = now_ms();
        let initial = create_initial(recipe, stats);
        PackedState::try_pack(&initial).map_err(|e| SolverError::StateDoesNotFit {
            field: e.field,
            value: e.value,
        })?;

        self.best_sequence.clear();
        let mut best_reward = -1.0;
        let seed = greedy::solve(
            recipe,
            stats,
            constraints,
            &self.greedy_config,
            &mut *now_ms,
        );
        if seed.status == SolverStatus::Completed
            && seed.simulation.as_ref().is_some_and(|s| s.success())
        {
            let final_state = seed.simulation.as_ref().expect("checked").final_state;
            best_reward = calculate_reward(&final_state, constraints, &self.config);
            self.best_sequence.extend_from_slice(&seed.action_ids);
        }

        self.eligible = ActionSet::eligible(stats, recipe);
        self.arena.clear();
        let root = self
            .arena
            .push_root(initial, self.legal(&initial, stats, recipe, constraints));

        self.session = Some(Session {
            recipe: *recipe,
            stats: *stats,
            constraints: *constraints,
            settings: *settings,
            root,
            rng: Xoshiro256::from_seed(settings.random_seed.unwrap_or(DEFAULT_SEED)),
            started_ms,
            iterations: 0,
            best_reward,
            without_improvement: 0,
            done: false,
        });
        Ok(())
    }

    /// Runs up to `max_iterations` more iterations of the search begun by [`MctsSolver::begin`],
    /// stopping early at the budget, the settings' iteration cap, a perfect reward or the
    /// early-termination threshold. A worker calls this in slices and yields between them.
    ///
    /// # Errors
    ///
    /// [`SolverError::NoSearchInProgress`] without a [`MctsSolver::begin`].
    pub fn run(
        &mut self,
        max_iterations: u64,
        now_ms: Clock<'_>,
    ) -> Result<SearchProgress, SolverError> {
        let mut session = self.session.take().ok_or(SolverError::NoSearchInProgress)?;
        let Session {
            recipe,
            stats,
            constraints,
            settings,
            root,
            ..
        } = session;
        let max_total = settings.max_iterations.unwrap_or(u64::MAX);
        let mut ran: u64 = 0;

        while !session.done && ran < max_iterations {
            // The clock is a host call (Date.now in WASM, ~100 ns and up): read every 64
            // iterations, the budget overruns by well under a millisecond at the rates seen.
            if ((ran & 63) == 0 && now_ms() - session.started_ms >= settings.time_budget_ms)
                || session.iterations >= max_total
            {
                session.done = true;
                break;
            }

            let selected = self.select(root);
            let (expanded, expanded_state) =
                self.expand(selected, &stats, &recipe, &constraints, &mut session.rng);
            let reward = self.rollout(
                expanded_state,
                &stats,
                &recipe,
                &constraints,
                &mut session.rng,
            );
            self.arena.backpropagate(expanded, reward);

            if reward > session.best_reward {
                self.candidate.clear();
                self.arena.path_actions(expanded, &mut self.candidate);
                self.candidate.extend_from_slice(&self.rollout_actions);
                if self.candidate.len() <= constraints.max_steps {
                    session.best_reward = reward;
                    core::mem::swap(&mut self.best_sequence, &mut self.candidate);
                    session.without_improvement = 0;
                } else {
                    session.without_improvement += 1;
                }
            } else {
                session.without_improvement += 1;
            }

            session.iterations += 1;
            ran += 1;

            if session.best_reward >= self.config.perfect_reward_threshold
                || session.without_improvement >= self.config.early_termination_threshold
            {
                session.done = true;
            }
        }

        let progress = self.snapshot(&session, now_ms() - session.started_ms);
        self.session = Some(session);
        Ok(progress)
    }

    /// The search so far, without advancing it.
    ///
    /// # Errors
    ///
    /// [`SolverError::NoSearchInProgress`] without a [`MctsSolver::begin`].
    pub fn progress(&self, now_ms: Clock<'_>) -> Result<SearchProgress, SolverError> {
        let session = self
            .session
            .as_ref()
            .ok_or(SolverError::NoSearchInProgress)?;
        Ok(self.snapshot(session, now_ms() - session.started_ms))
    }

    fn snapshot(&self, session: &Session, elapsed_ms: f64) -> SearchProgress {
        let (best_quality_percent, best_hq_percent) = if self.best_sequence.is_empty() {
            (0.0, 0)
        } else {
            let replay = simulate(&session.recipe, &session.stats, &self.best_sequence);
            (replay.final_quality_percent(), replay.final_hq_percent())
        };
        SearchProgress {
            iterations: session.iterations,
            nodes_explored: self.arena.len() as u64,
            elapsed_ms,
            best_steps: self.best_sequence.len(),
            best_quality_percent,
            best_hq_percent,
            done: session.done,
        }
    }

    /// Ends the search begun by [`MctsSolver::begin`] and reports the best rotation found, which
    /// after a partial run is the best so far.
    ///
    /// # Errors
    ///
    /// [`SolverError::NoSearchInProgress`] without a [`MctsSolver::begin`].
    pub fn finish(&mut self, now_ms: Clock<'_>) -> Result<SolverResult, SolverError> {
        let session = self.session.take().ok_or(SolverError::NoSearchInProgress)?;
        let solve_time_ms = now_ms() - session.started_ms;
        let nodes_explored = self.arena.len() as u64;

        if self.best_sequence.is_empty() {
            return Ok(SolverResult::failed(
                SolverAlgorithm::Mcts,
                "MCTS solver could not find a valid rotation.",
                SolverMetrics {
                    solve_time_ms,
                    iterations_completed: session.iterations,
                    nodes_explored,
                    ..SolverMetrics::default()
                },
            ));
        }

        let action_ids = self.best_sequence.clone();
        let simulation = simulate(&session.recipe, &session.stats, &action_ids);
        let macros = export_to_macro(&action_ids, &MacroOptions::default());

        Ok(SolverResult {
            status: SolverStatus::Completed,
            metrics: SolverMetrics {
                algorithm_used: Some(SolverAlgorithm::Mcts),
                solve_time_ms,
                total_steps: action_ids.len(),
                final_quality_percent: simulation.final_quality_percent(),
                final_hq_percent: simulation.final_hq_percent(),
                craft_completed: simulation.success(),
                nodes_explored,
                iterations_completed: session.iterations,
            },
            action_ids,
            simulation: Some(simulation),
            macros,
            error_message: None,
        })
    }

    fn legal(
        &self,
        state: &CraftingState,
        stats: &CrafterStats,
        recipe: &Recipe,
        constraints: &SolverConstraints,
    ) -> ActionSet {
        ActionSet::legal_from(
            self.eligible,
            state,
            stats,
            recipe,
            constraints.allow_unsafe_actions,
        )
    }

    fn select(&self, root: u32) -> u32 {
        let mut node = root;
        loop {
            let current = self.arena.get(node);
            if current.is_terminal() || !current.is_fully_expanded() || current.edge_len == 0 {
                return node;
            }
            node = self
                .arena
                .select_child(node, self.config.exploration_constant);
        }
    }

    /// Adds one child of `node` and returns it with its state; returns `node` itself when it is
    /// terminal, fully expanded, or the tree is at `max_nodes`.
    fn expand(
        &mut self,
        node: u32,
        stats: &CrafterStats,
        recipe: &Recipe,
        constraints: &SolverConstraints,
        rng: &mut Xoshiro256,
    ) -> (u32, CraftingState) {
        let current = *self.arena.get(node);
        let state = self.arena.state(node);
        if current.is_terminal()
            || current.untried.is_empty()
            || self.arena.len() >= self.config.max_nodes
        {
            return (node, state);
        }

        let action = pick_untried_action(&state, current.untried, rng, &self.config);
        let child_state = action.execute(state, stats, recipe, true);
        let untried = if child_state.status == CraftStatus::InProgress {
            self.legal(&child_state, stats, recipe, constraints)
        } else {
            ActionSet::EMPTY
        };
        let child = self.arena.push_child(node, action, child_state, untried);
        (child, child_state)
    }

    /// Plays the rollout policy out from `state`, leaving the actions in `rollout_actions`.
    fn rollout(
        &mut self,
        mut state: CraftingState,
        stats: &CrafterStats,
        recipe: &Recipe,
        constraints: &SolverConstraints,
        rng: &mut Xoshiro256,
    ) -> f64 {
        self.rollout_actions.clear();

        while state.status == CraftStatus::InProgress
            && self.rollout_actions.len() < constraints.max_steps
        {
            let Some(action) = select_rollout_action(
                self.eligible,
                &state,
                stats,
                recipe,
                constraints,
                rng,
                &self.config,
            ) else {
                break;
            };
            state = action.execute(state, stats, recipe, true);
            self.rollout_actions.push(action.id());
        }

        calculate_reward(&state, constraints, &self.config)
    }
}

fn pick_untried_action(
    state: &CraftingState,
    untried: ActionSet,
    rng: &mut Xoshiro256,
    config: &MctsConfig,
) -> Action {
    let count = untried.len();
    if count == 1 {
        return untried.nth(0);
    }

    if rng.next_f64() < config.heuristic_expansion_probability {
        let mut best = untried.nth(0);
        let mut best_score = f64::NEG_INFINITY;
        for action in untried.iter() {
            let score = quick_score(action, state);
            if score > best_score {
                best = action;
                best_score = score;
            }
        }
        return best;
    }

    untried.nth(rng.next_below(count as u32) as usize)
}

fn quick_score(action: Action, state: &CraftingState) -> f64 {
    let mut score = match action.category() {
        ActionCategory::Quality => 20.0 + state.inner_quiet_stacks as f64,
        ActionCategory::Progress => 15.0,
        ActionCategory::Buff => 10.0,
        ActionCategory::Repair => {
            if state.durability <= 25 {
                20.0
            } else {
                5.0
            }
        }
        ActionCategory::Other => 0.0,
    };

    score -= action.cp_cost(state) as f64 * 0.03;

    if state.is_first_step && is_opener(action) {
        score += 30.0;
    }

    score
}

fn is_opener(action: Action) -> bool {
    matches!(
        action,
        Action::Reflect | Action::MuscleMemory | Action::TrainedEye
    )
}

fn select_rollout_action(
    eligible: ActionSet,
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
    constraints: &SolverConstraints,
    rng: &mut Xoshiro256,
    config: &MctsConfig,
) -> Option<Action> {
    let mut candidates = [Action::BasicSynthesis; 36];
    let mut scores = [0.0f64; 36];
    let mut count = 0;

    for action in eligible.iter() {
        if action.fail_reason(state, stats, recipe).is_some() {
            continue;
        }
        if !constraints.allow_unsafe_actions && action.success_rate(state) < 100 {
            continue;
        }
        let score = score_for_rollout(action, state, config);
        if score > -100.0 {
            candidates[count] = action;
            scores[count] = score;
            count += 1;
        }
    }

    if count == 0 {
        return None;
    }

    let max_score = scores[..count]
        .iter()
        .copied()
        .fold(f64::NEG_INFINITY, f64::max);
    let mut weights = [0.0f64; 36];
    let mut total_weight = 0.0;
    for i in 0..count {
        weights[i] = ((scores[i] - max_score) / config.rollout_temperature).exp();
        total_weight += weights[i];
    }

    let roll = rng.next_f64() * total_weight;
    let mut cumulative = 0.0;
    for i in 0..count {
        cumulative += weights[i];
        if roll < cumulative {
            return Some(candidates[i]);
        }
    }
    Some(candidates[count - 1])
}

fn score_for_rollout(action: Action, state: &CraftingState, config: &MctsConfig) -> f64 {
    let mut score = 0.0;

    let cp_cost = action.cp_cost(state);
    if cp_cost > 0 {
        score -= cp_cost as f64 * config.rollout_cp_penalty;
    }
    let durability_cost = action.durability_cost(state);
    if durability_cost > 0 {
        score -= durability_cost as f64 * config.rollout_durability_penalty;
    }

    let category = action.category();
    let quality_capped = state.max_quality > 0 && state.quality >= state.max_quality;
    let cp_ratio = if state.max_cp > 0 {
        state.cp as f64 / state.max_cp as f64
    } else {
        0.0
    };
    let iq = state.inner_quiet_stacks;

    if cp_ratio < config.emergency_cp_ratio
        || state.durability <= config.emergency_durability_threshold
    {
        score += match category {
            ActionCategory::Progress => 50.0,
            ActionCategory::Repair if state.durability <= 10 => 40.0,
            _ => -20.0,
        };
        return score;
    }

    if quality_capped {
        score += match category {
            ActionCategory::Progress => 40.0,
            ActionCategory::Buff => 10.0,
            _ => -10.0,
        };
        return score;
    }

    if state.is_first_step && is_opener(action) {
        score += 40.0;
    }

    let innovation_active = state.innovation_turns > 0;
    let great_strides_active = state.great_strides_turns > 0;

    score += match action {
        Action::ByregotsBlessing if iq >= 8 && great_strides_active => 80.0,
        Action::ByregotsBlessing if iq >= 5 => 40.0,

        Action::GreatStrides if iq >= 7 && innovation_active => 50.0,
        Action::Innovation if !innovation_active => 45.0,

        Action::Manipulation if state.manipulation_turns == 0 && state.durability <= 35 => 45.0,
        Action::MastersMend if state.durability <= 20 => 40.0,

        Action::PreparatoryTouch if iq < 8 => 35.0,
        Action::TrainedFinesse if iq == 10 => 35.0,

        Action::AdvancedTouch if state.combo_action == Action::StandardTouch.id() => 30.0,
        Action::StandardTouch if state.combo_action == Action::BasicTouch.id() => 30.0,

        Action::PrudentTouch => 25.0,
        Action::BasicTouch => 20.0,

        Action::Groundwork if state.veneration_turns > 0 => 30.0,
        Action::Veneration => 15.0,
        Action::CarefulSynthesis => 10.0,
        Action::BasicSynthesis => 5.0,

        _ if category == ActionCategory::Quality => 20.0,
        _ if category == ActionCategory::Progress => 5.0,
        _ if category == ActionCategory::Buff => 10.0,
        _ if category == ActionCategory::Repair && state.durability <= 30 => 25.0,
        _ => 0.0,
    };

    score
}

/// `MCTSSolver.CalculateReward`: 0 for a failed craft, progress credit for one still in progress,
/// and base + quality share minus a step penalty for a completed one, clamped to `[0, 1]`.
pub fn calculate_reward(
    state: &CraftingState,
    constraints: &SolverConstraints,
    config: &MctsConfig,
) -> f64 {
    match state.status {
        CraftStatus::Failed => config.failed_reward,
        CraftStatus::InProgress => {
            config.in_progress_progress_weight * state.progress_percent() / 100.0
        }
        CraftStatus::Completed => {
            let mut quality_reward = if state.max_quality > 0 {
                state.quality_percent() / 100.0
            } else {
                1.0
            };
            if let Some(target) = constraints.target_quality_percent {
                if state.quality_percent() >= target as f64 {
                    quality_reward = 1.0;
                }
            }
            let step_penalty = state.step as f64 * config.step_penalty_per_step;
            (config.completed_base_reward + config.completed_quality_weight * quality_reward
                - step_penalty)
                .clamp(0.0, 1.0)
        }
    }
}
