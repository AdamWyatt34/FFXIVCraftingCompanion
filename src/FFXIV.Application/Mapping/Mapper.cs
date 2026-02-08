using FFXIV.Application.DTOs;
using FFXIV.Domain.Entities;

namespace FFXIV.Application.Mapping;

public static class Mapper
{
    public static ItemDto ToDto(Item item, int? recipeId = null, bool isEquipment = false)
    {
        return new ItemDto(
            ItemId: item.ItemId,
            Name: item.Name,
            IconUrl: item.IconPath,
            ItemLevel: item.ItemLevel,
            Category: item.Category.ToString(),
            CanBeHQ: item.CanBeHQ,
            RecipeId: recipeId,
            IsEquipment: isEquipment);
    }

    public static RecipeIngredientDto ToDto(RecipeIngredient ingredient, Item item)
    {
        return new RecipeIngredientDto(
            ItemId: ingredient.ItemId,
            ItemName: item.Name,
            IconUrl: item.IconPath,
            Quantity: ingredient.Quantity,
            CanBeHQ: ingredient.CanBeHQ);
    }

    public static RecipeDto ToDto(Recipe recipe, Item resultItem, List<RecipeIngredientDto> ingredients)
    {
        return new RecipeDto(
            RecipeId: recipe.RecipeId,
            ResultItem: ToDto(resultItem),
            CraftType: recipe.CraftType.ToString(),
            RecipeLevel: recipe.RecipeLevel,
            Difficulty: recipe.Difficulty,
            Quality: recipe.Quality,
            Durability: recipe.Durability,
            RequiredCraftsmanship: recipe.RequiredCraftsmanship,
            RequiredControl: recipe.RequiredControl,
            Yield: recipe.Yield,
            Ingredients: ingredients);
    }
}
