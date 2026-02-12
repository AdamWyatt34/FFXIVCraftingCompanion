using FFXIV.Application.Crafting.DTOs;
using FFXIV.Application.Recipes.DTOs;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Application.Crafting;

public static class CraftingMapper
{
    // Default state for computing base action costs (no buffs, no conditions)
    private static readonly CraftingState DefaultState = new()
    {
        MaxProgress = 1000,
        MaxQuality = 1000,
        Durability = 70,
        MaxDurability = 70,
        CP = 600,
        MaxCP = 600,
        Condition = CraftCondition.Normal,
        Status = CraftStatus.InProgress,
        IsFirstStep = true,
    };

    // ── Domain → DTO ──

    public static CraftingStateDto ToDto(CraftingState state)
    {
        var activeBuffs = new List<ActiveBuffDto>();

        if (state.WasteNotTurns > 0)
            activeBuffs.Add(new ActiveBuffDto { Name = "Waste Not", RemainingTurns = state.WasteNotTurns });
        if (state.WasteNotIITurns > 0)
            activeBuffs.Add(new ActiveBuffDto { Name = "Waste Not II", RemainingTurns = state.WasteNotIITurns });
        if (state.VenerationTurns > 0)
            activeBuffs.Add(new ActiveBuffDto { Name = "Veneration", RemainingTurns = state.VenerationTurns });
        if (state.InnovationTurns > 0)
            activeBuffs.Add(new ActiveBuffDto { Name = "Innovation", RemainingTurns = state.InnovationTurns });
        if (state.GreatStridesTurns > 0)
            activeBuffs.Add(new ActiveBuffDto { Name = "Great Strides", RemainingTurns = state.GreatStridesTurns });
        if (state.ManipulationTurns > 0)
            activeBuffs.Add(new ActiveBuffDto { Name = "Manipulation", RemainingTurns = state.ManipulationTurns });
        if (state.FinalAppraisalTurns > 0)
            activeBuffs.Add(new ActiveBuffDto { Name = "Final Appraisal", RemainingTurns = state.FinalAppraisalTurns });
        if (state.MuscleMemoryTurns > 0)
            activeBuffs.Add(new ActiveBuffDto { Name = "Muscle Memory", RemainingTurns = state.MuscleMemoryTurns });
        if (state.HeartAndSoulActive)
            activeBuffs.Add(new ActiveBuffDto { Name = "Heart and Soul", RemainingTurns = 1 });
        if (state.TrainedPerfectionActive)
            activeBuffs.Add(new ActiveBuffDto { Name = "Trained Perfection", RemainingTurns = 1 });

        return new CraftingStateDto
        {
            Progress = state.Progress,
            MaxProgress = state.MaxProgress,
            ProgressPercent = state.ProgressPercent,
            Quality = state.Quality,
            MaxQuality = state.MaxQuality,
            QualityPercent = state.QualityPercent,
            Durability = state.Durability,
            MaxDurability = state.MaxDurability,
            CP = state.CP,
            MaxCP = state.MaxCP,
            Step = state.Step,
            Condition = state.Condition.ToString(),
            Status = state.Status.ToString(),
            HQPercent = state.HQPercent,
            ActiveBuffs = activeBuffs,
            InnerQuietStacks = state.InnerQuietStacks,
            ComboAction = state.ComboAction,
            ObserveActive = state.ObserveActive,
            IsFirstStep = state.IsFirstStep,
            QuickInnovationUsed = state.QuickInnovationUsed,
            HeartAndSoulUsed = state.HeartAndSoulUsed,
            TrainedPerfectionUsed = state.TrainedPerfectionUsed,
            CarefulObservationCount = state.CarefulObservationCount,
        };
    }

    public static SimulationResultDto ToDto(SimulationResult result, RecipeSummaryDto recipe, List<string> macros)
    {
        return new SimulationResultDto
        {
            Success = result.Success,
            TotalSteps = result.TotalSteps,
            FinalQualityPercent = result.FinalQualityPercent,
            FinalHQPercent = result.FinalHQPercent,
            FinalState = ToDto(result.FinalState),
            Steps = result.Steps.Select(ToStepDto).ToList(),
            Macros = macros,
            Recipe = recipe,
        };
    }

    public static StepResultDto ToStepDto(StepResult step)
    {
        var action = CraftingActionRegistry.GetById(step.ActionId);
        return new StepResultDto
        {
            StepNumber = step.StepNumber,
            ActionName = step.ActionName,
            ActionId = step.ActionId,
            ActionCategory = action?.Category.ToString() ?? "Unknown",
            Succeeded = step.ActionSucceeded,
            FailReason = step.FailReason,
            Deltas = new StepDeltaDto
            {
                Progress = step.ProgressDelta,
                Quality = step.QualityDelta,
                Durability = step.DurabilityDelta,
                CP = step.CPDelta,
            },
            StateAfter = ToDto(step.StateAfter),
        };
    }

    public static ActionAvailabilityDto ToDto(ActionAvailability availability)
    {
        return new ActionAvailabilityDto
        {
            ActionId = availability.ActionId,
            ActionName = availability.ActionName,
            IsAvailable = availability.IsAvailable,
            UnavailableReason = availability.UnavailableReason,
            CPCost = availability.CPCost,
            DurabilityCost = availability.DurabilityCost,
            SuccessRate = availability.SuccessRate,
            Category = availability.Category.ToString(),
        };
    }

