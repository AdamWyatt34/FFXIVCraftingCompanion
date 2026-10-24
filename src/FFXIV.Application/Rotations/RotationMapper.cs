using FFXIV.Application.Rotations.DTOs;
using FFXIV.Domain.Entities;

namespace FFXIV.Application.Rotations;

public static class RotationMapper
{
    public static RotationListItemDto ToListItemDto(SharedRotation entity, string? authorName = null)
    {
        return new RotationListItemDto(
            Id: entity.Id,
            Name: entity.Name,
            RecipeId: entity.RecipeId,
            RecipeName: entity.Recipe.ResultItem.Name,
            RecipeLevel: entity.Recipe.RecipeLevel,
            CraftType: entity.Recipe.CraftType.ToString(),
            ActionIds: entity.ActionIds,
            QualityPercent: entity.QualityPercent,
            HQPercent: entity.HQPercent,
            TotalSteps: entity.TotalSteps,
            CraftCompleted: entity.CraftCompleted,
            Source: entity.Source.ToString(),
            AuthorName: authorName,
            Upvotes: entity.Upvotes,
            CreatedAt: entity.CreatedAt,
            Craftsmanship: entity.Craftsmanship,
            Control: entity.Control,
            CP: entity.CP,
            CrafterLevel: entity.CrafterLevel);
    }

    public static RotationDetailDto ToDetailDto(SharedRotation entity, string? authorName = null)
    {
        var actionNames = entity.ActionIds
            .Select(id => RotationActionCatalog.GetById(id)?.Name ?? $"Unknown({id})")
            .ToList();

        return new RotationDetailDto(
            Id: entity.Id,
            Name: entity.Name,
            Notes: entity.Notes,
            RecipeId: entity.RecipeId,
            RecipeName: entity.Recipe.ResultItem.Name,
            RecipeLevel: entity.Recipe.RecipeLevel,
            CraftType: entity.Recipe.CraftType.ToString(),
            ActionIds: entity.ActionIds,
            ActionNames: actionNames,
            QualityPercent: entity.QualityPercent,
            HQPercent: entity.HQPercent,
            TotalSteps: entity.TotalSteps,
            CraftCompleted: entity.CraftCompleted,
            Source: entity.Source.ToString(),
            Algorithm: entity.Algorithm,
            AuthorName: authorName,
            Upvotes: entity.Upvotes,
            CreatedAt: entity.CreatedAt,
            Craftsmanship: entity.Craftsmanship,
            Control: entity.Control,
            CP: entity.CP,
            CrafterLevel: entity.CrafterLevel,
            Macros: []);
    }
}
