//! `FormulaCalculator`: double arithmetic in the C# order of operations, truncated once, so both
//! languages round identically.

/// The modifier applies at or below the recipe's class job level, never its rlvl (0: no level
/// row). Raphael `raphael-data/src/lib.rs:145-149,175-192`; Teamcraft `getBaseProgression`.
pub fn base_progress(
    craftsmanship: i32,
    progress_divider: i32,
    progress_modifier: i32,
    crafter_level: i32,
    class_job_level: i32,
) -> i32 {
    let mut base = craftsmanship as f64 * 10.0 / progress_divider as f64 + 2.0;
    if class_job_level > 0 && crafter_level <= class_job_level {
        base = base * progress_modifier as f64 / 100.0;
    }
    base as i32
}

pub fn base_quality(
    control: i32,
    quality_divider: i32,
    quality_modifier: i32,
    crafter_level: i32,
    class_job_level: i32,
) -> i32 {
    let mut base = control as f64 * 10.0 / quality_divider as f64 + 35.0;
    if class_job_level > 0 && crafter_level <= class_job_level {
        base = base * quality_modifier as f64 / 100.0;
    }
    base as i32
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn progress_applies_modifier_only_at_or_below_class_job_level() {
        assert_eq!(base_progress(3800, 130, 80, 80, 90), 235);
        assert_eq!(base_progress(3800, 130, 80, 90, 90), 235);
        assert_eq!(base_progress(3800, 130, 80, 100, 90), 294);
        assert_eq!(base_progress(3800, 130, 80, 90, 0), 294);
    }

    #[test]
    fn quality_applies_modifier_only_at_or_below_class_job_level() {
        assert_eq!(base_quality(3600, 115, 70, 90, 90), 243);
        assert_eq!(base_quality(3600, 115, 70, 100, 90), 348);
        assert_eq!(base_quality(3600, 115, 70, 90, 0), 348);
    }

    #[test]
    fn base_values_truncate_once() {
        // (3000*10/130 + 2) * 90/100 = 209.5; flooring the division first gives 208.
        assert_eq!(base_progress(3000, 130, 90, 90, 90), 209);
        // (3000*10/115 + 35) * 70/100 = 207.1; flooring the division first gives 206.
        assert_eq!(base_quality(3000, 115, 70, 90, 90), 207);
        // Raphael on rlvl 690 at level 100 with 4000/4000.
        assert_eq!(base_progress(4000, 170, 90, 100, 100), 213);
        assert_eq!(base_quality(4000, 150, 75, 100, 100), 226);
    }
}
