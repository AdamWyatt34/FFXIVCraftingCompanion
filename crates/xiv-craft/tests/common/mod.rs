//! The C# `CraftingTestHelpers` fixtures plus the clocks the solver tests inject.

#![allow(dead_code)]

pub mod golden;
pub mod sources;

use std::time::Instant;
use xiv_craft::{CraftStatus, CrafterStats, CraftingState, Recipe};

pub fn default_stats() -> CrafterStats {
    CrafterStats {
        level: 90,
        craftsmanship: 3800,
        control: 3600,
        cp: 600,
        is_specialist: false,
    }
}

pub fn default_recipe() -> Recipe {
    Recipe {
        recipe_id: 100,
        recipe_level: 580,
        difficulty: 3500,
        quality: 7200,
        durability: 70,
        progress_divider: 130,
        progress_modifier: 80,
        quality_divider: 115,
        quality_modifier: 70,
        stars: 2,
        suggested_craftsmanship: 3700,
        suggested_control: 3280,
        ..Recipe::default()
    }
}

pub fn default_state() -> CraftingState {
    CraftingState {
        durability: 70,
        max_durability: 70,
        cp: 600,
        max_cp: 600,
        max_progress: 3500,
        max_quality: 7200,
        status: CraftStatus::InProgress,
        is_first_step: true,
        ..CraftingState::default()
    }
}

/// Wall clock for tests that exercise the time budget.
pub fn wall_clock() -> impl FnMut() -> f64 {
    let origin = Instant::now();
    move || origin.elapsed().as_secs_f64() * 1000.0
}

/// A clock that never advances, so only the iteration budget can stop a run.
pub fn frozen_clock() -> impl FnMut() -> f64 {
    || 0.0
}
