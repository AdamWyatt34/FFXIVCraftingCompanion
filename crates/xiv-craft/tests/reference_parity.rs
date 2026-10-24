use xiv_craft::{
    base_progress, base_quality, create_initial, execute_action, simulate, Action, Condition,
    CrafterStats, Recipe, SimulationResult,
};

fn rlvl690() -> Recipe {
    Recipe {
        recipe_level: 690,
        class_job_level: 100,
        difficulty: 6600,
        quality: 12000,
        durability: 80,
        progress_divider: 170,
        progress_modifier: 90,
        quality_divider: 150,
        quality_modifier: 75,
        ..Recipe::default()
    }
}

fn rlvl640() -> Recipe {
    Recipe {
        recipe_level: 640,
        class_job_level: 90,
        difficulty: 4400,
        quality: 9000,
        durability: 70,
        progress_divider: 130,
        progress_modifier: 80,
        quality_divider: 115,
        quality_modifier: 70,
        ..Recipe::default()
    }
}

fn rlvl133() -> Recipe {
    Recipe {
        recipe_level: 133,
        class_job_level: 54,
        difficulty: 5000,
        quality: 10000,
        durability: 80,
        progress_divider: 64,
        progress_modifier: 100,
        quality_divider: 44,
        quality_modifier: 100,
        ..Recipe::default()
    }
}

fn lv100() -> CrafterStats {
    CrafterStats {
        level: 100,
        craftsmanship: 4000,
        control: 4000,
        cp: 900,
        is_specialist: false,
    }
}

fn run(recipe: Recipe, stats: CrafterStats, names: &[&str]) -> SimulationResult {
    let actions: Vec<_> = names
        .iter()
        .map(|name| Action::from_name(name).expect("known action").id())
        .collect();
    simulate(&recipe, &stats, &actions)
}

// Raphael raphael-data/src/lib.rs:145-149,175-192.
#[test]
fn base_progress_and_quality_on_rlvl_690() {
    assert_eq!(base_progress(4000, 170, 90, 100, 100), 213);
    assert_eq!(base_quality(4000, 150, 75, 100, 100), 226);
}

// Raphael raphael-sim/src/effects.rs:70-75, actions.rs:28-36; Teamcraft progress-action.ts:26-36.
#[test]
fn veneration_multiplies_basic_synthesis() {
    let result = run(rlvl690(), lv100(), &["Veneration", "BasicSynthesis"]);
    assert_eq!(
        result.steps[1].state_after.progress - result.steps[1].state_before.progress,
        383
    );
}

// Raphael raphael-sim/src/effects.rs:70-75: Muscle Memory and Veneration sum to 25 tenths.
#[test]
fn muscle_memory_and_veneration_sum_into_one_multiplier() {
    let result = run(
        rlvl690(),
        lv100(),
        &["MuscleMemory", "Veneration", "Groundwork"],
    );
    for (index, expected) in [(0, 639), (2, 1917)] {
        let step = &result.steps[index];
        assert_eq!(
            step.state_after.progress - step.state_before.progress,
            expected
        );
    }
}

// Raphael raphael-sim/src/effects.rs:76-80, actions.rs:38-52; Teamcraft quality-action.ts:79-94.
#[test]
fn inner_quiet_innovation_and_great_strides_multiply_quality() {
    let result = run(
        rlvl690(),
        lv100(),
        &[
            "Reflect",
            "Innovation",
            "GreatStrides",
            "BasicTouch",
            "GreatStrides",
            "ByregotsBlessing",
        ],
    );
    let deltas: Vec<_> = result
        .steps
        .iter()
        .map(|s| s.state_after.quality - s.state_before.quality)
        .collect();
    assert_eq!(deltas, [678, 0, 0, 678, 0, 1175]);
    assert_eq!(result.final_state.quality, 2531);
}

// Raphael raphael-sim/src/actions.rs:30-41; Teamcraft quality-action.ts:65-77.
#[test]
fn condition_scales_basic_touch() {
    for (condition, expected) in [
        (Condition::Normal, 226),
        (Condition::Good, 339),
        (Condition::Excellent, 904),
        (Condition::Poor, 113),
    ] {
        let mut state = create_initial(&rlvl690(), &lv100());
        state.condition = condition;
        let step = execute_action(state, Action::BasicTouch.id(), &lv100(), &rlvl690());
        assert_eq!(
            step.state_after.quality - step.state_before.quality,
            expected,
            "{condition:?}"
        );
    }
}

// Raphael raphael-sim/src/effects.rs:76-80, actions.rs:30-41: condition halves stay inside the numerator.
#[test]
fn preparatory_touch_at_inner_quiet_8() {
    let recipe = Recipe {
        durability: 120,
        ..rlvl690()
    };
    let prefix = run(
        recipe,
        lv100(),
        &[
            "Reflect",
            "PreparatoryTouch",
            "PreparatoryTouch",
            "PreparatoryTouch",
        ],
    );
    assert_eq!(prefix.final_state.inner_quiet_stacks, 8);
    for (condition, expected) in [(Condition::Normal, 813), (Condition::Good, 1220)] {
        let mut state = prefix.final_state;
        state.condition = condition;
        let step = execute_action(state, Action::PreparatoryTouch.id(), &lv100(), &recipe);
        assert_eq!(
            step.state_after.quality - step.state_before.quality,
            expected,
            "{condition:?}"
        );
    }
}

