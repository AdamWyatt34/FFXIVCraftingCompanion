use xiv_craft::solver::greedy;
use xiv_craft::{
    create_initial, execute_action, legal_actions, simulate_with, Action, Condition, CraftStatus,
    CrafterStats, DotNetRandom, GreedySolverConfig, Recipe, SimulationOptions, SolverConstraints,
    SolverStatus,
};

// The case counts bound the snapshot size; changing any of them regenerates every golden.
const RANDOM_CASE_COUNT: i32 = 20;
const SEEDED_CASE_COUNT: i32 = 10;
const FORCED_CASES_PER_CONDITION: i32 = 2;
const FORCED_SEEDED_CASES_PER_CONDITION: i32 = 1;
const PARTIAL_MASK_CASES: i32 = 3;
const MAX_SEQUENCE_LENGTH: usize = 40;

pub struct Scenario {
    pub name: String,
    pub recipe: Recipe,
    pub stats: CrafterStats,
    pub options: SimulationOptions,
    pub actions: Vec<i32>,
    pub step_availability: bool,
    pub final_availability: bool,
}

impl Scenario {
    fn new(
        name: impl Into<String>,
        recipe: Recipe,
        stats: CrafterStats,
        options: SimulationOptions,
        actions: Vec<i32>,
        availability: bool,
    ) -> Self {
        Self {
            name: name.into(),
            recipe,
            stats,
            options,
            actions,
            step_availability: availability,
            final_availability: availability,
        }
    }
}

pub fn ids(names: &[&str]) -> Vec<i32> {
    names
        .iter()
        .map(|name| Action::from_name(name).expect("known action").id())
        .collect()
}

pub fn for_benchmark(recipe: Recipe, stats: CrafterStats, community: Vec<i32>) -> Vec<Scenario> {
    let defaults = SimulationOptions::default();
    let mut cases = vec![Scenario::new(
        "community",
        recipe,
        stats,
        defaults,
        community,
        true,
    )];
    let result = greedy::solve(
        &recipe,
        &stats,
        &SolverConstraints::default(),
        &GreedySolverConfig::default(),
        &mut super::wall_clock(),
    );
    assert_eq!(result.status, SolverStatus::Completed);
    cases.push(Scenario::new(
        "greedy",
        recipe,
        stats,
        defaults,
        result.action_ids,
        true,
    ));
    for i in 0..RANDOM_CASE_COUNT {
        cases.push(Scenario::new(
            format!("random-{i:03}"),
            recipe,
            stats,
            defaults,
            random_legal_sequence(&recipe, &stats, 1000 + i, None),
            false,
        ));
    }
    for condition in Condition::ALL {
        let options = SimulationOptions {
            force_condition: Some(condition),
            ..defaults
        };
        for i in 0..FORCED_CASES_PER_CONDITION {
            cases.push(Scenario::new(
                format!("forced-{condition:?}-{i}"),
                recipe,
                stats,
                options,
                random_legal_sequence(
                    &recipe,
                    &stats,
                    2000 + condition as i32 * 10 + i,
                    Some(condition),
                ),
                false,
            ));
        }
        cases.push(Scenario::new(
            format!("forced-gated-{condition:?}"),
            recipe,
            stats,
            options,
            ids(&[
                "IntensiveSynthesis",
                "BasicSynthesis",
                "TricksOfTheTrade",
                "IntensiveSynthesis",
                "BasicTouch",
            ]),
            true,
        ));
        for i in 0..FORCED_SEEDED_CASES_PER_CONDITION {
            let rolled = SimulationOptions {
                assume_all_success: false,
                random_seed: Some(7000 + condition as i32 * 10 + i),
                ..options
            };
            cases.push(Scenario::new(
                format!("forced-seeded-{condition:?}-{i}"),
                recipe,
                stats,
                rolled,
                seeded_legal_sequence(&recipe, &stats, &rolled, 8000 + condition as i32 * 10 + i),
                false,
            ));
        }
    }
    for i in 0..SEEDED_CASE_COUNT {
        let options = rolled_options(3000 + i);
        cases.push(Scenario::new(
            format!("seeded-{i:02}"),
            recipe,
            stats,
            options,
            seeded_legal_sequence(&recipe, &stats, &options, 4000 + i),
            false,
        ));
    }
    let wrapping = rolled_options(i32::MAX);
    cases.push(Scenario::new(
        "seeded-max",
        recipe,
        stats,
        wrapping,
        seeded_legal_sequence(&recipe, &stats, &wrapping, 4100),
        false,
    ));
    if recipe.class_job_level >= 100 {
        let specialist = CrafterStats {
            is_specialist: true,
            ..stats
        };
        let expert = Recipe {
            is_expert: true,
            conditions_flag: 0x7f,
            ..recipe
        };
        for i in 0..SEEDED_CASE_COUNT {
            let options = rolled_options(5000 + i);
            cases.push(Scenario::new(
                format!("expert-seeded-{i:02}"),
                expert,
                specialist,
                options,
                seeded_legal_sequence(&expert, &specialist, &options, 6000 + i),
                false,
            ));
        }
        for mask in [0x05, 0x22, 0x41] {
            let partial = Recipe {
                is_expert: true,
                conditions_flag: mask,
                ..recipe
            };
            for i in 0..PARTIAL_MASK_CASES {
                let options = rolled_options(9000 + mask * 10 + i);
                cases.push(Scenario::new(
                    format!("expert-mask{mask:02x}-seeded-{i}"),
                    partial,
                    specialist,
                    options,
                    seeded_legal_sequence(&partial, &specialist, &options, 9500 + mask * 10 + i),
                    false,
                ));
            }
        }
    }
    cases.extend(edge_scenarios(recipe, stats));
    cases
}

