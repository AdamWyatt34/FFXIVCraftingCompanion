//! The endpoint behaviours of `CraftingController` over the DTOs, with the clock injected so the
//! host tests can run them; `lib.rs` wraps these for wasm-bindgen with the JS clock.

use xiv_craft::solver::mcts::SearchProgress;
use xiv_craft::solver::{Clock, GreedySolverConfig, MctsConfig, MctsSolver, SolverConstraints};
use xiv_craft::{
    action_availability, calculate_starting_quality, execute_action as engine_execute,
    export_to_macro, simulate_with, Action, CraftingState, SolverSettings,
};

use crate::dto::*;

const MAX_ACTIONS: usize = 100;

/// `GetInitialStateQuery`: a manual starting quality is clamped to the recipe's maximum.
pub fn initial_state(
    recipe: &RecipeParametersDto,
    stats: &CrafterStatsDto,
    starting_quality: i32,
) -> CraftingStateDto {
    let starting_quality = if starting_quality > 0 {
        starting_quality.min(recipe.quality)
    } else {
        0
    };
    let recipe = recipe.to_recipe(starting_quality);
    CraftingStateDto::from_state(&CraftingState::create_initial(&recipe, &stats.to_stats()))
}

/// `ExecuteSingleActionCommand`.
pub fn execute_action(
    state: &CraftingStateDto,
    action_id: i32,
    stats: &CrafterStatsDto,
    recipe: &RecipeParametersDto,
) -> Result<StepResultDto, String> {
    if Action::from_id(action_id).is_none() {
        return Err(format!("Action ID {action_id} not found."));
    }
    let step = engine_execute(
        state.to_state(),
        action_id,
        &stats.to_stats(),
        &recipe.to_recipe(0),
    );
    Ok(StepResultDto::from_step(&step))
}

/// `GetAvailableActionsQuery`.
pub fn available_actions(
    state: &CraftingStateDto,
    stats: &CrafterStatsDto,
    recipe: &RecipeParametersDto,
) -> Vec<ActionAvailabilityDto> {
    action_availability(&state.to_state(), &stats.to_stats(), &recipe.to_recipe(0))
        .iter()
        .map(ActionAvailabilityDto::from_availability)
        .collect()
}

/// `HQCalculator.CalculateStartingQuality` over the recipe's own ingredient list.
pub fn starting_quality(recipe: &RecipeParametersDto, hq_ingredients: &[HqIngredientDto]) -> i32 {
    let inputs: Vec<_> = hq_ingredients.iter().map(|h| h.to_input()).collect();
    calculate_starting_quality(
        recipe.quality,
        recipe.material_quality_factor,
        &recipe.ingredients(),
        &inputs,
    )
}

fn validate_action_ids(action_ids: &[i32]) -> Result<(), String> {
    if action_ids.is_empty() {
        return Err("At least one action is required (provide ActionIds or ActionNames).".into());
    }
    if action_ids.len() > MAX_ACTIONS {
        return Err(format!(
            "Action sequence exceeds maximum of {MAX_ACTIONS} actions."
        ));
    }
    for id in action_ids {
        if Action::from_id(*id).is_none() {
            return Err(format!("Action ID {id} not found."));
        }
    }
    Ok(())
}

/// `SimulateCraftCommand` without its recipe summary, which the caller adds.
pub fn simulate(
    recipe: &RecipeParametersDto,
    stats: &CrafterStatsDto,
    action_ids: &[i32],
    options: &SimulationOptionsDto,
    hq_ingredients: &[HqIngredientDto],
) -> Result<SimulationResultDto, String> {
    validate_action_ids(action_ids)?;
    let starting = if hq_ingredients.is_empty() {
        0
    } else {
        starting_quality(recipe, hq_ingredients)
    };
    let result = simulate_with(
        &recipe.to_recipe(starting),
        &stats.to_stats(),
        action_ids,
        &options.to_options(),
    );
    let macros = export_to_macro(action_ids, &Default::default());
    Ok(SimulationResultDto::from_simulation(&result, macros))
}

