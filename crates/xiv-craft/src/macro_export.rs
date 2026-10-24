use crate::actions::{Action, ActionId};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct MacroOptions {
    pub lines_per_macro: usize,
    pub sound_effect: i32,
    pub complete_sound_effect: i32,
    pub include_echo: bool,
}

impl Default for MacroOptions {
    fn default() -> MacroOptions {
        MacroOptions {
            lines_per_macro: 14,
            sound_effect: 1,
            complete_sound_effect: 2,
            include_echo: true,
        }
    }
}

/// `MacroExporter.ExportToMacro`: one string per in-game macro, lines joined with `\n`.
/// Unknown ids are skipped, as the C# exporter skips them.
///
/// # Panics
///
/// When `lines_per_macro` is zero and there is at least one line to chunk; the C# exporter
/// throws on exactly that input and returns an empty list before reaching it otherwise.
pub fn export_to_macro(action_ids: &[ActionId], options: &MacroOptions) -> Vec<String> {
    let lines: Vec<String> = action_ids
        .iter()
        .filter_map(|id| Action::from_id(*id))
        .map(|action| {
            format!(
                "/ac \"{}\" <wait.{}>",
                action.macro_name(),
                action.macro_wait_seconds()
            )
        })
        .collect();

    if lines.is_empty() {
        return Vec::new();
    }
    assert!(
        options.lines_per_macro > 0,
        "lines_per_macro must be at least 1"
    );

    let mut macros = Vec::new();
    for (index, chunk) in lines.chunks(options.lines_per_macro).enumerate() {
        let start = index * options.lines_per_macro;
        let mut chunk: Vec<String> = chunk.to_vec();
        if options.include_echo {
            if start + options.lines_per_macro < lines.len() {
                chunk.push(format!(
                    "/echo Macro #{} complete <se.{}>",
                    index + 1,
                    options.sound_effect
                ));
            } else {
                chunk.push(format!(
                    "/echo Craft complete <se.{}>",
                    options.complete_sound_effect
                ));
            }
        }
        macros.push(chunk.join("\n"));
    }
    macros
}

#[cfg(test)]
mod tests {
    use super::*;

    fn lines(actions: &[Action]) -> Vec<String> {
        let ids: Vec<ActionId> = actions.iter().map(|a| a.id()).collect();
        let options = MacroOptions {
            include_echo: false,
            ..MacroOptions::default()
        };
        export_to_macro(&ids, &options)[0]
            .split('\n')
            .map(str::to_string)
            .collect()
    }

    #[test]
    fn timed_buffs_and_inner_quiet_wait_two_seconds() {
        let buffs = [
            Action::InnerQuiet,
            Action::WasteNot,
            Action::WasteNotII,
            Action::Veneration,
            Action::Innovation,
            Action::GreatStrides,
            Action::Manipulation,
            Action::FinalAppraisal,
        ];
        let buff_lines = lines(&buffs);
        assert_eq!(buff_lines.len(), buffs.len());
        assert!(buff_lines.iter().all(|l| l.ends_with("<wait.2>")));
    }

    #[test]
    fn buff_like_abilities_wait_three_seconds() {
        assert_eq!(
            lines(&[
                Action::Observe,
                Action::HeartAndSoul,
                Action::QuickInnovation,
                Action::TrainedPerfection
            ]),
            [
                "/ac \"Observe\" <wait.3>",
                "/ac \"Heart and Soul\" <wait.3>",
                "/ac \"Quick Innovation\" <wait.3>",
                "/ac \"Trained Perfection\" <wait.3>",
            ]
        );
    }

    #[test]
    fn daring_touch_is_called_by_hasty_touchs_name() {
        assert_eq!(
            lines(&[
                Action::PreciseTouch,
                Action::RefinedTouch,
                Action::DaringTouch
            ]),
            [
                "/ac \"Precise Touch\" <wait.3>",
                "/ac \"Refined Touch\" <wait.3>",
                "/ac \"Hasty Touch\" <wait.3>",
            ]
        );
    }

    #[test]
    fn legacy_actions_still_export() {
        assert_eq!(
            lines(&[
                Action::InnerQuiet,
                Action::FocusedSynthesis,
                Action::FocusedTouch
            ]),
            [
                "/ac \"Inner Quiet\" <wait.2>",
                "/ac \"Focused Synthesis\" <wait.3>",
                "/ac \"Focused Touch\" <wait.3>",
            ]
        );
    }
}