    public static ActionInfoDto ToDto(ICraftingAction action)
    {
        return new ActionInfoDto
        {
            Id = action.Id,
            Name = action.Name,
            InGameName = action.InGameName,
            LevelRequirement = action.LevelRequirement,
            Category = action.Category.ToString(),
            BaseCPCost = action.GetCPCost(DefaultState),
            BaseDurabilityCost = action.GetDurabilityCost(DefaultState),
            BaseSuccessRate = action.GetSuccessRate(DefaultState),
        };
    }

    // ── DTO → Domain ──

    public static CraftingState ToDomain(CraftingStateDto dto)
    {
        Enum.TryParse<CraftCondition>(dto.Condition, ignoreCase: true, out var condition);
        Enum.TryParse<CraftStatus>(dto.Status, ignoreCase: true, out var status);

        var wasteNot = dto.ActiveBuffs.FirstOrDefault(b => b.Name == "Waste Not");
        var wasteNotII = dto.ActiveBuffs.FirstOrDefault(b => b.Name == "Waste Not II");
        var veneration = dto.ActiveBuffs.FirstOrDefault(b => b.Name == "Veneration");
        var innovation = dto.ActiveBuffs.FirstOrDefault(b => b.Name == "Innovation");
        var greatStrides = dto.ActiveBuffs.FirstOrDefault(b => b.Name == "Great Strides");
        var manipulation = dto.ActiveBuffs.FirstOrDefault(b => b.Name == "Manipulation");
        var finalAppraisal = dto.ActiveBuffs.FirstOrDefault(b => b.Name == "Final Appraisal");
        var muscleMemory = dto.ActiveBuffs.FirstOrDefault(b => b.Name == "Muscle Memory");
        var heartAndSoul = dto.ActiveBuffs.FirstOrDefault(b => b.Name == "Heart and Soul");
        var trainedPerfection = dto.ActiveBuffs.FirstOrDefault(b => b.Name == "Trained Perfection");

        return new CraftingState
        {
            Progress = dto.Progress,
            MaxProgress = dto.MaxProgress,
            Quality = dto.Quality,
            MaxQuality = dto.MaxQuality,
            Durability = dto.Durability,
            MaxDurability = dto.MaxDurability,
            CP = dto.CP,
            MaxCP = dto.MaxCP,
            Step = dto.Step,
            Condition = condition,
            Status = status,
            InnerQuietStacks = dto.InnerQuietStacks,
            WasteNotTurns = wasteNot?.RemainingTurns ?? 0,
            WasteNotIITurns = wasteNotII?.RemainingTurns ?? 0,
            VenerationTurns = veneration?.RemainingTurns ?? 0,
            InnovationTurns = innovation?.RemainingTurns ?? 0,
            GreatStridesTurns = greatStrides?.RemainingTurns ?? 0,
            ManipulationTurns = manipulation?.RemainingTurns ?? 0,
            FinalAppraisalTurns = finalAppraisal?.RemainingTurns ?? 0,
            MuscleMemoryTurns = muscleMemory?.RemainingTurns ?? 0,
            HeartAndSoulActive = heartAndSoul is not null,
            TrainedPerfectionActive = trainedPerfection is not null,
            ComboAction = dto.ComboAction,
            ObserveActive = dto.ObserveActive,
            IsFirstStep = dto.IsFirstStep,
            QuickInnovationUsed = dto.QuickInnovationUsed,
            HeartAndSoulUsed = dto.HeartAndSoulUsed,
            TrainedPerfectionUsed = dto.TrainedPerfectionUsed,
            CarefulObservationCount = dto.CarefulObservationCount,
        };
    }

    public static CrafterStats ToDomain(CrafterStatsInput input)
    {
        return new CrafterStats
        {
            Level = input.Level,
            Craftsmanship = input.Craftsmanship,
            Control = input.Control,
            CP = input.CP,
            IsSpecialist = input.IsSpecialist,
        };
    }

    public static SimulationOptions ToDomain(SimulationOptionsInput? input)
    {
        if (input is null) return new SimulationOptions();

        CraftCondition? forceCondition = null;
        if (!string.IsNullOrEmpty(input.ForceCondition) &&
            Enum.TryParse<CraftCondition>(input.ForceCondition, ignoreCase: true, out var parsed))
        {
            forceCondition = parsed;
        }

        return new SimulationOptions
        {
            AssumeAllSuccess = input.AssumeAllSuccess,
            AssumeNormalCondition = input.AssumeNormalCondition,
            RandomSeed = input.RandomSeed,
            ForceCondition = forceCondition,
        };
    }

    public static MacroOptions ToDomain(MacroOptionsInput? input)
    {
        if (input is null) return new MacroOptions();

        return new MacroOptions
        {
            LinesPerMacro = input.LinesPerMacro,
            SoundEffect = input.SoundEffect,
            CompleteSoundEffect = input.CompleteSoundEffect,
            IncludeEcho = input.IncludeEcho,
        };
    }

    // ── Action Resolution ──

    public static (List<int> ActionIds, string? Error) ResolveActionIds(
        List<int>? actionIds, List<string>? actionNames)
    {
        var resolved = new List<int>();

        if (actionIds is { Count: > 0 })
        {
            foreach (var id in actionIds)
            {
                if (CraftingActionRegistry.GetById(id) is null)
                    return ([], $"Action ID {id} not found.");
                resolved.Add(id);
            }
        }
        else if (actionNames is { Count: > 0 })
        {
            foreach (var name in actionNames)
            {
                var action = CraftingActionRegistry.GetByName(name);
                if (action is null)
                    return ([], $"Action '{name}' not found.");
                resolved.Add(action.Id);
            }
        }

        return (resolved, null);
    }
}
