using FFXIV.Application.SavedLists.DTOs;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed record CraftingPlanItem(int RecipeId, int Quantity);

public sealed record CreateCraftingPlanCommand(
    string UserId,
    string Name,
    List<CraftingPlanItem> Items) : IRequest<SavedListDto?>;
