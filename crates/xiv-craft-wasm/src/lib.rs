//! wasm-bindgen surface over `xiv-craft` for `frontend/src/wasm`. Every export takes and returns
//! the FFXIV.Api JSON shapes (see `dto.rs`), so the frontend's dispatcher can hand a result from
//! either engine to the same components. Errors carry the API's messages.
//!
//! Build with `npm run wasm:build` from `frontend/`.

pub mod dto;
pub mod engine;

use serde::Serialize;
use wasm_bindgen::prelude::*;

use dto::*;

/// `Option::None` becomes `null`, as the API sends it; the frontend types say `| null`.
fn to_js<T: Serialize>(value: &T) -> Result<JsValue, JsError> {
    value
        .serialize(&serde_wasm_bindgen::Serializer::json_compatible())
        .map_err(|e| JsError::new(&e.to_string()))
}

fn from_js<T: serde::de::DeserializeOwned>(value: JsValue) -> Result<T, JsError> {
    serde_wasm_bindgen::from_value(value).map_err(|e| JsError::new(&e.to_string()))
}

fn optional<T: serde::de::DeserializeOwned + Default>(value: JsValue) -> Result<T, JsError> {
    if value.is_undefined() || value.is_null() {
        Ok(T::default())
    } else {
        from_js(value)
    }
}

/// Milliseconds since the epoch; only differences are read, and the solver reads it once per
/// iteration, so the JS interop cost is the same as `performance.now()`.
fn js_clock() -> impl FnMut() -> f64 {
    js_sys::Date::now
}

#[wasm_bindgen]
pub fn initial_state(
    recipe: JsValue,
    stats: JsValue,
    starting_quality: i32,
) -> Result<JsValue, JsError> {
    let recipe: RecipeParametersDto = from_js(recipe)?;
    let stats: CrafterStatsDto = from_js(stats)?;
    to_js(&engine::initial_state(&recipe, &stats, starting_quality))
}

#[wasm_bindgen]
pub fn execute_action(
    state: JsValue,
    action_id: i32,
    stats: JsValue,
    recipe: JsValue,
) -> Result<JsValue, JsError> {
    let state: CraftingStateDto = from_js(state)?;
    let stats: CrafterStatsDto = from_js(stats)?;
    let recipe: RecipeParametersDto = from_js(recipe)?;
    let step =
        engine::execute_action(&state, action_id, &stats, &recipe).map_err(|m| JsError::new(&m))?;
    to_js(&step)
}

#[wasm_bindgen]
pub fn available_actions(
    state: JsValue,
    stats: JsValue,
    recipe: JsValue,
) -> Result<JsValue, JsError> {
    let state: CraftingStateDto = from_js(state)?;
    let stats: CrafterStatsDto = from_js(stats)?;
    let recipe: RecipeParametersDto = from_js(recipe)?;
    to_js(&engine::available_actions(&state, &stats, &recipe))
}

#[wasm_bindgen]
pub fn starting_quality(recipe: JsValue, hq_ingredients: JsValue) -> Result<i32, JsError> {
    let recipe: RecipeParametersDto = from_js(recipe)?;
    let hq: Vec<HqIngredientDto> = optional(hq_ingredients)?;
    Ok(engine::starting_quality(&recipe, &hq))
}

#[wasm_bindgen]
pub fn simulate(
    recipe: JsValue,
    stats: JsValue,
    action_ids: Vec<i32>,
    options: JsValue,
    hq_ingredients: JsValue,
) -> Result<JsValue, JsError> {
    let recipe: RecipeParametersDto = from_js(recipe)?;
    let stats: CrafterStatsDto = from_js(stats)?;
    let options: SimulationOptionsDto = optional(options)?;
    let hq: Vec<HqIngredientDto> = optional(hq_ingredients)?;
    let result = engine::simulate(&recipe, &stats, &action_ids, &options, &hq)
        .map_err(|m| JsError::new(&m))?;
    to_js(&result)
}

#[wasm_bindgen]
pub fn export_macro(action_ids: Vec<i32>, options: JsValue) -> Result<JsValue, JsError> {
    let options: MacroOptionsDto = optional(options)?;
    let result = engine::export_macro(&action_ids, &options).map_err(|m| JsError::new(&m))?;
    to_js(&result)
}

#[wasm_bindgen]
pub fn action_catalog(level: Option<i32>) -> Result<JsValue, JsError> {
    to_js(&engine::action_catalog(level))
}

/// The greedy solver; `constraints` may be null for the API's quick-solve defaults.
#[wasm_bindgen]
pub fn greedy_solve(
    recipe: JsValue,
    stats: JsValue,
    starting_quality: i32,
    constraints: JsValue,
) -> Result<JsValue, JsError> {
    let recipe: RecipeParametersDto = from_js(recipe)?;
    let stats: CrafterStatsDto = from_js(stats)?;
    let constraints: SolverConstraintsDto = optional(constraints)?;
    to_js(&engine::greedy_solve(
        &recipe,
        &stats,
        starting_quality,
        &constraints,
        &mut js_clock(),
    ))
}

/// A resumable MCTS search. `begin`, then `run` in slices until the progress says `done` (or the
/// caller decides to stop), then `finish`. The tree is kept between searches.
#[wasm_bindgen]
pub struct SolverSession {
    inner: engine::Session,
}

#[wasm_bindgen]
impl SolverSession {
    /// `max_nodes` caps the tree (about 130 bytes per node in practice). `early_termination`
    /// is the iterations-without-improvement stop; omit it for the C# default of 5000.
    #[wasm_bindgen(constructor)]
    pub fn new(max_nodes: u32, early_termination: Option<u32>) -> SolverSession {
        SolverSession {
            inner: engine::Session::new(max_nodes as usize, early_termination.map(u64::from)),
        }
    }

    pub fn begin(
        &mut self,
        recipe: JsValue,
        stats: JsValue,
        starting_quality: i32,
        constraints: JsValue,
        settings: JsValue,
    ) -> Result<(), JsError> {
        let recipe: RecipeParametersDto = from_js(recipe)?;
        let stats: CrafterStatsDto = from_js(stats)?;
        let constraints: SolverConstraintsDto = optional(constraints)?;
        let settings: SolverSettingsDto = from_js(settings)?;
        self.inner.begin(
            &recipe,
            &stats,
            starting_quality,
            &constraints,
            &settings,
            &mut js_clock(),
        );
        Ok(())
    }

    pub fn run(&mut self, max_iterations: u32) -> Result<JsValue, JsError> {
        to_js(&self.inner.run(max_iterations as u64, &mut js_clock()))
    }

    pub fn progress(&self) -> Result<JsValue, JsError> {
        to_js(&self.inner.progress(&mut js_clock()))
    }

    pub fn finish(&mut self) -> Result<JsValue, JsError> {
        to_js(&self.inner.finish(&mut js_clock()))
    }

    pub fn tree_memory_bytes(&self) -> f64 {
        self.inner.tree_memory_bytes() as f64
    }
}