fn rolled_options(seed: i32) -> SimulationOptions {
    SimulationOptions {
        assume_all_success: false,
        assume_normal_condition: false,
        random_seed: Some(seed),
        ..SimulationOptions::default()
    }
}

fn random_legal_sequence(
    recipe: &Recipe,
    stats: &CrafterStats,
    seed: i32,
    force: Option<Condition>,
) -> Vec<i32> {
    let mut rng = DotNetRandom::new(seed);
    let mut state = create_initial(recipe, stats);
    let mut actions = Vec::new();
    while state.status == CraftStatus::InProgress && actions.len() < MAX_SEQUENCE_LENGTH {
        let mut legal = legal_actions(&state, stats, recipe);
        legal.sort_unstable();
        if legal.is_empty() {
            break;
        }
        let pick = legal[(rng.next_double() * legal.len() as f64) as usize];
        actions.push(pick);
        state = execute_action(state, pick, stats, recipe).state_after;
        if let Some(condition) = force {
            state.condition = condition;
        }
    }
    actions
}

fn seeded_legal_sequence(
    recipe: &Recipe,
    stats: &CrafterStats,
    options: &SimulationOptions,
    seed: i32,
) -> Vec<i32> {
    let mut rng = DotNetRandom::new(seed);
    let mut actions = Vec::new();
    // Replaying the prefix preserves both RNG streams, including failed actions and condition rolls.
    while actions.len() < MAX_SEQUENCE_LENGTH {
        let state = simulate_with(recipe, stats, &actions, options).final_state;
        if state.status != CraftStatus::InProgress {
            break;
        }
        let mut legal = legal_actions(&state, stats, recipe);
        legal.sort_unstable();
        if legal.is_empty() {
            break;
        }
        actions.push(legal[(rng.next_double() * legal.len() as f64) as usize]);
    }
    actions
}

