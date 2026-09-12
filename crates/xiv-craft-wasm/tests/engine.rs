//! Host-side tests of the DTO mappings and endpoint behaviours; the wasm exports are thin
//! wrappers over these. The fixture is the C# `CraftingTestHelpers` recipe and crafter.

use xiv_craft::{Action, CraftingState, Recipe};
use xiv_craft_wasm::dto::*;
use xiv_craft_wasm::engine;

fn recipe() -> RecipeParametersDto {
    RecipeParametersDto {
        recipe_id: 100,
        recipe_level: 580,
        difficulty: 3500,
        quality: 7200,
        durability: 70,
        progress_divider: 130,
        progress_modifier: 80,
        quality_divider: 115,
        quality_modifier: 70,
        class_job_level: 90,
        stars: 2,
        is_expert: false,
        conditions_flag: 0,
        suggested_craftsmanship: 3700,
        suggested_control: 3280,
        material_quality_factor: 75,
        ingredients: vec![
            IngredientDto {
                item_id: 1,
                quantity: 2,
                can_be_hq: true,
            },
            IngredientDto {
                item_id: 2,
                quantity: 1,
                can_be_hq: false,
            },
        ],
    }
}

fn stats() -> CrafterStatsDto {
    CrafterStatsDto {
        level: 90,
        craftsmanship: 3800,
        control: 3600,
        cp: 600,
        is_specialist: false,
    }
}

fn frozen() -> impl FnMut() -> f64 {
    || 0.0
}

#[test]
fn recipe_dto_uses_the_api_field_names() {
    let json = serde_json::to_value(recipe()).unwrap();
    assert_eq!(json["progressDivider"], 130);
    assert_eq!(json["materialQualityFactor"], 75);
    assert_eq!(json["ingredients"][0]["canBeHQ"], true);

    let parsed: RecipeParametersDto = serde_json::from_value(json).unwrap();
    assert_eq!(parsed, recipe());
    assert_eq!(parsed.to_recipe(0).recipe_level, 580);
}

#[test]
fn recipe_dto_accepts_a_minimal_payload() {
    let parsed: RecipeParametersDto = serde_json::from_str(
        r#"{"recipeId":1,"recipeLevel":10,"difficulty":100,"quality":200,"durability":40,
            "progressDivider":50,"progressModifier":100,"qualityDivider":30,"qualityModifier":100}"#,
    )
    .unwrap();
    assert_eq!(parsed.stars, 0);
    assert!(!parsed.is_expert);
    assert!(parsed.ingredients.is_empty());
}

#[test]
fn initial_state_matches_the_api_shape() {
    let state = engine::initial_state(&recipe(), &stats(), 0);
    let json = serde_json::to_value(&state).unwrap();

    assert_eq!(json["maxCP"], 600);
    assert_eq!(json["hqPercent"], 1);
    assert_eq!(json["condition"], "Normal");
    assert_eq!(json["status"], "InProgress");
    assert_eq!(json["isFirstStep"], true);
    assert_eq!(json["activeBuffs"], serde_json::json!([]));
    assert_eq!(state.max_progress, 3500);
    assert_eq!(state.durability, 70);
}

#[test]
fn initial_state_clamps_a_manual_starting_quality() {
    assert_eq!(
        engine::initial_state(&recipe(), &stats(), 9000).quality,
        7200
    );
    assert_eq!(engine::initial_state(&recipe(), &stats(), 500).quality, 500);
    assert_eq!(engine::initial_state(&recipe(), &stats(), -5).quality, 0);
}

