using System.Text.Json;
using FFXIV.Application.Recipes;
using FFXIV.Application.SavedLists.DTOs;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed class CreateShoppingListCommandHandler
    : IRequestHandler<CreateShoppingListCommand, SavedListDto?>
{
    private readonly ISavedListRepository _savedListRepo;
    private readonly IRecipeRepository _recipeRepo;
    private readonly IRecipeTreeResolver _treeResolver;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CreateShoppingListCommandHandler(
        ISavedListRepository savedListRepo,
        IRecipeRepository recipeRepo,
        IRecipeTreeResolver treeResolver)
    {
        _savedListRepo = savedListRepo;
        _recipeRepo = recipeRepo;
        _treeResolver = treeResolver;
    }

    public async Task<SavedListDto?> Handle(
        CreateShoppingListCommand request, CancellationToken cancellationToken)
    {
        var count = await _savedListRepo.GetCountByUserAsync(request.UserId, cancellationToken);
        if (count >= 50)
            return null;

        var recipe = await _recipeRepo.GetByIdAsync(request.RecipeId, cancellationToken);
        if (recipe is null)
            return null;

        var tree = await _treeResolver.ResolveTreeAsync(recipe.ResultItemId, request.Quantity, cancellationToken);

        var materials = tree.ShoppingList
            .Select(m => new
            {
                m.ItemId,
                m.ItemName,
                Quantity = m.TotalQuantity,
                Category = m.SourceType.ToString()
            })
            .ToList();

        var crystals = tree.CrystalRequirements
            .Select(c => new
            {
                c.ItemId,
                c.ItemName,
                Quantity = c.TotalQuantity,
                Category = "Crystal"
            })
            .ToList();

        var data = new
        {
            SourceRecipeId = request.RecipeId,
            SourceItemName = recipe.ResultItem.Name,
            Quantity = request.Quantity,
            Materials = materials,
            Crystals = crystals,
            SavedAt = DateTimeOffset.UtcNow,
            Notes = request.Notes
        };

        var now = DateTimeOffset.UtcNow;
        var savedList = new SavedList
        {
            UserId = request.UserId,
            Name = request.Name,
            Type = SavedListType.ShoppingList,
            Data = JsonSerializer.Serialize(data, JsonOptions),
            IsPinned = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _savedListRepo.CreateAsync(savedList, cancellationToken);
        return SavedListMapper.ToDto(savedList);
    }
}