fn edge_scenarios(recipe: Recipe, stats: CrafterStats) -> Vec<Scenario> {
    let low_level = CrafterStats { level: 10, ..stats };
    let starved = CrafterStats { cp: 10, ..stats };
    let specialist = CrafterStats {
        is_specialist: true,
        ..stats
    };
    let under_level = CrafterStats {
        level: recipe.class_job_level + 5,
        ..stats
    };
    let over_level = CrafterStats {
        level: recipe.class_job_level + 10,
        ..stats
    };
    let expert = Recipe {
        is_expert: true,
        conditions_flag: 0x7f,
        ..recipe
    };
    let no_job_level = Recipe {
        class_job_level: 0,
        ..recipe
    };
    let mut cases = vec![
        edge(
            "first-step-only",
            recipe,
            stats,
            &["BasicSynthesis", "MuscleMemory", "Reflect", "TrainedEye"],
        ),
        edge(
            "byregot-no-iq",
            recipe,
            stats,
            &["ByregotsBlessing", "BasicTouch", "ByregotsBlessing"],
        ),
        edge(
            "finesse-under-ten-iq",
            recipe,
            stats,
            &["BasicTouch", "TrainedFinesse"],
        ),
        edge(
            "good-only-on-normal",
            recipe,
            stats,
            &["IntensiveSynthesis", "TricksOfTheTrade", "BasicSynthesis"],
        ),
        edge(
            "prudent-under-waste-not",
            recipe,
            stats,
            &["WasteNot", "PrudentTouch", "PrudentSynthesis", "BasicTouch"],
        ),
        edge(
            "specialist-only",
            recipe,
            stats,
            &[
                "HeartAndSoul",
                "CarefulObservation",
                "QuickInnovation",
                "BasicSynthesis",
            ],
        ),
        edge(
            "specialist-abilities-keep-first-step",
            recipe,
            specialist,
            &[
                "HeartAndSoul",
                "CarefulObservation",
                "MuscleMemory",
                "CarefulObservation",
                "CarefulObservation",
                "CarefulObservation",
                "Veneration",
                "Groundwork",
            ],
        ),
        edge(
            "quick-innovation-limits",
            recipe,
            specialist,
            &[
                "QuickInnovation",
                "BasicTouch",
                "QuickInnovation",
                "Innovation",
                "QuickInnovation",
            ],
        ),
        edge(
            "quick-innovation-blocked-by-innovation",
            recipe,
            specialist,
            &[
                "Innovation",
                "QuickInnovation",
                "BasicTouch",
                "BasicTouch",
                "BasicTouch",
                "BasicTouch",
                "QuickInnovation",
                "BasicTouch",
            ],
        ),
        edge(
            "final-appraisal-is-step-free",
            recipe,
            stats,
            &[
                "Veneration",
                "FinalAppraisal",
                "FinalAppraisal",
                "BasicSynthesis",
                "MuscleMemory",
            ],
        ),
        edge(
            "buffs-last-full-duration",
            recipe,
            stats,
            &[
                "Innovation",
                "BasicTouch",
                "BasicTouch",
                "BasicTouch",
                "BasicTouch",
                "BasicTouch",
                "Manipulation",
                "Manipulation",
                "Groundwork",
                "Groundwork",
            ],
        ),
        edge(
            "trained-perfection-once",
            recipe,
            stats,
            &[
                "TrainedPerfection",
                "BasicTouch",
                "TrainedPerfection",
                "PreparatoryTouch",
            ],
        ),
        edge(
            "level-gated",
            recipe,
            low_level,
            &[
                "BasicSynthesis",
                "CarefulSynthesis",
                "BasicTouch",
                "StandardTouch",
                "Veneration",
                "Groundwork",
            ],
        ),
        edge(
            "cp-starved",
            recipe,
            starved,
            &[
                "Manipulation",
                "BasicSynthesis",
                "Veneration",
                "BasicTouch",
                "BasicSynthesis",
            ],
        ),
        edge(
            "trained-eye-under-level",
            recipe,
            under_level,
            &["TrainedEye", "BasicSynthesis"],
        ),
        edge(
            "trained-eye-over-level",
            recipe,
            over_level,
            &["TrainedEye", "BasicSynthesis", "BasicSynthesis"],
        ),
        edge(
            "trained-eye-no-job-level",
            no_job_level,
            over_level,
            &["TrainedEye", "BasicSynthesis"],
        ),
        edge(
            "trained-eye-on-expert",
            expert,
            CrafterStats {
                is_specialist: true,
                ..over_level
            },
            &[
                "TrainedEye",
                "HeartAndSoul",
                "HeartAndSoul",
                "CarefulObservation",
                "CarefulObservation",
                "CarefulObservation",
                "CarefulObservation",
                "IntensiveSynthesis",
                "TricksOfTheTrade",
            ],
        ),
        edge(
            "manipulation-to-zero",
            recipe,
            stats,
            &[
                "Manipulation",
                "Groundwork",
                "Groundwork",
                "Groundwork",
                "Groundwork",
                "Groundwork",
                "Groundwork",
            ],
        ),
        edge(
            "final-appraisal-cap",
            recipe,
            stats,
            &[
                "MuscleMemory",
                "FinalAppraisal",
                "Veneration",
                "Groundwork",
                "Groundwork",
                "Groundwork",
                "Groundwork",
                "Groundwork",
            ],
        ),
        edge(
            "legacy-focused-after-observe",
            recipe,
            stats,
            &[
                "Observe",
                "FocusedSynthesis",
                "Observe",
                "FocusedTouch",
                "FocusedTouch",
            ],
        ),
        edge(
            "touch-combo",
            recipe,
            stats,
            &[
                "BasicTouch",
                "StandardTouch",
                "AdvancedTouch",
                "AdvancedTouch",
                "StandardTouch",
            ],
        ),
        edge(
            "masters-mend-cap",
            recipe,
            stats,
            &[
                "MastersMend",
                "Groundwork",
                "MastersMend",
                "Groundwork",
                "Groundwork",
                "Groundwork",
                "ImmaculateMend",
            ],
        ),
        edge(
            "delicate-with-buffs",
            recipe,
            stats,
            &[
                "Innovation",
                "Veneration",
                "GreatStrides",
                "DelicateSynthesis",
                "DelicateSynthesis",
            ],
        ),
        edge(
            "lone-standard-then-advanced",
            recipe,
            stats,
            &[
                "StandardTouch",
                "AdvancedTouch",
                "StandardTouch",
                "StandardTouch",
                "AdvancedTouch",
            ],
        ),
        edge(
            "observe-then-advanced",
            recipe,
            stats,
            &[
                "Observe",
                "AdvancedTouch",
                "Observe",
                "StandardTouch",
                "AdvancedTouch",
            ],
        ),
        edge(
            "waste-not-replaces-waste-not-ii",
            recipe,
            stats,
            &[
                "WasteNotII",
                "WasteNot",
                "BasicTouch",
                "BasicTouch",
                "BasicTouch",
                "BasicTouch",
                "BasicTouch",
                "PrudentTouch",
            ],
        ),
        edge(
            "waste-not-ii-replaces-waste-not",
            recipe,
            stats,
            &[
                "WasteNot",
                "WasteNotII",
                "BasicTouch",
                "BasicTouch",
                "BasicTouch",
                "BasicTouch",
                "BasicTouch",
                "PrudentTouch",
            ],
        ),
        edge(
            "rapid-synthesis",
            recipe,
            stats,
            &["RapidSynthesis", "Veneration", "RapidSynthesis"],
        ),
        edge(
            "inner-quiet-below-level-11",
            recipe,
            CrafterStats { level: 8, ..stats },
            &["BasicTouch", "BasicTouch", "BasicSynthesis", "BasicTouch"],
        ),
        edge(
            "legacy-inner-quiet",
            recipe,
            stats,
            &["InnerQuiet", "BasicTouch", "InnerQuiet"],
        ),
        edge(
            "precise-touch-needs-good",
            recipe,
            stats,
            &["PreciseTouch", "BasicTouch", "PreciseTouch"],
        ),
        edge(
            "precise-touch-under-heart-and-soul",
            recipe,
            specialist,
            &["HeartAndSoul", "PreciseTouch", "PreciseTouch"],
        ),
        edge(
            "refined-touch-combo",
            recipe,
            stats,
            &[
                "BasicTouch",
                "RefinedTouch",
                "RefinedTouch",
                "BasicTouch",
                "StandardTouch",
                "RefinedTouch",
            ],
        ),
        edge(
            "daring-touch-after-hasty",
            recipe,
            stats,
            &[
                "DaringTouch",
                "HastyTouch",
                "DaringTouch",
                "DaringTouch",
                "HastyTouch",
                "BasicTouch",
                "DaringTouch",
            ],
        ),
        edge(
            "daring-touch-below-its-level",
            recipe,
            CrafterStats {
                level: Action::DaringTouch.level_requirement() - 1,
                ..stats
            },
            &["HastyTouch", "DaringTouch"],
        ),
        edge(
            "hasty-touch-under-expedience",
            recipe,
            stats,
            &[
                "HastyTouch",
                "HastyTouch",
                "DaringTouch",
                "HastyTouch",
                "HastyTouch",
            ],
        ),
        edge(
            "hasty-touch-repeats-below-daring-level",
            recipe,
            CrafterStats {
                level: Action::DaringTouch.level_requirement() - 1,
                ..stats
            },
            &["HastyTouch", "HastyTouch"],
        ),
        edge(
            "expedience-survives-step-free-abilities",
            recipe,
            specialist,
            &[
                "HastyTouch",
                "FinalAppraisal",
                "HeartAndSoul",
                "CarefulObservation",
                "QuickInnovation",
                "DaringTouch",
            ],
        ),
    ];
    let rolled = SimulationOptions {
        assume_all_success: false,
        random_seed: Some(31),
        ..SimulationOptions::default()
    };
    let mut seeded = edge(
        "seeded-hasty-daring",
        recipe,
        stats,
        &["HastyTouch", "DaringTouch"].repeat(6),
    );
    seeded.options = rolled;
    cases.push(seeded);
    for condition in [Condition::Good, Condition::Excellent, Condition::Poor] {
        for (suffix, actions) in [
            (
                "touches",
                ids(&[
                    "BasicTouch",
                    "StandardTouch",
                    "BasicTouch",
                    "PreparatoryTouch",
                    "ByregotsBlessing",
                    "DelicateSynthesis",
                ]),
            ),
            (
                "precise",
                ids(&["BasicTouch", "PreciseTouch", "PreciseTouch"]),
            ),
        ] {
            let mut scenario = Scenario::new(
                format!("edge-forced-{condition:?}-{suffix}"),
                recipe,
                stats,
                SimulationOptions {
                    force_condition: Some(condition),
                    ..SimulationOptions::default()
                },
                actions,
                false,
            );
            scenario.final_availability = true;
            cases.push(scenario);
        }
    }
    let mut unknown = edge(
        "unknown-action",
        recipe,
        stats,
        &["BasicSynthesis", "BasicTouch"],
    );
    unknown.actions.insert(1, 999999);
    cases.push(unknown);
    cases
}

fn edge(name: &str, recipe: Recipe, stats: CrafterStats, actions: &[&str]) -> Scenario {
    let mut scenario = Scenario::new(
        format!("edge-{name}"),
        recipe,
        stats,
        SimulationOptions::default(),
        ids(actions),
        false,
    );
    scenario.final_availability = true;
    scenario
}
