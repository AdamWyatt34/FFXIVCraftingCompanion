use core::fmt;

/// Why an action cannot execute. `Display` reproduces the C# `GetFailReason` strings verbatim,
/// which is what saved simulations and the parity goldens carry.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum FailReason {
    NotInProgress,
    RequiresLevel(i32),
    NotEnoughCp,
    RequiresGoodOrExcellent,
    FirstActionOnly,
    WasteNotActive,
    RequiresInnerQuiet,
    RequiresTenInnerQuiet,
    RequiresSpecialist,
    NotOnExpert,
    RecipeLevelUnavailable,
    RequiresCrafterLevel(i32),
    HeartAndSoulOnce,
    QuickInnovationOnce,
    InnovationActive,
    TrainedPerfectionOnce,
    CarefulObservationLimit,
    RequiresExpedience,
    HastyTouchUnderExpedience,
    UnknownAction,
    SuccessRoll,
}

impl fmt::Display for FailReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            FailReason::NotInProgress => f.write_str("Craft is not in progress"),
            FailReason::RequiresLevel(level) => write!(f, "Requires level {level}"),
            FailReason::NotEnoughCp => f.write_str("Not enough CP"),
            FailReason::RequiresGoodOrExcellent => {
                f.write_str("Requires Good or Excellent condition")
            }
            FailReason::FirstActionOnly => f.write_str("Can only be used as the first action"),
            FailReason::WasteNotActive => f.write_str("Cannot be used while Waste Not is active"),
            FailReason::RequiresInnerQuiet => f.write_str("Requires Inner Quiet stacks"),
            FailReason::RequiresTenInnerQuiet => f.write_str("Requires 10 Inner Quiet stacks"),
            FailReason::RequiresSpecialist => f.write_str("Requires specialist"),
            FailReason::NotOnExpert => f.write_str("Cannot be used on expert recipes"),
            FailReason::RecipeLevelUnavailable => f.write_str("Recipe level data unavailable"),
            FailReason::RequiresCrafterLevel(level) => {
                write!(f, "Requires crafter level {level} or higher")
            }
            FailReason::HeartAndSoulOnce => {
                f.write_str("Heart and Soul can only be used once per craft")
            }
            FailReason::QuickInnovationOnce => {
                f.write_str("Quick Innovation can only be used once per craft")
            }
            FailReason::InnovationActive => {
                f.write_str("Cannot be used while Innovation is active")
            }
            FailReason::TrainedPerfectionOnce => {
                f.write_str("Trained Perfection can only be used once per craft")
            }
            FailReason::CarefulObservationLimit => {
                f.write_str("Careful Observation can only be used 3 times per craft")
            }
            FailReason::RequiresExpedience => {
                f.write_str("Requires Expedience (a successful Hasty Touch)")
            }
            FailReason::HastyTouchUnderExpedience => {
                f.write_str("Hasty Touch becomes Daring Touch under Expedience")
            }
            FailReason::UnknownAction => f.write_str("Unknown action"),
            FailReason::SuccessRoll => f.write_str("Action failed (success rate roll)"),
        }
    }
}
