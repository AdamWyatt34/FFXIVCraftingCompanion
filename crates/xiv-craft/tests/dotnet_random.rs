use serde::Deserialize;
use std::path::PathBuf;
use xiv_craft::DotNetRandom;

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Golden {
    seeds: Vec<Series>,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Series {
    seed: i32,
    values: Vec<f64>,
}

// Frozen System.Random output: regenerating it with the Rust implementation would erase the independent oracle.
fn golden_path() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("tests/goldens/dotnet-random.json")
}

#[test]
fn seeded_random_matches_dotnet_for_every_pinned_seed() {
    let text = std::fs::read_to_string(golden_path()).expect("dotnet-random.json is committed");
    let golden: Golden = serde_json::from_str(&text).expect("valid golden");
    assert!(!golden.seeds.is_empty());

    for series in golden.seeds {
        let mut rng = DotNetRandom::new(series.seed);
        for (index, expected) in series.values.iter().enumerate() {
            let actual = rng.next_double();
            assert!(
                actual.to_bits() == expected.to_bits(),
                "seed {} sample {index}: expected {expected:?}, got {actual:?}",
                series.seed
            );
        }
    }
}