/// `ExportMacroCommand`.
pub fn export_macro(
    action_ids: &[i32],
    options: &MacroOptionsDto,
) -> Result<ExportMacroResultDto, String> {
    if action_ids.is_empty() {
        return Err("At least one action ID is required.".into());
    }
    if action_ids.len() > MAX_ACTIONS {
        return Err(format!(
            "Action sequence exceeds maximum of {MAX_ACTIONS} actions."
        ));
    }
    for id in action_ids {
        if Action::from_id(*id).is_none() {
            return Err(format!("Action ID {id} not found."));
        }
    }
    let options = options.to_options();
    if options.lines_per_macro == 0 {
        return Err("LinesPerMacro must be at least 1.".into());
    }
    let macros = export_to_macro(action_ids, &options);
    Ok(ExportMacroResultDto {
        macro_count: macros.len() as i32,
        total_actions: action_ids.len() as i32,
        macros,
    })
}

/// `GetCraftingActionsQuery`: registry order, then a stable sort by category name and level,
/// filtered to the crafter's level when given.
pub fn action_catalog(level: Option<i32>) -> Vec<ActionInfoDto> {
    let mut catalog: Vec<ActionInfoDto> = Action::ALL
        .iter()
        .map(|a| ActionInfoDto::from_action(*a))
        .collect();
    catalog.sort_by(|a, b| {
        a.category
            .cmp(&b.category)
            .then(a.level_requirement.cmp(&b.level_requirement))
    });
    if let Some(level) = level {
        catalog.retain(|a| a.level_requirement <= level);
    }
    catalog
}

/// `QuickSolveQuery`: the greedy solver with default constraints. Stats and HQ materials are the
/// caller's; the API's query takes neither a specialist flag nor HQ inputs, so pass the defaults
/// to match it.
pub fn quick_solve(
    recipe: &RecipeParametersDto,
    stats: &CrafterStatsDto,
    starting_quality: i32,
    now_ms: Clock<'_>,
) -> SolverResultDto {
    let result = xiv_craft::solver::greedy::solve(
        &recipe.to_recipe(starting_quality),
        &stats.to_stats(),
        &SolverConstraints::default(),
        &GreedySolverConfig::default(),
        now_ms,
    );
    SolverResultDto::from_result(&result)
}

/// One MCTS search at a time, sliced so a Web Worker can yield between slices.
pub struct Session {
    solver: MctsSolver,
}

impl Session {
    pub fn new(max_nodes: usize) -> Session {
        let config = MctsConfig {
            max_nodes,
            ..MctsConfig::default()
        };
        Session {
            solver: MctsSolver::new(config, GreedySolverConfig::default()),
        }
    }

    pub fn begin(
        &mut self,
        recipe: &RecipeParametersDto,
        stats: &CrafterStatsDto,
        starting_quality: i32,
        constraints: &SolverConstraintsDto,
        settings: &SolverSettingsDto,
        now_ms: Clock<'_>,
    ) {
        self.solver.begin(
            &recipe.to_recipe(starting_quality),
            &stats.to_stats(),
            &constraints.to_constraints(),
            &SolverSettings {
                time_budget_ms: settings.time_budget_ms,
                max_iterations: settings.max_iterations,
                random_seed: settings.random_seed,
            },
            now_ms,
        );
    }

    pub fn run(&mut self, max_iterations: u64, now_ms: Clock<'_>) -> SearchProgressDto {
        progress_dto(self.solver.run(max_iterations, now_ms))
    }

    pub fn progress(&self, now_ms: Clock<'_>) -> SearchProgressDto {
        progress_dto(self.solver.progress(now_ms))
    }

    pub fn finish(&mut self, now_ms: Clock<'_>) -> SolverResultDto {
        SolverResultDto::from_result(&self.solver.finish(now_ms))
    }

    pub fn tree_memory_bytes(&self) -> usize {
        self.solver.tree_memory_bytes()
    }
}

fn progress_dto(progress: SearchProgress) -> SearchProgressDto {
    SearchProgressDto {
        iterations: progress.iterations as f64,
        nodes_explored: progress.nodes_explored as f64,
        elapsed_ms: progress.elapsed_ms,
        best_steps: progress.best_steps as i32,
        best_quality_percent: progress.best_quality_percent,
        best_hq_percent: progress.best_hq_percent,
        done: progress.done,
    }
}
