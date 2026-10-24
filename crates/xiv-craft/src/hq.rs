//! `HQCalculator.CalculateStartingQuality`: the quality a craft starts with from HQ materials.

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Ingredient {
    pub item_id: i32,
    pub quantity: i32,
    pub can_be_hq: bool,
    /// 0 in data exported without item levels; the calculator then weighs by count.
    pub item_level: i32,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct HqInput {
    pub item_id: i32,
    pub hq_quantity: i32,
}

/// Each HQ material weighs its item level times its count, or its count alone when the data has
/// no item levels (Raphael `raphael-data/src/lib.rs:203-239`; Teamcraft `recipes.extractor.ts`).
pub fn calculate_starting_quality(
    max_quality: i32,
    material_quality_factor: i32,
    ingredients: &[Ingredient],
    hq_inputs: &[HqInput],
) -> i32 {
    if material_quality_factor <= 0 || max_quality <= 0 {
        return 0;
    }
    if hq_inputs.is_empty() {
        return 0;
    }

    let mut total_item_level: i64 = 0;
    let mut provided_item_level: i64 = 0;
    let mut total_count: i64 = 0;
    let mut provided_count: i64 = 0;
    for ingredient in ingredients {
        if !ingredient.can_be_hq {
            continue;
        }
        total_item_level += ingredient.item_level as i64 * ingredient.quantity as i64;
        total_count += ingredient.quantity as i64;
        // The C# side uses FirstOrDefault, so a duplicated item id counts its first entry only.
        if let Some(input) = hq_inputs.iter().find(|i| i.item_id == ingredient.item_id) {
            let hq_quantity = input.hq_quantity.min(ingredient.quantity) as i64;
            provided_item_level += ingredient.item_level as i64 * hq_quantity;
            provided_count += hq_quantity;
        }
    }

    let (provided, total) = if total_item_level > 0 {
        (provided_item_level, total_item_level)
    } else {
        (provided_count, total_count)
    };
    if total == 0 || provided <= 0 {
        return 0;
    }

    (max_quality as i64 * material_quality_factor as i64 * provided / total / 100) as i32
}
