using FFXIV.Application.Rotations.DTOs;
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

    public CreateRotationCommandHandler(
        ISharedRotationRepository rotationRepo,
        IRecipeRepository recipeRepo)
    {
        _rotationRepo = rotationRepo;
        _recipeRepo = recipeRepo;
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
            if (RotationActionCatalog.GetById(actionId) is null)
                return null;
        }

        // Validate stats
        if (request.Stats.Level < 1 || request.Stats.Level > 100 ||
            request.Stats.Craftsmanship < 0 || request.Stats.Control < 0 || request.Stats.CP < 0)
            return null;

        if (request.Metrics is null || request.Metrics.TotalSteps < 1 ||
            request.Metrics.TotalSteps > request.ActionIds.Count ||
            request.Metrics.QualityPercent is < 0 or > 100 || request.Metrics.HQPercent is < 0 or > 100)
            return null;

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
            QualityPercent = request.Metrics.QualityPercent,
            HQPercent = request.Metrics.HQPercent,
            TotalSteps = request.Metrics.TotalSteps,
            CraftCompleted = request.Metrics.CraftCompleted,
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