#[test]
fn state_round_trips_through_the_named_buff_list() {
    let state = CraftingState {
        progress: 100,
        max_progress: 3500,
        quality: 200,
        max_quality: 7200,
        durability: 50,
        max_durability: 70,
        cp: 400,
        max_cp: 600,
        step: 3,
        inner_quiet_stacks: 2,
        waste_not_ii_turns: 5,
        innovation_turns: 3,
        manipulation_turns: 7,
        heart_and_soul_active: true,
        heart_and_soul_used: true,
        careful_observation_count: 1,
        combo_action: Action::BasicTouch.id(),
        observe_active: true,
        ..CraftingState::default()
    };

    let dto = CraftingStateDto::from_state(&state);
    let names: Vec<&str> = dto.active_buffs.iter().map(|b| b.name.as_str()).collect();
    assert_eq!(
        names,
        [
            "Waste Not II",
            "Innovation",
            "Manipulation",
            "Heart and Soul"
        ]
    );
    assert_eq!(dto.active_buffs[3].remaining_turns, 1);
    assert!(dto.active_buffs.iter().all(|b| b.stacks.is_none()));

    let json = serde_json::to_string(&dto).unwrap();
    assert!(json.contains("\"stacks\":null"));
    let back: CraftingStateDto = serde_json::from_str(&json).unwrap();
    assert_eq!(back.to_state(), state);
}

#[test]
fn every_timed_buff_field_is_mapped() {
    // A new turn counter on CraftingState must be added to the named list, or the
    // step-by-step round trip silently drops it.
    let full = CraftingState {
        waste_not_turns: 1,
        waste_not_ii_turns: 2,
        veneration_turns: 3,
        innovation_turns: 4,
        great_strides_turns: 5,
        manipulation_turns: 6,
        final_appraisal_turns: 7,
        muscle_memory_turns: 8,
        heart_and_soul_active: true,
        trained_perfection_active: true,
        ..CraftingState::default()
    };
    let dto = CraftingStateDto::from_state(&full);
    assert_eq!(dto.active_buffs.len(), 10);
    assert_eq!(dto.to_state(), full);
}

#[test]
fn unknown_condition_and_status_names_fall_back_like_try_parse() {
    assert_eq!(parse_condition("gOOd"), xiv_craft::Condition::Good);
    assert_eq!(parse_condition("nope"), xiv_craft::Condition::Normal);
    assert_eq!(parse_status("completed"), xiv_craft::CraftStatus::Completed);
    assert_eq!(parse_status(""), xiv_craft::CraftStatus::InProgress);
}

#[test]
fn execute_action_returns_the_step_dto() {
    let initial = engine::initial_state(&recipe(), &stats(), 0);
    let step =
        engine::execute_action(&initial, Action::BasicSynthesis.id(), &stats(), &recipe()).unwrap();

    assert_eq!(step.step_number, 1);
    assert_eq!(step.action_name, "Basic Synthesis");
    assert_eq!(step.action_category, "Progress");
    assert!(step.succeeded);
    assert_eq!(step.fail_reason, None);
    assert!(step.deltas.progress > 0);
    assert_eq!(step.deltas.durability, -10);
    assert_eq!(step.state_after.step, 1);
    assert!(!step.state_after.is_first_step);
}

#[test]
fn execute_action_reports_a_fail_reason_and_an_unknown_id() {
    let initial = engine::initial_state(&recipe(), &stats(), 0);
    let step = engine::execute_action(&initial, Action::ByregotsBlessing.id(), &stats(), &recipe())
        .unwrap();
    assert!(!step.succeeded);
    assert_eq!(
        step.fail_reason.as_deref(),
        Some("Requires Inner Quiet stacks")
    );
    assert_eq!(step.state_after, initial);

    assert_eq!(
        engine::execute_action(&initial, 42, &stats(), &recipe()).unwrap_err(),
        "Action ID 42 not found."
    );
}

#[test]
fn available_actions_lists_every_action_in_registry_order() {
    let initial = engine::initial_state(&recipe(), &stats(), 0);
    let table = engine::available_actions(&initial, &stats(), &recipe());

    assert_eq!(table.len(), Action::ALL.len());
    assert_eq!(table[0].action_id, Action::BasicSynthesis.id());
    assert_eq!(table[0].action_name, "Basic Synthesis");
    assert!(table[0].is_available);
    assert_eq!(table[0].category, "Progress");
    let byregot = table
        .iter()
        .find(|a| a.action_id == Action::ByregotsBlessing.id())
        .unwrap();
    assert!(!byregot.is_available);
    assert_eq!(
        byregot.unavailable_reason.as_deref(),
        Some("Requires Inner Quiet stacks")
    );
    assert_eq!(byregot.cp_cost, 24);
}

