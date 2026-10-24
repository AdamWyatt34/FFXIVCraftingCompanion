//! Full-run snapshot pins. Arithmetic is checked independently in reference_parity.rs.

mod benchmarks;
mod common;

use serde::{Deserialize, Serialize};
use std::path::PathBuf;
use xiv_craft::{
    action_availability, create_initial, execute_action, export_to_macro, simulate_with, Action,
    Condition, CraftStatus, CrafterStats, CraftingState, MacroOptions, Recipe, SimulationOptions,
};

const MIN_GOLDEN_FILES: usize = 10;

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct Case {
    case: String,
    recipe: RecipeJson,
    stats: StatsJson,
    options: OptionsJson,
    actions: Vec<i32>,
    initial: StateJson,
    steps: Vec<StepJson>,
    #[serde(rename = "final")]
    final_state: StateJson,
    final_availability: Option<Vec<AvailabilityJson>>,
    macros: Vec<String>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
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
    is_expert: bool,
    conditions_flag: i32,
    starting_quality: i32,
    material_quality_factor: i32,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct StatsJson {
    level: i32,
    craftsmanship: i32,
    control: i32,
    cp: i32,
    is_specialist: bool,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct OptionsJson {
    assume_all_success: bool,
    assume_normal_condition: bool,
    random_seed: Option<i32>,
    force_condition: Option<i32>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct StepJson {
    step: i32,
    action_id: i32,
    action_name: String,
    succeeded: bool,
    fail_reason: Option<String>,
    legal_before: Vec<i32>,
    after: StateJson,
    availability: Option<Vec<AvailabilityJson>>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct AvailabilityJson {
    action_id: i32,
    is_available: bool,
    unavailable_reason: Option<String>,
    cp_cost: i32,
    durability_cost: i32,
    success_rate: i32,
    category: i32,
    is_legacy: bool,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct StateJson {
    progress: i32,
    max_progress: i32,
    quality: i32,
    max_quality: i32,
    durability: i32,
    max_durability: i32,
    cp: i32,
    max_cp: i32,
    step: i32,
    condition: i32,
    status: i32,
    inner_quiet_stacks: i32,
    waste_not_turns: i32,
    waste_not2_turns: i32,
    veneration_turns: i32,
    innovation_turns: i32,
    great_strides_turns: i32,
    manipulation_turns: i32,
    final_appraisal_turns: i32,
    muscle_memory_turns: i32,
    heart_and_soul_active: bool,
    trained_perfection_active: bool,
    expedience_active: bool,
    quick_innovation_used: bool,
    heart_and_soul_used: bool,
    trained_perfection_used: bool,
    careful_observation_count: i32,
    is_first_step: bool,
    combo_action: i32,
    is_complete: bool,
    is_failed: bool,
    quality_percent: f64,
    progress_percent: f64,
    hq_percent: i32,
}

impl StateJson {
    fn to_state(&self) -> CraftingState {
        CraftingState {
            progress: self.progress,
            max_progress: self.max_progress,
            quality: self.quality,
            max_quality: self.max_quality,
            durability: self.durability,
            max_durability: self.max_durability,
            cp: self.cp,
            max_cp: self.max_cp,
            step: self.step,
            condition: Condition::from_i32(self.condition).expect("known condition"),
            status: CraftStatus::from_i32(self.status).expect("known status"),
            inner_quiet_stacks: self.inner_quiet_stacks,
            waste_not_turns: self.waste_not_turns,
            waste_not_ii_turns: self.waste_not2_turns,
            veneration_turns: self.veneration_turns,
            innovation_turns: self.innovation_turns,
            great_strides_turns: self.great_strides_turns,
            manipulation_turns: self.manipulation_turns,
            final_appraisal_turns: self.final_appraisal_turns,
            muscle_memory_turns: self.muscle_memory_turns,
            heart_and_soul_active: self.heart_and_soul_active,
            trained_perfection_active: self.trained_perfection_active,
            expedience_active: self.expedience_active,
            quick_innovation_used: self.quick_innovation_used,
            heart_and_soul_used: self.heart_and_soul_used,
            trained_perfection_used: self.trained_perfection_used,
            careful_observation_count: self.careful_observation_count,
            is_first_step: self.is_first_step,
            combo_action: self.combo_action,
        }
    }
}

fn recipe_of(json: &RecipeJson) -> Recipe {
    Recipe {
        recipe_id: json.recipe_id,
        recipe_level: json.recipe_level,
        difficulty: json.difficulty,
        quality: json.quality,
        durability: json.durability,
        progress_divider: json.progress_divider,
        progress_modifier: json.progress_modifier,
        quality_divider: json.quality_divider,
        quality_modifier: json.quality_modifier,
        class_job_level: json.class_job_level,
        stars: json.stars,
        is_expert: json.is_expert,
        conditions_flag: json.conditions_flag,
        suggested_craftsmanship: json.suggested_craftsmanship,
        suggested_control: json.suggested_control,
        starting_quality: json.starting_quality,
        material_quality_factor: json.material_quality_factor,
    }
}

fn stats_of(json: &StatsJson) -> CrafterStats {
    CrafterStats {
        level: json.level,
        craftsmanship: json.craftsmanship,
        control: json.control,
        cp: json.cp,
        is_specialist: json.is_specialist,
    }
}

fn options_of(json: &OptionsJson) -> SimulationOptions {
    SimulationOptions {
        assume_all_success: json.assume_all_success,
        assume_normal_condition: json.assume_normal_condition,
        random_seed: json.random_seed,
        force_condition: json
            .force_condition
            .map(|c| Condition::from_i32(c).expect("known condition")),
    }
}

fn golden_dir() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("tests/goldens")
}

fn golden_files() -> Vec<PathBuf> {
    let mut files: Vec<PathBuf> = std::fs::read_dir(golden_dir())
        .expect("snapshot golden directory exists; run with XIV_CRAFT_GENERATE=1")
        .map(|entry| entry.expect("readable entry").path())
        .filter(|path| path.extension().is_some_and(|ext| ext == "jsonl"))
        .collect();
    files.sort();
    assert!(
        files.len() >= MIN_GOLDEN_FILES,
        "expected at least {MIN_GOLDEN_FILES} golden files in {}, found {}",
        golden_dir().display(),
        files.len()
    );
    files
}

fn assert_state(context: &str, expected: &StateJson, actual: &CraftingState) {
    assert_eq!(*actual, expected.to_state(), "{context}: state differs");
    assert_eq!(
        actual.is_complete(),
        expected.is_complete,
        "{context}: isComplete"
    );
    assert_eq!(
        actual.is_failed(),
        expected.is_failed,
        "{context}: isFailed"
    );
    assert_eq!(
        actual.quality_percent().to_bits(),
        expected.quality_percent.to_bits(),
        "{context}: qualityPercent {} vs {}",
        actual.quality_percent(),
        expected.quality_percent
    );
    assert_eq!(
        actual.progress_percent().to_bits(),
        expected.progress_percent.to_bits(),
        "{context}: progressPercent {} vs {}",
        actual.progress_percent(),
        expected.progress_percent
    );
    assert_eq!(
        actual.hq_percent(),
        expected.hq_percent,
        "{context}: hqPercent"
    );
}

fn assert_availability(
    context: &str,
    expected: &[AvailabilityJson],
    before: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
) {
    let mut actual = action_availability(before, stats, recipe);
    actual.sort_by_key(|a| a.action.id());
    assert_eq!(
        actual.len(),
        expected.len(),
        "{context}: availability row count"
    );
    for (row, want) in actual.iter().zip(expected) {
        let ctx = format!("{context}: availability of {}", row.action.name());
        assert_eq!(row.action.id(), want.action_id, "{ctx}: id");
        assert_eq!(row.is_available(), want.is_available, "{ctx}: isAvailable");
        assert_eq!(
            row.fail_reason.map(|r| r.to_string()),
            want.unavailable_reason,
            "{ctx}: reason"
        );
        assert_eq!(row.cp_cost, want.cp_cost, "{ctx}: cpCost");
        assert_eq!(
            row.durability_cost, want.durability_cost,
            "{ctx}: durabilityCost"
        );
        assert_eq!(row.success_rate, want.success_rate, "{ctx}: successRate");
        assert_eq!(
            row.action.category() as i32,
            want.category,
            "{ctx}: category"
        );
        assert_eq!(row.action.is_legacy(), want.is_legacy, "{ctx}: isLegacy");
    }
}

fn replay(file: &str, case: &Case) {
    let recipe = recipe_of(&case.recipe);
    let stats = stats_of(&case.stats);
    let options = options_of(&case.options);
    let name = format!("{file}/{}", case.case);

    assert_state(
        &format!("{name} initial"),
        &case.initial,
        &create_initial(&recipe, &stats),
    );

    let result = simulate_with(&recipe, &stats, &case.actions, &options);
    assert_eq!(result.steps.len(), case.steps.len(), "{name}: step count");

    let mut before = create_initial(&recipe, &stats);
    for (step, want) in result.steps.iter().zip(&case.steps) {
        let ctx = format!("{name} step {}", want.step);
        assert_eq!(step.step_number, want.step, "{ctx}: stepNumber");
        assert_eq!(step.action_id, want.action_id, "{ctx}: actionId");
        assert_eq!(step.action_name(), want.action_name, "{ctx}: actionName");
        assert_eq!(step.succeeded, want.succeeded, "{ctx}: succeeded");
        assert_eq!(
            step.fail_reason.map(|r| r.to_string()),
            want.fail_reason,
            "{ctx}: failReason"
        );
        assert_eq!(step.state_before, before, "{ctx}: stateBefore");

        let mut legal = xiv_craft::legal_actions(&before, &stats, &recipe);
        legal.sort_unstable();
        assert_eq!(legal, want.legal_before, "{ctx}: legalBefore");
        assert!(
            legal
                .iter()
                .all(|id| Action::from_id(*id).is_some_and(|a| !a.is_legacy())),
            "{ctx}: legalBefore lists a legacy action"
        );

        if let Some(table) = &want.availability {
            assert_availability(&ctx, table, &before, &stats, &recipe);
        }

        assert_state(&ctx, &want.after, &step.state_after);
        before = step.state_after;
    }

    assert_state(
        &format!("{name} final"),
        &case.final_state,
        &result.final_state,
    );
    if let Some(table) = &case.final_availability {
        assert_availability(
            &format!("{name} final"),
            table,
            &result.final_state,
            &stats,
            &recipe,
        );
    }
    assert_eq!(
        export_to_macro(&case.actions, &MacroOptions::default()),
        case.macros,
        "{name}: macros"
    );

    // Gated steps return before the forced condition applies; patch only successful steps.
    if options.assume_all_success
        && (options.assume_normal_condition || options.force_condition.is_some())
    {
        let mut state = create_initial(&recipe, &stats);
        for want in &case.steps {
            let ctx = format!("{name} step {} via execute_action", want.step);
            let step = execute_action(state, want.action_id, &stats, &recipe);
            let mut after = step.state_after;
            if let (Some(forced), true) = (options.force_condition, step.succeeded) {
                after.condition = forced;
            }
            assert_eq!(step.step_number, want.step, "{ctx}: stepNumber");
            assert_eq!(step.succeeded, want.succeeded, "{ctx}: succeeded");
            assert_eq!(
                step.fail_reason.map(|r| r.to_string()),
                want.fail_reason,
                "{ctx}: failReason"
            );
            assert_state(&ctx, &want.after, &after);
            state = after;
        }
    }
}

#[test]
fn every_golden_case_replays_identically() {
    assert_snapshots_current();
    let mut asserted = 0usize;

    for path in golden_files() {
        let file = path.file_name().unwrap().to_string_lossy().into_owned();
        let text = std::fs::read_to_string(&path).expect("golden is readable");
        for (index, line) in text.lines().enumerate() {
            if line.trim().is_empty() {
                continue;
            }
            let case: Case = serde_json::from_str(line)
                .unwrap_or_else(|e| panic!("{file} line {}: {e}", index + 1));
            replay(&file, &case);
            asserted += 1;
        }
    }

    println!("snapshot: {asserted} cases asserted");
    assert!(asserted > 0, "no golden cases found");
}

impl From<&Recipe> for RecipeJson {
    fn from(value: &Recipe) -> Self {
        Self {
            recipe_id: value.recipe_id,
            recipe_level: value.recipe_level,
            class_job_level: value.class_job_level,
            difficulty: value.difficulty,
            quality: value.quality,
            durability: value.durability,
            stars: value.stars,
            progress_divider: value.progress_divider,
            progress_modifier: value.progress_modifier,
            quality_divider: value.quality_divider,
            quality_modifier: value.quality_modifier,
            suggested_craftsmanship: value.suggested_craftsmanship,
            suggested_control: value.suggested_control,
            is_expert: value.is_expert,
            conditions_flag: value.conditions_flag,
            starting_quality: value.starting_quality,
            material_quality_factor: value.material_quality_factor,
        }
    }
}

impl From<&CrafterStats> for StatsJson {
    fn from(value: &CrafterStats) -> Self {
        Self {
            level: value.level,
            craftsmanship: value.craftsmanship,
            control: value.control,
            cp: value.cp,
            is_specialist: value.is_specialist,
        }
    }
}

impl From<&CraftingState> for StateJson {
    fn from(value: &CraftingState) -> Self {
        Self {
            progress: value.progress,
            max_progress: value.max_progress,
            quality: value.quality,
            max_quality: value.max_quality,
            durability: value.durability,
            max_durability: value.max_durability,
            cp: value.cp,
            max_cp: value.max_cp,
            step: value.step,
            condition: value.condition as i32,
            status: value.status as i32,
            inner_quiet_stacks: value.inner_quiet_stacks,
            waste_not_turns: value.waste_not_turns,
            waste_not2_turns: value.waste_not_ii_turns,
            veneration_turns: value.veneration_turns,
            innovation_turns: value.innovation_turns,
            great_strides_turns: value.great_strides_turns,
            manipulation_turns: value.manipulation_turns,
            final_appraisal_turns: value.final_appraisal_turns,
            muscle_memory_turns: value.muscle_memory_turns,
            heart_and_soul_active: value.heart_and_soul_active,
            trained_perfection_active: value.trained_perfection_active,
            expedience_active: value.expedience_active,
            quick_innovation_used: value.quick_innovation_used,
            heart_and_soul_used: value.heart_and_soul_used,
            trained_perfection_used: value.trained_perfection_used,
            careful_observation_count: value.careful_observation_count,
            is_first_step: value.is_first_step,
            combo_action: value.combo_action,
            is_complete: value.is_complete(),
            is_failed: value.is_failed(),
            quality_percent: value.quality_percent(),
            progress_percent: value.progress_percent(),
            hq_percent: value.hq_percent(),
        }
    }
}

fn availability(
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
) -> Vec<AvailabilityJson> {
    let mut rows = action_availability(state, stats, recipe);
    rows.sort_by_key(|row| row.action.id());
    rows.into_iter()
        .map(|row| AvailabilityJson {
            action_id: row.action.id(),
            is_available: row.is_available(),
            unavailable_reason: row.fail_reason.map(|reason| reason.to_string()),
            cp_cost: row.cp_cost,
            durability_cost: row.durability_cost,
            success_rate: row.success_rate,
            category: row.action.category() as i32,
            is_legacy: row.action.is_legacy(),
        })
        .collect()
}

fn build(scenario: common::sources::Scenario) -> Case {
    let recipe = &scenario.recipe;
    let stats = &scenario.stats;
    let options = &scenario.options;
    let result = simulate_with(recipe, stats, &scenario.actions, options);
    let steps = result
        .steps
        .iter()
        .map(|step| {
            let mut legal_before = xiv_craft::legal_actions(&step.state_before, stats, recipe);
            legal_before.sort_unstable();
            StepJson {
                step: step.step_number,
                action_id: step.action_id,
                action_name: step.action_name(),
                succeeded: step.succeeded,
                fail_reason: step.fail_reason.map(|reason| reason.to_string()),
                legal_before,
                after: StateJson::from(&step.state_after),
                availability: scenario
                    .step_availability
                    .then(|| availability(&step.state_before, stats, recipe)),
            }
        })
        .collect();
    Case {
        case: scenario.name,
        recipe: RecipeJson::from(recipe),
        stats: StatsJson::from(stats),
        options: OptionsJson {
            assume_all_success: options.assume_all_success,
            assume_normal_condition: options.assume_normal_condition,
            random_seed: options.random_seed,
            force_condition: options.force_condition.map(|c| c as i32),
        },
        initial: StateJson::from(&create_initial(recipe, stats)),
        steps,
        final_state: StateJson::from(&result.final_state),
        final_availability: scenario
            .final_availability
            .then(|| availability(&result.final_state, stats, recipe)),
        macros: export_to_macro(&scenario.actions, &MacroOptions::default()),
        actions: scenario.actions,
    }
}

fn assert_snapshots_current() {
    for benchmark in benchmarks::load() {
        let mut bytes = Vec::new();
        for scenario in common::sources::for_benchmark(
            benchmark.recipe(),
            benchmark.stats(),
            benchmark.community_action_ids(),
        ) {
            serde_json::to_writer(&mut bytes, &build(scenario)).expect("serialize snapshot");
            bytes.push(b'\n');
        }
        common::golden::assert_or_write(&format!("{}.jsonl", benchmark.id), &bytes);
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct ActionJson {
    id: i32,
    name: &'static str,
    is_legacy: bool,
    category: i32,
}

#[test]
fn action_catalog_is_current() {
    let mut actions: Vec<_> = Action::ALL
        .into_iter()
        .map(|action| ActionJson {
            id: action.id(),
            name: action.name(),
            is_legacy: action.is_legacy(),
            category: action.category() as i32,
        })
        .collect();
    actions.sort_by_key(|action| action.id);
    let mut bytes = serde_json::to_vec_pretty(&actions).expect("serialize actions");
    bytes.push(b'\n');
    common::golden::assert_or_write("actions.json", &bytes);
}
