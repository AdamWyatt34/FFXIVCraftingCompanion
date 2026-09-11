use serde::Deserialize;
use std::path::PathBuf;
use xiv_craft::{calculate_starting_quality, export_to_macro, HqInput, Ingredient, MacroOptions};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Golden {
    hq: Vec<HqCase>,
    macros: Vec<MacroCase>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct HqCase {
    case: String,
    max_quality: i32,
    material_quality_factor: i32,
    ingredients: Vec<HqIngredient>,
    hq_ingredients: Vec<HqInputJson>,
    expected: i32,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct HqIngredient {
    item_id: i32,
    quantity: i32,
    can_be_hq: bool,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct HqInputJson {
    item_id: i32,
    hq_quantity: i32,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct MacroCase {
    case: String,
    action_ids: Vec<i32>,
    options: MacroOptionsJson,
    expected: Vec<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct MacroOptionsJson {
    lines_per_macro: usize,
    sound_effect: i32,
    complete_sound_effect: i32,
    include_echo: bool,
}

fn load() -> Golden {
    let path = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../../tests/FFXIV.Tests/CraftingEngine/TestData/parity/engine-units.json");
    let text = std::fs::read_to_string(path).expect("engine-units.json is committed");
    serde_json::from_str(&text).expect("valid golden")
}

#[test]
fn starting_quality_matches_hq_calculator() {
    let golden = load();
    assert!(!golden.hq.is_empty());
    for case in golden.hq {
        let ingredients: Vec<Ingredient> = case
            .ingredients
            .iter()
            .map(|i| Ingredient {
                item_id: i.item_id,
                quantity: i.quantity,
                can_be_hq: i.can_be_hq,
            })
            .collect();
        let inputs: Vec<HqInput> = case
            .hq_ingredients
            .iter()
            .map(|i| HqInput {
                item_id: i.item_id,
                hq_quantity: i.hq_quantity,
            })
            .collect();
        let actual = calculate_starting_quality(
            case.max_quality,
            case.material_quality_factor,
            &ingredients,
            &inputs,
        );
        assert_eq!(actual, case.expected, "hq case {}", case.case);
    }
}

#[test]
fn macro_export_matches_macro_exporter() {
    let golden = load();
    assert!(!golden.macros.is_empty());
    for case in golden.macros {
        let options = MacroOptions {
            lines_per_macro: case.options.lines_per_macro,
            sound_effect: case.options.sound_effect,
            complete_sound_effect: case.options.complete_sound_effect,
            include_echo: case.options.include_echo,
        };
        assert_eq!(
            export_to_macro(&case.action_ids, &options),
            case.expected,
            "macro case {}",
            case.case
        );
    }
}
