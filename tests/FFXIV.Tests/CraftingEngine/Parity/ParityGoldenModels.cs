namespace FFXIV.Tests.CraftingEngine.Parity;

// One JSONL line per case. Property names serialize camelCase; enums serialize as their
// C# ordinal so the Rust side compares integers, never names.
public sealed record ParityCase(
    string Case,
    ParityRecipe Recipe,
    ParityStats Stats,
    ParityOptions Options,
    List<int> Actions,
    ParityState Initial,
    List<ParityStep> Steps,
    ParityState Final,
    List<ParityAvailability>? FinalAvailability,
    List<string> Macros);

public sealed record ParityRecipe(
    int RecipeId,
    int RecipeLevel,
    int ClassJobLevel,
    int Difficulty,
    int Quality,
    int Durability,
    int Stars,
    int ProgressDivider,
    int ProgressModifier,
    int QualityDivider,
    int QualityModifier,
    int SuggestedCraftsmanship,
    int SuggestedControl,
    bool IsExpert,
    int ConditionsFlag,
    int StartingQuality,
    int MaterialQualityFactor);

public sealed record ParityStats(int Level, int Craftsmanship, int Control, int Cp, bool IsSpecialist);

public sealed record ParityOptions(bool AssumeAllSuccess, bool AssumeNormalCondition, int? RandomSeed, int? ForceCondition);

public sealed record ParityStep(
    int Step,
    int ActionId,
    string ActionName,
    bool Succeeded,
    string? FailReason,
    List<int> LegalBefore,
    ParityState After,
    List<ParityAvailability>? Availability);

public sealed record ParityAvailability(
    int ActionId,
    bool IsAvailable,
    string? UnavailableReason,
    int CpCost,
    int DurabilityCost,
    int SuccessRate,
    int Category);

public sealed record ParityState(
    int Progress,
    int MaxProgress,
    int Quality,
    int MaxQuality,
    int Durability,
    int MaxDurability,
    int Cp,
    int MaxCp,
    int Step,
    int Condition,
    int Status,
    int InnerQuietStacks,
    int WasteNotTurns,
    int WasteNot2Turns,
    int VenerationTurns,
    int InnovationTurns,
    int GreatStridesTurns,
    int ManipulationTurns,
    int FinalAppraisalTurns,
    int MuscleMemoryTurns,
    bool HeartAndSoulActive,
    bool TrainedPerfectionActive,
    bool QuickInnovationUsed,
    bool HeartAndSoulUsed,
    bool TrainedPerfectionUsed,
    int CarefulObservationCount,
    bool IsFirstStep,
    int ComboAction,
    bool ObserveActive,
    bool IsComplete,
    bool IsFailed,
    double QualityPercent,
    double ProgressPercent,
    int HqPercent);

public sealed record DotNetRandomGolden(List<DotNetRandomSeries> Seeds);

public sealed record DotNetRandomSeries(int Seed, List<double> Values);

public sealed record EngineUnitsGolden(List<HqGoldenCase> Hq, List<MacroGoldenCase> Macros);

public sealed record HqGoldenCase(
    string Case,
    int MaxQuality,
    int MaterialQualityFactor,
    List<HqGoldenIngredient> Ingredients,
    List<HqGoldenInput> HqIngredients,
    int Expected);

public sealed record HqGoldenIngredient(int ItemId, int Quantity, bool CanBeHq);

public sealed record HqGoldenInput(int ItemId, int HqQuantity);

public sealed record MacroGoldenCase(string Case, List<int> ActionIds, MacroGoldenOptions Options, List<string> Expected);

public sealed record MacroGoldenOptions(int LinesPerMacro, int SoundEffect, int CompleteSoundEffect, bool IncludeEcho);
