use crate::actions::Action;
use crate::formula;
use crate::state::{Condition, CrafterStats, CraftingState, Recipe};

pub(super) fn apply_effect(
    action: Action,
    state: CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
) -> CraftingState {
    match action {
        // Trained Eye replaces the whole effect: quality jumps to max and nothing else changes.
        Action::TrainedEye => {
            let mut next = state;
            next.quality = state.max_quality;
            next
        }
        Action::ByregotsBlessing => {
            let potency = 100 + state.inner_quiet_stacks * 20;
            let mut next = state;
            next.quality = (state.quality + calculate_quality(&state, stats, recipe, potency))
                .min(state.max_quality);
            next.great_strides_turns = 0;
            next.inner_quiet_stacks = 0;
            next
        }
        _ => {
            let (potency, inner_quiet_gain) = match action {
                Action::BasicTouch => (100, 1),
                Action::StandardTouch => (125, 1),
                Action::AdvancedTouch => (150, 1),
                Action::HastyTouch => (100, 1),
                Action::FocusedTouch => (150, 1),
                Action::PreparatoryTouch => (200, 2),
                Action::PrudentTouch => (100, 1),
                Action::Reflect => (300, 2),
                Action::TrainedFinesse => (100, 1),
                Action::PreciseTouch => (150, 2),
                Action::RefinedTouch => (100, 1),
                Action::DaringTouch => (150, 1),
                _ => unreachable!("{action:?} is not a quality action"),
            };
            let mut next = state;
            next.quality = (state.quality + calculate_quality(&state, stats, recipe, potency))
                .min(state.max_quality);
            next.great_strides_turns = 0;
            next.inner_quiet_stacks =
                gain_inner_quiet(state.inner_quiet_stacks, inner_quiet_gain, stats);

            match action {
                // Heart and Soul is spent only when it substituted for the missing Good condition.
                Action::PreciseTouch
                    if state.heart_and_soul_active
                        && !matches!(state.condition, Condition::Good | Condition::Excellent) =>
                {
                    next.heart_and_soul_active = false;
                }
                // Usable outside the combo, like every combo action in game; only the extra Inner Quiet needs Basic Touch
                // (Teamcraft quality/refined-touch.ts; Raphael raphael-sim/src/actions.rs:923-966 prunes the uncomboed use).
                Action::RefinedTouch if state.combo_action == Action::BasicTouch.id() => {
                    next.inner_quiet_stacks = gain_inner_quiet(next.inner_quiet_stacks, 1, stats);
                }
                _ => {}
            }
            next
        }
    }
}

// Inner Quiet is a level 11 trait (Raphael raphael-sim/src/state.rs:120; Teamcraft quality-action.ts:96).
pub(super) fn gain_inner_quiet(stacks: i32, gain: i32, stats: &CrafterStats) -> i32 {
    if stats.level >= 11 {
        (stacks + gain).min(10)
    } else {
        stacks
    }
}

// Buffs and Inner Quiet multiply the potency; the condition counts in halves inside the numerator so
// the gain truncates once (Raphael raphael-sim/src/effects.rs:76-80, actions.rs:28-52).
pub(super) fn calculate_quality(
    state: &CraftingState,
    stats: &CrafterStats,
    recipe: &Recipe,
    potency: i32,
) -> i32 {
    let base = formula::base_quality(
        stats.effective_control(state.inner_quiet_stacks),
        recipe.quality_divider,
        recipe.quality_modifier,
        stats.level,
        recipe.class_job_level,
    );
    let mut buff_modifier = 10;
    if state.great_strides_turns > 0 {
        buff_modifier += 10;
    }
    if state.innovation_turns > 0 {
        buff_modifier += 5;
    }
    let condition_halves: i64 = match state.condition {
        Condition::Good => 3,
        Condition::Excellent => 8,
        Condition::Poor => 1,
        _ => 2,
    };
    let inner_quiet_modifier = (state.inner_quiet_stacks + 10) * buff_modifier;
    (base as i64 * potency as i64 * inner_quiet_modifier as i64 * condition_halves / 20000) as i32
}

#[cfg(test)]
mod tests {
    use super::*;

