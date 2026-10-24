//! Replays the C# parity goldens. This crate is monorepo-only: the goldens live in the .NET test
//! tree and are written by ParityGoldenTests (see README.md).

use serde::Deserialize;
use std::path::PathBuf;
use xiv_craft::{
    action_availability, create_initial, execute_action, export_to_macro, simulate_with, Action,
    Condition, CraftStatus, CrafterStats, CraftingState, MacroOptions, Recipe, SimulationOptions,
};

const MIN_GOLDEN_FILES: usize = 10;

#[derive(Deserialize)]
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

#[derive(Deserialize)]
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

#[derive(Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct StatsJson {
    level: i32,
    craftsmanship: i32,
    control: i32,
    cp: i32,
    is_specialist: bool,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct OptionsJson {
    assume_all_success: bool,
    assume_normal_condition: bool,
    random_seed: Option<i32>,
    force_condition: Option<i32>,
}

#[derive(Deserialize)]
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

#[derive(Deserialize)]
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

#[derive(Deserialize)]
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
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../../tests/FFXIV.Tests/CraftingEngine/TestData/parity")
}

fn golden_files() -> Vec<PathBuf> {
    let mut files: Vec<PathBuf> = std::fs::read_dir(golden_dir())
        .expect("parity golden directory exists")
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

    // The public single-step call must agree with the full run whenever no success roll is
    // involved. A forced condition is applied after a step that executed; a gated step returns
    // before it in C#, so only successful steps are patched.
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

    println!("parity: {asserted} cases asserted");
    assert!(asserted > 0, "no golden cases found");
}
