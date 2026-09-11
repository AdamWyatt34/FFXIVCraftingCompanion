//! `FormulaCalculator`: integer arithmetic with C# truncation semantics (both languages truncate
//! toward zero on signed division), so the order of operations is kept exactly.

pub fn base_progress(
    craftsmanship: i32,
    progress_divider: i32,
    progress_modifier: i32,
    crafter_level: i32,
    recipe_level: i32,
) -> i32 {
    let mut base = craftsmanship * 10 / progress_divider + 2;
    if crafter_level <= recipe_level {
        base = base * progress_modifier / 100;
    }
    base
}

pub fn base_quality(
    control: i32,
    quality_divider: i32,
    quality_modifier: i32,
    crafter_level: i32,
    recipe_level: i32,
) -> i32 {
    let mut base = control * 10 / quality_divider + 35;
    if crafter_level <= recipe_level {
        base = base * quality_modifier / 100;
    }
    base
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn progress_applies_modifier_only_at_or_below_recipe_level() {
        assert_eq!(base_progress(3800, 130, 80, 90, 580), 235);
        assert_eq!(base_progress(3800, 130, 80, 600, 580), 294);
    }

    #[test]
    fn quality_applies_modifier_only_at_or_below_recipe_level() {
        assert_eq!(base_quality(3600, 115, 70, 90, 580), 243);
        assert_eq!(base_quality(3600, 115, 70, 600, 580), 348);
    }
}