// Raphael raphael-data/src/lib.rs:147-149 (job_level, not rlvl); Teamcraft crafting-action.ts getBaseProgression.
#[test]
fn level_modifier_follows_the_class_job_level() {
    for (level, progress, quality) in [(100, 370, 382), (90, 296, 267)] {
        let result = run(
            rlvl640(),
            CrafterStats { level, ..lv100() },
            &["BasicSynthesis", "BasicTouch"],
        );
        assert_eq!(
            result.steps[0].state_after.progress - result.steps[0].state_before.progress,
            progress
        );
        assert_eq!(
            result.steps[1].state_after.quality - result.steps[1].state_before.quality,
            quality
        );
    }
}

// Raphael raphael-data/src/lib.rs:145-149; Teamcraft crafting-action.ts getBaseProgression: one truncation.
#[test]
fn base_progress_truncates_once() {
    let recipe = Recipe {
        recipe_level: 590,
        class_job_level: 90,
        difficulty: 5000,
        quality: 10000,
        durability: 80,
        progress_divider: 130,
        progress_modifier: 90,
        quality_divider: 115,
        quality_modifier: 75,
        ..Recipe::default()
    };
    let stats = CrafterStats {
        level: 90,
        craftsmanship: 3000,
        control: 3500,
        cp: 300,
        is_specialist: false,
    };
    assert_eq!(base_progress(3000, 130, 90, 90, 90), 209);
    let result = run(recipe, stats, &["BasicSynthesis"]);
    assert_eq!(
        result.steps[0].state_after.progress - result.steps[0].state_before.progress,
        250
    );
}

// Raphael raphael-sim/src/state.rs:120; Teamcraft quality-action.ts:96.
#[test]
fn inner_quiet_is_gained_from_level_11() {
    for (level, stacks) in [(8, 0), (10, 0), (11, 1)] {
        let stats = CrafterStats {
            level,
            craftsmanship: 529,
            control: 500,
            cp: 300,
            is_specialist: false,
        };
        assert_eq!(
            run(rlvl133(), stats, &["BasicTouch"])
                .final_state
                .inner_quiet_stacks,
            stacks
        );
    }
}

// Raphael raphael-sim/src/actions.rs:158-172,236-280,553-590; Teamcraft quality/advanced-touch.ts hasCombo.
#[test]
fn advanced_touch_is_discounted_after_a_comboed_standard_touch_or_observe() {
    let cases: &[(&[&str], &[i32])] = &[
        (&["StandardTouch", "AdvancedTouch"], &[32, 46]),
        (&["Observe", "AdvancedTouch"], &[7, 18]),
        (
            &["BasicTouch", "StandardTouch", "AdvancedTouch"],
            &[18, 18, 18],
        ),
        (&["Observe", "StandardTouch", "AdvancedTouch"], &[7, 32, 46]),
    ];
    for (actions, costs) in cases {
        let result = run(rlvl690(), lv100(), actions);
        let actual: Vec<_> = result
            .steps
            .iter()
            .map(|s| s.state_before.cp - s.state_after.cp)
            .collect();
        assert_eq!(&actual, costs, "{actions:?}");
    }
}

// Raphael raphael-sim/src/actions.rs RapidSynthesis progress_modifier; Teamcraft progression/rapid-synthesis.ts getPotency.
#[test]
fn rapid_synthesis_potency_doubles_at_level_63() {
    for (level, expected) in [(50, 210), (63, 420)] {
        let stats = CrafterStats {
            level,
            craftsmanship: 529,
            control: 500,
            cp: 300,
            is_specialist: false,
        };
        let result = run(rlvl133(), stats, &["RapidSynthesis"]);
        assert_eq!(
            result.steps[0].state_after.progress - result.steps[0].state_before.progress,
            expected
        );
    }
}

// Raphael raphael-sim/src/actions.rs WasteNot/WasteNot2 .with_waste_not(0);
// Teamcraft buff/waste-not.ts:35-37, waste-not-ii.ts:35-37.
#[test]
fn waste_not_replaces_waste_not_ii() {
    let recipe = Recipe {
        difficulty: 99999,
        quality: 99999,
        durability: 100,
        progress_modifier: 100,
        quality_modifier: 100,
        ..rlvl690()
    };
    let stats = CrafterStats {
        cp: 2000,
        ..lv100()
    };
    let mut actions = vec!["WasteNotII", "WasteNot"];
    actions.extend(["BasicTouch"; 8]);
    assert_eq!(run(recipe, stats, &actions).final_state.durability, 40);
}