    fn fixture() -> (CraftingState, CrafterStats, Recipe) {
        let stats = CrafterStats {
            level: 90,
            craftsmanship: 3800,
            control: 3600,
            cp: 600,
            is_specialist: false,
        };
        let recipe = Recipe {
            recipe_level: 580,
            class_job_level: 90,
            difficulty: 3500,
            quality: 7200,
            durability: 70,
            progress_divider: 130,
            progress_modifier: 80,
            quality_divider: 115,
            quality_modifier: 70,
            ..Default::default()
        };
        (
            CraftingState::create_initial(&recipe, &stats),
            stats,
            recipe,
        )
    }

    #[test]
    fn basic_touch_adds_quality_and_one_inner_quiet_stack() {
        let (state, stats, recipe) = fixture();
        let next = Action::BasicTouch.execute(state, &stats, &recipe, true);
        assert_eq!(next.quality, 243);
        assert_eq!(next.inner_quiet_stacks, 1);
        assert_eq!(next.combo_action, Action::BasicTouch.id());
    }

    #[test]
    fn reflect_grants_two_stacks() {
        let (state, stats, recipe) = fixture();
        let next = Action::Reflect.execute(state, &stats, &recipe, true);
        assert_eq!(next.inner_quiet_stacks, 2);
        assert_eq!(next.quality, 729);
    }

    #[test]
    fn inner_quiet_and_buffs_multiply_and_great_strides_is_consumed() {
        let (mut state, stats, recipe) = fixture();
        state.inner_quiet_stacks = 5;
        state.great_strides_turns = 2;
        state.innovation_turns = 2;
        let next = Action::PreparatoryTouch.execute(state, &stats, &recipe, true);
        assert_eq!(next.quality, 1822);
        assert_eq!(next.great_strides_turns, 0);
        assert_eq!(next.inner_quiet_stacks, 7);
    }

    #[test]
    fn byregots_scales_with_stacks_then_clears_them() {
        let (mut state, stats, recipe) = fixture();
        state.inner_quiet_stacks = 10;
        let next = Action::ByregotsBlessing.execute(state, &stats, &recipe, true);
        assert_eq!(next.quality, 1458);
        assert_eq!(next.inner_quiet_stacks, 0);
    }

    #[test]
    fn condition_counts_in_halves_and_truncates_once() {
        let (state, stats, recipe) = fixture();
        let touch = |action: Action, condition: Condition| {
            let state = CraftingState { condition, ..state };
            action.execute(state, &stats, &recipe, true).quality
        };
        assert_eq!(touch(Action::BasicTouch, Condition::Normal), 243);
        assert_eq!(touch(Action::BasicTouch, Condition::Good), 364);
        assert_eq!(touch(Action::BasicTouch, Condition::Excellent), 972);
        assert_eq!(touch(Action::BasicTouch, Condition::Poor), 121);
        assert_eq!(touch(Action::BasicTouch, Condition::Pliant), 243);
        // 243 * 125 * 100 * 3 / 20000 = 455.6; truncating before the condition gives 454.
        assert_eq!(touch(Action::StandardTouch, Condition::Good), 455);
    }

    #[test]
    fn inner_quiet_is_gained_from_level_11() {
        let (state, stats, recipe) = fixture();
        let stacks = |level: i32| {
            let stats = CrafterStats { level, ..stats };
            Action::BasicTouch
                .execute(state, &stats, &recipe, true)
                .inner_quiet_stacks
        };
        assert_eq!(stacks(10), 0);
        assert_eq!(stacks(11), 1);
    }

    // The Dawntrail touches against Raphael's simulator (rlvl 690, Lv100, 4000/4000), the C#
    // QualityActionsTests fixture.
    mod dawntrail {
        use super::*;
        use crate::fail_reason::FailReason;
        use crate::simulator::{simulate, simulate_with, SimulationOptions, SimulationResult};

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
                ..Default::default()
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

        fn specialist() -> CrafterStats {
            CrafterStats {
                is_specialist: true,
                ..lv100()
            }
        }

        fn run(stats: &CrafterStats, actions: &[Action]) -> SimulationResult {
            let ids: Vec<i32> = actions.iter().map(|a| a.id()).collect();
            simulate(&rlvl690(), stats, &ids)
        }

        fn quality_deltas(result: &SimulationResult) -> Vec<i32> {
            result.steps.iter().map(|s| s.quality_delta()).collect()
        }

        fn on(condition: Condition, stats: &CrafterStats) -> CraftingState {
            CraftingState {
                condition,
                ..CraftingState::create_initial(&rlvl690(), stats)
            }
        }

