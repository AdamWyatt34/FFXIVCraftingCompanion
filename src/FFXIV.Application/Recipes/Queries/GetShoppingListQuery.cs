using FFXIV.Application.Recipes.DTOs;
using MediatR;

namespace FFXIV.Application.Recipes.Queries;

public sealed record GetShoppingListQuery(int RecipeId, int Quantity = 1)
    : IRequest<ShoppingListResultDto?>;