#[test]
fn starting_quality_follows_the_hq_calculator() {
    let all_hq = engine::starting_quality(
        &recipe(),
        &[HqIngredientDto {
            item_id: 1,
            hq_quantity: 2,
        }],
    );
    assert_eq!(all_hq, 5400, "75% of 7200 when every HQ-able unit is HQ");
    let half = engine::starting_quality(
        &recipe(),
        &[HqIngredientDto {
            item_id: 1,
            hq_quantity: 1,
        }],
    );
    assert_eq!(half, 2700);
    assert_eq!(engine::starting_quality(&recipe(), &[]), 0);
}

#[test]
fn simulate_applies_hq_materials_and_returns_macros() {
    let ids = [Action::MuscleMemory.id(), Action::BasicSynthesis.id()];
    let result = engine::simulate(
        &recipe(),
        &stats(),
        &ids,
        &SimulationOptionsDto::default(),
        &[HqIngredientDto {
            item_id: 1,
            hq_quantity: 2,
        }],
    )
    .unwrap();

    assert_eq!(result.total_steps, 2);
    assert_eq!(result.steps[0].state_after.quality, 5400);
    assert_eq!(result.macros.len(), 1);
    assert!(result.macros[0].starts_with("/ac \"Muscle Memory\" <wait.3>"));
    assert!(result.recipe.is_none());
    assert_eq!(
        serde_json::to_value(&result).unwrap()["finalHQPercent"],
        result.final_hq_percent
    );
}

#[test]
fn simulate_validates_like_the_command() {
    let err = |ids: &[i32]| {
        engine::simulate(
            &recipe(),
            &stats(),
            ids,
            &SimulationOptionsDto::default(),
            &[],
        )
        .unwrap_err()
    };
    assert_eq!(
        err(&[]),
        "At least one action is required (provide ActionIds or ActionNames)."
    );
    assert_eq!(err(&[7]), "Action ID 7 not found.");
    assert_eq!(
        err(&[Action::BasicTouch.id(); 101]),
        "Action sequence exceeds maximum of 100 actions."
    );
}

#[test]
fn simulate_options_take_the_csharp_defaults() {
    let options = SimulationOptionsDto {
        force_condition: Some("good".into()),
        ..Default::default()
    }
    .to_options();
    assert!(options.assume_all_success);
    assert!(options.assume_normal_condition);
    assert_eq!(options.force_condition, Some(xiv_craft::Condition::Good));
    assert_eq!(
        SimulationOptionsDto {
            force_condition: Some("bogus".into()),
            ..Default::default()
        }
        .to_options()
        .force_condition,
        None
    );
}

#[test]
fn export_macro_chunks_and_validates() {
    let ids = vec![Action::BasicTouch.id(); 20];
    let result = engine::export_macro(&ids, &MacroOptionsDto::default()).unwrap();
    assert_eq!(result.total_actions, 20);
    assert_eq!(result.macro_count, 2);
    assert!(result.macros[0].ends_with("/echo Macro #1 complete <se.1>"));
    assert!(result.macros[1].ends_with("/echo Craft complete <se.2>"));

    let short = engine::export_macro(
        &ids,
        &MacroOptionsDto {
            lines_per_macro: Some(5),
            include_echo: Some(false),
            ..Default::default()
        },
    )
    .unwrap();
    assert_eq!(short.macro_count, 4);
    assert!(!short.macros[0].contains("/echo"));

    assert_eq!(
        engine::export_macro(&[], &MacroOptionsDto::default()).unwrap_err(),
        "At least one action ID is required."
    );
    assert_eq!(
        engine::export_macro(&[1], &MacroOptionsDto::default()).unwrap_err(),
        "Action ID 1 not found."
    );
}

