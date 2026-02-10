using System.Text.Json;
using FFXIV.Application.Recipes;
using FFXIV.Application.SavedLists.DTOs;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed class CreateCraftingPlanCommandHandler
    : IRequestHandler<CreateCraftingPlanCommand, SavedListDto?>
{
    private readonly ISavedListRepository _savedListRepo;
    private readonly IRecipeRepository _recipeRepo;
    private readonly IRecipeTreeResolver _treeResolver;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CreateCraftingPlanCommandHandler(
        ISavedListRepository savedListRepo,
        IRecipeRepository recipeRepo,
        IRecipeTreeResolver treeResolver)
    {
        _savedListRepo = savedListRepo;
        _recipeRepo = recipeRepo;
        _treeResolver = treeResolver;
    }

    public async Task<SavedListDto?> Handle(
        CreateCraftingPlanCommand request, CancellationToken cancellationToken)
    {
        var count = await _savedListRepo.GetCountByUserAsync(request.UserId, cancellationToken);
        if (count >= 50)
            return null;

        var items = new List<object>();
        var materialTotals = new Dictionary<int, (string Name, int Quantity, string Category)>();

        foreach (var planItem in request.Items)
        {
            var recipe = await _recipeRepo.GetByIdAsync(planItem.RecipeId, cancellationToken);
            if (recipe is null) continue;

            items.Add(new
            {
                RecipeId = planItem.RecipeId,
                ItemName = recipe.ResultItem.Name,
                Quantity = planItem.Quantity
            });

            var tree = await _treeResolver.ResolveTreeAsync(
                recipe.ResultItemId, planItem.Quantity, cancellationToken);

            MergeMaterials(materialTotals, tree.ShoppingList);
            MergeMaterials(materialTotals, tree.CrystalRequirements, "Crystal");
        }

        var combinedMaterials = materialTotals
            .Select(kvp => new
            {
                ItemId = kvp.Key,
                ItemName = kvp.Value.Name,
                Quantity = kvp.Value.Quantity,
                Category = kvp.Value.Category
            })
            .OrderBy(m => m.Category)
            .ThenBy(m => m.ItemName)
            .ToList();

        var data = new
        {
            Items = items,
            CombinedMaterials = combinedMaterials
        };

        var now = DateTimeOffset.UtcNow;
        var savedList = new SavedList
        {
            UserId = request.UserId,
            Name = request.Name,
            Type = SavedListType.CraftingPlan,
            Data = JsonSerializer.Serialize(data, JsonOptions),
            IsPinned = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _savedListRepo.CreateAsync(savedList, cancellationToken);
        return SavedListMapper.ToDto(savedList);
    }

    private static void MergeMaterials(
        Dictionary<int, (string Name, int Quantity, string Category)> totals,
        List<ShoppingListItem> materials,
        string? categoryOverride = null)
    {
        foreach (var m in materials)
        {
            var category = categoryOverride ?? m.SourceType.ToString();

            if (totals.TryGetValue(m.ItemId, out var existing))
            {
                totals[m.ItemId] = (existing.Name, existing.Quantity + m.TotalQuantity, existing.Category);
            }
            else
            {
                totals[m.ItemId] = (m.ItemName, m.TotalQuantity, category);
            }
        }
    }
}
