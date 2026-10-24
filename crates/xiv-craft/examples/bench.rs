//! Solver benchmark over the ten recipes in `tests/benchmarks/solver-benchmarks.json`:
//!
//! ```text
//! cargo run --release --example bench [budget_ms] [seed] [id_filter]
//! ```
//!
//! MCTS runs twice per recipe: with the default config, and with early termination off so the
//! full budget is spent. The C# rows are the numbers recorded in
//! `docs/solver-optimization-recommendations.md` (a run that predates early termination and used
//! the whole budget) and a run of the current C# suite with its defaults on 2026-09-12.

#[path = "../tests/benchmarks/mod.rs"]
mod benchmarks;

use std::time::Instant;
use xiv_craft::solver::greedy;
use xiv_craft::{
    GreedySolverConfig, MctsConfig, MctsSolver, SolverConstraints, SolverResult, SolverSettings,
};

/// `(id, iter/s, quality %)` from the recommendations doc.
const CSHARP_DOC: [(&str, f64, f64); 10] = [
    ("lv100_4star_endgame", 11_988.0, 62.2),
    ("lv90_3star_standard", 14_527.0, 62.3),
    ("lv80_2star_overgeared", 46_105.0, 100.0),
    ("lv70_standard", 11_026.0, 95.5),
    ("lv60_standard", 15_940.0, 92.8),
    ("lv50_classic", 19_861.0, 100.0),
    ("lv90_35dur_food", 23_278.0, 96.0),
    ("lv100_40dur_potion", 22_088.0, 66.4),
    ("lv100_undergeared", 13_951.0, 58.9),
    ("lv100_4star_progress_heavy", 13_112.0, 48.1),
];

/// `(id, iterations, ms, quality %)` printed by `SolverBenchmarks.MctsSolver_AchievesTargetQualityVsCommunity`
/// with today's defaults, which stop early after 5000 iterations without improvement.
const CSHARP_TODAY: [(&str, f64, f64, f64); 10] = [
    ("lv100_4star_endgame", 12_237.0, 948.0, 53.3),
    ("lv90_3star_standard", 8_771.0, 269.0, 64.0),
    ("lv80_2star_overgeared", 5_100.0, 92.0, 100.0),
    ("lv70_standard", 12_678.0, 639.0, 94.5),
    ("lv60_standard", 10_040.0, 375.0, 95.8),
    ("lv50_classic", 5_057.0, 155.0, 100.0),
    ("lv90_35dur_food", 6_566.0, 139.0, 85.8),
    ("lv100_40dur_potion", 8_531.0, 207.0, 56.7),
    ("lv100_undergeared", 6_127.0, 190.0, 51.9),
    ("lv100_4star_progress_heavy", 8_908.0, 276.0, 45.3),
];

struct Row {
    id: String,
    greedy_ms: f64,
    greedy_quality: f64,
    greedy_steps: usize,
    defaults: SolverResult,
    full: SolverResult,
    full_tree_mb: f64,
}

fn wall_clock() -> impl FnMut() -> f64 {
    let origin = Instant::now();
    move || origin.elapsed().as_secs_f64() * 1000.0
}

fn iter_per_s(result: &SolverResult) -> f64 {
    let seconds = result.metrics.solve_time_ms / 1000.0;
    if seconds > 0.0 {
        result.metrics.iterations_completed as f64 / seconds
    } else {
        0.0
    }
}

