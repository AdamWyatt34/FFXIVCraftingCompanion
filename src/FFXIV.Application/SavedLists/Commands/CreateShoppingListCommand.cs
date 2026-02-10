using FFXIV.Application.SavedLists.DTOs;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed record CreateShoppingListCommand(
    string UserId,
    string Name,
    int RecipeId,
    int Quantity,
    string? Notes) : IRequest<SavedListDto?>;
