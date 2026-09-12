//! `HQCalculator.CalculateStartingQuality`: the quality a craft starts with from HQ materials.

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Ingredient {
    pub item_id: i32,
    pub quantity: i32,
    pub can_be_hq: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct HqInput {
    pub item_id: i32,
    pub hq_quantity: i32,
}

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

    let mut total_hqable = 0;
    let mut total_hq = 0;
    for ingredient in ingredients {
        if !ingredient.can_be_hq {
            continue;
        }
        total_hqable += ingredient.quantity;
        // The C# side uses FirstOrDefault, so a duplicated item id counts its first entry only.
        if let Some(input) = hq_inputs.iter().find(|i| i.item_id == ingredient.item_id) {
            total_hq += input.hq_quantity.min(ingredient.quantity);
        }
    }

    if total_hqable == 0 || total_hq == 0 {
        return 0;
    }

    let max_starting = (max_quality as f64 * material_quality_factor as f64 / 100.0).floor() as i32;
    (max_starting as f64 * total_hq as f64 / total_hqable as f64).floor() as i32
}