fn main() {
    let mut args = std::env::args().skip(1);
    let budget_ms: f64 = args
        .next()
        .map(|a| a.parse().expect("budget in ms"))
        .unwrap_or(10_000.0);
    let seed: i32 = args.next().map(|a| a.parse().expect("seed")).unwrap_or(42);
    let filter = args.next().unwrap_or_default();

    let settings = SolverSettings {
        time_budget_ms: budget_ms,
        max_iterations: None,
        random_seed: Some(seed),
    };
    let constraints = SolverConstraints::default();
    let mut defaults = MctsSolver::new(MctsConfig::default(), GreedySolverConfig::default());
    let mut full = MctsSolver::new(
        MctsConfig {
            early_termination_threshold: u64::MAX,
            ..MctsConfig::default()
        },
        GreedySolverConfig::default(),
    );

    let mut rows = Vec::new();
    for entry in benchmarks::load() {
        if !entry.id.contains(&filter) {
            continue;
        }
        let recipe = entry.recipe();
        let stats = entry.stats();
        eprintln!("[{}] solving...", entry.id);

        let greedy = greedy::solve(
            &recipe,
            &stats,
            &constraints,
            &GreedySolverConfig::default(),
            &mut wall_clock(),
        );
        let defaults_result =
            defaults.solve(&recipe, &stats, &constraints, &settings, &mut wall_clock());
        let full_result = full.solve(&recipe, &stats, &constraints, &settings, &mut wall_clock());
        let full_tree_mb = full.tree_memory_bytes() as f64 / (1024.0 * 1024.0);

        rows.push(Row {
            id: entry.id,
            greedy_ms: greedy.metrics.solve_time_ms,
            greedy_quality: greedy.metrics.final_quality_percent,
            greedy_steps: greedy.metrics.total_steps,
            defaults: defaults_result,
            full: full_result,
            full_tree_mb,
        });
    }

    println!();
    println!("Greedy (Rust)");
    println!("| Benchmark | Time(ms) | Quality% | Steps |");
    println!("|---|---:|---:|---:|");
    for row in &rows {
        println!(
            "| {} | {:.2} | {:.1} | {} |",
            row.id, row.greedy_ms, row.greedy_quality, row.greedy_steps
        );
    }

    println!();
    println!("MCTS (Rust), budget {budget_ms} ms, seed {seed}");
    println!("| Benchmark | Mode | Time(ms) | Iterations | Nodes | Tree MB | Iter/s | Quality% | HQ% | Steps |");
    println!("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");
    for row in &rows {
        for (mode, result, tree_mb) in [
            ("defaults", &row.defaults, None),
            ("full budget", &row.full, Some(row.full_tree_mb)),
        ] {
            println!(
                "| {} | {} | {:.0} | {} | {} | {} | {:.0} | {:.1} | {} | {} |",
                row.id,
                mode,
                result.metrics.solve_time_ms,
                result.metrics.iterations_completed,
                result.metrics.nodes_explored,
                tree_mb.map_or(String::from("-"), |mb| format!("{mb:.0}")),
                iter_per_s(result),
                result.metrics.final_quality_percent,
                result.metrics.final_hq_percent,
                result.metrics.total_steps
            );
        }
    }

    println!();
    println!("C# vs Rust (full budget)");
    println!("| Benchmark | C# doc iter/s | C# doc Q% | C# today iter/s | C# today Q% | Rust iter/s | Rust Q% | Speedup vs doc | Q delta vs doc |");
    println!("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
    let mut min_speedup = f64::INFINITY;
    let mut min_delta = f64::INFINITY;
    for row in &rows {
        let (_, doc_rate, doc_quality) = CSHARP_DOC
            .iter()
            .find(|(id, _, _)| *id == row.id)
            .expect("doc row");
        let (_, today_iters, today_ms, today_quality) = CSHARP_TODAY
            .iter()
            .find(|(id, _, _, _)| *id == row.id)
            .expect("today row");
        let rust_rate = iter_per_s(&row.full);
        let rust_quality = row.full.metrics.final_quality_percent;
        let speedup = rust_rate / doc_rate;
        let delta = rust_quality - doc_quality;
        min_speedup = min_speedup.min(speedup);
        min_delta = min_delta.min(delta);
        println!(
            "| {} | {:.0} | {:.1} | {:.0} | {:.1} | {:.0} | {:.1} | {:.1}x | {:+.1} |",
            row.id,
            doc_rate,
            doc_quality,
            today_iters / (today_ms / 1000.0),
            today_quality,
            rust_rate,
            rust_quality,
            speedup,
            delta
        );
    }
    println!();
    println!("min speedup vs doc: {min_speedup:.1}x; min quality delta vs doc: {min_delta:+.1}");
}
