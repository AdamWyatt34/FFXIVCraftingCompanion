//! wasm-bindgen surface over `xiv-craft` for `frontend/src/wasm`. Every export takes and returns
//! the FFXIV.Api JSON shapes (see `dto.rs`), so the frontend's dispatcher can hand a result from
//! either engine to the same components. Errors carry the API's messages.
//!
//! Build with `npm run wasm:build` from `frontend/`.

pub mod dto;
pub mod engine;

use serde::Serialize;
use wasm_bindgen::prelude::*;
use wasm_bindgen::JsCast;

use dto::*;

#[wasm_bindgen]
extern "C" {
    #[wasm_bindgen(js_namespace = console)]
    fn error(message: &str);
}

/// The crate is built with `panic = "abort"`, so a panic ends in an unreachable trap and the
/// instance's memory is no longer trusted; the hook writes the message to the console first,
/// where the worker's error path would otherwise only see "unreachable executed".
#[wasm_bindgen(start)]
pub fn start() {
    std::panic::set_hook(Box::new(|info| {
        error(&format!("xiv-craft panicked: {info}"))
    }));
}

// The TypeScript the bindings publish: the frontend's own DTO types, imported relative to
// frontend/src/wasm where the generated declarations live, so `wasm-engine.ts` needs no casts.
#[wasm_bindgen(typescript_custom_section)]
const TYPESCRIPT_TYPES: &'static str = r#"
import type { RecipeCraftingParametersDto } from "../types/api";
import type {
  ActionAvailability,
  ActionInfo,
  CrafterStatsInput,
  CraftingState,
  ExportMacroResult,
  HQIngredientInput,
  MacroOptions,
  SimulationOptions,
  SimulationResult,
  StepResult,
} from "../types/simulator";
import type { SolverConstraints, SolverResult } from "../types/solver";

/** The simulate endpoint's result without its recipe summary, which the caller fills in. */
export type WasmSimulationResult = Omit<SimulationResult, "recipe"> & { recipe: null };

/** What an MCTS search has found so far (`SearchProgress` in the crate). */
export interface SearchProgress {
  iterations: number;
  nodesExplored: number;
  elapsedMs: number;
  bestSteps: number;
  bestQualityPercent: number;
  bestHQPercent: number;
  done: boolean;
}

export interface SolverSessionSettings {
  timeBudgetMs: number;
  maxIterations?: number;
  randomSeed?: number;
}
"#;

#[wasm_bindgen]
extern "C" {
    #[wasm_bindgen(typescript_type = "RecipeCraftingParametersDto")]
    pub type RecipeParametersJs;
    #[wasm_bindgen(typescript_type = "CrafterStatsInput")]
    pub type CrafterStatsJs;
    #[wasm_bindgen(typescript_type = "CraftingState")]
    pub type CraftingStateJs;
    #[wasm_bindgen(typescript_type = "StepResult")]
    pub type StepResultJs;
    #[wasm_bindgen(typescript_type = "ActionAvailability[]")]
    pub type ActionAvailabilityListJs;
    #[wasm_bindgen(typescript_type = "HQIngredientInput[] | null | undefined")]
    pub type HqIngredientsJs;
    #[wasm_bindgen(typescript_type = "SimulationOptions | null | undefined")]
    pub type SimulationOptionsJs;
    #[wasm_bindgen(typescript_type = "WasmSimulationResult")]
    pub type SimulationResultJs;
    #[wasm_bindgen(typescript_type = "MacroOptions | null | undefined")]
    pub type MacroOptionsJs;
    #[wasm_bindgen(typescript_type = "ExportMacroResult")]
    pub type ExportMacroResultJs;
    #[wasm_bindgen(typescript_type = "ActionInfo[]")]
    pub type ActionCatalogJs;
    #[wasm_bindgen(typescript_type = "SolverConstraints | null | undefined")]
    pub type SolverConstraintsJs;
    #[wasm_bindgen(typescript_type = "SolverResult")]
    pub type SolverResultJs;
    #[wasm_bindgen(typescript_type = "SolverSessionSettings")]
    pub type SolverSettingsJs;
    #[wasm_bindgen(typescript_type = "SearchProgress")]
    pub type SearchProgressJs;
}

/// `Option::None` becomes `null`, as the API sends it; the frontend types say `| null`.
fn to_js<T: Serialize, J: JsCast>(value: &T) -> Result<J, JsError> {
    value
        .serialize(&serde_wasm_bindgen::Serializer::json_compatible())
        .map(JsCast::unchecked_from_js)
        .map_err(|e| JsError::new(&e.to_string()))
}

fn from_js<T: serde::de::DeserializeOwned>(value: impl Into<JsValue>) -> Result<T, JsError> {
    serde_wasm_bindgen::from_value(value.into()).map_err(|e| JsError::new(&e.to_string()))
}

fn optional<T: serde::de::DeserializeOwned + Default>(
    value: impl Into<JsValue>,
) -> Result<T, JsError> {
    let value = value.into();
    if value.is_undefined() || value.is_null() {
        Ok(T::default())
    } else {
        from_js(value)
    }
}

fn message(error: String) -> JsError {
    JsError::new(&error)
}

/// Milliseconds since the epoch; only differences are read, and the solver samples it every 64
/// iterations, so the JS interop cost is negligible against the search itself.
fn js_clock() -> impl FnMut() -> f64 {
    js_sys::Date::now
}

#[wasm_bindgen]
pub fn initial_state(
    recipe: RecipeParametersJs,
    stats: CrafterStatsJs,
    starting_quality: i32,
) -> Result<CraftingStateJs, JsError> {
    let recipe: RecipeParametersDto = from_js(recipe)?;
    let stats: CrafterStatsDto = from_js(stats)?;
    to_js(&engine::initial_state(&recipe, &stats, starting_quality).map_err(message)?)
}