        #[test]
        fn precise_touch_on_good_or_excellent() {
            for (condition, quality) in [(Condition::Good, 508), (Condition::Excellent, 1356)] {
                let state = on(condition, &lv100());
                assert_eq!(
                    Action::PreciseTouch.fail_reason(&state, &lv100(), &rlvl690()),
                    None
                );
                let next = Action::PreciseTouch.execute(state, &lv100(), &rlvl690(), true);
                assert_eq!(next.quality, quality, "{condition:?}");
                assert_eq!(state.cp - next.cp, 18);
                assert_eq!(state.durability - next.durability, 10);
                assert_eq!(next.inner_quiet_stacks, 2);
                assert_eq!(next.combo_action, 0);
            }
        }

        #[test]
        fn precise_touch_needs_good_or_heart_and_soul() {
            for condition in [Condition::Normal, Condition::Poor, Condition::Centered] {
                assert_eq!(
                    Action::PreciseTouch.fail_reason(
                        &on(condition, &lv100()),
                        &lv100(),
                        &rlvl690()
                    ),
                    Some(FailReason::RequiresGoodOrExcellent)
                );
            }
            let good = on(Condition::Good, &lv100());
            let level_52 = CrafterStats {
                level: 52,
                ..lv100()
            };
            assert_eq!(
                Action::PreciseTouch.fail_reason(&good, &level_52, &rlvl690()),
                Some(FailReason::RequiresLevel(53))
            );
        }

        #[test]
        fn precise_touch_spends_heart_and_soul_only_off_good() {
            for (condition, quality, kept) in [
                (Condition::Normal, 339, false),
                (Condition::Good, 508, true),
            ] {
                let state = CraftingState {
                    heart_and_soul_active: true,
                    heart_and_soul_used: true,
                    ..on(condition, &specialist())
                };
                assert_eq!(
                    Action::PreciseTouch.fail_reason(&state, &specialist(), &rlvl690()),
                    None
                );
                let next = Action::PreciseTouch.execute(state, &specialist(), &rlvl690(), true);
                assert_eq!(next.quality, quality, "{condition:?}");
                assert_eq!(next.heart_and_soul_active, kept, "{condition:?}");
            }
        }

        #[test]
        fn refined_touch_after_basic_touch_gains_an_extra_inner_quiet() {
            let result = run(&lv100(), &[Action::BasicTouch, Action::RefinedTouch]);
            assert_eq!(quality_deltas(&result), [226, 248]);
            let cp: Vec<i32> = result.steps.iter().map(|s| -s.cp_delta()).collect();
            assert_eq!(cp, [18, 24]);
            assert_eq!(result.final_state.inner_quiet_stacks, 3);
            assert_eq!(result.final_state.combo_action, 0);
        }

        #[test]
        fn refined_touch_is_usable_outside_the_combo() {
            let result = run(
                &lv100(),
                &[
                    Action::BasicTouch,
                    Action::StandardTouch,
                    Action::RefinedTouch,
                ],
            );
            assert!(result.steps.iter().all(|s| s.succeeded));
            assert_eq!(quality_deltas(&result), [226, 310, 271]);
            assert_eq!(result.final_state.inner_quiet_stacks, 3);

            let level_91 = CrafterStats {
                level: 91,
                ..lv100()
            };
            let state = CraftingState {
                combo_action: Action::BasicTouch.id(),
                ..CraftingState::create_initial(&rlvl690(), &level_91)
            };
            assert_eq!(
                Action::RefinedTouch.fail_reason(&state, &level_91, &rlvl690()),
                Some(FailReason::RequiresLevel(92))
            );
        }

        #[test]
        fn daring_touch_after_hasty_touch() {
            for level in [96, 100] {
                let stats = CrafterStats { level, ..lv100() };
                let result = run(&stats, &[Action::HastyTouch, Action::DaringTouch]);
                assert!(result.steps.iter().all(|s| s.succeeded), "level {level}");
                assert_eq!(quality_deltas(&result), [226, 372], "level {level}");
                let cp: Vec<i32> = result.steps.iter().map(|s| -s.cp_delta()).collect();
                assert_eq!(cp, [0, 0]);
                assert_eq!(result.final_state.durability, 60);
                assert_eq!(result.final_state.inner_quiet_stacks, 2);
                assert!(!result.final_state.expedience_active);
            }
        }

