//! The endpoint behaviours of `CraftingController` over the DTOs, with the clock injected so the
//! host tests can run them; `lib.rs` wraps these for wasm-bindgen with the JS clock.

use xiv_craft::solver::mcts::SearchProgress;
use xiv_craft::solver::{Clock, GreedySolverConfig, MctsConfig, MctsSolver};
use xiv_craft::{
    action_availability, calculate_starting_quality, execute_action as engine_execute,
    export_to_macro, simulate_with, Action, CraftingState, SolverSettings,
};

use crate::dto::*;

const MAX_ACTIONS: usize = 100;

/// The request rules of `StartSolveCommand` and `SimulateCraftCommand`, plus what the engine
/// itself cannot take: a divider of zero divides by it, and a value past the tree's packed
/// representation would abort a search (and, in WASM, the instance). Returns the starting
/// quality clamped to the recipe's maximum, as `GetInitialStateQuery` does.
pub fn validate(
    recipe: &RecipeParametersDto,
    stats: &CrafterStatsDto,
    starting_quality: i32,
) -> Result<i32, String> {
    if stats.level < 1 || stats.level > 100 {
        return Err("Level must be between 1 and 100.".into());
    }
    if stats.craftsmanship <= 0 || stats.cp <= 0 {
        return Err("Craftsmanship and CP must be positive.".into());
    }
    if stats.control < 0 {
        return Err("Control must be non-negative.".into());
    }
    if recipe.progress_divider <= 0 || recipe.quality_divider <= 0 {
        return Err("Recipe level data is unavailable for this recipe.".into());
    }
    if recipe.difficulty <= 0 || recipe.quality <= 0 || recipe.durability <= 0 {
        return Err("Recipe difficulty, quality and durability must be positive.".into());
    }
    for (field, value, max) in [
        ("Difficulty", recipe.difficulty, i32::from(u16::MAX)),
        ("Quality", recipe.quality, i32::from(u16::MAX)),
        ("Durability", recipe.durability, i32::from(i16::MAX)),
        ("CP", stats.cp, i32::from(i16::MAX)),
    ] {
        if value > max {
            return Err(format!("{field} {value} is above the engine's maximum of {max}."));
        }
    }
    Ok(starting_quality.clamp(0, recipe.quality))
}

/// `GetInitialStateQuery`: a manual starting quality is clamped to the recipe's maximum.
pub fn initial_state(
    recipe: &RecipeParametersDto,
    stats: &CrafterStatsDto,
    starting_quality: i32,
) -> Result<CraftingStateDto, String> {
    let starting_quality = validate(recipe, stats, starting_quality)?;
    let recipe = recipe.to_recipe(starting_quality);
    Ok(CraftingStateDto::from_state(&CraftingState::create_initial(
        &recipe,
        &stats.to_stats(),
    )))
}

/// `ExecuteSingleActionCommand`.
pub fn execute_action(
    state: &CraftingStateDto,
    action_id: i32,
    stats: &CrafterStatsDto,
    recipe: &RecipeParametersDto,
) -> Result<StepResultDto, String> {
    validate(recipe, stats, 0)?;
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
) -> Result<Vec<ActionAvailabilityDto>, String> {
    validate(recipe, stats, 0)?;
    Ok(
        action_availability(&state.to_state(), &stats.to_stats(), &recipe.to_recipe(0))
            .iter()
            .map(ActionAvailabilityDto::from_availability)
            .collect(),
    )
}

/// `HQCalculator.CalculateStartingQuality` over the recipe's own ingredient list. An HQ count
/// above the ingredient's quantity (or below zero) counts as the quantity (or zero).
pub fn starting_quality(recipe: &RecipeParametersDto, hq_ingredients: &[HqIngredientDto]) -> i32 {
    let inputs: Vec<_> = hq_ingredients
        .iter()
        .map(|h| {
            let quantity = recipe
                .ingredients
                .iter()
                .find(|i| i.item_id == h.item_id)
                .map_or(h.hq_quantity, |i| i.quantity)
                .max(0);
            HqIngredientDto {
                item_id: h.item_id,
                hq_quantity: h.hq_quantity.clamp(0, quantity),
            }
            .to_input()
        })
        .collect();
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
    validate(recipe, stats, 0)?;
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

/// The greedy solver. `QuickSolveQuery` runs it with default constraints and no HQ materials; a
/// solve job that resolved to Greedy runs it with the request's constraints and starting quality.
pub fn greedy_solve(
    recipe: &RecipeParametersDto,
    stats: &CrafterStatsDto,
    starting_quality: i32,
    constraints: &SolverConstraintsDto,
    now_ms: Clock<'_>,
) -> Result<SolverResultDto, String> {
    let starting_quality = validate(recipe, stats, starting_quality)?;
    let result = xiv_craft::solver::greedy::solve(
        &recipe.to_recipe(starting_quality),
        &stats.to_stats(),
        &constraints.to_constraints(),
        &GreedySolverConfig::default(),
        now_ms,
    );
    Ok(SolverResultDto::from_result(&result))
}

/// One MCTS search at a time, sliced so a Web Worker can yield between slices.
pub struct Session {
    solver: MctsSolver,
}

impl Session {
    /// `early_termination_threshold` is the C# rule (iterations without improvement) when `None`.
    /// That count was tuned for the C# iteration rate; at WASM rates it stops a search within a
    /// few tens of milliseconds, so a browser passes a large value and applies its own stall rule.
    pub fn new(max_nodes: usize, early_termination_threshold: Option<u64>) -> Session {
        let defaults = MctsConfig::default();
        let config = MctsConfig {
            max_nodes,
            early_termination_threshold: early_termination_threshold
                .unwrap_or(defaults.early_termination_threshold),
            ..defaults
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
    ) -> Result<(), String> {
        let starting_quality = validate(recipe, stats, starting_quality)?;
        self.solver
            .begin(
                &recipe.to_recipe(starting_quality),
                &stats.to_stats(),
                &constraints.to_constraints(),
                &SolverSettings {
                    time_budget_ms: settings.time_budget_ms,
                    max_iterations: settings.max_iterations,
                    random_seed: settings.random_seed,
                },
                now_ms,
            )
            .map_err(|e| e.to_string())
    }

    pub fn run(&mut self, max_iterations: u64, now_ms: Clock<'_>) -> Result<SearchProgressDto, String> {
        self.solver
            .run(max_iterations, now_ms)
            .map(progress_dto)
            .map_err(|e| e.to_string())
    }

    pub fn progress(&self, now_ms: Clock<'_>) -> Result<SearchProgressDto, String> {
        self.solver
            .progress(now_ms)
            .map(progress_dto)
            .map_err(|e| e.to_string())
    }

    pub fn finish(&mut self, now_ms: Clock<'_>) -> Result<SolverResultDto, String> {
        self.solver
            .finish(now_ms)
            .map(|r| SolverResultDto::from_result(&r))
            .map_err(|e| e.to_string())
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
