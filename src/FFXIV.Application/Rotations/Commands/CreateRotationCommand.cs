using FFXIV.Application.CraftingEngine;
using FFXIV.Application.Rotations.DTOs;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Rotations.Commands;

public sealed record CreateRotationCommand(
    string UserId,
    SubmitRotationRequest Request) : IRequest<RotationDetailDto?>;

public sealed class CreateRotationCommandHandler
    : IRequestHandler<CreateRotationCommand, RotationDetailDto?>
{
    private readonly ISharedRotationRepository _rotationRepo;
    private readonly IRecipeRepository _recipeRepo;
    private readonly IRecipeParameterFactory _recipeParamFactory;
    private readonly ICraftingSimulator _simulator;

    public CreateRotationCommandHandler(
        ISharedRotationRepository rotationRepo,
        IRecipeRepository recipeRepo,
        IRecipeParameterFactory recipeParamFactory,
        ICraftingSimulator simulator)
    {
        _rotationRepo = rotationRepo;
        _recipeRepo = recipeRepo;
        _recipeParamFactory = recipeParamFactory;
        _simulator = simulator;
    }

    public async Task<RotationDetailDto?> Handle(
        CreateRotationCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        // Quota check
        var count = await _rotationRepo.GetCountByUserAsync(command.UserId, cancellationToken);
        if (count >= 200)
            return null;

        // Validate recipe exists
        var recipe = await _recipeRepo.GetByIdAsync(request.RecipeId, cancellationToken);
        if (recipe is null)
            return null;

        // Validate action IDs
        if (request.ActionIds.Count == 0 || request.ActionIds.Count > 50)
            return null;

        foreach (var actionId in request.ActionIds)
        {
            if (CraftingActionRegistry.GetById(actionId) is null)
                return null;
        }

        // Validate stats
        if (request.Stats.Level < 1 || request.Stats.Level > 100 ||
            request.Stats.Craftsmanship < 0 || request.Stats.Control < 0 || request.Stats.CP < 0)
            return null;

        // Auto-simulate to compute metrics
        var recipeParams = await _recipeParamFactory.CreateAsync(request.RecipeId, cancellationToken);
        if (recipeParams is null)
            return null;

        var crafterStats = new CrafterStats
        {
            Level = request.Stats.Level,
            Craftsmanship = request.Stats.Craftsmanship,
            Control = request.Stats.Control,
            CP = request.Stats.CP,
            IsSpecialist = request.Stats.IsSpecialist
        };

        var simulation = _simulator.Simulate(recipeParams, crafterStats, request.ActionIds);

        // Parse source
        var source = Enum.TryParse<RotationSource>(request.Source, true, out var s)
            ? s
            : RotationSource.User;

        var now = DateTimeOffset.UtcNow;
        var entity = new SharedRotation
        {
            UserId = command.UserId,
            RecipeId = request.RecipeId,
            Name = request.Name?.Trim(),
            Notes = request.Notes?.Trim(),
            ActionIds = request.ActionIds,
            Craftsmanship = request.Stats.Craftsmanship,
            Control = request.Stats.Control,
            CP = request.Stats.CP,
            CrafterLevel = request.Stats.Level,
            QualityPercent = (decimal)simulation.FinalState.QualityPercent,
            HQPercent = (int)simulation.FinalState.HQPercent,
            TotalSteps = simulation.Steps.Count,
            CraftCompleted = simulation.Success,
            Source = source,
            Algorithm = request.Algorithm?.Trim(),
            Upvotes = 0,
            CreatedAt = now
        };

        await _rotationRepo.CreateAsync(entity, cancellationToken);

        // Reload with includes for mapping
        var saved = await _rotationRepo.GetByIdAsync(entity.Id, cancellationToken);
        var authorName = await _rotationRepo.GetAuthorNameAsync(command.UserId, cancellationToken);

        return RotationMapper.ToDetailDto(saved!, authorName);
    }
}
