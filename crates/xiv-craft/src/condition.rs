use crate::rng::DotNetRandom;
use crate::state::Condition;

/// `ConditionResolver`: deterministic follow-ups (Excellent to Poor, Good and Poor to Normal,
/// Good Omen to Good) and seeded rolls otherwise.
#[derive(Clone, Debug)]
pub struct ConditionResolver {
    rng: DotNetRandom,
}

impl ConditionResolver {
    pub fn new(seed: i32) -> ConditionResolver {
        ConditionResolver {
            rng: DotNetRandom::new(seed),
        }
    }

    pub fn next_condition(
        &mut self,
        current: Condition,
        is_expert: bool,
        conditions_flag: i32,
    ) -> Condition {
        match current {
            Condition::Excellent => return Condition::Poor,
            Condition::Good | Condition::Poor => return Condition::Normal,
            Condition::GoodOmen => return Condition::Good,
            _ => {}
        }

        if is_expert {
            self.next_expert_condition(conditions_flag)
        } else {
            self.next_standard_condition()
        }
    }

    fn next_standard_condition(&mut self) -> Condition {
        let roll = self.rng.next_double();
        if roll < 0.04 {
            Condition::Excellent
        } else if roll < 0.24 {
            Condition::Good
        } else {
            Condition::Normal
        }
    }

    fn next_expert_condition(&mut self, conditions_flag: i32) -> Condition {
        const FLAGGED: [(i32, Condition); 7] = [
            (0x01, Condition::Good),
            (0x02, Condition::Centered),
            (0x04, Condition::Sturdy),
            (0x08, Condition::Pliant),
            (0x10, Condition::Malleable),
            (0x20, Condition::Primed),
            (0x40, Condition::GoodOmen),
        ];

        let mut candidates = [Condition::Normal; 8];
        let mut count = 1;
        for (flag, condition) in FLAGGED {
            if conditions_flag & flag != 0 {
                candidates[count] = condition;
                count += 1;
            }
        }

        // Every candidate weighs 1.0; the sum and the running total stay in f64 like the C# LINQ
        // Sum and the cumulative loop, so the comparison boundaries are identical.
        let mut total_weight = 0.0f64;
        for _ in 0..count {
            total_weight += 1.0;
        }
        let roll = self.rng.next_double() * total_weight;
        let mut cumulative = 0.0f64;
        for candidate in candidates.iter().take(count) {
            cumulative += 1.0;
            if roll < cumulative {
                return *candidate;
            }
        }
        Condition::Normal
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn deterministic_transitions_ignore_rng() {
        let mut resolver = ConditionResolver::new(1);
        assert_eq!(
            resolver.next_condition(Condition::Excellent, false, 0),
            Condition::Poor
        );
        assert_eq!(
            resolver.next_condition(Condition::Good, true, 0x7F),
            Condition::Normal
        );
        assert_eq!(
            resolver.next_condition(Condition::Poor, false, 0),
            Condition::Normal
        );
        assert_eq!(
            resolver.next_condition(Condition::GoodOmen, true, 0x7F),
            Condition::Good
        );
    }

    #[test]
    fn expert_with_no_flags_is_always_normal() {
        let mut resolver = ConditionResolver::new(9);
        for _ in 0..50 {
            assert_eq!(
                resolver.next_condition(Condition::Normal, true, 0),
                Condition::Normal
            );
        }
    }
}