#[test]
fn action_catalog_sorts_by_category_name_then_level() {
    let all = engine::action_catalog(None);
    assert_eq!(all.len(), Action::ALL.len());
    let keys: Vec<(String, i32)> = all
        .iter()
        .map(|a| (a.category.clone(), a.level_requirement))
        .collect();
    let mut sorted = keys.clone();
    sorted.sort();
    assert_eq!(keys, sorted);
    assert_eq!(all[0].category, "Buff");

    let basic = all
        .iter()
        .find(|a| a.id == Action::BasicSynthesis.id())
        .unwrap();
    assert_eq!(basic.name, "BasicSynthesis");
    assert_eq!(basic.in_game_name, "Basic Synthesis");
    assert_eq!(serde_json::to_value(basic).unwrap()["baseCPCost"], 0);
    assert_eq!(basic.base_durability_cost, 10);

    let low = engine::action_catalog(Some(10));
    assert!(low.iter().all(|a| a.level_requirement <= 10));
    assert!(low.len() < all.len());
}

#[test]
fn quick_solve_is_greedy_with_the_api_metrics() {
    let result = engine::quick_solve(&recipe(), &stats(), 0, &mut frozen());
    assert_eq!(result.metrics.algorithm_used, "Greedy");
    assert!(result.metrics.craft_completed);
    assert_eq!(result.action_ids.len() as i32, result.metrics.total_steps);
    assert_eq!(result.action_names.len(), result.action_ids.len());
    assert!(
        result.action_names[0].chars().all(|c| c != ' '),
        "registry names"
    );
    let simulation = result.simulation.unwrap();
    assert_eq!(simulation.steps.len(), result.action_ids.len());
    assert_eq!(simulation.macros, result.macros);
    assert_eq!(result.error_message, None);
}

#[test]
fn quick_solve_on_an_expert_recipe_fails_like_the_api() {
    let expert = RecipeParametersDto {
        is_expert: true,
        ..recipe()
    };
    let result = engine::quick_solve(&expert, &stats(), 0, &mut frozen());
    assert_eq!(
        result.error_message.as_deref(),
        Some("Expert recipes require the MCTS solver.")
    );
    assert!(result.action_ids.is_empty());
    assert!(result.simulation.is_none());
    assert_eq!(result.metrics.algorithm_used, "Greedy");
}

#[test]
fn session_runs_in_slices_and_finishes_with_the_best_rotation() {
    let mut session = engine::Session::new(100_000);
    session.begin(
        &recipe(),
        &stats(),
        0,
        &SolverConstraintsDto::default(),
        &SolverSettingsDto {
            time_budget_ms: f64::INFINITY,
            max_iterations: Some(3_000),
            random_seed: Some(42),
        },
        &mut frozen(),
    );

    let first = session.run(1_000, &mut frozen());
    assert_eq!(first.iterations, 1_000.0);
    assert!(!first.done);
    assert!(first.best_steps > 0);
    assert!(first.best_quality_percent > 0.0);
    assert_eq!(session.progress(&mut frozen()), first);

    let mut last = first;
    while !last.done {
        last = session.run(1_000, &mut frozen());
    }
    assert_eq!(last.iterations, 3_000.0);

    let result = session.finish(&mut frozen());
    assert_eq!(result.metrics.algorithm_used, "MCTS");
    assert_eq!(result.metrics.iterations_completed, 3_000);
    assert_eq!(result.metrics.total_steps, last.best_steps);
    assert_eq!(
        result.metrics.final_quality_percent,
        last.best_quality_percent
    );
    assert!(result.metrics.craft_completed);
    assert!(session.tree_memory_bytes() > 0);
}

#[test]
fn metrics_dto_names_the_algorithm_like_the_enum() {
    assert_eq!(algorithm_name(None), "Auto");
    let recipe: Recipe = recipe().to_recipe(0);
    assert_eq!(recipe.material_quality_factor, 75);
}