#[wasm_bindgen]
pub fn execute_action(
    state: CraftingStateJs,
    action_id: i32,
    stats: CrafterStatsJs,
    recipe: RecipeParametersJs,
) -> Result<StepResultJs, JsError> {
    let state: CraftingStateDto = from_js(state)?;
    let stats: CrafterStatsDto = from_js(stats)?;
    let recipe: RecipeParametersDto = from_js(recipe)?;
    to_js(&engine::execute_action(&state, action_id, &stats, &recipe).map_err(message)?)
}

#[wasm_bindgen]
pub fn available_actions(
    state: CraftingStateJs,
    stats: CrafterStatsJs,
    recipe: RecipeParametersJs,
) -> Result<ActionAvailabilityListJs, JsError> {
    let state: CraftingStateDto = from_js(state)?;
    let stats: CrafterStatsDto = from_js(stats)?;
    let recipe: RecipeParametersDto = from_js(recipe)?;
    to_js(&engine::available_actions(&state, &stats, &recipe).map_err(message)?)
}

#[wasm_bindgen]
pub fn starting_quality(
    recipe: RecipeParametersJs,
    hq_ingredients: HqIngredientsJs,
) -> Result<i32, JsError> {
    let recipe: RecipeParametersDto = from_js(recipe)?;
    let hq: Vec<HqIngredientDto> = optional(hq_ingredients)?;
    Ok(engine::starting_quality(&recipe, &hq))
}

#[wasm_bindgen]
pub fn simulate(
    recipe: RecipeParametersJs,
    stats: CrafterStatsJs,
    action_ids: Vec<i32>,
    options: SimulationOptionsJs,
    hq_ingredients: HqIngredientsJs,
) -> Result<SimulationResultJs, JsError> {
    let recipe: RecipeParametersDto = from_js(recipe)?;
    let stats: CrafterStatsDto = from_js(stats)?;
    let options: SimulationOptionsDto = optional(options)?;
    let hq: Vec<HqIngredientDto> = optional(hq_ingredients)?;
    to_js(&engine::simulate(&recipe, &stats, &action_ids, &options, &hq).map_err(message)?)
}

#[wasm_bindgen]
pub fn export_macro(
    action_ids: Vec<i32>,
    options: MacroOptionsJs,
) -> Result<ExportMacroResultJs, JsError> {
    let options: MacroOptionsDto = optional(options)?;
    to_js(&engine::export_macro(&action_ids, &options).map_err(message)?)
}

#[wasm_bindgen]
pub fn action_catalog(level: Option<i32>) -> Result<ActionCatalogJs, JsError> {
    to_js(&engine::action_catalog(level))
}

/// The greedy solver; `constraints` may be null for the API's quick-solve defaults.
#[wasm_bindgen]
pub fn greedy_solve(
    recipe: RecipeParametersJs,
    stats: CrafterStatsJs,
    starting_quality: i32,
    constraints: SolverConstraintsJs,
) -> Result<SolverResultJs, JsError> {
    let recipe: RecipeParametersDto = from_js(recipe)?;
    let stats: CrafterStatsDto = from_js(stats)?;
    let constraints: SolverConstraintsDto = optional(constraints)?;
    to_js(
        &engine::greedy_solve(
            &recipe,
            &stats,
            starting_quality,
            &constraints,
            &mut js_clock(),
        )
        .map_err(message)?,
    )
}

/// A resumable MCTS search. `begin`, then `run` in slices until the progress says `done` (or the
/// caller decides to stop), then `finish`. The tree is kept between searches. Bad input is an
/// error from `begin` and leaves the session as it was; a step without a search is an error too.
#[wasm_bindgen]
pub struct SolverSession {
    inner: engine::Session,
}

#[wasm_bindgen]
impl SolverSession {
    /// `max_nodes` caps the tree (about 130 bytes per node in practice). `early_termination`
    /// is the iterations-without-improvement stop; omit it for the default of 5000.
    #[wasm_bindgen(constructor)]
    pub fn new(max_nodes: u32, early_termination: Option<u32>) -> SolverSession {
        SolverSession {
            inner: engine::Session::new(max_nodes as usize, early_termination.map(u64::from)),
        }
    }

    pub fn begin(
        &mut self,
        recipe: RecipeParametersJs,
        stats: CrafterStatsJs,
        starting_quality: i32,
        constraints: SolverConstraintsJs,
        settings: SolverSettingsJs,
    ) -> Result<(), JsError> {
        let recipe: RecipeParametersDto = from_js(recipe)?;
        let stats: CrafterStatsDto = from_js(stats)?;
        let constraints: SolverConstraintsDto = optional(constraints)?;
        let settings: SolverSettingsDto = from_js(settings)?;
        self.inner
            .begin(
                &recipe,
                &stats,
                starting_quality,
                &constraints,
                &settings,
                &mut js_clock(),
            )
            .map_err(message)
    }

    pub fn run(&mut self, max_iterations: u32) -> Result<SearchProgressJs, JsError> {
        to_js(
            &self
                .inner
                .run(max_iterations as u64, &mut js_clock())
                .map_err(message)?,
        )
    }

    pub fn progress(&self) -> Result<SearchProgressJs, JsError> {
        to_js(&self.inner.progress(&mut js_clock()).map_err(message)?)
    }

    pub fn finish(&mut self) -> Result<SolverResultJs, JsError> {
        to_js(&self.inner.finish(&mut js_clock()).map_err(message)?)
    }

    pub fn tree_memory_bytes(&self) -> f64 {
        self.inner.tree_memory_bytes() as f64
    }
}
