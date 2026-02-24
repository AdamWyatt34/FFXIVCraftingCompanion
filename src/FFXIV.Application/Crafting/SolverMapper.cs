using FFXIV.Application.Crafting.DTOs;
using FFXIV.Application.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;

namespace FFXIV.Application.Crafting;

public static class SolverMapper
{
    // ── DTO → Domain ──

    public static SolveRequest ToDomain(SolveRequestDto dto, CrafterStats resolvedStats)
    {
        return new SolveRequest
        {
            RecipeId = dto.RecipeId,
            Stats = resolvedStats,
            Constraints = ToDomain(dto.Constraints),
            Settings = ToDomain(dto.Settings),
            HQIngredients = dto.HQIngredients?
                .Select(h => new HQIngredientInput(h.ItemId, h.HQQuantity))
                .ToList(),
        };
    }

    public static SolverConstraints ToDomain(SolverConstraintsDto? dto)
    {
        if (dto is null) return new SolverConstraints();

        return new SolverConstraints
        {
            MustComplete = dto.MustComplete,
            MaximizeQuality = dto.MaximizeQuality,
            MinimizeSteps = dto.MinimizeSteps,
            TargetQualityPercent = dto.TargetQualityPercent,
            AllowUnsafeActions = dto.AllowUnsafeActions,
            MaxSteps = dto.MaxSteps,
        };
    }

    public static SolverSettings ToDomain(SolverSettingsDto? dto)
    {
        if (dto is null) return new SolverSettings();

        Enum.TryParse<SolverAlgorithm>(dto.Algorithm, ignoreCase: true, out var algorithm);

        return new SolverSettings
        {
            Algorithm = algorithm,
            TimeBudgetMs = dto.TimeBudgetMs,
            RandomSeed = dto.RandomSeed,
        };
    }

    // ── Domain → DTO ──

    public static SolveJobResponseDto ToEnqueueResponseDto(string jobId, SolverStatus status)
    {
        return new SolveJobResponseDto
        {
            JobId = jobId,
            Status = status.ToString(),
        };
    }

    public static SolverJobDto ToJobDto(SolverJobInfo info)
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = info.CompletedAt.HasValue
            ? (int)(info.CompletedAt.Value - info.CreatedAt).TotalMilliseconds
            : (int)(now - info.CreatedAt).TotalMilliseconds;

        return new SolverJobDto
        {
            JobId = info.JobId,
            Status = info.Status.ToString(),
            Result = info.Result is not null ? ToDto(info.Result) : null,
            ElapsedMs = elapsed,
            ErrorMessage = info.Result?.ErrorMessage,
        };
    }

    public static SolverResultDto ToDto(SolverResult result)
    {
        return new SolverResultDto
        {
            ActionIds = result.ActionIds,
            ActionNames = result.ActionIds
                .Select(id => CraftingActionRegistry.GetById(id)?.Name ?? $"Unknown({id})")
                .ToList(),
            Simulation = result.Simulation is not null
                ? ToSimulationDto(result.Simulation, result.Macros)
                : null,
            Macros = result.Macros,
            Metrics = ToDto(result.Metrics),
            ErrorMessage = result.ErrorMessage,
        };
    }

    private static SimulationResultDto ToSimulationDto(
        SimulationResult sim, List<string> macros)
    {
        return new SimulationResultDto
        {
            Success = sim.Success,
            TotalSteps = sim.TotalSteps,
            FinalQualityPercent = sim.FinalQualityPercent,
            FinalHQPercent = sim.FinalHQPercent,
            FinalState = CraftingMapper.ToDto(sim.FinalState),
            Steps = sim.Steps.Select(CraftingMapper.ToStepDto).ToList(),
            Macros = macros,
        };
    }

    public static SolverMetricsDto ToDto(SolverMetrics metrics)
    {
        return new SolverMetricsDto
        {
            AlgorithmUsed = metrics.AlgorithmUsed.ToString(),
            SolveTimeMs = (int)metrics.SolveTimeMs,
            TotalSteps = metrics.TotalSteps,
            FinalQualityPercent = metrics.FinalQualityPercent,
            FinalHQPercent = metrics.FinalHQPercent,
            CraftCompleted = metrics.CraftCompleted,
            NodesExplored = (int)metrics.NodesExplored,
            IterationsCompleted = (int)metrics.IterationsCompleted,
        };
    }
}
