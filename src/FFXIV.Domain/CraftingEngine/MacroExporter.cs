using FFXIV.Domain.CraftingEngine.Actions;

namespace FFXIV.Domain.CraftingEngine;

public record MacroOptions
{
    public int LinesPerMacro { get; init; } = 14;
    public int SoundEffect { get; init; } = 1;
    public int CompleteSoundEffect { get; init; } = 2;
    public bool IncludeEcho { get; init; } = true;
}

public static class MacroExporter
{
    public static List<string> ExportToMacro(IReadOnlyList<int> actionIds, MacroOptions? options = null)
    {
        options ??= new MacroOptions();
        var lines = new List<string>();

        foreach (var actionId in actionIds)
        {
            var action = CraftingActionRegistry.GetById(actionId);
            if (action == null) continue;

            int waitTime = action.Category == ActionCategory.Buff ? 2 : 3;
            lines.Add($"/ac \"{action.InGameName}\" <wait.{waitTime}>");
        }

        if (lines.Count == 0)
            return new List<string>();

        var macros = new List<string>();
        for (int i = 0; i < lines.Count; i += options.LinesPerMacro)
        {
            var chunk = lines.Skip(i).Take(options.LinesPerMacro).ToList();
            int macroNumber = (i / options.LinesPerMacro) + 1;

            if (options.IncludeEcho)
            {
                if (i + options.LinesPerMacro < lines.Count)
                    chunk.Add($"/echo Macro #{macroNumber} complete <se.{options.SoundEffect}>");
                else
                    chunk.Add($"/echo Craft complete <se.{options.CompleteSoundEffect}>");
            }

            macros.Add(string.Join("\n", chunk));
        }

        return macros;
    }
}