        #[test]
        fn hasty_touch_grants_expedience_from_level_96() {
            for (level, expected) in [(95, false), (96, true), (100, true)] {
                let stats = CrafterStats { level, ..lv100() };
                let result = run(&stats, &[Action::HastyTouch]);
                assert_eq!(
                    result.final_state.expedience_active, expected,
                    "level {level}"
                );
            }
            let initial = CraftingState::create_initial(&rlvl690(), &lv100());
            let failed = Action::HastyTouch.execute(initial, &lv100(), &rlvl690(), false);
            assert!(!failed.expedience_active);
        }

        #[test]
        fn hasty_touch_is_refused_under_expedience() {
            let result = run(
                &lv100(),
                &[Action::HastyTouch, Action::HastyTouch, Action::DaringTouch],
            );
            assert!(!result.steps[1].succeeded);
            assert_eq!(
                result.steps[1].fail_reason,
                Some(FailReason::HastyTouchUnderExpedience)
            );
            assert_eq!(
                FailReason::HastyTouchUnderExpedience.to_string(),
                "Hasty Touch becomes Daring Touch under Expedience"
            );
            assert!(result.steps[2].succeeded);
            assert_eq!(quality_deltas(&result), vec![226, 0, 372]);
            assert!(!result.final_state.expedience_active);

            let level_95 = CrafterStats {
                level: 95,
                ..lv100()
            };
            let below = run(&level_95, &[Action::HastyTouch, Action::HastyTouch]);
            assert!(below.steps.iter().all(|s| s.succeeded));
        }

        #[test]
        fn daring_touch_needs_expedience_before_its_level() {
            let level_95 = CrafterStats {
                level: 95,
                ..lv100()
            };
            let result = run(&level_95, &[Action::HastyTouch, Action::DaringTouch]);
            assert!(!result.steps[1].succeeded);
            assert_eq!(
                result.steps[1].fail_reason,
                Some(FailReason::RequiresExpedience)
            );
            let under_expedience = CraftingState {
                expedience_active: true,
                ..CraftingState::create_initial(&rlvl690(), &level_95)
            };
            assert_eq!(
                Action::DaringTouch.fail_reason(&under_expedience, &level_95, &rlvl690()),
                Some(FailReason::RequiresLevel(96))
            );
        }

        #[test]
        fn any_step_ends_expedience() {
            let result = run(
                &lv100(),
                &[Action::HastyTouch, Action::BasicTouch, Action::DaringTouch],
            );
            assert_eq!(
                result.steps[2].fail_reason,
                Some(FailReason::RequiresExpedience)
            );
        }

        #[test]
        fn expedience_survives_step_free_abilities() {
            for (step_free, quality) in
                [(Action::HeartAndSoul, 372), (Action::QuickInnovation, 559)]
            {
                let result = run(
                    &specialist(),
                    &[Action::HastyTouch, step_free, Action::DaringTouch],
                );
                assert!(result.steps.iter().all(|s| s.succeeded), "{step_free:?}");
                assert_eq!(result.steps[2].quality_delta(), quality, "{step_free:?}");
            }
        }

        #[test]
        fn gated_steps_leave_expedience_alone() {
            // Precise Touch is gated on Normal, so it must not execute and must not end Expedience.
            let result = run(
                &lv100(),
                &[
                    Action::HastyTouch,
                    Action::PreciseTouch,
                    Action::DaringTouch,
                ],
            );
            assert!(!result.steps[1].succeeded);
            assert!(result.steps[2].succeeded);
            assert_eq!(result.steps[2].quality_delta(), 372);
        }

        #[test]
        fn a_failed_daring_touch_spends_expedience() {
            let state = CraftingState {
                expedience_active: true,
                ..CraftingState::create_initial(&rlvl690(), &lv100())
            };
            assert_eq!(Action::DaringTouch.cp_cost(&state), 0);
            assert_eq!(Action::DaringTouch.success_rate(&state), 60);
            let failed = Action::DaringTouch.execute(state, &lv100(), &rlvl690(), false);
            assert!(!failed.expedience_active);

            // A failed Hasty Touch grants no Expedience, so the Daring Touch after it is refused.
            let rolled = simulate_with(
                &rlvl690(),
                &lv100(),
                &[Action::HastyTouch.id(), Action::DaringTouch.id()],
                &SimulationOptions {
                    assume_all_success: false,
                    random_seed: Some(31),
                    ..SimulationOptions::default()
                },
            );
            assert_eq!(
                rolled.steps[1].fail_reason == Some(FailReason::RequiresExpedience),
                !rolled.steps[0].succeeded
            );
        }
    }
}
