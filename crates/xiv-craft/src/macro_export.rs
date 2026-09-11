use crate::actions::{Action, ActionCategory, ActionId};

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
/// When `lines_per_macro` is zero; the C# exporter throws on that input as well.
pub fn export_to_macro(action_ids: &[ActionId], options: &MacroOptions) -> Vec<String> {
    assert!(
        options.lines_per_macro > 0,
        "lines_per_macro must be at least 1"
    );

    let lines: Vec<String> = action_ids
        .iter()
        .filter_map(|id| Action::from_id(*id))
        .map(|action| {
            let wait = if action.category() == ActionCategory::Buff {
                2
            } else {
                3
            };
            format!("/ac \"{}\" <wait.{wait}>", action.in_game_name())
        })
        .collect();

    if lines.is_empty() {
        return Vec::new();
    }

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
