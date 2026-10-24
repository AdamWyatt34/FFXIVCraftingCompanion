mod common;

use serde::{Deserialize, Serialize};
use xiv_craft::{calculate_starting_quality, export_to_macro, HqInput, Ingredient, MacroOptions};

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct Golden {
    hq: Vec<HqCase>,
    macros: Vec<MacroCase>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct HqCase {
    case: String,
    max_quality: i32,
    material_quality_factor: i32,
    ingredients: Vec<HqIngredient>,
    hq_ingredients: Vec<HqInputJson>,
    expected: i32,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct HqIngredient {
    item_id: i32,
    quantity: i32,
    can_be_hq: bool,
    #[serde(default)]
    item_level: i32,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct HqInputJson {
    item_id: i32,
    hq_quantity: i32,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct MacroCase {
    case: String,
    action_ids: Vec<i32>,
    options: MacroOptionsJson,
    expected: Vec<String>,
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct MacroOptionsJson {
    lines_per_macro: usize,
    sound_effect: i32,
    complete_sound_effect: i32,
    include_echo: bool,
}

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
                item_level: i.item_level,
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

fn load() -> Golden {
    let text = std::fs::read_to_string(
        std::path::PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("tests/goldens/engine-units.json"),
    )
    .expect("engine-units.json missing; run with XIV_CRAFT_GENERATE=1");
    serde_json::from_str(&text).expect("valid golden")
}

fn cases() -> Golden {
    let mut golden = Golden {
        hq: vec![
            HqCase {
                case: "all-hq".into(),
                max_quality: 12000,
                material_quality_factor: 75,
                ingredients: vec![
                    HqIngredient {
                        item_id: 1,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 2,
                        quantity: 1,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 3,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 560,
                    },
                ],
                hq_ingredients: vec![
                    HqInputJson {
                        item_id: 1,
                        hq_quantity: 2,
                    },
                    HqInputJson {
                        item_id: 2,
                        hq_quantity: 1,
                    },
                ],
                expected: 0,
            },
            HqCase {
                case: "half-hq".into(),
                max_quality: 12000,
                material_quality_factor: 75,
                ingredients: vec![
                    HqIngredient {
                        item_id: 1,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 2,
                        quantity: 1,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 3,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 560,
                    },
                ],
                hq_ingredients: vec![HqInputJson {
                    item_id: 1,
                    hq_quantity: 1,
                }],
                expected: 0,
            },
            HqCase {
                case: "no-hq-inputs".into(),
                max_quality: 12000,
                material_quality_factor: 75,
                ingredients: vec![
                    HqIngredient {
                        item_id: 1,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 2,
                        quantity: 1,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 3,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 560,
                    },
                ],
                hq_ingredients: vec![],
                expected: 0,
            },
            HqCase {
                case: "zero-factor".into(),
                max_quality: 12000,
                material_quality_factor: 0,
                ingredients: vec![
                    HqIngredient {
                        item_id: 1,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 2,
                        quantity: 1,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 3,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 560,
                    },
                ],
                hq_ingredients: vec![HqInputJson {
                    item_id: 1,
                    hq_quantity: 2,
                }],
                expected: 0,
            },
            HqCase {
                case: "zero-max-quality".into(),
                max_quality: 0,
                material_quality_factor: 75,
                ingredients: vec![
                    HqIngredient {
                        item_id: 1,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 2,
                        quantity: 1,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 3,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 560,
                    },
                ],
                hq_ingredients: vec![HqInputJson {
                    item_id: 1,
                    hq_quantity: 2,
                }],
                expected: 0,
            },
            HqCase {
                case: "no-hqable-ingredients".into(),
                max_quality: 12000,
                material_quality_factor: 75,
                ingredients: vec![HqIngredient {
                    item_id: 3,
                    quantity: 3,
                    can_be_hq: false,
                    item_level: 560,
                }],
                hq_ingredients: vec![HqInputJson {
                    item_id: 3,
                    hq_quantity: 3,
                }],
                expected: 0,
            },
            HqCase {
                case: "hq-quantity-clamped".into(),
                max_quality: 12000,
                material_quality_factor: 75,
                ingredients: vec![
                    HqIngredient {
                        item_id: 1,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 2,
                        quantity: 1,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 3,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 560,
                    },
                ],
                hq_ingredients: vec![
                    HqInputJson {
                        item_id: 1,
                        hq_quantity: 99,
                    },
                    HqInputJson {
                        item_id: 2,
                        hq_quantity: 99,
                    },
                ],
                expected: 0,
            },
            HqCase {
                case: "hq-item-not-in-recipe".into(),
                max_quality: 12000,
                material_quality_factor: 75,
                ingredients: vec![
                    HqIngredient {
                        item_id: 1,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 2,
                        quantity: 1,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 3,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 560,
                    },
                ],
                hq_ingredients: vec![HqInputJson {
                    item_id: 42,
                    hq_quantity: 5,
                }],
                expected: 0,
            },
            HqCase {
                case: "duplicate-hq-input-first-wins".into(),
                max_quality: 12000,
                material_quality_factor: 75,
                ingredients: vec![
                    HqIngredient {
                        item_id: 1,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 2,
                        quantity: 1,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 3,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 560,
                    },
                ],
                hq_ingredients: vec![
                    HqInputJson {
                        item_id: 1,
                        hq_quantity: 1,
                    },
                    HqInputJson {
                        item_id: 1,
                        hq_quantity: 2,
                    },
                ],
                expected: 0,
            },
            HqCase {
                case: "odd-rounding".into(),
                max_quality: 7777,
                material_quality_factor: 33,
                ingredients: vec![
                    HqIngredient {
                        item_id: 1,
                        quantity: 3,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 2,
                        quantity: 4,
                        can_be_hq: true,
                        item_level: 560,
                    },
                ],
                hq_ingredients: vec![
                    HqInputJson {
                        item_id: 1,
                        hq_quantity: 2,
                    },
                    HqInputJson {
                        item_id: 2,
                        hq_quantity: 1,
                    },
                ],
                expected: 0,
            },
            HqCase {
                case: "ingredient-hq-flag-false-with-input".into(),
                max_quality: 5000,
                material_quality_factor: 50,
                ingredients: vec![
                    HqIngredient {
                        item_id: 1,
                        quantity: 1,
                        can_be_hq: true,
                        item_level: 560,
                    },
                    HqIngredient {
                        item_id: 2,
                        quantity: 1,
                        can_be_hq: false,
                        item_level: 560,
                    },
                ],
                hq_ingredients: vec![
                    HqInputJson {
                        item_id: 1,
                        hq_quantity: 1,
                    },
                    HqInputJson {
                        item_id: 2,
                        hq_quantity: 1,
                    },
                ],
                expected: 0,
            },
            HqCase {
                case: "item-level-alkahest".into(),
                max_quality: 12900,
                material_quality_factor: 50,
                ingredients: vec![
                    HqIngredient {
                        item_id: 15,
                        quantity: 5,
                        can_be_hq: false,
                        item_level: 1,
                    },
                    HqIngredient {
                        item_id: 17,
                        quantity: 5,
                        can_be_hq: false,
                        item_level: 1,
                    },
                    HqIngredient {
                        item_id: 37830,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 610,
                    },
                    HqIngredient {
                        item_id: 37833,
                        quantity: 3,
                        can_be_hq: true,
                        item_level: 610,
                    },
                    HqIngredient {
                        item_id: 38932,
                        quantity: 3,
                        can_be_hq: true,
                        item_level: 620,
                    },
                    HqIngredient {
                        item_id: 39595,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 620,
                    },
                ],
                hq_ingredients: vec![HqInputJson {
                    item_id: 38932,
                    hq_quantity: 3,
                }],
                expected: 0,
            },
            HqCase {
                case: "item-level-quartz".into(),
                max_quality: 12900,
                material_quality_factor: 50,
                ingredients: vec![
                    HqIngredient {
                        item_id: 15,
                        quantity: 5,
                        can_be_hq: false,
                        item_level: 1,
                    },
                    HqIngredient {
                        item_id: 17,
                        quantity: 5,
                        can_be_hq: false,
                        item_level: 1,
                    },
                    HqIngredient {
                        item_id: 37830,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 610,
                    },
                    HqIngredient {
                        item_id: 37833,
                        quantity: 3,
                        can_be_hq: true,
                        item_level: 610,
                    },
                    HqIngredient {
                        item_id: 38932,
                        quantity: 3,
                        can_be_hq: true,
                        item_level: 620,
                    },
                    HqIngredient {
                        item_id: 39595,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 620,
                    },
                ],
                hq_ingredients: vec![HqInputJson {
                    item_id: 37830,
                    hq_quantity: 2,
                }],
                expected: 0,
            },
            HqCase {
                case: "item-level-all".into(),
                max_quality: 12900,
                material_quality_factor: 50,
                ingredients: vec![
                    HqIngredient {
                        item_id: 15,
                        quantity: 5,
                        can_be_hq: false,
                        item_level: 1,
                    },
                    HqIngredient {
                        item_id: 17,
                        quantity: 5,
                        can_be_hq: false,
                        item_level: 1,
                    },
                    HqIngredient {
                        item_id: 37830,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 610,
                    },
                    HqIngredient {
                        item_id: 37833,
                        quantity: 3,
                        can_be_hq: true,
                        item_level: 610,
                    },
                    HqIngredient {
                        item_id: 38932,
                        quantity: 3,
                        can_be_hq: true,
                        item_level: 620,
                    },
                    HqIngredient {
                        item_id: 39595,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 620,
                    },
                ],
                hq_ingredients: vec![
                    HqInputJson {
                        item_id: 38932,
                        hq_quantity: 3,
                    },
                    HqInputJson {
                        item_id: 37833,
                        hq_quantity: 3,
                    },
                    HqInputJson {
                        item_id: 37830,
                        hq_quantity: 2,
                    },
                ],
                expected: 0,
            },
            HqCase {
                case: "item-level-partial-quantities".into(),
                max_quality: 12900,
                material_quality_factor: 50,
                ingredients: vec![
                    HqIngredient {
                        item_id: 15,
                        quantity: 5,
                        can_be_hq: false,
                        item_level: 1,
                    },
                    HqIngredient {
                        item_id: 17,
                        quantity: 5,
                        can_be_hq: false,
                        item_level: 1,
                    },
                    HqIngredient {
                        item_id: 37830,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 610,
                    },
                    HqIngredient {
                        item_id: 37833,
                        quantity: 3,
                        can_be_hq: true,
                        item_level: 610,
                    },
                    HqIngredient {
                        item_id: 38932,
                        quantity: 3,
                        can_be_hq: true,
                        item_level: 620,
                    },
                    HqIngredient {
                        item_id: 39595,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 620,
                    },
                ],
                hq_ingredients: vec![
                    HqInputJson {
                        item_id: 38932,
                        hq_quantity: 1,
                    },
                    HqInputJson {
                        item_id: 37833,
                        hq_quantity: 2,
                    },
                ],
                expected: 0,
            },
            HqCase {
                case: "item-level-zero-among-weighted".into(),
                max_quality: 10000,
                material_quality_factor: 50,
                ingredients: vec![
                    HqIngredient {
                        item_id: 1,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 700,
                    },
                    HqIngredient {
                        item_id: 2,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 0,
                    },
                ],
                hq_ingredients: vec![HqInputJson {
                    item_id: 2,
                    hq_quantity: 2,
                }],
                expected: 0,
            },
            HqCase {
                case: "legacy-no-item-levels".into(),
                max_quality: 12900,
                material_quality_factor: 50,
                ingredients: vec![
                    HqIngredient {
                        item_id: 15,
                        quantity: 5,
                        can_be_hq: false,
                        item_level: 0,
                    },
                    HqIngredient {
                        item_id: 17,
                        quantity: 5,
                        can_be_hq: false,
                        item_level: 0,
                    },
                    HqIngredient {
                        item_id: 37830,
                        quantity: 2,
                        can_be_hq: true,
                        item_level: 0,
                    },
                    HqIngredient {
                        item_id: 37833,
                        quantity: 3,
                        can_be_hq: true,
                        item_level: 0,
                    },
                    HqIngredient {
                        item_id: 38932,
                        quantity: 3,
                        can_be_hq: true,
                        item_level: 0,
                    },
                    HqIngredient {
                        item_id: 39595,
                        quantity: 3,
                        can_be_hq: false,
                        item_level: 0,
                    },
                ],
                hq_ingredients: vec![HqInputJson {
                    item_id: 38932,
                    hq_quantity: 3,
                }],
                expected: 0,
            },
        ],
        macros: vec![
            MacroCase {
                case: "default-two-macros".into(),
                action_ids: vec![
                    100379, 100297, 100403, 100403, 100278, 100209, 100002, 100004, 100411, 100260,
                    100339, 100003, 100297, 100403, 100203, 100371, 100395,
                ],
                options: MacroOptionsJson {
                    lines_per_macro: 14,
                    sound_effect: 1,
                    complete_sound_effect: 2,
                    include_echo: true,
                },
                expected: Vec::new(),
            },
            MacroCase {
                case: "exactly-fourteen".into(),
                action_ids: vec![
                    100379, 100297, 100403, 100403, 100278, 100209, 100002, 100004, 100411, 100260,
                    100339, 100003, 100297, 100403,
                ],
                options: MacroOptionsJson {
                    lines_per_macro: 14,
                    sound_effect: 1,
                    complete_sound_effect: 2,
                    include_echo: true,
                },
                expected: Vec::new(),
            },
            MacroCase {
                case: "fifteen".into(),
                action_ids: vec![
                    100379, 100297, 100403, 100403, 100278, 100209, 100002, 100004, 100411, 100260,
                    100339, 100003, 100297, 100403, 100203,
                ],
                options: MacroOptionsJson {
                    lines_per_macro: 14,
                    sound_effect: 1,
                    complete_sound_effect: 2,
                    include_echo: true,
                },
                expected: Vec::new(),
            },
            MacroCase {
                case: "five-per-macro-custom-sounds".into(),
                action_ids: vec![
                    100379, 100297, 100403, 100403, 100278, 100209, 100002, 100004, 100411, 100260,
                    100339, 100003, 100297, 100403, 100203, 100371, 100395,
                ],
                options: MacroOptionsJson {
                    lines_per_macro: 5,
                    sound_effect: 3,
                    complete_sound_effect: 4,
                    include_echo: true,
                },
                expected: Vec::new(),
            },
            MacroCase {
                case: "no-echo".into(),
                action_ids: vec![100379, 100297, 100403, 100403, 100278, 100209],
                options: MacroOptionsJson {
                    lines_per_macro: 14,
                    sound_effect: 1,
                    complete_sound_effect: 2,
                    include_echo: false,
                },
                expected: Vec::new(),
            },
            MacroCase {
                case: "empty".into(),
                action_ids: vec![],
                options: MacroOptionsJson {
                    lines_per_macro: 14,
                    sound_effect: 1,
                    complete_sound_effect: 2,
                    include_echo: true,
                },
                expected: Vec::new(),
            },
            MacroCase {
                case: "unknown-ids-skipped".into(),
                action_ids: vec![100001, 999999, 100002],
                options: MacroOptionsJson {
                    lines_per_macro: 14,
                    sound_effect: 1,
                    complete_sound_effect: 2,
                    include_echo: true,
                },
                expected: Vec::new(),
            },
            MacroCase {
                case: "only-unknown".into(),
                action_ids: vec![999999],
                options: MacroOptionsJson {
                    lines_per_macro: 14,
                    sound_effect: 1,
                    complete_sound_effect: 2,
                    include_echo: true,
                },
                expected: Vec::new(),
            },
            MacroCase {
                case: "dawntrail-touches".into(),
                action_ids: vec![100002, 100502, 100501, 100355, 100503],
                options: MacroOptionsJson {
                    lines_per_macro: 14,
                    sound_effect: 1,
                    complete_sound_effect: 2,
                    include_echo: true,
                },
                expected: Vec::new(),
            },
            MacroCase {
                case: "legacy-actions".into(),
                action_ids: vec![100252, 100235, 100010, 100243],
                options: MacroOptionsJson {
                    lines_per_macro: 14,
                    sound_effect: 1,
                    complete_sound_effect: 2,
                    include_echo: true,
                },
                expected: Vec::new(),
            },
        ],
    };
    for case in &mut golden.hq {
        let ingredients: Vec<_> = case
            .ingredients
            .iter()
            .map(|i| Ingredient {
                item_id: i.item_id,
                quantity: i.quantity,
                can_be_hq: i.can_be_hq,
                item_level: i.item_level,
            })
            .collect();
        let inputs: Vec<_> = case
            .hq_ingredients
            .iter()
            .map(|i| HqInput {
                item_id: i.item_id,
                hq_quantity: i.hq_quantity,
            })
            .collect();
        case.expected = calculate_starting_quality(
            case.max_quality,
            case.material_quality_factor,
            &ingredients,
            &inputs,
        );
    }
    for case in &mut golden.macros {
        case.expected = export_to_macro(
            &case.action_ids,
            &MacroOptions {
                lines_per_macro: case.options.lines_per_macro,
                sound_effect: case.options.sound_effect,
                complete_sound_effect: case.options.complete_sound_effect,
                include_echo: case.options.include_echo,
            },
        );
    }
    golden
}

fn assert_units_current() {
    let mut bytes = serde_json::to_vec_pretty(&cases()).expect("serialize engine units");
    bytes.push(b'\n');
    common::golden::assert_or_write("engine-units.json", &bytes);
}

#[test]
fn engine_units_are_current_and_replay() {
    assert_units_current();
    starting_quality_matches_hq_calculator();
    macro_export_matches_macro_exporter();
}
